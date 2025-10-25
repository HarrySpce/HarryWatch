using Harry.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Harry.Interception.Modules
{
    public class InstanceModule : PacketModuleBase
    {
        PacketProviderBase provider;
        public InstanceModule() : base("З0K", true, InterceptionManager.GetProvider("30000"))
        {
            Icon = System.Windows.Application.Current.FindResource("Traveller") as Geometry;
            Description = @"blocks inbound updates";
            provider = PacketProviders.First();
            Buffer = Config.GetNamed(Name).GetSettings<bool>("Buffer");
        }

        public override void Toggle()
        {
            IsActivated = !IsActivated;
            if (!IsActivated)
            {
                Task.Run(async () =>
                {
                    foreach (var addr in TcpReordering.Cache.Keys.Where(x => x.Contains(":300")).ToArray())
                    {
                        try
                        {
                            var send = TcpReordering.Cache[addr].Location[FlagType.Remote].Blocked.ToArray();
                            TcpReordering.Cache[addr].Location[FlagType.Remote].Blocked.Clear();

                            if (!send.Any()) continue;

                            foreach (var p in send)
                            {
                                p.CreatedAt = DateTime.Now;
                                p.Delayed = false;
                                p.AckNum = 0; // let storepacket assign highest
                                p.SourceProvider.StorePacket(p);
                                if (Buffer && !p.Flags.HasFlag(TcpFlags.FIN) && !p.Flags.HasFlag(TcpFlags.RST)) await p.SourceProvider.SendPacket(p, true);

                                Logger.Debug($"{Name}: Seq dist {TcpReordering.Cache[addr].Location[FlagType.Remote].HighSeq - p.SeqNum}");
                            }

                            Logger.Debug($"{Name}: Sent {send.Length} on {addr}");

                        }
                        catch (Exception e)
                        {
                            Logger.Error(e, "at 30k");
                        }
                    }
                });
            }
        }

        public static bool Buffer;
        public override bool AllowPacket(Packet p)
        {
            if (!base.AllowPacket(p)) return false;

            if (!IsActivated) return true;

            if (p.Outbound || p.Length == 0) return true;

            return false;
        }
    }
}
