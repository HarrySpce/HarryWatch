using Harry.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

using WindivertDotnet;

namespace Harry.Interception.Modules
{
    public class SoloModule : PacketModuleBase
    {
        public SoloModule() : base("Solo", true, InterceptionManager.GetProvider("Players"))
        {
            Icon = System.Windows.Application.Current.FindResource("LonelyIcon") as Geometry;
            Description = "blocks matchmaking";
        }

        public override void Toggle()
        {
            IsActivated = !IsActivated;
        }

        public override bool AllowPacket(Packet packet)
        {
            if (!base.AllowPacket(packet)) return false;

            return !IsActivated;
        }
    }
}
