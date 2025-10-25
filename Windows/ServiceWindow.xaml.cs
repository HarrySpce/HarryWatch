 using Harry.Controls;
using Harry.Utility;

using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;


using Application = System.Windows.Application;

namespace Harry
{
    public partial class ServiceWindow : Window
    {
        bool loaded = false;
        public ServiceWindow()
        {
            Visibility = Visibility.Collapsed;
            InitializeComponent();
            MouseDown += ServiceWindow_MouseDown;
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () => SetupValues());
        }

        private void SetupValues()
        {
            VersionString.Content = $"{Updater.VersionString}";
            var left = MainWindow.Instance.Checker.TimeLeft;
            if (left < TimeSpan.FromDays(400))
            {
                var month = left.Days / 30;
                if (month > 0)
                {
                    TimeString.Content = $"Expires in {month} month{(month > 1 ? "s" : "")}";
                }
                else if (left.Days > 0)
                {
                    TimeString.Content = $"Expires in {left.Days} day{(left.Days > 1 ? "s" : "")}";
                }
                else
                {
                    TimeString.Content = $"Expires in {left.Hours} hour{(left.Hours > 1 ? "s" : "")}";
                }
            }

            User_Nickname.Content = MainWindow.Instance.Checker.Name;
            User_Rank.Content = MainWindow.Instance.Checker.Type.ToString();

            MAIN_Clock.SetState(Config.Instance.Settings.Window_DisplayClock);
            MAIN_Speed.SetState(Config.Instance.Settings.Window_DisplaySpeed);
            MAIN_DecaySecs.Text = Config.Instance.Settings.Window_TimerDecaySeconds.ToString();
            MAIN_Snow.SetState(Config.Instance.Settings.Window_Snow);

            AHK_Close.SetState(Config.Instance.Settings.AHK_AutoClose);
            AHK_Start.SetState(Config.Instance.Settings.AHK_AutoOpen);

            KB_Log.SetState(Config.Instance.Settings.DB_KeyPresses);
            DB_Save.SetState(Config.Instance.Settings.DB_SavePackets);
            //DB_MaxAge.Text = Config.Instance.Settings.DB_HoursMaxAge.ToString();

            KeybindsSuppress.SetState(Config.Instance.Settings.AltTabSupressKeybinds);
            OverlayActive.SetState(Config.Instance.Settings.Overlay_DisableOnInactivity);
            OverlayStartup.SetState(Config.Instance.Settings.Overlay_StartOnLaunch);
            ShowTime.SetState(Config.Instance.Settings.Overlay_ShowTime);
            ShowTimer.SetState(Config.Instance.Settings.Overlay_ShowTimer);
            ShowAllTimers.SetState(!Config.Instance.Settings.Overlay_DisplayOnlyTogglable);
            CountRaids.SetState(Config.Instance.Settings.Tracker_CountRaids);
            CountDungeons.SetState(Config.Instance.Settings.Tracker_CountDungeons);
            OverlayX.Text = Config.Instance.Settings.Overlay_LeftOffset.ToString();
            OverlayY.Text = Config.Instance.Settings.Overlay_BottomOffset.ToString();
            BungieName.Text = Config.Instance.Settings.Tracker_BungieName ?? "Name#0000";
            BungieName.Foreground = Application.Current.Resources[Config.Instance.Settings.Tracker_BungieName is not null ? "AccentColor" : "InactiveColor"] as SolidColorBrush;
            
            Visibility = Visibility.Visible;
            loaded = true;
        }

