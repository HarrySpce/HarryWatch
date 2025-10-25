using Microsoft.Extensions.Logging.Abstractions;
using Harry.Database;
using Harry.Interception.Modules;
using Harry.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using WindivertDotnet;

using static System.Net.Mime.MediaTypeNames;

namespace Harry.Interception
{
    public abstract class PacketProviderBase : IDisposable
    {
        public string Name { get; set; }
        public WinDivert Divert { get; set; }
        public bool IsEnabled => subs.Any();
        
        public List<Packet> CurrentQueue { get; set; }
        public Dictionary<string, List<Packet>> Connections { get; set; }
        public int BufferSeconds = 5;
        public int PortRangeStart;
        public int PortRangeEnd;
        bool isTcp;

        public List<Packet> Delay { get; set; }

        protected abstract WinDivert CreateInstance();

        

        public PacketProviderBase(string name, int fromPort, int toPort, bool isTcp = false)
        {
            Name = name;
            PortRangeStart = fromPort;
            PortRangeEnd = toPort;
            this.isTcp = isTcp;

            CurrentQueue = new List<Packet>();
            Connections = new Dictionary<string, List<Packet>>();
        }

        private HashSet<PacketModuleBase> subs = new HashSet<PacketModuleBase>();
        private CancellationTokenSource cts;

        


        public async Task SendPacket(Packet packet, bool force = false)
        {
            if (!force && (packet.OriginalPacket is null || packet.Addr == null || packet.IsSent))
            {
                Logger.Debug($"{Name} provider: Unable to inject the packet");
                return;
            }

            if (isTcp) packet.onPass();

            await Divert.SendAsync(packet.OriginalPacket, packet.Addr);
            packet.IsSent = true;
            packet.IsSaved = packet.Delayed = false;
        }
        public virtual bool AllowPacket(Packet p)
        {
            return !subs.Any(x => !x.AllowPacket(p));
        }
        public virtual void StorePacket(Packet p)
        {
            CurrentQueue.Add(p);
            var r = p.RemoteAddress;
            if (!Connections.ContainsKey(r)) 
                Connections[r] = new List<Packet>();

            Connections[r].Add(p);

            if (isTcp)
            {
                try
                {
                    p.onNewPacket();
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                }
            }
        }

        private async Task PacketPoll(CancellationToken ct)
        {
            Dictionary<Type, int> exceptionsCounter = new Dictionary<Type, int>();
            WinDivertPacket packet = null;
            WinDivertAddress addr = null;
            for (int i = 0; i < 6; i++)
            {
                try
                {
                    CreateInstance();
                    break;
                }
                catch (Exception e)
                {
                    if (i == 5)
                    {
                        Logger.Error(e);
                        throw e;
                    }
                    await Task.Delay(100);
                }
            }

            Logger.Debug($"{Name} provider: Started");
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    packet = new WinDivertPacket();
                    addr = new WinDivertAddress();

                    var recvLength = await Divert.RecvAsync(packet, addr, ct);

                    if (ct.IsCancellationRequested)
                        break;

                    var p = new Packet(packet, addr, PortRangeStart, PortRangeEnd, this);

                    StorePacket(p);

                    if (AllowPacket(p))
                    {
                        SendPacket(p);
                    }
                    else
                    {
                        if (isTcp) p.onBlock();
                    }
                }
                catch (TaskCanceledException) { }
                catch (Exception e)
                {
                    var t = e.GetType();
                    exceptionsCounter.TryGetValue(t, out var currentCount);
                    exceptionsCounter[t] = currentCount + 1;
                    if (exceptionsCounter[t] > 10)
                    {
                        Divert.Dispose();
                        await FinalizePackets(CurrentQueue.Where(x => !x.IsSaved));
                        CurrentQueue.Clear();
                        Connections.Clear();
                        cts.Cancel();

                        cts = new CancellationTokenSource();
                        Task.Run(() => PacketPoll(cts.Token));
                        Task.Run(() => SavePoll(cts.Token));
                        Task.Run(() => DelayPoll(cts.Token));

                        return;
                    }

                    Logger.Error($"{Name} provider: {e}");
                }
            }


            Divert.Dispose();

            await FinalizePackets(CurrentQueue.Where(x => !x.IsSaved));
            CurrentQueue.ToList().ForEach(x => x.Dispose());
            CurrentQueue.Clear();
            Connections.Clear();

