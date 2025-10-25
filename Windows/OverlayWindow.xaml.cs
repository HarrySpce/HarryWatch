using Harry.Controls;
using Harry.Interception;
using Harry.Interception.Modules;
using Harry.Interception.PacketProviders;
using Harry.Utility;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

using static System.Net.Mime.MediaTypeNames;

using Path = System.Windows.Shapes.Path;

namespace Harry.Windows
{
    public partial class OverlayWindow : Window
    {
        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            const int WS_EX_TOOLWINDOW = 0x00000080;
            const int WS_EX_TRANSPARENT = 0x00000020;
            const int GWL_EXSTYLE = -20;

            [DllImport("user32.dll")]
            static extern int GetWindowLong(IntPtr hwnd, int index);

            [DllImport("user32.dll")]
            static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

            var hwnd = new WindowInteropHelper(this).Handle;
            var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW);
        }



        DispatcherTimer timer;
        PacketModuleBase m;
        XboxProvider provider;
        TimeSpan delta = TimeSpan.FromSeconds(0.5);
        public OverlayWindow()
        {
            InitializeComponent();
            provider = InterceptionManager.GetProvider("Xbox") as XboxProvider;
            Topmost = true;
            timer = new DispatcherTimer();
            timer.Tick += Tick;
            timer.Interval = delta;
            timer.Start();
            Closed += (s, e) => timer.Stop();
        }

        private async void Tick(object? sender, EventArgs e)
        {
            if (!CheckGameFocus())
            {
                Visibility = Visibility.Collapsed;
                timer.Interval = TimeSpan.FromSeconds(1);
                return;
            }
            // 1 3/3, 2 2/3
            if (Config.Instance.Settings.Overlay_DisableOnInactivity)
            {
                var dur = provider.InstanceDuration();
                if (dur == TimeSpan.Zero)
                {
                    this.ElementFadeOut();
                    return;
                }
            }

            CheckModules();
            CheckInstanceTimer();

            if (Visibility == Visibility.Collapsed)
            {
                TryFollowWindow();
                D2CharacterTracker.Update();
                this.ElementFadeIn();
                timer.Interval = delta;
            }
        }



        static Process cachedProcess;
        const string targetProcessName = "destiny2";
        static bool LastGameFocusResult = false;
        static DateTime lastCheckTime = DateTime.MinValue;
        public static bool CheckGameFocus(bool skipCheck = false)
        {
            if (!skipCheck && DateTime.Now - lastCheckTime < TimeSpan.FromSeconds(1))
                return LastGameFocusResult;

            lastCheckTime = DateTime.Now;

            if (cachedProcess == null)
            {
                var process = Process.GetProcessesByName(targetProcessName);
                if (!process.Any()) return LastGameFocusResult = false;
                cachedProcess = process.First();
            }

            if (cachedProcess.HasExited)
            {
                cachedProcess = null;
                return CheckGameFocus(skipCheck);
            }

            if (!GetForegroundWindow().Equals(cachedProcess.MainWindowHandle))
            {
                return LastGameFocusResult = false;
            }

            lastCheckTime += TimeSpan.FromSeconds(1.5);
            return LastGameFocusResult = true;
        }

        public bool TryFollowWindow()
        {
            if (GetWindowRect(cachedProcess.MainWindowHandle, out var rect))
            {
                //TODO: 4k issue

                double h_base = 1440d;
                double w_base = 2560d;
                var ratio_base = w_base / h_base;

                double win_h = rect.Bottom - rect.Top;
                double win_w = rect.Right - rect.Left;
                var ratio = win_w / win_h;
                
                double x_multiplier = win_w / w_base;
                double y_multiplier = win_h / h_base;

                double yoffset = 0;
                double xoffset = 0;

                // widescreen
                if (ratio > ratio_base) xoffset += (win_w - win_h * ratio_base) / 2;
                if (ratio < ratio_base) yoffset += (win_h - win_w / ratio_base) / 2;
                
                // 345 200
                Width = (win_w - xoffset * 2) / 4;
                Height = (win_h - yoffset * 2) / 4;
                //Logger.Debug($"{win_w}/{win_h} -> {Width}:{Height}, xoff-{xoffset}, yoff-{yoffset}");

                Left = rect.Left + xoffset + 118 * (win_w - xoffset * 2) / w_base + Config.Instance.Settings.Overlay_LeftOffset;
                Top = rect.Top + yoffset + ((win_h - yoffset * 2) * 3 / 4) - Config.Instance.Settings.Overlay_BottomOffset;

                Scale.ScaleX = (win_w - xoffset * 2) / w_base;
                Scale.ScaleY = (win_h - yoffset * 2) / h_base;

                //Left = 0;
                //Top = 0;
                //Width = 2560 / 2;
                //Height = 1440;
                //Left = rect.Left + xoffset + 123d * x_multiplier;
                //Top = rect.Bottom - yoffset - 318d * y_multiplier;
                //Height = rect.Bottom - rect.Top - yoffset * 2;
                //Width = Height * target;
                //Width = 345d * x_multiplier * 2;
                //Height = 200d * y_multiplier * 2;
                return true;
            }
            return false;
        }



        Dictionary<PacketModuleBase, EnabledModuleTimer> modules = new();
        private void CheckModules()
        {
            foreach (var m in InterceptionManager.Modules)
            {
                var c = modules.ContainsKey(m);
                var e = m.IsEnabled;
                var a = m.IsActivated;

                // displayed
                if (c)
                {
                    var ui = modules[m];

                    if (!m.Togglable && Config.Instance.Settings.Overlay_DisplayOnlyTogglable && !a)
                    {
                        modules.Remove(m);
                        ui.ElementFadeOut();
                        Task.Run(async () =>
                        {
                            await Task.Delay(TimeSpan.FromSeconds(0.5));
                            Dispatcher.Invoke(() => Modules.Children.Remove(ui));
                        });
                        continue;
                    }

                    // disabled = disappear
                    if (!e)
                    {
                        modules.Remove(m);
                        ui.ElementFadeOut();
                        Task.Run(async () =>
                        {
                            await Task.Delay(TimeSpan.FromSeconds(0.5));
                            Dispatcher.Invoke(() => Modules.Children.Remove(ui));
                        });
                        continue;
                    }

                    // enabled = count
                    if (m.Togglable || !Config.Instance.Settings.Overlay_DisplayOnlyTogglable)
                    {
                        ui.UpdateTimer();
                        continue;
                    }
                }

                if (!m.Togglable && Config.Instance.Settings.Overlay_DisplayOnlyTogglable && !m.IsActivated)
                {
                    continue;
                }

                // Something got enabled
                if (!c && e)
                {
                    var ui = new EnabledModuleTimer(m.Name) { Visibility = Visibility.Hidden };
                    modules.Add(m, ui);
                    Modules.Children.Add(ui);
                    ui.ElementFadeIn();
                }
            }
        }
        private void CheckInstanceTimer()
        {
            if (D2CharacterTracker.RaidsCount > 0)
            {
                RaidLabel.Content = $"{D2CharacterTracker.RaidsCount}";
                Raid.ElementFadeIn();
            }
            else
            {
                Raid.ElementFadeOut();
            }


            if (Config.Instance.Settings.Overlay_ShowTime)
            {
                Timer.Content = DateTime.Now.ToString("hh':'mm':'ss");
                Timer.ElementFadeIn();
            }
            else 
            {
                if (!provider.IsEnabled || !Config.Instance.Settings.Overlay_ShowTimer)
                {
                    Timer.ElementFadeOut();
                    return;
                }

                var duration = provider.InstanceDuration();
                if (duration == TimeSpan.Zero)
                {
                    Timer.ElementFadeOut();
                    return;
                }

                Timer.Content = duration.ToString("hh':'mm':'ss");
                Timer.ElementFadeIn();

                if (duration > TimeSpan.FromSeconds(30) && (Timer.Opacity < 0.5 || Timer.Visibility != Visibility.Visible))
                {
                    Timer.Opacity = 1;
                    Timer.Visibility = Visibility.Visible;
                }
            }
        }
    }
}
