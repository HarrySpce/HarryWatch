using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Harry.Interception;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System.Windows.Threading;
using Harry.Controls;
using Harry.Windows.Controls;
using System.ComponentModel;
using Harry.Windows;
using Harry.Interception.Modules;
using Harry.Utility;
using System.Windows.Media.Effects;
using System.Diagnostics;
using System.Windows.Forms;
using Application = System.Windows.Application;
using System.Windows.Controls;

namespace Harry
{
    public partial class MainWindow : Window
    {
        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (Config.Instance.Volume != VolumeSlider.Value)
            {
                Config.Instance.Volume = VolumeSlider.Value;
                InterceptionManager.Modules.ForEach(module =>
                {
                    module.DisableSound.Volume =
                    module.EnableSound.Volume = Config.Instance.Volume / 100d;
                });
            }
        }

        DateTime lastModuleSelection = DateTime.MinValue;
        private void ModuleSelectionClick(object sender, RoutedEventArgs e)
        {
            if (DateTime.Now - lastModuleSelection < TimeSpan.FromSeconds(0.5))
                return;

            lastModuleSelection = DateTime.Now;
            var time = TimeSpan.FromSeconds(0.3);
            DescriptionPanel.Visibility = Visibility.Collapsed;

            TopBlock.ElementFadeOut(time);
            ModuleSettingsBorder.ElementFadeOut(time);
            ModuleSelection.ElementFadeIn(time);

            int i = 0;
            int j = 0;
            foreach (var c in ModuleSelection.Children)
            {
                if (c is WindowControlButton b)
                {
                    var sum = i + j;
                    b.Visibility = Visibility.Hidden;
                    Dispatcher.BeginInvoke(async () =>
                    {
                        await Task.Delay(sum * TimeSpan.FromSeconds(0.15));
                        b.ElementFadeIn(TimeSpan.FromSeconds(0.4));
                    });

                    i++;
                    if (i == 4)
                    {
                        j++;
                        i = 0;
                    }
                }
            }
        }

        private void NewModuleClicked(object sender, RoutedEventArgs e)
        {
            var time = TimeSpan.FromSeconds(0.3);

            var moduleButton = sender as WindowControlButton;
            Config.Instance.CurrentModule = moduleButton.Name.Replace("_", " ");
            UpdateSelectedModule();

            TopBlock.ElementFadeIn(time);
            ModuleSettingsBorder.ElementFadeIn(time);
            ModuleSelection.ElementFadeOut(time);
        }

        private void Description_Click(object sender, RoutedEventArgs e)
        {
            if (DescriptionPanel.Visibility == Visibility.Visible)
            {
                DescriptionPanel.ElementDisappear();
            }
            else
            {
                DescriptionPanel.ElementAppear();
            }
        }

