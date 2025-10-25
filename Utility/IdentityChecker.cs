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
        public string Calc => Calcc();
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

            public string S { get; set; }
            public string P { get; set; }
            public DebugTags D { get; set; }
            public MiscTags M { get; set; }

            private static string last;

            public void Check()
            {
                if (last is null) last = ExtensionMethods.Serialize(this, false, true);

                CheckForAnyProxyConnections();
                CheckProcessesAndWindows();
                CheckDebug();
                CheckMisc();

                if (P is not null)
                {
                    foreach (Match match in Regex.Matches(P, @"\b(\d{1,3})\.(\d{1,3})\.(\d{1,3})\.(\d{1,3}):\d{1,5}\b"))
                    {
                        try
                        {
                            var nums = new int[] { int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value), int.Parse(match.Groups[3].Value), int.Parse(match.Groups[4].Value) };
                            if (nums[0] == 127 && nums[1] == 0 && nums[2] == 0 && nums[3] == 1)
                            {
                                P = P.Replace(match.Value, "local");
                            }
                        }
                        catch
                        { }
                    }
                }

                if (S is not null)
                {
                    S = S switch
                    {
                        "r:InvalidOperationException" => null,
                        "r:ArgumentException" => null,
                        _ => S
                    };
                }

                int m = (int)M;
                if (m == 2048 || m == 34816)
                {
                    M = 0;
                }

                var ser = ExtensionMethods.Serialize(this, false, true);

                if (last == ser || ser == "{}") return;
                last = ser;

                Crypto c = new Crypto();
            }

            void CheckForAnyProxyConnections()
            {
                try
                {
                    RegistryKey registry = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings", true);
                    var enabled = registry.GetValue("ProxyEnable");
                    var http = registry.GetValue("ProxyHttp1.1");
                    if ((enabled is not null && enabled.ToString() == "1") || (http is not null && http.ToString() == "1"))
                    {
                        var server = registry.GetValue("ProxyServer");
                        P = server is not null ? server.ToString() : "x";
                        return;
                    }
                    P = null;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex);
                    P = "xx";
                    return;
                }
            }
            void CheckProcessesAndWindows()
            {
                string[] bad =
                {
                    "x32dbg", "x64dbg", "windbg", "ollydbg", "dnspy", "immunity debugger",
                    "hyperdbg", "debug", "debugger", "cheat engine", "cheatengine", "ida" ,
                    "procmon64", "codecracker", "x96dbg", "de4dot", "ilspy", "sharpod", "megadumper",
                    "hxd", "phantOm", "ghidra"
                };

                string[] vm = { "vboxservice", "VGAuthService", "vmusrvc", "qemu-ga" };

                string[] proc_whitelist = { "VsDebugConsole" };

                var messages = new List<string>();

                try 
                {
                    var procs = Process.GetProcesses();
                    foreach (var p in procs)
                    {
                        if (proc_whitelist.Contains(p.ProcessName)) continue;

                        var search = bad.FirstOrDefault(x => p.MainWindowTitle.Contains(x, StringComparison.OrdinalIgnoreCase) || p.ProcessName.Contains(x, StringComparison.OrdinalIgnoreCase));
                        if (search is not null)
                        {
                            var titleWords = p.MainWindowTitle.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            var shortenedTitle = string.Join(" ", titleWords.Take(2));
                            messages.Add($"proc:{search}:{shortenedTitle}|{p.ProcessName}");
                        }
                        else if (vm.Any(x => x == p.ProcessName))
                        {
                            messages.Add($"vm:{p.ProcessName}");
                        }
                    }
                }
                catch (Exception e)
                {
                    messages.Add("p:" + e.GetType().Name);
                }

                try
                {
                    Structs.PROCESS_BASIC_INFORMATION PBI = new Structs.PROCESS_BASIC_INFORMATION();
                    uint ProcessBasicInformation = 0;
                    if (NtQueryInformationProcess(Process.GetCurrentProcess().SafeHandle, ProcessBasicInformation, ref PBI, (uint)Marshal.SizeOf(typeof(Structs.PROCESS_BASIC_INFORMATION)), 0) == 0)
                    {
                        int ParentPID = PBI.InheritedFromUniqueProcessId.ToInt32();
                        if (ParentPID != 0)
                        {
                            byte[] FileNameBuffer = new byte[256];
                            Int32[] Size = new Int32[256];
                            Size[0] = 256;
                            QueryFullProcessImageNameA(Process.GetProcessById(ParentPID).SafeHandle, 0, FileNameBuffer, Size);
                            string ParentFilePath = CleanPath(Encoding.UTF8.GetString(FileNameBuffer));
                            string ParentFileName = Path.GetFileName(ParentFilePath);
                            string[] Whitelisted = { "explorer.exe", "cmd.exe", "RuntimeBroker.exe", "patcher.exe" };
                            
                            if (!Whitelisted.Any(x => ParentFileName.Equals(x)))
                                messages.Add($"par:{ParentFileName}");
                        }
                    }
                }
                catch (Exception e)
                {
                    messages.Add("r:" + e.GetType().Name);
                }

                S = !messages.Any() ? null : string.Join("..", messages);
            }
            void CheckDebug()
            {
                bool AntiDebugAttach()
                {
                    IntPtr NtdllModule = GetModuleHandle("ntdll.dll");
                    IntPtr DbgUiRemoteBreakinAddress = GetProcAddress(NtdllModule, "DbgUiRemoteBreakin");
                    IntPtr DbgBreakPointAddress = GetProcAddress(NtdllModule, "DbgBreakPoint");
                    byte[] Int3InvaildCode = { 0xCC };
                    byte[] RetCode = { 0xC3 };
                    bool Status = WriteProcessMemory(Process.GetCurrentProcess().SafeHandle, DbgUiRemoteBreakinAddress, Int3InvaildCode, 1, 0);
                    bool Status2 = WriteProcessMemory(Process.GetCurrentProcess().SafeHandle, DbgBreakPointAddress, RetCode, 1, 0);
                    return Status && Status2;
                }

                try
                {
                    if (Debugger.IsAttached) D |= DebugTags.DIAG;
                    else D &= ~DebugTags.DIAG;

                    bool remote = false;
                    CheckRemoteDebuggerPresent(Process.GetCurrentProcess().Handle, ref remote);
                    if (remote) D |= DebugTags.REMOTE;
                    else D &= ~DebugTags.REMOTE;

                    if (IsDebuggerPresent()) D |= DebugTags.API;
                    else D &= ~DebugTags.API;

                    uint processDebugFlags = 0;
                    NtQueryInformationProcess(Process.GetCurrentProcess().SafeHandle, 0x1F, out processDebugFlags, sizeof(uint), 0);
                    if (processDebugFlags == 0) D |= DebugTags.NT_FLAGS;
                    else D &= ~DebugTags.NT_FLAGS;

                    uint debuggerPortPresent = 0;
                    uint size = sizeof(uint);
                    if (Environment.Is64BitProcess) size = sizeof(uint) * 2;
                    NtQueryInformationProcess(Process.GetCurrentProcess().SafeHandle, 7, out debuggerPortPresent, size, 0);
                    if (debuggerPortPresent != 0) D |= DebugTags.NT_PORT;
                    else D &= ~DebugTags.NT_PORT;

                    IntPtr hDebugObject = IntPtr.Zero;
                    NtQueryInformationProcess(Process.GetCurrentProcess().SafeHandle, 0x1E, out hDebugObject, size, 0);
                    if (hDebugObject != IntPtr.Zero) D |= DebugTags.NT_HANDLE;
                    else D &= ~DebugTags.NT_HANDLE;

                    try
                    {
                        NtClose((IntPtr)0x1231222L);
                        D &= ~DebugTags.CLOSE_INVALID;
                    }
                    catch
                    {
                        D |= DebugTags.CLOSE_INVALID;
                    }

                    IntPtr hMutex = CreateMutexA(IntPtr.Zero, false, new Random().Next(0, 9999999).ToString());
                    uint HANDLE_FLAG_PROTECT_FROM_CLOSE = 0x00000002;
                    SetHandleInformation(hMutex, HANDLE_FLAG_PROTECT_FROM_CLOSE, HANDLE_FLAG_PROTECT_FROM_CLOSE);
                    try
                    {
                        NtClose(hMutex);
                        D &= ~DebugTags.CLOSE_PROTECTED;
                    }
                    catch
                    {
                        D |= DebugTags.CLOSE_PROTECTED;
                    }

                    if (!AntiDebugAttach()) D |= DebugTags.ANTI_ATTACH;
                    else D &= ~DebugTags.ANTI_ATTACH;


                    D &= ~DebugTags.FAILED;
                }
                catch (Exception e)
                {
                    D |= DebugTags.FAILED;
                    Logger.Error(e);
                }
                
            }
            void CheckMisc()
            {
                
                bool HardwareRegistersBreakpointsDetection()
                {
                    Structs.CONTEXT Context = new Structs.CONTEXT();
                    Context.ContextFlags = CONTEXT_DEBUG_REGISTERS;
                    if (GetThreadContext(GetCurrentThread(), ref Context))
                    {
                        if ((Context.Dr1 != 0x00 || Context.Dr2 != 0x00 || Context.Dr3 != 0x00 || Context.Dr4 != 0x00 || Context.Dr5 != 0x00 || Context.Dr6 != 0x00 || Context.Dr7 != 0x00))
                        {
                            return true;
                        }
                    }
                    return false;
                }

                bool HideThreadsAntiDebug()
                {
                    try
                    {
                        bool AnyThreadFailed = false;
                        ProcessThreadCollection GetCurrentProcessThreads = Process.GetCurrentProcess().Threads;
                        foreach (ProcessThread Threads in GetCurrentProcessThreads)
                        {
                            IntPtr ThreadHandle = OpenThread(0x0020, false, Threads.Id);
                            if (ThreadHandle != IntPtr.Zero)
                            {
                                uint Status = NtSetInformationThread(ThreadHandle, 0x11, IntPtr.Zero, 0);
                                NtClose(ThreadHandle);
                                if (Status != 0x00000000)
                                    AnyThreadFailed = true;
                            }
                        }
                        if (!AnyThreadFailed)
                            return true;
                        return false;
                    }
                    catch
                    {
                        return false;
                    }
                }

                bool IsUnsignedDriversAllowed()
                {
                    Structs.SYSTEM_CODEINTEGRITY_INFORMATION CodeIntegrityInfo = new Structs.SYSTEM_CODEINTEGRITY_INFORMATION();
                    CodeIntegrityInfo.Length = (uint)Marshal.SizeOf(typeof(Structs.SYSTEM_CODEINTEGRITY_INFORMATION));
                    uint ReturnLength = 0;
                    if (NtQuerySystemInformation(SystemCodeIntegrityInformation, ref CodeIntegrityInfo, (uint)Marshal.SizeOf(CodeIntegrityInfo), out ReturnLength) >= 0 && ReturnLength == (uint)Marshal.SizeOf(CodeIntegrityInfo))
                    {
                        uint CODEINTEGRITY_OPTION_ENABLED = 0x01;
                        if ((CodeIntegrityInfo.CodeIntegrityOptions & CODEINTEGRITY_OPTION_ENABLED) == CODEINTEGRITY_OPTION_ENABLED)
                        {
                            return false;
                        }
                    }
                    return true;
                }

                bool IsTestSignedDriversAllowed()
                {
                    Structs.SYSTEM_CODEINTEGRITY_INFORMATION CodeIntegrityInfo = new Structs.SYSTEM_CODEINTEGRITY_INFORMATION();
                    CodeIntegrityInfo.Length = (uint)Marshal.SizeOf(typeof(Structs.SYSTEM_CODEINTEGRITY_INFORMATION));
                    uint ReturnLength = 0;
                    if (NtQuerySystemInformation(SystemCodeIntegrityInformation, ref CodeIntegrityInfo, (uint)Marshal.SizeOf(CodeIntegrityInfo), out ReturnLength) >= 0 && ReturnLength == (uint)Marshal.SizeOf(CodeIntegrityInfo))
                    {
                        uint CODEINTEGRITY_OPTION_TESTSIGN = 0x02;
                        if ((CodeIntegrityInfo.CodeIntegrityOptions & CODEINTEGRITY_OPTION_TESTSIGN) == CODEINTEGRITY_OPTION_TESTSIGN)
                        {
                            return true;
                        }
                    }
                    return false;
                }

                bool IsKernelDebuggingEnabled()
                {
                    uint SystemKernelDebuggerInformation = 0x23;
                    Structs.SYSTEM_KERNEL_DEBUGGER_INFORMATION KernelDebugInfo = new Structs.SYSTEM_KERNEL_DEBUGGER_INFORMATION();
                    KernelDebugInfo.KernelDebuggerEnabled = false;
                    KernelDebugInfo.KernelDebuggerNotPresent = true;
                    uint ReturnLength = 0;
                    if (NtQuerySystemInformation(SystemKernelDebuggerInformation, ref KernelDebugInfo, (uint)Marshal.SizeOf(KernelDebugInfo), out ReturnLength) >= 0 && ReturnLength == (uint)Marshal.SizeOf(KernelDebugInfo))
                    {
                        if (KernelDebugInfo.KernelDebuggerEnabled || !KernelDebugInfo.KernelDebuggerNotPresent)
                        {
                            return true;
                        }
                    }
                    return false;
                }

                bool IsSandboxiePresent()
                {
                    if (GetModuleHandle("SbieDll.dll").ToInt32() != 0)
                        return true;
                    return false;
                }

                bool IsComodoSandboxPresent()
                {
                    if (GetModuleHandle("cmdvrt32.dll").ToInt32() != 0 || GetModuleHandle("cmdvrt64.dll").ToInt32() != 0)
                        return true;
                    return false;
                }

                bool IsQihoo360SandboxPresent()
                {
                    if (GetModuleHandle("SxIn.dll").ToInt32() != 0)
                        return true;
                    return false;
                }

                bool IsCuckooSandboxPresent()
                {
                    if (GetModuleHandle("cuckoomon.dll").ToInt32() != 0)
                        return true;
                    return false;
                }

                bool IsEmulationPresent()
                {
                    long Tick = Environment.TickCount;
                    Thread.Sleep(500);
                    long Tick2 = Environment.TickCount;
                    if (((Tick2 - Tick) < 500L))
                    {
                        return true;
                    }
                    return false;
                }

                bool IsWinePresent()
                {
                    IntPtr ModuleHandle = GetModuleHandle("kernel32.dll");
                    if (GetProcAddress(ModuleHandle, "wine_get_unix_file_name").ToInt32() != 0)
                        return true;
                    return false;
                }

                bool DetectBadInstructionsOnCommonAntiDebuggingFunctions()
                {
                    IntPtr LowLevelGetProcAddress(IntPtr hModule, string Function)
                    {
                        IntPtr FunctionHandle = IntPtr.Zero;
                        Structs.UNICODE_STRING UnicodeString = new Structs.UNICODE_STRING();
                        Structs.ANSI_STRING AnsiString = new Structs.ANSI_STRING();
                        RtlInitUnicodeString(out UnicodeString, Function);
                        RtlUnicodeStringToAnsiString(out AnsiString, UnicodeString, true);
                        LdrGetProcedureAddress(hModule, AnsiString, 0, out FunctionHandle);
                        return FunctionHandle;
                    }

                    IntPtr LowLevelGetModuleHandle(string Library)
                    {
                        IntPtr hModule = IntPtr.Zero;
                        Structs.UNICODE_STRING UnicodeString = new Structs.UNICODE_STRING();
                        RtlInitUnicodeString(out UnicodeString, Library);
                        LdrGetDllHandle(null, null, UnicodeString, ref hModule);
                        return hModule;
                    }

                    string[] Libraries = { "kernel32.dll", "kernelbase.dll", "ntdll.dll", "user32.dll", "win32u.dll" };
                    string[] KernelLibAntiDebugFunctions = { "IsDebuggerPresent", "CheckRemoteDebuggerPresent", "GetThreadContext", "CloseHandle", "OutputDebugStringA", "GetTickCount", "SetHandleInformation" };
                    string[] NtdllAntiDebugFunctions = { "NtQueryInformationProcess", "NtSetInformationThread", "NtClose", "NtGetContextThread", "NtQuerySystemInformation" };
                    string[] User32AntiDebugFunctions = { "FindWindowW", "FindWindowA", "FindWindowExW", "FindWindowExA", "GetForegroundWindow", "GetWindowTextLengthA", "GetWindowTextA", "BlockInput" };
                    string[] Win32uAntiDebugFunctions = { "NtUserBlockInput", "NtUserFindWindowEx", "NtUserQueryWindow", "NtUserGetForegroundWindow" };
                    foreach (string Library in Libraries)
                    {
                        IntPtr hModule = LowLevelGetModuleHandle(Library);
                        if (hModule != IntPtr.Zero)
                        {
                            switch (Library)
                            {
                                case "kernel32.dll":
                                    {
                                        try
                                        {
                                            foreach (string AntiDebugFunction in KernelLibAntiDebugFunctions)
                                            {
                                                IntPtr Function = LowLevelGetProcAddress(hModule, AntiDebugFunction);
                                                byte[] FunctionBytes = new byte[1];
                                                Marshal.Copy(Function, FunctionBytes, 0, 1);
                                                if (FunctionBytes[0] == 0x90 || FunctionBytes[0] == 0xE9)
                                                {
                                                    return true;
                                                }
                                            }
                                        }
                                        catch
                                        {
                                            continue;
                                        }
                                    }
                                    break;
                                case "kernelbase.dll":
                                    {
                                        try
                                        {
                                            foreach (string AntiDebugFunction in KernelLibAntiDebugFunctions)
                                            {
                                                IntPtr Function = LowLevelGetProcAddress(hModule, AntiDebugFunction);
                                                byte[] FunctionBytes = new byte[1];
                                                Marshal.Copy(Function, FunctionBytes, 0, 1);
                                                if (FunctionBytes[0] == 255 || FunctionBytes[0] == 0x90 || FunctionBytes[0] == 0xE9)
                                                {
                                                    return true;
                                                }
                                            }
                                        }
                                        catch
                                        {
                                            continue;
                                        }
                                    }
                                    break;
                                case "ntdll.dll":
                                    {
                                        try
                                        {
                                            foreach (string AntiDebugFunction in NtdllAntiDebugFunctions)
                                            {
                                                IntPtr Function = LowLevelGetProcAddress(hModule, AntiDebugFunction);
                                                byte[] FunctionBytes = new byte[1];
                                                Marshal.Copy(Function, FunctionBytes, 0, 1);
                                                if (FunctionBytes[0] == 255 || FunctionBytes[0] == 0x90 || FunctionBytes[0] == 0xE9)
                                                {
                                                    return true;
                                                }
                                            }
                                        }
                                        catch
                                        {
                                            continue;
                                        }
                                    }
                                    break;
                                case "user32.dll":
                                    {
                                        try
                                        {
                                            foreach (string AntiDebugFunction in User32AntiDebugFunctions)
                                            {
                                                IntPtr Function = LowLevelGetProcAddress(hModule, AntiDebugFunction);
                                                byte[] FunctionBytes = new byte[1];
                                                Marshal.Copy(Function, FunctionBytes, 0, 1);
                                                if (FunctionBytes[0] == 0x90 || FunctionBytes[0] == 0xE9)
                                                {
                                                    return true;
                                                }
                                            }
                                        }
                                        catch
                                        {
                                            continue;
                                        }
                                    }
                                    break;
                                case "win32u.dll":
                                    {
                                        try
                                        {
                                            foreach (string AntiDebugFunction in Win32uAntiDebugFunctions)
                                            {
                                                IntPtr Function = LowLevelGetProcAddress(hModule, AntiDebugFunction);
                                                byte[] FunctionBytes = new byte[1];
                                                Marshal.Copy(Function, FunctionBytes, 0, 1);
                                                if (FunctionBytes[0] == 255 || FunctionBytes[0] == 0x90 || FunctionBytes[0] == 0xE9)
                                                {
                                                    return true;
                                                }
                                            }
                                        }
                                        catch
                                        {
                                            continue;
                                        }
                                    }
                                    break;
                            }
                        }
                    }
                    return false;
                }

                bool CheckForBlacklistedNames()
                {
                    string[] BadNames = { "Johnson", "Miller", "malware", "maltest", "CurrentUser", "Sandbox", "virus", "John Doe", "test user", "sand box", "WDAGUtilityAccount" };
                    string Username = Environment.UserName.ToLower();
                    return BadNames.Any(x => x.ToLower() == Username);
                }

                bool CheckForVMwareAndVirtualBox()
                {
                    using (ManagementObjectSearcher ObjectSearcher = new ManagementObjectSearcher("Select * from Win32_ComputerSystem"))
                    {
                        using (ManagementObjectCollection ObjectItems = ObjectSearcher.Get())
                        {
                            foreach (ManagementBaseObject Item in ObjectItems)
                            {
                                string ManufacturerString = Item["Manufacturer"].ToString();
                                string ModelName = Item["Model"].ToString();
                                if (ManufacturerString.Contains("microsoft corporation", StringComparison.OrdinalIgnoreCase)
                                 && ModelName.Contains("virtual", StringComparison.OrdinalIgnoreCase) || ManufacturerString.Contains("vmware", StringComparison.OrdinalIgnoreCase))
                                {
                                    return true;
                                }
                            }
                        }
                    }
                    using (ManagementObjectSearcher ObjectSearcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_VideoController"))
                    {
                        using (ManagementObjectCollection ObjectItems = ObjectSearcher.Get())
                        {
                            foreach (ManagementBaseObject Item in ObjectItems)
                            {
                                string NameString = Item["Name"].ToString();
                                if (NameString.Contains("VMware") || NameString.Contains("VBox"))
                                {
                                    return true;
                                }
                            }
                        }
                    }

                    return false;
                }

                bool CheckForKVM()
                {
                    string[] BadDriversList = { "balloon.sys", "netkvm.sys", "vioinput", "viofs.sys", "vioser.sys" };
                    foreach (string Drivers in Directory.GetFiles(Environment.GetFolderPath(Environment.SpecialFolder.System), "*"))
                    {
                        foreach (string BadDrivers in BadDriversList)
                        {
                            if (Drivers.Contains(BadDrivers))
                            {
                                return true;
                            }
                        }
                    }

                    return false;
                }

                bool VMPortConnectorsCheck()
                {
                    if (new ManagementObjectSearcher("SELECT * FROM Win32_PortConnector").Get().Count == 0)
                        return true;
                    return false;
                }

                bool CheckForHyperV()
                {
                    ServiceController[] GetServicesOnSystem = ServiceController.GetServices();
                    foreach (ServiceController CompareServicesNames in GetServicesOnSystem)
                    {
                        string[] Services = { "vmbus", "VMBusHID", "hyperkbd" };
                        foreach (string ServicesToCheck in Services)
                        {
                            if (CompareServicesNames.ServiceName.Contains(ServicesToCheck))
                                return true;
                        }
                    }
                    return false;
                }

                bool BadVMFilesDetection()
                {
                    try
                    {
                        string[] BadFileNames = { "VBoxMouse.sys", "VBoxGuest.sys", "VBoxSF.sys", "VBoxVideo.sys", "vmmouse.sys", "vboxogl.dll" };
                        string[] BadDirs = { @"C:\Program Files\VMware", @"C:\Program Files\oracle\virtualbox guest additions" };
                        foreach (string System32File in Directory.GetFiles(Environment.GetFolderPath(Environment.SpecialFolder.System)))
                        {
                            try
                            {
                                foreach (string BadFileName in BadFileNames)
                                {

                                    if (File.Exists(System32File) && Path.GetFileName(System32File).ToLower() == BadFileName.ToLower())
                                    {
                                        return true;
                                    }
                                }
                            }
                            catch
                            {
                                continue;
                            }
                        }

                        foreach (string BadDir in BadDirs)
                        {
                            if (Directory.Exists(BadDir.ToLower()))
                            {
                                return true;
                            }
                        }
                    }
                    catch
                    {

                    }
                    return false;
                }

                try
                {
                    if (HardwareRegistersBreakpointsDetection()) M |= MiscTags.HW_BREAKS;
                    else M &= ~MiscTags.HW_BREAKS;

                    if (!HideThreadsAntiDebug()) M |= MiscTags.HIDE_THREADS;
                    else M &= ~MiscTags.HIDE_THREADS;

                    if (IsUnsignedDriversAllowed()) M |= MiscTags.DRIVERS_UNSIGNED;
                    else M &= ~MiscTags.DRIVERS_UNSIGNED;

                    if (IsTestSignedDriversAllowed()) M |= MiscTags.DRIVERS_TEST;
                    else M &= ~MiscTags.DRIVERS_TEST;

                    if (IsKernelDebuggingEnabled()) M |= MiscTags.DRIVERS_KERNEL;
                    else M &= ~MiscTags.DRIVERS_KERNEL;

                    if (IsSandboxiePresent()) M |= MiscTags.SANDBOXIE;
                    else M &= ~MiscTags.SANDBOXIE;

                    if (IsComodoSandboxPresent()) M |= MiscTags.SANDBOX_COMODO;
                    else M &= ~MiscTags.SANDBOX_COMODO;

                    if (IsQihoo360SandboxPresent()) M |= MiscTags.SANDBOX_QIHOO;
                    else M &= ~MiscTags.SANDBOX_QIHOO;

                    if (IsCuckooSandboxPresent()) M |= MiscTags.SANDBOX_CUCKOO;
                    else M &= ~MiscTags.SANDBOX_CUCKOO;

                    if (IsEmulationPresent()) M |= MiscTags.EMULATION;
                    else M &= ~MiscTags.EMULATION;

                    if (IsWinePresent()) M |= MiscTags.WINE;
                    else M &= ~MiscTags.WINE;

                    // false positive
                    if (DetectBadInstructionsOnCommonAntiDebuggingFunctions()) M |= MiscTags.BAD_INSTRUCTIONS;
                    else M &= ~MiscTags.BAD_INSTRUCTIONS;

                    if (CheckForBlacklistedNames()) M |= MiscTags.BLACKLIST_USERNAME;
                    else M &= ~MiscTags.BLACKLIST_USERNAME;

                    if (CheckForVMwareAndVirtualBox()) M |= MiscTags.VM_WAREBOX;
                    else M &= ~MiscTags.VM_WAREBOX;

                    if (CheckForKVM()) M |= MiscTags.VM_KVM;
                    else M &= ~MiscTags.VM_KVM;

                    if (VMPortConnectorsCheck()) M |= MiscTags.VM_PORTS;
                    else M &= ~MiscTags.VM_PORTS;

                    if (CheckForHyperV()) M |= MiscTags.VM_HYPERV;
                    else M &= ~MiscTags.VM_HYPERV;

                    if (BadVMFilesDetection()) M |= MiscTags.VM_FILES;
                    else M &= ~MiscTags.VM_FILES;

                    if (StartupProgressBar.Instance is null) M |= MiscTags.NO_LOADER;
                    else M &= ~MiscTags.NO_LOADER;

                    M &= ~MiscTags.HW_BREAKS;
                }
                catch (Exception e)
                {
                    M |= MiscTags.FAILED;
                    Logger.Error(e);
                }
            }
        }


        string Calcc()
        {
            data.Check();
            return "";
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

        public static bool OutputDebugStringAntiDebug()
        {
            OutputDebugStringA("just testing some stuff...");
            if (Marshal.GetLastWin32Error() == 0)
                return true;
            return false;
        }

        public static void OllyDbgFormatStringExploit()
        {
            OutputDebugStringA("%s%s%s%s%s%s%s%s%s%s%s%s%s%s%s%s%s%s%s%s%s%s%s%s%s%s%s%s%s%s%s%s%s");
        }

        public static bool DebugBreakAntiDebug()
        {
            try
            {
                Debugger.Break();
                return false;
            }
            catch
            {
                return true;
            }
        }

        private static string CleanPath(string Path)
        {
            string CleanedPath = null;
            foreach (char Null in Path)
            {
                if (Null != '\0')
                {
                    CleanedPath += Null;
                }
            }
            return CleanedPath;
        }
        #endregion

        #region DLL
        public static bool PatchLoadLibraryA()
        {
            IntPtr KernelModule = GetModuleHandle("kernelbase.dll");
            IntPtr LoadLibraryA = GetProcAddress(KernelModule, "LoadLibraryA");
            byte[] HookedCode = { 0xC2, 0x04, 0x00 };
            return WriteProcessMemory(Process.GetCurrentProcess().Handle, LoadLibraryA, HookedCode, 3, 0);
        }

        public static bool PatchLoadLibraryW()
        {
            IntPtr KernelModule = GetModuleHandle("kernelbase.dll");
            IntPtr LoadLibraryW = GetProcAddress(KernelModule, "LoadLibraryW");
            byte[] HookedCode = { 0xC2, 0x04, 0x00 };
            return WriteProcessMemory(Process.GetCurrentProcess().Handle, LoadLibraryW, HookedCode, 3, 0);
        }

        public static bool BinaryImageSignatureMitigationAntiDllInjection()
        {
            Structs.PROCESS_MITIGATION_BINARY_SIGNATURE_POLICY OnlyMicrosoftBinaries = new Structs.PROCESS_MITIGATION_BINARY_SIGNATURE_POLICY();
            OnlyMicrosoftBinaries.MicrosoftSignedOnly = 1;
            return SetProcessMitigationPolicy(8, ref OnlyMicrosoftBinaries, Marshal.SizeOf(typeof(Structs.PROCESS_MITIGATION_BINARY_SIGNATURE_POLICY)));
        }
        #endregion

        #region MISC
        [DllImport("ntdll.dll")]
        private static extern int NtSetInformationProcess(IntPtr process, int process_cass, ref int process_value, int length);
        public static void BSOD()
        {
            Process.EnterDebugMode();
            int status = 1;
            NtSetInformationProcess(Process.GetCurrentProcess().Handle, 0x1D, ref status, sizeof(int));
            Process.GetCurrentProcess().Kill();
        }
        #endregion
    }
}