            packet.Dispose();
            addr.Dispose();
            Logger.Debug($"{Name} provider: Stopped");
        }
        private async Task SavePoll(CancellationToken ct)
        {
            while (CurrentQueue is null || Connections is null)
            {
                await Task.Delay(500);
                Logger.Debug($"{Name} provider: Awaiting variables initialization");
            }

            Logger.Debug($"{Name} provider: Started saving packets");
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    Packet[] copy = CurrentQueue.ToArray();

                    var remove = copy.Where(x => 
                        x.CreatedAt < DateTime.Now - TimeSpan.FromSeconds(BufferSeconds) && 
                        !x.IsSaved && !x.Delayed);

                    if (remove.Any())
                    {
                        foreach (var r in remove)
                        {
                            CurrentQueue.Remove(r);

                            var addr = r.RemoteAddress;
                            Connections[addr].Remove(r);
                            if (!Connections[addr].Any())
                            {
                                Connections.Remove(addr);
                            }
                        }

                        if (Config.Instance.Settings.DB_SavePackets)
                        {
                            await FinalizePackets(remove);
                        }
                        else
                        {
                            foreach (var r in remove)
                            {
                                r.Dispose();
                            }
                        }
                    }

                    await Task.Delay(750, ct);
                }
                catch (TaskCanceledException) { }
                catch (NullReferenceException) 
                {
                    for (int i = 0; i < 5; i++)
                    {
                        try
                        {
                            var copy = CurrentQueue.Where(x => x is null).ToList();
                            foreach (var c in copy)
                                CurrentQueue.Remove(c);
                            break;
                        }
                        catch { }
                    }
                }
                catch (Exception e)
                {
                    Logger.Error($"{Name} provider: Packet save {e}");
                }
            }

            Logger.Debug($"{Name} provider: Stopped saving packets");
        }
        private async Task DelayPoll(CancellationToken ct)
        {
            Delay = new List<Packet>();

            Logger.Debug($"{Name} provider: Delay poll started");
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    if (ct.IsCancellationRequested)
                        break;

                    // Wait until it's time to inject
                    if (!Delay.Any(x => x.CreatedAt <= DateTime.Now))
                    {
                        await Task.Delay(50, ct);
                        continue;
                    }

                    // Find the earliest to inject
                    var p = Delay.MinBy(x => x.CreatedAt);
                    Delay.Remove(p);
                    StorePacket(p);
                    SendPacket(p);
                }
                catch (TaskCanceledException) { }
                catch (Exception e)
                {
                    Logger.Error($"{Name} delay: {e}");
                }
            }

            ClearDelayQueue();
            Logger.Debug($"{Name} delay: Stopped");
        }

        private async Task FinalizePackets(IEnumerable<Packet> unsaved)
        {
            if (unsaved.Count() == 0)
                return;

            if (Config.Instance.Settings.DB_SavePackets)
            {
                var temp = unsaved.Where(x => !x.IsSaved)
                .Select(x => new DbPacket()
                {
                    CreatedAt = x.CreatedAt,
                    Payload = x.Payload.ToArray(),
                    Length = x.Length,
                    IsInbound = x.Inbound,
                    IsSent = x.IsSent,
                    SrcAddr = x.SrcAddr.ToString(),
                    DstAddr = x.DstAddr.ToString(),
                    SrcPort = x.SrcPort,
                    DstPort = x.DstPort,
                    Flags = x.BuildTcpFlagsString(),
                });

                using var db = new HarryDBC();
                db.ChangeTracker.AutoDetectChangesEnabled = false;
                db.Packets.AddRange(temp);
                await db.SaveChangesAsync();
            }

            foreach (var p in unsaved)
            {
                p.IsSaved = true;
                p.Dispose();
            }
        }

        static Dictionary<string, DateTime> lastDelays = new ();
        public void DelayPacket(Packet packet, TimeSpan delay = default, bool addFromLatest = false, bool sameDirection = false)
        {
            if (Delay.Count > 2048)
            {
                Logger.Warning($"{Name}: Delay buffer is at capacity");
                var remove = Delay.Take(16);
                Delay.RemoveRange(0, 16);
                Task.Run(async () => await FinalizePackets(remove));
            }

            if (delay == default)
            {
                delay = TimeSpan.FromSeconds(1);
            }

            var clone = packet.Clone();

            if (addFromLatest)
            {
                var con = Delay.Where(x => x.RemoteAddress == packet.RemoteAddress).ToArray();
                if (sameDirection)
                {
                    con = con.Where(x => x.Inbound == packet.Inbound).ToArray();
                }

                var last = con.LastOrDefault();
                if (lastDelays.TryGetValue($"{packet.RemoteAddress}{packet.Inbound}", out var lastDelay) && 
                    DateTime.Now - lastDelay < TimeSpan.FromMilliseconds(10))
                    delay = TimeSpan.FromMilliseconds(10);

                clone.CreatedAt = (last ?? clone).CreatedAt.Add(delay);
            }
            else
            {
                clone.CreatedAt = clone.CreatedAt.Add(delay);
            }

            clone.Delayed = true;
            lastDelays[$"{packet.RemoteAddress}{packet.Inbound}"] = DateTime.Now;

            Delay.Add(clone);
        }
        public async Task ClearDelayQueue(string addr = null, bool sendSaved = false, int delay = 25)
        {
            var copy = Delay.ToList();
            if (addr is not null)
            {
                copy = copy.Where(x => x.RemoteAddress == addr).ToList();
            }

            Delay.RemoveAll(x => copy.Contains(x));

            if (sendSaved)
            {
                var groupped = copy.GroupBy(x => x.RemoteAddress);
                var sendTasks = groupped.Select(x => Task.Run(async () =>
                {
                    var connection = x.OrderBy(x => x.CreatedAt);
                    foreach (var p in connection)
                    {
                        p.CreatedAt = DateTime.Now;
                        StorePacket(p);
                        SendPacket(p, true);
                        p.Delayed = false;

                        await Task.Delay(delay);
                    }
                }));

                await Task.WhenAll(sendTasks);
                Logger.Debug($"{Name}: Sent {copy.Count} packets from buffer");
            }
            else
            {
                foreach (var p in copy)
                {
                    p.Dispose();
                }
            }
        }


        public void Subscribe(PacketModuleBase module)
        {
            if (subs.Contains(module))
                return;

            subs.Add(module);
            if (subs.Count == 1)
            {
                cts = new CancellationTokenSource();
                Task.Run(() => PacketPoll(cts.Token));
                Task.Run(() => SavePoll(cts.Token));
                Task.Run(() => DelayPoll(cts.Token));
            }
        }
        public void Unsubscribe(PacketModuleBase module)
        {
            if (!subs.Contains(module))
                return;

            subs.Remove(module);
            if (!subs.Any())
            {
                cts.Cancel();
            }
        }

        public void Dispose()
        {
            subs.ToList().ForEach(s => Unsubscribe(s));
            Divert.Dispose();
            FinalizePackets(CurrentQueue).GetAwaiter().GetResult();
        }
    }
}
