using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Xml.Linq;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Reflection;

namespace Harry
{
    public static class AhkManager
    {
        public static Dictionary<string, int> Ahks = new Dictionary<string, int>();

        const string folderName = "Ahk";
        public static string directory;
        static bool init = false;

        public static void Init()
        {
            if (init) return;
            init = true;

            directory = Path.Combine(App.ExeDirectory, folderName);
            if (!File.Exists(directory))
                Directory.CreateDirectory(directory);

            Logger.Debug($"Ahk dir: {directory}");
            ReloadAhksFromDirectory();

            if (Config.Instance.Settings.AHK_AutoOpen)
            {
                foreach (var ahk in Config.Instance.LastOpenAhks)
                {
                    TryStartAhk(ahk);
                }
            }
        }


        public static bool TryStartAhk(string name)
        {
            if (Ahks.ContainsKey(name) && Ahks[name] > 0)
                return false;

            var path = Path.Combine(directory, name);
            if (!File.Exists(path))
                return false;

            var p = new Process();
            p.StartInfo = new ProcessStartInfo(path) { UseShellExecute = true };
            p.Start();
            Ahks[name] = p.Id;
            Logger.Debug($"Started {name} with pid {Ahks[name]}");
            return true;
        }
        public static bool TryStopAhk(string name)
        {
            if (Ahks.ContainsKey(name))
            {
                try
                {
                    var p = Process.GetProcessById((int)Ahks[name]);
                    p.Kill(true);
                    p.WaitForExit();
                    Ahks[name] = 0;
                    Logger.Debug($"Killed {name}");
                    return true;
                }
                catch (ArgumentException)
                {
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex);
                }
            }

            Logger.Debug($"{name} not found");
            return false;
        }


        public static void ReloadAhksFromDirectory()
        {
            Ahks.Clear();
            var query = Directory.GetFiles(directory, "*.ahk").Select(x => Path.GetFileName(x));
            foreach (string file in query)
                Ahks.Add(file, 0);

            PopulateOpenAhks();
            Logger.Debug($"Found {Ahks.Count} ahk scripts, {Ahks.Values.Where(x => x > 0).Count()} active");
        }

        static void PopulateOpenAhks()
        {
            [DllImport("USER32.DLL")]
            static extern IntPtr GetShellWindow();

            [DllImport("USER32.DLL")]
            static extern int GetWindowTextLength(IntPtr IntPtr);

            [DllImport("USER32.DLL")]
            static extern int GetWindowText(IntPtr IntPtr, StringBuilder lpString, int nMaxCount);

            [DllImport("USER32.DLL")]
            static extern bool EnumWindows(EnumWindowsProc enumFunc, int lParam);

            [DllImport("user32.dll", SetLastError = true)]
            static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

            IntPtr shellWindow = GetShellWindow();

            EnumWindows(delegate (IntPtr IntPtr, int lParam)
            {
                if (IntPtr == shellWindow) return true;

                int length = GetWindowTextLength(IntPtr);
                if (length == 0) return true;

                StringBuilder builder = new StringBuilder(length);
                GetWindowText(IntPtr, builder, length + 1);

                var title = builder.ToString();
                var split = title.Split(" - ");

                if (split.Length < 2 || !split[1].Contains("AutoHotkey")) return true;

                if (!split[0].Contains(directory) && !directory.Contains(split[0]))
                {
                    if (!split[0].Contains("launcher"))
                        Logger.Debug($"Wrong dir: {split[0]}");
                    return true;
                }

                var name = Path.GetFileName(split[0]);
                Logger.Debug($"Found working {name}");

                uint processID = 0;
                uint threadID = GetWindowThreadProcessId(IntPtr, out processID);
                Ahks[name] = (int)processID;
                return true;

            }, 0);
        }
        delegate bool EnumWindowsProc(IntPtr IntPtr, int lParam);
    }

    public partial class AhkManagerWindow : Window
    {
        const string tempvar = "q7i";
        Dictionary<string, int> Ahks => AhkManager.Ahks;
        
        public AhkManagerWindow()
        {
            InitializeComponent();
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () => Refresh());
        }

        void Refresh()
        {
            AhkManager.ReloadAhksFromDirectory();
            UpdateList();
        }

        void UpdateList()
        {
            Listing.Children.Clear();
            foreach (var s in Ahks.OrderBy(x => x.Key))
            {
                var name = s.Key.Replace(".ahk", "");
                var sp = new StackPanel();
                var cb = new Controls.Checkbox() { Name = tempvar + name.Replace(" ", tempvar) };
                cb.Click += Checkbox_Click;
                var tb = new TextBlock() { Text = name, Foreground = Application.Current.Resources["AccentColor"] as SolidColorBrush, Opacity = 0.8 };

                cb.SetState(s.Value > 0);
                sp.Children.Add(cb);
                sp.Children.Add(tb);
                Listing.Children.Add(sp);
            }
        }

        

        private void Checkbox_Click(object sender, RoutedEventArgs e)
        {
            var checkbox = sender as Controls.Checkbox;
            checkbox.IsEnabled = false;
            var name = checkbox.Name[tempvar.Length..].Replace(tempvar, " ") + ".ahk";

            var result = checkbox.Checked ? AhkManager.TryStartAhk(name) : AhkManager.TryStopAhk(name);
            if (!result)
            {
                checkbox.SetState(!checkbox.Checked);
            }

            Dispatcher.BeginInvoke(async () =>
            {
                await Task.Delay(300);
                checkbox.IsEnabled = true;
            });
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            Refresh();
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("explorer.exe", AhkManager.directory);
        }
    }
}