        private void ExitButtonClick(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void MAIN_Clock_Click(object sender, RoutedEventArgs e)
        {
            Config.Instance.Settings.Window_DisplayClock = MAIN_Clock.Checked;
            Config.Save();
            if (MainWindow.Instance.CurrentTime.Visibility == Visibility.Visible != MAIN_Clock.Checked)
            {
                if (MAIN_Clock.Checked)
                {
                    MainWindow.Instance.CurrentTime.ElementAppear();
                }
                else
                {
                    MainWindow.Instance.CurrentTime.ElementDisappear();
                }
            }
        }

        private void MAIN_Snow_Click(object sender, RoutedEventArgs e)
        {
            Config.Instance.Settings.Window_Snow = MAIN_Snow.Checked;
            App.snow = MAIN_Snow.Checked;
            Config.Save();
        }

        private void MAIN_Speed_Click(object sender, RoutedEventArgs e)
        {
            Config.Instance.Settings.Window_DisplaySpeed = MAIN_Speed.Checked;
            Config.Save();
            if (MainWindow.Instance.Speed.Visibility == Visibility.Visible != MAIN_Speed.Checked)
            {
                if (MAIN_Speed.Checked)
                {
                    MainWindow.Instance.Speed.ElementAppear();
                }
                else
                {
                    MainWindow.Instance.Speed.ElementDisappear();
                }
            }
        }

        private void MAIN_ModuleDecayTime_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!loaded) return;
            if (int.TryParse(MAIN_DecaySecs.Text.Trim(), out int secs))
            {
                Config.Instance.Settings.Window_TimerDecaySeconds = secs;
                Config.Save();
            }
        }




        private void KB_Log_Click(object sender, RoutedEventArgs e)
        {
            Config.Instance.Settings.DB_KeyPresses = KB_Log.Checked;
            Config.Save();

            if (Config.Instance.Settings.DB_KeyPresses)
                KeyListener.KeysPressed += MainWindow.Instance.KeyLogger;
            else
                KeyListener.KeysPressed -= MainWindow.Instance.KeyLogger;
        }

        private void DB_Save_Click(object sender, RoutedEventArgs e)
        {
            Config.Instance.Settings.DB_SavePackets = DB_Save.Checked;
            Config.Save();
        }

        //private void DB_MaxAge_TextChanged(object sender, TextChangedEventArgs e)
        //{
        //    if (!loaded) return;
        //    if (int.TryParse(DB_MaxAge.Text, out int hours))
        //    {
        //        if (hours > 48 || hours < 1)
        //        {
        //            DB_MaxAge.Text = Config.Instance.Settings.DB_HoursMaxAge.ToString();
        //            return;
        //        }

        //        Config.Instance.Settings.DB_HoursMaxAge = hours;
        //        Config.Save();
        //    } 
        //}

        private void OverlayX_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!loaded) return;
            if (int.TryParse(OverlayX.Text.Trim(), out var px))
            { 
                Config.Instance.Settings.Overlay_LeftOffset = px;
                OverlayX.Text = Config.Instance.Settings.Overlay_LeftOffset.ToString();
                Config.Save();
                MainWindow.Instance.overlay?.TryFollowWindow();
            }
        }

        private void OverlayY_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!loaded) return;
            if (int.TryParse(OverlayY.Text.Trim(), out var px))
            {
                Config.Instance.Settings.Overlay_BottomOffset = px;
                OverlayY.Text = Config.Instance.Settings.Overlay_BottomOffset.ToString();
                Config.Save();
                MainWindow.Instance.overlay?.TryFollowWindow();
            }
        }

        private void AHK_Click(object sender, RoutedEventArgs e)
        {
            var cb = sender as Checkbox;
            if (cb.Name.Contains("Start"))
                Config.Instance.Settings.AHK_AutoOpen = cb.Checked;
            else
                Config.Instance.Settings.AHK_AutoClose = cb.Checked;
            Config.Save();
        }


        private void BungieName_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!BungieName.IsEnabled) return;

            if (string.IsNullOrEmpty(BungieName.Text) || BungieName.Text == Config.Instance.Settings.Tracker_BungieName)
            {
                if (Config.Instance.Settings.Tracker_BungieName is not null)
                {
                    BungieName.Text = Config.Instance.Settings.Tracker_BungieName;
                    BungieName.Foreground = Application.Current.Resources["AccentColor"] as SolidColorBrush;
                }
                else
                {
                    BungieName.Text = $"Name#0000";
                    BungieName.Foreground = Application.Current.Resources["InactiveColor"] as SolidColorBrush;
                }
            }
        }
        private void ServiceWindow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!BungieName.IsEnabled) return;

            if (string.IsNullOrEmpty(BungieName.Text) || BungieName.Text == Config.Instance.Settings.Tracker_BungieName)
            {
                if (Config.Instance.Settings.Tracker_BungieName is not null)
                {
                    BungieName.Text = Config.Instance.Settings.Tracker_BungieName;
                    BungieName.Foreground = Application.Current.Resources["AccentColor"] as SolidColorBrush;
                }
                else
                {
                    BungieName.Text = $"Name#0000";
                    BungieName.Foreground = Application.Current.Resources["InactiveColor"] as SolidColorBrush;
                }
            }
        }

        private void BungieName_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (BungieName.IsEnabled && BungieName.Text == (Config.Instance.Settings.Tracker_BungieName ?? "Name#0000"))
            {
                BungieName.Text = string.Empty;
                BungieName.Foreground = Application.Current.Resources["AccentColor"] as SolidColorBrush;
            }
        }

        private void BungieName_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (FindButton is null || BungieName is null) return;

            if (string.IsNullOrEmpty(BungieName.Text) || BungieName.Text == (Config.Instance.Settings.Tracker_BungieName ?? "Name#0000"))
            {
                FindButton.ElementDisappear();
                return;
            }

            Regex validation = new Regex(@"(.+?)#(\d{1,4})");
            if (validation.IsMatch(BungieName.Text))
            {
                FindButton.Text = "Find";
                FindButton.ElementAppear();
            }
            else
            {
                FindButton.ElementDisappear();
            }
        }

        private void FindButton_Click(object sender, RoutedEventArgs e)
        {
            Regex validation = new Regex(@"(.+?)#(\d{1,4})");
            var m = validation.Match(BungieName.Text);
            if (!m.Success) return;

            FindButton.Text = "Wait";
            BungieName.IsEnabled = FindButton.IsEnabled = false;

            Task.Run(async () =>
            {
                bool res = false;

                try
                {
                    res = await D2CharacterTracker.TrySetUser(m.Groups[1].Value, short.Parse(m.Groups[2].Value));
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                }
                 
                Dispatcher.Invoke(() =>
                {
                    FindButton.Text = "Find";
                    FindButton.ElementDisappear();
                    if (res)
                    {
                        BungieName.Text = Config.Instance.Settings.Tracker_BungieName = m.Value;
                        Config.Save();
                        BungieName.Foreground = Application.Current.Resources["AccentColor"] as SolidColorBrush;
                    }
                    else
                    {
                        BungieName.Text = "Name#0000";
                        BungieName.Foreground = Application.Current.Resources["InactiveColor"] as SolidColorBrush;
                    }
                    BungieName.IsEnabled = FindButton.IsEnabled = true;
                });
            });
        }

        private void OverlayActive_Click(object sender, RoutedEventArgs e)
        {
            Config.Instance.Settings.Overlay_DisableOnInactivity = OverlayActive.Checked;
            Config.Save();
        }

        private void OverlayTogglable_Click(object sender, RoutedEventArgs e)
        {
            Config.Instance.Settings.Overlay_DisplayOnlyTogglable = !ShowAllTimers.Checked;
            Config.Save();
        }

        private void CountRaids_Click(object sender, RoutedEventArgs e)
        {
            Config.Instance.Settings.Tracker_CountRaids = CountRaids.Checked;
            Config.Save();
        }

        private void CountDungeons_Click(object sender, RoutedEventArgs e)
        {
            Config.Instance.Settings.Tracker_CountDungeons = CountDungeons.Checked;
            Config.Save();
        }

        private void OverlayTime_Click(object sender, RoutedEventArgs e)
        {
            Config.Instance.Settings.Overlay_ShowTime = ShowTime.Checked;
            Config.Save();
        }
        private void OverlayTimer_Click(object sender, RoutedEventArgs e)
        {
            Config.Instance.Settings.Overlay_ShowTimer = ShowTimer.Checked;
            Config.Save();
        }

        private void OverlayStartup_Click(object sender, RoutedEventArgs e)
        {
            Config.Instance.Settings.Overlay_StartOnLaunch = OverlayStartup.Checked;
            Config.Save();
        }

        // TODO: FIND HOW TO BIND CHECKBOXES
        
        private void KeysSuppress_Click(object sender, RoutedEventArgs e)
        {
            Config.Instance.Settings.AltTabSupressKeybinds = KeybindsSuppress.Checked;
            Config.Save();
        }
    }
}
