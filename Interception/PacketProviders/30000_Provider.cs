using Harry.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using WindivertDotnet;

namespace Harry.Interception.PacketProviders
{
    public class _30000_Provider : PacketProviderBase
    {
        public _30000_Provider() : base("30000", 30000, 30009, true)
        {
            BufferSeconds = 20;
        }

        protected override WinDivert CreateInstance()
        {
            return Divert = new WinDivert(Filter.True.And(x => x.IsTcp && x.Network.RemotePort >= 30000 && x.Network.RemotePort <= 30009), WinDivertLayer.Network);
        }

        public override bool AllowPacket(Packet p)
        {
            var text = Encoding.ASCII.GetString(p.Payload);
            var matchesAny = Regex.Matches(text, @"(?!\d)[A-Z|a-z|\d]{7,}");
            if (matchesAny.Any())
            {
                string message = matchesAny[0].Value;

                if (message.StartsWith("DESTINY"))
                {
                    message = $"{Name}: New instance";
                }
                else
                {
                    message = $"{Name}: {(p.Inbound ? "DL" : "UL")} {p.Length} - {string.Join(" > ", matchesAny.Select(x => x.Value))}";
                }

                Logger.Debug(message);
            }

            return base.AllowPacket(p);
        }
    }
}
