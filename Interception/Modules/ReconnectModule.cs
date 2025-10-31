using Microsoft.EntityFrameworkCore;

using Harry.Database;
using Harry.Models;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Media;

using WindivertDotnet;

namespace Harry.Interception.Modules
{
    public class ReconnectModule : PacketModuleBase
    {
        PacketProviderBase _30k;
        public ReconnectModule() : base("Reconnect", false, InterceptionManager.GetProvider("30000"))
        {
            IsActivated = false;
            Icon = System.Windows.Application.Current.FindResource("ReconnectIcon") as Geometry ?? Icon;
            Description =
@"instant disconnect/reconnect [30k]
change public instances
reload world state
pull yourself to ft leader";

            _30k = PacketProviders.First();
        }

        public override void Toggle()
        {
            IsActivated = true;
            StartTime = DateTime.Now;

            foreach (var addr in _30k.Connections.Keys.ToArray())
            {
                try
                {
                    if (_30k.Connections.TryGetValue(addr, out var q) && q is not null && DateTime.Now - q.LastOrDefault()?.CreatedAt < TimeSpan.FromSeconds(10))
                        Inject(addr);

                }
                catch (Exception e)
                {
                    Logger.Error(e);
                }
                
            }

            Task.Run(async () =>
            {
                await Task.Delay(150);
                IsActivated = false;
            });
        }

        unsafe void Inject(string addr)
        {
            var con = _30k.Connections[addr];
            var out_example = con.LastOrDefault(x => !x.Inbound && x.Length != 0);
            var in_example = con.LastOrDefault(x => x.Inbound && x.Length != 0);

            if (out_example is null || in_example is null)
            {
                Logger.Warning($"{Name}: Can't kill {addr}");
                return;
            }

            var p1 = out_example.BuildSameDirection();
            p1.ParseResult.TcpHeader->Rst = true;
            p1.Recalc();
            _30k.StorePacket(p1);
            _30k.SendPacket(p1, true);

            // inbound rst // solo 6 sec // fireteam 40 sec
            var p2 = in_example.BuildSameDirection();
            p2.ParseResult.TcpHeader->Rst = true;
            p2.Recalc();
            _30k.StorePacket(p2);
            _30k.SendPacket(p2, true);
        }
    }
}
