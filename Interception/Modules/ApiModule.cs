using Microsoft.EntityFrameworkCore.ValueGeneration.Internal;
using Harry.Interception.PacketProviders;
using Harry.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Media;

namespace Harry.Interception.Modules
{
    public class ApiModule : PacketModuleBase
    {
        public ApiModule() : base("API Block", true, InterceptionManager.GetProvider("7500"))
        {
            Icon = System.Windows.Application.Current.FindResource("ApiblockIcon") as Geometry;
            Description = 
@"blocks api updates [7.5k]
infinite rally
lobby crash (patched???)
predamage";

            Disable = Config.GetNamed(Name).GetSettings<bool>("SelfDisable");
            Buffer = Config.GetNamed(Name).GetSettings<bool>("Buffer");
        }

        public static bool Disable;
        public static bool Buffer;

        public override void Toggle()
        {
            IsActivated = !IsActivated;

            if (IsActivated)
            {
                reachedCapacity = DateTime.MaxValue;
                captured = false;

                cts = new CancellationTokenSource();
                Task.Run(async () =>
                {
                    while (IsActivated && !cts.IsCancellationRequested)
                    {
                        if (!captured)
                        {
                            await Task.Delay(5000, cts.Token);
                            continue;
                        }

                        if (!Disable)
                        {
                            await Task.Delay(500, cts.Token);
                            continue;
                        }

                        var cap = DateTime.Now - reachedCapacity;
                        var last = DateTime.Now - latest;

                        if (cap.TotalSeconds > 54 || (cap.TotalSeconds > 19 && last.TotalSeconds > 24))
                        {
                            Logger.Debug($"{Name}: Timed trigger [2]");
                            ForceDisable();
                        }

                        await Task.Delay(1000, cts.Token);
                    }
                    cts.Dispose();
                });
            }
            else
            {
                cts.Cancel();
                Task.Run(async () =>
                {
                    Logger.Debug($"{Name}: {(DateTime.Now - latest).TotalSeconds}sec since latest");
                    Logger.Debug($"{Name}: {(DateTime.Now - reachedCapacity).TotalSeconds}sec since first 1460");
                    foreach (var addr in TcpReordering.Cache.Keys.Where(x => x.Contains(":750")).ToArray())
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
                                if (Buffer) await p.SourceProvider.SendPacket(p, true);

                                Logger.Debug($"{Name}: Seq dist {TcpReordering.Cache[addr].Location[FlagType.Remote].HighSeq - p.SeqNum}");
                            }

                            Logger.Debug($"{Name}: Sent {send.Length} on {addr}");

                        }
                        catch (Exception e)
                        {
                            Logger.Error(e, "at API Block");
                        }
                    }
                });
            }
        }

        bool captured = false;
        DateTime latest;
        DateTime reachedCapacity;
        CancellationTokenSource cts;
        public override bool AllowPacket(Packet p)
        {
            if (!base.AllowPacket(p)) return false;

            if (!IsActivated) return true;

            if (p.Inbound && p.Flags.HasFlag(TcpFlags.RST) && Disable)
            {
                Logger.Debug($"{Name}: Weasel :<");
                ForceDisable();
                return true;
            }

            if (!captured && TcpReordering.Cache[p.RemoteAddress].Location[FlagType.Remote].Blocked.Any())
            {
                var last = TcpReordering.Cache[p.RemoteAddress].Location[FlagType.Remote].Blocked.Max(x => x.CreatedAt);
                if (p.CreatedAt - last > TimeSpan.FromMilliseconds(100))
                {
                    captured = true;
                    Logger.Debug($"{Name}: Captured");
                }
            }

            if (p.Inbound)
            {
                if (!captured && p.Flags.HasFlag(TcpFlags.PSH) && p.Length > 0)
                {
                    return false;
                }

                if (captured && TcpReordering.Cache[p.RemoteAddress].Location[FlagType.Remote].Blocked.Any(x => x.SeqNum == p.SeqNum))
                {
                    latest = p.CreatedAt;

                    if (reachedCapacity == DateTime.MaxValue)
                    {
                        var min = TcpReordering.Cache[p.RemoteAddress].Location[FlagType.Remote].Blocked.Min(x => x.SeqNum);
                        var limit = min + 1460;

                        if (p.SeqNum + p.Length == limit)
                        {
                            Logger.Debug($"{Name}: Capacity");
                            reachedCapacity = p.CreatedAt;
                        }
                    }

                    return false;
                }
            }

            return true;
        }
    }
}
