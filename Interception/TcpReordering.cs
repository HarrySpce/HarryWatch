using Harry.Interception.Modules;
using Harry.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Harry.Interception
{
    public class TcpCache
    {
        public class Data
        {
            public uint HighSeq { get; set; }
            public uint HighAck { get; set; }
            public uint LastLength { get; set; }

            public List<Packet> Blocked { get; set; } = new List<Packet>();
            public uint Dropped { get; set; }
        }

        public Dictionary<FlagType, Data> Location { get; set; } = new Dictionary<FlagType, Data>();
    }

    public enum FlagType
    {
        Local,
        Remote
    }

    public static class TcpReordering
    {
        public static Dictionary<string, TcpCache> Cache = new Dictionary<string, TcpCache>();

        static void InitValues(string addr)
        {
            if (Cache.ContainsKey(addr)) return;

            Cache[addr] = new TcpCache
            {
                Location = new Dictionary<FlagType, TcpCache.Data>()
            };
            Cache[addr].Location.Add(FlagType.Local, new TcpCache.Data());
            Cache[addr].Location.Add(FlagType.Remote, new TcpCache.Data());
        }

        public static unsafe void onNewPacket(this Packet p)
        {
            var r = p.RemoteAddress;

            if (p.Delayed || !p.SourceProvider.Connections.ContainsKey(r)) return;
            var con = p.SourceProvider.Connections[r];

            var type = p.Inbound ? FlagType.Remote : FlagType.Local;
            var otherType = p.Inbound ? FlagType.Local : FlagType.Remote;

            InitValues(r);

            var cache = Cache[r];

            if (!p.SeqNum.HasValue && !p.AckNum.HasValue)
            {
                p.SeqNum = 0;
            }

            if (p.SeqNum == 0)
            {
                p.SeqNum = p.ParseResult.TcpHeader->SeqNum = cache.Location[type].HighSeq + cache.Location[type].LastLength;
                Logger.Debug($"Seq set to {p.ParseResult.TcpHeader->SeqNum}");
            }

            if (!p.AckNum.HasValue)
            {
                Logger.Debug("No ack packet");
                p.Recalc();
                return;
            }

            if (p.Flags.HasFlag(TcpFlags.ACK) && p.AckNum == 0)
            {
                p.ParseResult.TcpHeader->AckNum = cache.Location[type].HighAck;
                Logger.Debug($"Ack set to {p.ParseResult.TcpHeader->AckNum}");
            }
            p.Recalc();
        }

        public static unsafe void onBlock(this Packet p)
        {
            var type = p.Inbound ? FlagType.Remote : FlagType.Local;
            var otherType = p.Inbound ? FlagType.Local : FlagType.Remote;
            var r = p.RemoteAddress;
            var cache = Cache[r];

            var existingPacket = cache.Location[type].Blocked.FirstOrDefault(x => x.SeqNum == p.SeqNum);

            if (existingPacket != null && existingPacket.Length >= p.Length)
            {
                return; // The existing packet is larger or equal; do not replace.
            }

            // If reaching here, either there's no existing packet at the same SeqNum, or it's smaller.
            // Proceed to check for fully encompassed packets to potentially replace.
            var min = p.SeqNum.Value;
            var max = p.SeqNum.Value + (uint)p.Length;

            var remove = cache.Location[type].Blocked
                .Where(x => x.SeqNum >= min && x.SeqNum + x.Length <= max).ToArray();

            var copy = p.Clone();
            copy.Delayed = true;
            cache.Location[type].Blocked.Add(copy);
            
            if (remove.Any())
            {
                foreach (var pr in remove)
                {
                    cache.Location[type].Blocked.Remove(pr);
                    pr.Dispose();
                }

                Logger.Debug($"Updated {remove.Length} packets with stacked");
            }
        }
        
        public static unsafe void onPass(this Packet p)
        {
            if (p.Delayed) return;

            var type = p.Inbound ? FlagType.Remote : FlagType.Local;
            var otherType = p.Inbound ? FlagType.Local : FlagType.Remote;
            var r = p.RemoteAddress;
            var cache = Cache[r];

            if (p.SeqNum > cache.Location[type].HighSeq) // keep what's greatest seq been sent
            {
                cache.Location[type].HighSeq = p.SeqNum.Value;
                cache.Location[type].LastLength = (uint)p.Length;
            }

            else if (p.SeqNum == cache.Location[type].HighSeq) // keep latest seq length to predict ack
            {
                cache.Location[type].LastLength = (uint)p.Length;
            }

            else if (cache.Location[type].HighSeq - p.SeqNum > reset)
            {
                cache.Location[type].HighSeq = p.SeqNum.Value;
                cache.Location[type].LastLength = (uint)p.Length;
            }



            if (!p.AckNum.HasValue || p.AckNum == 0) return;

            if (p.AckNum >= cache.Location[type].HighAck) // keep latest ack for spoofing
            {
                cache.Location[type].HighAck = p.AckNum.Value;
            }

            else if (cache.Location[type].HighAck - p.AckNum > reset)
            {
                cache.Location[type].HighAck = p.AckNum.Value;
                cache.Location[type].LastLength = (uint)p.Length;
            }

            if (p.Length == 0) return;

            if (cache.Location[type].Blocked.Any())
            {
                var sum = cache.Location[type].Blocked .Sum(x => x.Length);
            }

            if (cache.Location[otherType].Blocked.Any())
            {
                var sum = cache.Location[otherType].Blocked.Sum(x => x.Length);
            }

            p.Recalc();
        }

        const uint reset = 20000;
    }
}
