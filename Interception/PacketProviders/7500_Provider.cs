using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using WindivertDotnet;

namespace Harry.Interception.PacketProviders
{
    public class _7500_Provider : PacketProviderBase
    {
        public _7500_Provider() : base("7500", 7500, 7509, true)
        {
        }

        protected override WinDivert CreateInstance()
        {
            return Divert = new WinDivert(Filter.True.And(x => x.IsTcp && x.Network.RemotePort >= 7500 && x.Network.RemotePort <= 7509), WinDivertLayer.Network);
        }
    }
}
