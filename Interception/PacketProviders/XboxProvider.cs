using Harry.Interception.Modules;
using Harry.Models;
using Harry.Utility;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Navigation;

using WindivertDotnet;

namespace Harry.Interception.PacketProviders
{
    public class XboxProvider : PacketProviderBase
    {
        // tickrate 20
        public XboxProvider() : base("Xbox", 3074, 3074)
        {
        }

        protected override WinDivert CreateInstance()
        {
            return Divert = new WinDivert(Filter.True.And(x => x.IsUdp && x.Network.RemotePort == 3074), WinDivertLayer.Network);
        }

        public class ResyncInfo
        {
            public DateTime LastStart { get; set; }
            public DateTime LastEndInbound { get; set; }
            public DateTime LastEndOutbound { get; set; }

            public bool InboundActive => LastEndInbound < LastStart;
            public bool OutboundActive => LastEndOutbound < LastStart;
            public bool Active => InboundActive || OutboundActive;
        }

        public Dictionary<string, ResyncInfo> Resyncs { get; set; } = new ();
        public override bool AllowPacket(Packet p)
        {
            var id = p.GetPveId();
            if (!id.HasValue) return true;

            if (!Resyncs.TryGetValue(p.RemoteAddress, out var resync))
                resync = Resyncs[p.RemoteAddress] = new ResyncInfo();

            // Make a resync on new connection
            if (id < 4 && !resync.Active &&
                (PveModule.AutoResync || PveModule.SlowInbound || PveModule.SlowOutbound))
            {
                Logger.Debug($"{Name}: Start {p.RemoteAddress}");
                resync.LastStart = DateTime.Now;
            }

            // Resync logic
            if ((p.Inbound && resync.InboundActive) || (p.Outbound && resync.OutboundActive))
            {
                var con = Connections[p.RemoteAddress].TakeLast(100).Where(x => resync.LastStart <= x.CreatedAt).ToArray();

                if (p.Inbound && resync.InboundActive)
                {
                    var inbound = con.Where(x => x.Inbound).ToList();

                    if (id < 5) return true;

                    // filter out data packets
                    if (id > 40 && 
                        (p.Length == 233 || !con.Any(x => x.Length == 233)) && 
                        p.CreatedAt - resync.LastStart < TimeSpan.FromSeconds(1))
                        return p.Length == 42 || p.Length == 128 || p.Length == 136 || p.Length == 233;

                    if (p.Length == 1258) return true; // have to accept 1 data packet

                    if (inbound.Count > 1)
                    {
                        // if ul resync succeeded and dl still fails it will pass 1 ul
                        if (!resync.OutboundActive && inbound.Count > 2 && 
                            p.CreatedAt - resync.LastStart > TimeSpan.FromSeconds(1.5) && 
                            inbound.TakeLast(3).All(x => x.Length < 52))
                        {
                            var ul = con.FirstOrDefault(x => x.Outbound && !x.IsSent);
                            if (ul != null) SendPacket(ul);
                        }

                        if (inbound[^2].Length != 1258 && p.IsPveService()) return true;
                    }

                    resync.LastEndInbound = p.CreatedAt;
                    Logger.Debug($"{Name}: Finish in {p.RemoteAddress}");
                    ClearDelayQueue(p.RemoteAddress);
                    return true;
                }

                if (p.Outbound && resync.OutboundActive)
                {
                    if (id < 5) return true;

                    if (p.IsPveQuestionableReconnectRequest()) return true;

                    if (p.IsPveService()) return true;

                    if (p.Length == 1258) return true;

                    resync.LastEndOutbound = p.CreatedAt;
                    Logger.Debug($"{Name}: Finish out {p.RemoteAddress}");
                    ClearDelayQueue(p.RemoteAddress);
                    return true;
                }
            }

            // Check for new resyncs
            else if ((PveModule.AutoResync || PveModule.SlowInbound || PveModule.SlowOutbound) && p.IsPveReconnectRequest())
            {
                var dir = Connections[p.RemoteAddress].TakeLast(100).Where(x => x.Inbound == p.Inbound).ToList();
                if (dir.Count > 1 && dir[^2].IsPveReconnectRequest() && dir[^2].Length != p.Length)
                {
                    if (!dir[^2].IsSent) SendPacket(dir[^2]);
                    Logger.Debug($"{Name}: Start {p.RemoteAddress}");
                    Resyncs[p.RemoteAddress].LastStart = DateTime.Now;
                    return true;
                }
            }

            return base.AllowPacket(p);
        }

        Task poll;
        CancellationTokenSource cts;
        public override void StorePacket(Packet p)
        {
            base.StorePacket(p);

            if ((DateTime.Now - last).TotalSeconds > 10)
            {
                InstanceStarted = DateTime.Now;

                if (cts is not null)
                {
                    cts.Cancel();
                }

                cts = new CancellationTokenSource();
                poll = Task.Run(async () =>
                {
                    var token = cts.Token;
                    for (int i = 0; i < 20; i++)
                    {
                        if (token.IsCancellationRequested || (D2CharacterTracker.User is null && !await D2CharacterTracker.TryLoadFromConfig())) return;

                        await D2CharacterTracker.Update();
                        await Task.Delay(20000, cts.Token);
                    }
                });
            }

            last = p.CreatedAt;
        }

        TimeSpan lastResult = TimeSpan.Zero;
        public TimeSpan InstanceDuration()
        {
            var dif = DateTime.Now - last;
            if (dif.TotalSeconds > 10) return TimeSpan.Zero;

            if (dif.TotalSeconds > 1) return lastResult;

            lastResult = DateTime.Now - InstanceStarted;

            return lastResult;
        }

        static DateTime start = DateTime.MinValue;
        public static DateTime InstanceStarted
        {
            get { return start; }
            set 
            {
                if (start == value) return;
                start = value;
                Logger.Debug($"Instance start set to {value}");
            }
        }
        DateTime last;
    }
}
