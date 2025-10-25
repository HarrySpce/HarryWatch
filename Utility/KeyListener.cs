using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using static System.Runtime.CompilerServices.RuntimeHelpers;

namespace Harry.Utility
{
    public class KeyListener : IDisposable
    {
        #region DLL_IMPORT
        [DllImport("user32.dll")]
        static extern Int16 GetKeyState(int vKey);
        [DllImport("user32.dll")]
        static extern ushort GetAsyncKeyState(int vKey);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern IntPtr SetWindowsHookEx(int idHook, LowLevelInputProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern IntPtr GetModuleHandle(string lpModuleName);
        #endregion

        #region CONSTANTS
        const int WH_KEYBOARD_LL = 13;
        const int WM_SYSKEYDOWN = 0x0104;
        const int WM_SYSKEYUP = 0x0105;
        const int WM_KEYDOWN = 0x0100;
        const int WM_KEYUP = 0x0101;

        const int WH_MOUSE_LL = 14;
        const int WM_MOUSEMOVE = 0x0200;
        const int WM_LBUTTONDOWN = 0x0201;
        const int WM_LBUTTONUP = 0x0202;
        const int WM_RBUTTONDOWN = 0x0204;
        const int WM_RBUTTONUP = 0x0205;
        const int WM_MBUTTONDOWN = 0x0207;
        const int WM_MBUTTONUP = 0x0208;
        const int WM_XBUTTONDOWN = 0x020B;
        const int WM_XBUTTONUP = 0x020C;

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }
        #endregion

        struct KeyEvent
        {
            public Keycode Keycode;
            public bool IsKeyUp;
        }

        private static Channel<KeyEvent> _eventChannel = Channel.CreateUnbounded<KeyEvent>();



        delegate IntPtr LowLevelInputProc(int nCode, IntPtr wParam, IntPtr lParam);
        static IntPtr KeyboardCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode < 0) return CallNextHookEx(_kbHook, nCode, wParam, lParam);

            var keycode = (Keycode)Marshal.ReadInt32(lParam);
            var type = wParam.ToInt32();
            var isKeyUp = type == WM_KEYUP || type == WM_SYSKEYUP;
            var keyEvent = new KeyEvent { Keycode = keycode, IsKeyUp = isKeyUp };

            _eventChannel.Writer.TryWrite(keyEvent);

            return CallNextHookEx(_kbHook, nCode, wParam, lParam);
        }
        static IntPtr MouseCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode < 0) return CallNextHookEx(_mouseHook, nCode, wParam, lParam);

            KeyEvent keyEvent;
            switch (wParam.ToInt32())
            {
                case WM_MOUSEMOVE: break;
                case WM_LBUTTONDOWN:
                    keyEvent = new KeyEvent { Keycode = Keycode.VK_LMB, IsKeyUp = false };
                    _eventChannel.Writer.TryWrite(keyEvent);
                    break;
                case WM_LBUTTONUP:
                    keyEvent = new KeyEvent { Keycode = Keycode.VK_LMB, IsKeyUp = true };
                    _eventChannel.Writer.TryWrite(keyEvent);
                    break;
                case WM_RBUTTONDOWN:
                    keyEvent = new KeyEvent { Keycode = Keycode.VK_RMB, IsKeyUp = false };
                    _eventChannel.Writer.TryWrite(keyEvent);
                    break;
                case WM_RBUTTONUP:
                    keyEvent = new KeyEvent { Keycode = Keycode.VK_RMB, IsKeyUp = true };
                    _eventChannel.Writer.TryWrite(keyEvent);
                    break;
                case WM_XBUTTONDOWN:
                    var mouseStruct = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
                    var keycode = (mouseStruct.mouseData >> 16) == 1 ? Keycode.VK_M4 : Keycode.VK_M5;
                    keyEvent = new KeyEvent { Keycode = keycode, IsKeyUp = false };
                    _eventChannel.Writer.TryWrite(keyEvent);
                    break;
                case WM_XBUTTONUP:
                    var _mouseStruct = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
                    var _keycode = (_mouseStruct.mouseData >> 16) == 1 ? Keycode.VK_M4 : Keycode.VK_M5;
                    keyEvent = new KeyEvent { Keycode = _keycode, IsKeyUp = true };
                    _eventChannel.Writer.TryWrite(keyEvent);
                    break;
                case WM_MBUTTONDOWN:
                    keyEvent = new KeyEvent { Keycode = Keycode.VK_MMB, IsKeyUp = false };
                    _eventChannel.Writer.TryWrite(keyEvent);
                    break;
                case WM_MBUTTONUP:
                    keyEvent = new KeyEvent { Keycode = Keycode.VK_MMB, IsKeyUp = true };
                    _eventChannel.Writer.TryWrite(keyEvent);
                    break;
            }

            return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }



        private static CancellationTokenSource _cts = new CancellationTokenSource();
        private void StartEventProcessor()
        {
            Task.Run(async () =>
            {
                await foreach (var keyEvent in _eventChannel.Reader.ReadAllAsync(_cts.Token))
                {
                    try
                    {
                        RaiseEvent(keyEvent);
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, "at input handling");
                    }
                }
            });
        }

        static void RaiseEvent(KeyEvent keyEvent)
        {
            if (!keyEvent.IsKeyUp)
            {
                if (CurrentPressed.Contains(keyEvent.Keycode)) return;

                CurrentPressed.AddLast(keyEvent.Keycode);
                KeysPressed?.Invoke(new LinkedList<Keycode>(CurrentPressed.ToArray()));
                return;
            }

            CurrentPressed.Remove(keyEvent.Keycode);
        }

        public delegate void KeysPressedEventHandler(LinkedList<Keycode> keycodes);
        public static event KeysPressedEventHandler KeysPressed;
        public static LinkedList<Keycode> CurrentPressed = new ();
        public KeyListener()
        {
            Hook();
        }
        ~KeyListener()
        {
            Dispose();
        }


        static IntPtr _kbHook = IntPtr.Zero;
        static IntPtr _mouseHook = IntPtr.Zero;
        static LowLevelInputProc _kbCallback;
        static LowLevelInputProc _mouseCallback;
        private static IntPtr SetHook(LowLevelInputProc proc, IntPtr hookId)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
                return SetWindowsHookEx((int)hookId, proc, GetModuleHandle(curModule.ModuleName), 0);
        }

        void Hook()
        {
            if (_kbCallback is null || _mouseCallback is null)
            {
                Thread keyboardHookThread = new Thread(() =>
                {
                    _kbCallback = KeyboardCallback;
                    _kbHook = SetHook(_kbCallback, new IntPtr(WH_KEYBOARD_LL));
                    Dispatcher.Run();
                });

                Thread mouseHookThread = new Thread(() =>
                {
                    _mouseCallback = MouseCallback;
                    _mouseHook = SetHook(_mouseCallback, new IntPtr(WH_MOUSE_LL));
                    Dispatcher.Run();
                });

                keyboardHookThread.IsBackground = true;
                mouseHookThread.IsBackground = true;

                keyboardHookThread.Start();
                mouseHookThread.Start();
                StartEventProcessor();
            }
        }
        void Unhook()
        {
            if (_kbCallback is not null && _mouseCallback is not null)
            {
                UnhookWindowsHookEx(_kbHook);
                UnhookWindowsHookEx(_mouseHook);
            }
        }

        public void Dispose()
        {
            Unhook();
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}