        private void LogButtonClick(object sender, RoutedEventArgs e)
        {
            Thread thread = new Thread(() =>
            {
                var log = new LogsViewer() { WindowStartupLocation = WindowStartupLocation.CenterScreen };
                log.Show();
                log.Closed += (sender2, e2) => log.Dispatcher.InvokeShutdown();
                Dispatcher.Run();
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
        }

        private void ExitButtonClick(object sender, RoutedEventArgs e)
        {
            Process.GetCurrentProcess().CloseMainWindow();
            Application.Current.Shutdown();
        }

        private void TrayButtonClick(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
            this.ShowInTaskbar = false;
            //this.Visibility = Visibility.Collapsed;

            tbIcon.ContextMenu = new System.Windows.Controls.ContextMenu()
            {
                Items =
                {
                    new System.Windows.Controls.MenuItem()
                    {
                        Header = "Exit",
                        Command = new ExitCommand()
                    }
                }
            };
            tbIcon.Visibility = Visibility.Visible;
            tbIcon.TrayMouseDoubleClick += TrayIconClick;
            tbIcon.TrayRightMouseDown += TrayIconRightClick;
        }

        private void TrayIconRightClick(object? sender, EventArgs e)
        {
            if (sender is TaskbarIcon icon)
            {
                icon.ContextMenu.IsOpen = true;
            }
        }

        private void TrayIconClick(object? sender, EventArgs e)
        {
            this.WindowState = WindowState.Normal;
            this.ShowInTaskbar = true;
            var old = this.Topmost;
            this.Topmost = true;
            this.Topmost = old;

            if (sender is TaskbarIcon icon)
            {
                icon.Visibility = Visibility.Hidden;
                icon.TrayMouseDoubleClick -= TrayIconClick;
            }
        }

        private void SoundButtonClick(object sender, RoutedEventArgs e)
        {
            if (VolumeSlider.Visibility == Visibility.Visible)
            {
                VolumeSlider.ElementFadeOut();
            }
            else
            {
                VolumeSlider.ElementFadeIn();
            }
        }

        private void ServiceWindowButtonClick(object sender, RoutedEventArgs e)
        {
            var app = new ServiceWindow()
            {
                Left = this.Left + this.Width + 4,
                Top = this.Top
            };
            app.Show();
        }

        private void AhkClick(object sender, RoutedEventArgs e)
        {
            var app = new AhkManagerWindow()
            {
                Left = this.Left + this.Width + 4,
                Top = this.Top
            };
            app.Show();
        }

        private void PinClick(object sender, RoutedEventArgs e)
        {
            this.Topmost = !this.Topmost;
            Pin.FillColor = Topmost ? Pin.GlowColor : (SolidColorBrush)Application.Current.FindResource("InactiveColor");
        }

        OverlayWindow _overWin;

        private void OverlayClick(object sender, RoutedEventArgs e)
        {
            if (_overWin == null)
            {
                _overWin = new OverlayWindow();
            }
            else
            {
                _overWin.Close();
                _overWin = null;
            }

            Overlay.FillColor = _overWin != null ? Pin.GlowColor : (SolidColorBrush)Application.Current.FindResource("InactiveColor");
        }

        private void CurrentModuleToggle(object sender, RoutedEventArgs e)
        {
            var b = FindName(CurrentModuleName.Replace(" ", "_")) as WindowControlButton;
            if (ModuleCheckbox.Checked)
            {
                CurrentModule.StartListening();
                b.stayActive = true;
                b.Border_MouseEnter();
            }
            else
            {
                CurrentModule.StopListening();
                b.stayActive = false;
                b.Border_MouseLeave();
            }
        }

        private void PveOutCBClick(object sender, RoutedEventArgs e)
        {
            var m = CurrentModule as PveModule;
            if (m.IsEnabled)
            {
                m.ToggleSwitch(ref PveModule.Outbound);
            }
        }

        private void PveInCBClick(object sender, RoutedEventArgs e)
        {
            var m = CurrentModule as PveModule;
            if (m.IsEnabled)
            {
                m.ToggleSwitch(ref PveModule.Inbound);
            }
        }

        private void PveSlowInCBClick(object sender, RoutedEventArgs e)
        {
            var m = CurrentModule as PveModule;
            if (m.IsEnabled)
            {
                m.ToggleSwitch(ref PveModule.SlowInbound);
            }
        }

        private void PveSlowOutCBClick(object sender, RoutedEventArgs e)
        {
            var m = CurrentModule as PveModule;
            if (m.IsEnabled)
            {
                m.ToggleSwitch(ref PveModule.SlowOutbound);
            }
        }

        private void PveResyncCBClick(object sender, RoutedEventArgs e)
        {
            PveModule.AutoResync = PveResyncCB.Checked;
            Config.Save();
        }

        private void PveBufferCBClick(object sender, RoutedEventArgs e)
        {
            PveModule.Buffer = PveBufferCB.Checked;
            Config.Save();
        }

        private void PvpOutCBClick(object sender, RoutedEventArgs e)
        {
            var m = CurrentModule as PvpModule;
            if (m.IsEnabled)
            {
                m.ToggleSwitch(ref PvpModule.Outbound);
            }
        }

        private void PvpInCBClick(object sender, RoutedEventArgs e)
        {
            var m = CurrentModule as PvpModule;
            if (m.IsEnabled)
            {
                m.ToggleSwitch(ref PvpModule.Inbound);
            }
        }

        private void PvpResyncCBClick(object sender, RoutedEventArgs e)
        {
            PvpModule.AutoResync = PvpResyncCB.Checked;
            Config.Save();
        }
        
        private void PvpBufferCBClick(object sender, RoutedEventArgs e)
        {
            PvpModule.Buffer = PvpBufferCB.Checked;
            Config.Save();
        }

        private void InstBufferCBClick(object sender, RoutedEventArgs e)
        {
            InstanceModule.Buffer = InstBufferCB.Checked;
            Config.Save();
        }

        private void MS_PlayersClick(object sender, RoutedEventArgs e)
        {
            MultishotModule.PlayersMode = MS_PVP.Checked;
            Config.Save();
        }

        private void MS_DetectClick(object sender, RoutedEventArgs e)
        {
            MultishotModule.WaitShot = MS_DETECT.Checked;
            Config.Save();
        }

        private void MS_InboundClick(object sender, RoutedEventArgs e)
        {
            Config.GetNamed(CurrentModuleName).Settings["Inbound"] = MS_INBOUND.Checked;
            MultishotModule.Inbound = MS_INBOUND.Checked;
            Config.Save();
        }

        private void MS_OutboundClick(object sender, RoutedEventArgs e)
        {
            Config.GetNamed(CurrentModuleName).Settings["Outbound"] = MS_OUTBOUND.Checked;
            MultishotModule.Outbound = MS_OUTBOUND.Checked;
            Config.Save();
        }

        private void MS_TogglableClick(object sender, RoutedEventArgs e)
        {
            Config.GetNamed(CurrentModuleName).Settings["Togglable"] = MS_TOGGLABLE.Checked;
            (CurrentModule as MultishotModule).Togglable = MS_TOGGLABLE.Checked;
            if (!MS_TOGGLABLE.Checked)
            {
                (CurrentModule as MultishotModule).ForceDisable();
                var d = DisplayModules.FirstOrDefault(x => x.ModuleName == CurrentModuleName);
                Dispatcher.BeginInvoke(async () =>
                {
                    d.ElementDisappear();
                    await Task.Delay(animationTime);
                    DisplayModules.Remove(d);
                });
            }

            Config.Save();
        }

        private void API_DisableClick(object sender, RoutedEventArgs e)
        {
            ApiModule.Disable = API_Disable.Checked;
            Config.Save();
        }

        private void API_BufferClick(object sender, RoutedEventArgs e)
        {
            ApiModule.Buffer = API_Buffer.Checked;
            Config.Save();
        }

        private void Slider_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is Slider s)
            {
                s.Value += e.Delta > 0 ? s.TickFrequency : -s.TickFrequency;
                e.Handled = true;
            }
        }

        private void MS_MaxTimeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            MS_MaxTimeSlider.Value = MultishotModule.MaxTime = Math.Clamp(Math.Round(e.NewValue /  0.05) * 0.05, 0.5, 1.8);

            var formatted = MS_MaxTimeSlider.Value.ToString("G").Replace(',', '.');
            var i = formatted.IndexOf("000");
            if (i >= 0)
            {
                formatted = formatted[0..i];
                if (formatted[^1] == '.')
                    formatted = formatted[0..^2];
            }
            MS_MaxTime.Content = $"{formatted}sec";
        }
    }
}
