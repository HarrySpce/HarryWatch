using OxyPlot.Wpf;

using Harry.Interception;
using Harry.Interception.Modules;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Harry.Controls
{
    public partial class EnabledModuleTimer : UserControl
    {
        PacketModuleBase Module;
        new public Brush Foreground
        {
            get { return (Brush)GetValue(ForegroundProperty); }
            set { SetValue(ForegroundProperty, value); ToggleButton.Data = Module.Icon; }
        }
        new public static readonly DependencyProperty ForegroundProperty =
            DependencyProperty.Register("Foreground", typeof(Brush), typeof(EnabledModuleTimer), new PropertyMetadata(Color.FromArgb(0xcc, 0xff, 0xff, 0xff).ToOxyColor().ToBrush()));

        public string ModuleName
        {
            get { return (string)GetValue(ModuleNameProperty); }
            set { Module = InterceptionManager.GetModule(value); ToggleButton.Data = Module.Icon; SetValue(ModuleNameProperty, value); }
        }
        public static readonly DependencyProperty ModuleNameProperty =
            DependencyProperty.Register("ModuleName", typeof(string), typeof(EnabledModuleTimer), new PropertyMetadata("None"));



        public double Scale
        {
            get { return (double)GetValue(ScaleProperty); }
            set { SetValue(ScaleProperty, value); }
        }
        public static readonly DependencyProperty ScaleProperty =
            DependencyProperty.Register("Scale", typeof(double), typeof(EnabledModuleTimer), new PropertyMetadata(0.8d));



        public double MinOpacity
        {
            get { return (double)GetValue(MinOpacityProperty); }
            set { SetValue(MinOpacityProperty, value); }
        }
        public static readonly DependencyProperty MinOpacityProperty =
            DependencyProperty.Register("MinOpacity", typeof(double), typeof(EnabledModuleTimer), new PropertyMetadata(0.7d));




        public EnabledModuleTimer()
        {
            InitializeComponent();
        }

        TimeSpan animTime = TimeSpan.FromSeconds(0.25);
        public EnabledModuleTimer(string moduleName)
        {
            InitializeComponent();

            DataContext = this;
            ModuleName = moduleName;
            ToggleButton.Data = Module.Icon;
        }

        bool lastActive = false;
        public void UpdateTimer()
        {
            var active = Module.IsActivated;

            if (Module is PveModule)
            {
                if (PveModule.Inbound || PveModule.SlowInbound)
                {
                    DL.ElementFadeIn();
                }
                else
                {
                    DL.ElementFadeOut();
                }
                if (PveModule.Outbound || PveModule.SlowOutbound)
                {
                    UL.ElementFadeIn();
                }
                else
                {
                    UL.ElementFadeOut();
                }
            }

            if (active != lastActive)
            {
                lastActive = active;
                if (lastActive)
                {
                    var anim = new DoubleAnimation(1.0d, animTime);
                    ToggleButton.OpacityMask = new LinearGradientBrush()
                    {
                        StartPoint = new Point(0d, 0d),
                        EndPoint = new Point(0d, 1d),
                        GradientStops =
                        {
                            new GradientStop(Color.FromArgb(255,255,255,255), 0.0d),
                            new GradientStop(Color.FromArgb(255,255,255,255), 0.6d),
                            new GradientStop(Color.FromArgb(0,255,255,255), 0.9d),
                            new GradientStop(Color.FromArgb(0,255,255,255), 1.0d),
                        }
                    };
                    ToggleButton.BeginAnimation(OpacityProperty, anim);
                    Dispatcher.BeginInvoke(async () =>
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            if (Module.IsActivated)
                            {
                                Timer.ElementFadeIn(checkOverride: true);
                            }
                            await Task.Delay(1000);
                        }
                    });
                }
                else
                {
                    var anim = new DoubleAnimation(MinOpacity, animTime);
                    ToggleButton.BeginAnimation(OpacityProperty, anim);
                    hide ??= Task.Run(async () =>
                    {
                        await Task.Delay(Config.Instance.Settings.Window_TimerDecaySeconds * 1000);
                        HideTimer();
                        hide = null;
                    });
                }
            }

            if (active)
            {
                var passed = DateTime.Now - Module.StartTime;
                Timer.Content = string.Format("{0:mm\\:ss}", passed);
            }
        }

        Task hide;
        void HideTimer()
        {
            if (!Module.IsActivated)
            {
                Dispatcher.BeginInvoke(() =>
                {
                    ToggleButton.OpacityMask = Brushes.White;
                    Timer.ElementFadeOut(checkOverride: true, timeOverride: TimeSpan.FromSeconds(0.2));
                });
            }
        }

        private void ToggleButton_MouseEnter(object sender, MouseEventArgs e)
        {
            var anim = new DoubleAnimation(lastActive ? MinOpacity : 1.0d, animTime);
            ToggleButton.BeginAnimation(OpacityProperty, anim);
        }

        private void ToggleButton_MouseLeave(object sender, MouseEventArgs e)
        {
            var anim = new DoubleAnimation(lastActive ? 1.0d : MinOpacity, animTime);
            ToggleButton.BeginAnimation(OpacityProperty, anim);
        }

        private void ToggleButton_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (Module is PveModule pve)
                {
                    if (!PveModule.SlowInbound && !PveModule.Inbound && !PveModule.Outbound && !PveModule.SlowOutbound)
                    {
                        pve.ForceEnable();
                    }
                    else
                    {
                        pve.ToggleSwitch(ref PveModule.Inbound, false);
                        pve.ToggleSwitch(ref PveModule.Outbound, false);
                        pve.ToggleSwitch(ref PveModule.SlowInbound, false);
                        pve.ToggleSwitch(ref PveModule.SlowOutbound, false);
                    }
                    return;
                }

                if (Module is PvpModule pvp)
                {
                    if (!PvpModule.Inbound && !PvpModule.Outbound)
                    {
                        pvp.ForceEnable();
                    }
                    else
                    {
                        pvp.ToggleSwitch(ref PvpModule.Inbound, false);
                        pvp.ToggleSwitch(ref PvpModule.Outbound, false);
                    }
                    return;
                }

                if (Module.IsActivated)
                    Module.ForceDisable();
                else
                    Module.ForceEnable();
            }
        }
    }
}
