using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Harry.Interception.Modules
{
    public class TimerModule : PacketModuleBase
    {
        public TimerModule() : base("Timer", true, null)
        {
            Icon = System.Windows.Application.Current.FindResource("Clock") as Geometry;
            Description = "just a timer";
        }

        public override void Toggle()
        {
            IsActivated = !IsActivated;
        }
    }
}
