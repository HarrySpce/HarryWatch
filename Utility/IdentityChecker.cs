using Harry.Models;
using Harry.Utility;
using System;
using System.ServiceProcess;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Linq.Expressions;

namespace Harry
{
    public class IdentityChecker
    {
        string last = string.Empty;
        public string Name { get; set; } = "null";
        public AccessType Type { get; set; }
        public TimeSpan TimeLeft => TimeSpan.FromDays(9999);
        RuntimeData data;

        public IdentityChecker()
        {
            data = new RuntimeData();
        }

        public class RuntimeData
        {
            [Flags]
            public enum DebugTags
            {
                FAILED = -1,
                DIAG = 1,
                REMOTE = 2,
                API = 4,
                NT_FLAGS = 8,
                NT_PORT = 16,
                NT_HANDLE = 32,
                CLOSE_INVALID = 64,
                CLOSE_PROTECTED = 128,
                ANTI_ATTACH = 256,
            }
            [Flags]
            public enum MiscTags
            {
                FAILED = -1,
                HW_BREAKS = 1,
                HIDE_THREADS = 2,
                DRIVERS_UNSIGNED = 4,
                DRIVERS_TEST = 8,
                DRIVERS_KERNEL = 16,
                SANDBOXIE = 32,
                SANDBOX_COMODO = 64,
                SANDBOX_QIHOO = 128,
                SANDBOX_CUCKOO = 256,
                EMULATION = 512,
                WINE = 1024,
                BAD_INSTRUCTIONS = 2048,
                BLACKLIST_USERNAME = 4096,
                VM_WAREBOX = 8192,
                VM_KVM = 16384,
                VM_PORTS = 16384 * 2,
                VM_HYPERV = 16384 * 4,
                VM_FILES = 16384 * 8,
                NO_LOADER = 16384 * 16,
            }
        }

        public void CheckSubs()
        {
            
            Name = "Harry";
            Type = AccessType.Debug;
            
        }
        
        public enum AccessType
        {
            Free = 0,
            Lite = 1,
            Basic = 2,
            Full = 3,
            Debug = 4
        }

        #region DEFINITIONS
        private static uint SystemCodeIntegrityInformation = 0x67;
        const long CONTEXT_DEBUG_REGISTERS = 0x00010000L | 0x00000010L;
        internal readonly object Calc;
        #endregion

        #region IMPORTS
        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern uint NtQuerySystemInformation(uint SystemInformationClass, ref Structs.SYSTEM_CODEINTEGRITY_INFORMATION SystemInformation, uint SystemInformationLength, out uint ReturnLength);

        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern uint NtQuerySystemInformation(uint SystemInformationClass, ref Structs.SYSTEM_KERNEL_DEBUGGER_INFORMATION SystemInformation, uint SystemInformationLength, out uint ReturnLength);

        [DllImport("ntdll.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern void RtlInitUnicodeString(out Structs.UNICODE_STRING DestinationString, string SourceString);

        [DllImport("ntdll.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern void RtlUnicodeStringToAnsiString(out Structs.ANSI_STRING DestinationString, Structs.UNICODE_STRING UnicodeString, bool AllocateDestinationString);

        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern uint LdrGetDllHandle([MarshalAs(UnmanagedType.LPWStr)] string DllPath, [MarshalAs(UnmanagedType.LPWStr)] string DllCharacteristics, Structs.UNICODE_STRING LibraryName, ref IntPtr DllHandle);

        [DllImport("ntdll.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern uint LdrGetProcedureAddress(IntPtr Module, Structs.ANSI_STRING ProcedureName, ushort ProcedureNumber, out IntPtr FunctionHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetHandleInformation(IntPtr hObject, uint dwMask, uint dwFlags);

        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern bool NtClose(IntPtr Handle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateMutexA(IntPtr lpMutexAttributes, bool bInitialOwner, string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool IsDebuggerPresent();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CheckRemoteDebuggerPresent(IntPtr Handle, ref bool CheckBool);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lib);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr ModuleHandle, string Function);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(SafeHandle ProcHandle, IntPtr BaseAddress, byte[] Buffer, uint size, int NumOfBytes);

        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern uint NtSetInformationThread(IntPtr ThreadHandle, uint ThreadInformationClass, IntPtr ThreadInformation, int ThreadInformationLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenThread(uint DesiredAccess, bool InheritHandle, int ThreadId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint GetTickCount();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern void OutputDebugStringA(string Text);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetCurrentThread();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetThreadContext(IntPtr hThread, ref Structs.CONTEXT Context);

        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern uint NtQueryInformationProcess(SafeHandle hProcess, uint ProcessInfoClass, out uint ProcessInfo, uint nSize, uint ReturnLength);

        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern uint NtQueryInformationProcess(SafeHandle hProcess, uint ProcessInfoClass, out IntPtr ProcessInfo, uint nSize, uint ReturnLength);

        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern uint NtQueryInformationProcess(SafeHandle hProcess, uint ProcessInfoClass, ref Structs.PROCESS_BASIC_INFORMATION ProcessInfo, uint nSize, uint ReturnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern int QueryFullProcessImageNameA(SafeHandle hProcess, uint Flags, byte[] lpExeName, Int32[] lpdwSize);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowTextLengthA(IntPtr HWND);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowTextA(IntPtr HWND, StringBuilder WindowText, int nMaxCount);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(IntPtr ProcHandle, IntPtr BaseAddress, byte[] Buffer, uint size, int NumOfBytes);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool IsProcessCritical(IntPtr Handle, ref bool BoolToCheck);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool SetProcessMitigationPolicy(int policy, ref Structs.PROCESS_MITIGATION_BINARY_SIGNATURE_POLICY lpBuffer, int size);
        #endregion

        #region DEBUG
        public static bool GetTickCountAntiDebug()
        {
            uint Start = GetTickCount();
            return (GetTickCount() - Start) > 0x10;
        }
        #endregion

        #region MISC
        [DllImport("ntdll.dll")]
        private static extern int NtSetInformationProcess(IntPtr process, int process_cass, ref int process_value, int length);
        #endregion
    }
}
