using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Harry.Interception.Modules
{
    internal class PauserModule : PacketModuleBase
    {
        public PauserModule() : base("GamePauser", true)
        {
            Icon = System.Windows.Application.Current.FindResource("PauseIcon") as Geometry;
            Description =
@"uses windows api
pauses game process";
        }

        public override void Toggle()
        {
            IsActivated = !IsActivated;
            var p = Process.GetProcessesByName("destiny2");
            if (p.Length == 0)
            {
                IsActivated = false;
                Logger.Warning($"{Name}: Did not find game process");
                return;
            }

            if (IsActivated)
            {
                SuspendProcess(p[0]);
            }
            else
            {
                ResumeProcess(p[0]);
            }
        }


        private static void SuspendProcess(Process p)
        {
            foreach (ProcessThread pT in p.Threads)
            {
                var pOpenThread = OpenThread(ThreadAccess.SUSPEND_RESUME, false, (uint)pT.Id);

                if (pOpenThread == IntPtr.Zero)
                    continue;

                SuspendThread(pOpenThread);
                CloseHandle(pOpenThread);
            }
        }

        public static void ResumeProcess(Process p)
        {
            if (p.ProcessName == string.Empty)
                return;

            foreach (ProcessThread pT in p.Threads)
            {
                IntPtr pOpenThread = OpenThread(ThreadAccess.SUSPEND_RESUME, false, (uint)pT.Id);

                if (pOpenThread == IntPtr.Zero)
                    continue;

                var suspendCount = 0;
                do
                {
                    suspendCount = ResumeThread(pOpenThread);
                } while (suspendCount > 0);

                CloseHandle(pOpenThread);
            }
        }


        [Flags]
        public enum ThreadAccess : int
        {
            TERMINATE = (0x0001),
            SUSPEND_RESUME = (0x0002),
            GET_CONTEXT = (0x0008),
            SET_CONTEXT = (0x0010),
            SET_INFORMATION = (0x0020),
            QUERY_INFORMATION = (0x0040),
            SET_THREAD_TOKEN = (0x0080),
            IMPERSONATE = (0x0100),
            DIRECT_IMPERSONATION = (0x0200)
        }

        [DllImport("kernel32.dll")]
        static extern IntPtr OpenThread(ThreadAccess dwDesiredAccess, bool bInheritHandle, uint dwThreadId);
        [DllImport("kernel32.dll")]
        static extern uint SuspendThread(IntPtr hThread);
        [DllImport("kernel32.dll")]
        static extern int ResumeThread(IntPtr hThread);
        [DllImport("kernel32", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool CloseHandle(IntPtr handle);
    }
}
