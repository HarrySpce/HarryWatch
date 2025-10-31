using Hardcodet.Wpf.TaskbarNotification.Interop;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

using Rectangle = System.Drawing.Rectangle;

namespace Harry
{
    public partial class App : System.Windows.Application
    {
        #region winBlur

        [DllImport("user32.dll")]
        internal static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

        [StructLayout(LayoutKind.Sequential)]
        internal struct WindowCompositionAttributeData
        {
            public WindowCompositionAttribute Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }
        internal enum WindowCompositionAttribute
        {
            WCA_ACCENT_POLICY = 19
        }
        internal enum AccentState
        {
            ACCENT_DISABLED = 0,
            ACCENT_ENABLE_GRADIENT = 1,
            ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
            ACCENT_ENABLE_BLURBEHIND = 3,
            ACCENT_ENABLE_ACRYLICBLURBEHIND = 4,
            ACCENT_INVALID_STATE = 5
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct AccentPolicy
        {
            public AccentState AccentState;
            public int AccentFlags;
            public int GradientColor;
            public int AnimationId;
            public int BlurRadius;
        }
        

        public void EnableBlur(Window window)
        {
            var windowHelper = new WindowInteropHelper(window);

            var accent = new AccentPolicy();
            var accentStructSize = Marshal.SizeOf(accent);
            accent.AccentState = AccentState.ACCENT_ENABLE_BLURBEHIND;

            var accentPtr = Marshal.AllocHGlobal(accentStructSize);
            Marshal.StructureToPtr(accent, accentPtr, false);

            var data = new WindowCompositionAttributeData();
            data.Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY;
            data.SizeOfData = accentStructSize;
            data.Data = accentPtr;

            SetWindowCompositionAttribute(windowHelper.Handle, ref data);

            Marshal.FreeHGlobal(accentPtr);
        }

        #endregion

        #region corners
        [DllImport("dwmapi.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        internal static extern void DwmSetWindowAttribute(IntPtr hwnd,
                                                 DWMWINDOWATTRIBUTE attribute,
                                                 ref DWM_WINDOW_CORNER_PREFERENCE pvAttribute,
                                                 uint cbAttribute);

        public enum DWMWINDOWATTRIBUTE
        {
            DWMWA_WINDOW_CORNER_PREFERENCE = 33
        }

        public enum DWM_WINDOW_CORNER_PREFERENCE
        {
            DWMWCP_DEFAULT = 0,
            DWMWCP_DONOTROUND = 1,
            DWMWCP_ROUND = 2,
            DWMWCP_ROUNDSMALL = 3
        }

        public void EnableRoundedCorners(Window window)
        {
            var windowHelper = new WindowInteropHelper(window);
            var preference = DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUND;
            DwmSetWindowAttribute(windowHelper.Handle, DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(uint));
        }
        #endregion

        public static string ExeDirectory => getExePath();

        static string dir;
        static string getExePath()
        {
            if (dir is not null) return dir;

            var currentDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location);
            if (currentDirectory != null)
            {
                Directory.SetCurrentDirectory(currentDirectory);
                return dir = currentDirectory;
            }

            return dir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
        }


        private void TransparentWindowLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is Window window)
            {
                EnableBlur(window);
            }
        }

        private void BorderMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Grid b && e.ChangedButton == MouseButton.Left && e.LeftButton == MouseButtonState.Pressed)
            {
                Window.GetWindow(b).DragMove();
            }
        }
    }
}
