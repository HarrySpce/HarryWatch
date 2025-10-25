using Harry.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

using WindivertDotnet;

namespace Harry.Interception.PacketProviders
{
    public class PlayersProvider : PacketProviderBase
    {
        // tickrate 30
        public PlayersProvider() : base ("Players", 27015, 27200)
        {
        }

        public class ResyncInfo
        {
            public DateTime LastClosed { get; set; }
            public DateTime LastStart { get; set; }
            public DateTime LastEndInbound { get; set; }
            public DateTime LastEndOutbound { get; set; }

            public List<string> Ids { get; set; } = new ();

            public bool InboundActive => LastEndInbound < LastStart;
            public bool OutboundActive => LastEndOutbound < LastStart;
            public bool Active => InboundActive && OutboundActive;
        }

        public Dictionary<string, ResyncInfo> Resyncs { get; set; } = new();
        public override bool AllowPacket(Packet p)
        {
            return base.AllowPacket(p);
        }

        protected override WinDivert CreateInstance()
        {
            return Divert = new WinDivert(Filter.True.And(x => x.IsUdp && x.Network.RemotePort >= 27015 && x.Network.RemotePort <= 27200), WinDivertLayer.Network);
        }
    }
}
