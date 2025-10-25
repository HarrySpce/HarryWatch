using Harry.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Harry.Interception.Modules
{
    public class KickModule : PacketModuleBase
    {
        public bool Host { get; set; }
        public KickModule() : base("Kick", true, InterceptionManager.GetProvider("Players"))
        {
            Icon = System.Windows.Application.Current.FindResource("DisconnectIcon") as Geometry;
        }
        public override void Toggle()
        {
            IsActivated = !IsActivated;
            inLast = DateTime.MinValue;
        }

        DateTime inLast;
        int[] Reconnect = new int[] { 1300, 100, 315 };
        public override bool AllowPacket(Packet p)
        {
            if (!base.AllowPacket(p)) return false;

            if (!IsActivated)
                return true;

            int[] inRecon = new int[] { 39, 58 };

            int[] c = new int[] { 9, 1300, 11, 25 }; // out 100 315 284 // in <20 looks like coords

            if (Host)
            {
                if (p.Outbound && inLast == DateTime.MinValue && p.Length == 1300)
                    inLast = DateTime.Now;

                if (DateTime.Now - StartTime > TimeSpan.FromSeconds(27))
                {
                    ForceDisable();
                    return true;
                }

                return p.Outbound || p.Length == 1300;
            }
            else
            {
                if (p.Outbound && inLast == DateTime.MinValue && p.Length == 1300)
                    inLast = DateTime.Now;

                if (inLast != DateTime.MinValue && DateTime.Now - inLast > TimeSpan.FromSeconds(26)) // 25 kicked then beaver
                {
                    Logger.Debug($"{Name}: Disabled cuz timeout");
                    ForceDisable();
                    return true;
                }

                return p.Outbound && c.Contains(p.Length) || p.Length == 1300;
            }
            return true;
        }
    }
}
