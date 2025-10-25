using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Harry.Models
{
    [Flags]
    public enum TcpFlags
    {
        None = 0,
        SYN = 1,
        ACK = 2,
        FIN = 4,
        RST = 8,
        PSH = 16,
        URG = 32,
        ECE = 64,
        CWR = 128
    }
}
