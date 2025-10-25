using Harry.Interception;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;

namespace Harry.Controls
{
    public partial class ConnectionInfoPanel : UserControl
    {
        DispatcherTimer loop;
        public ConnectionInfoPanel()
        {
            InitializeComponent();
            Loaded += ConnectionInfoPanel_Loaded;
        }

        private async void ConnectionInfoPanel_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            while (!InterceptionManager.Providers.Any())
                await Task.Delay(500);

            providers.Add(InterceptionManager.GetProvider("Xbox"), (_3074, _3074DL, _3074UL));
            providers.Add(InterceptionManager.GetProvider("7500"), (_7500, _7500DL, _7500UL));
            providers.Add(InterceptionManager.GetProvider("Players"), (_27k, _27kDL, _27kUL));
            providers.Add(InterceptionManager.GetProvider("30000"), (_30k, _30kDL, _30kUL));

            loop = new DispatcherTimer();
            loop.Interval = TimeSpan.FromSeconds(0.5d);
            loop.Tick += Loop_Tick;
            loop.Start();
        }

        Dictionary<PacketProviderBase, (Border border, Label dl, Label ul)> providers = new();
        private void Loop_Tick(object? sender, EventArgs ev)
        {
            foreach (var p in providers)
            {
                try
                {
                    var provider = p.Key;
                    var border = p.Value.border;
                    var dl = p.Value.dl;
                    var ul = p.Value.ul;

                    if (border.Visibility == System.Windows.Visibility.Visible != provider.IsEnabled)
                    {
                        if (provider.IsEnabled)
                        {
                            border.ElementAppear();
                            if (Top.Visibility == System.Windows.Visibility.Collapsed)
                            {
                                Top.ElementAppear();
                            }
                        }
                        else
                        {
                            border.ElementDisappear();
                            if (providers.Count(x => x.Value.border.Visibility != System.Windows.Visibility.Collapsed) == 1) 
                            {
                                Top.ElementDisappear();
                            }
                        }
                    }

                    if (!provider.IsEnabled) continue;

                    var date = DateTime.Now - TimeSpan.FromSeconds(1);
                    var all = provider.CurrentQueue.TakeLast(200).Where(x => x.CreatedAt > date && x.IsSent).ToArray();
                    var inAmount = all.Where(x => x.Inbound).Sum(x => x.Length);
                    var outAmount = all.Where(x => !x.Inbound).Sum(x => x.Length);
                    Dispatcher.BeginInvoke(() =>
                    {
                        dl.Content = $"{inAmount.BytesLenghtToString()}/s";
                        dl.Opacity = inAmount == 0 ? 0.5 : 1;
                        //dl.Foreground = System.Windows.Application.Current.Resources[inAmount == 0 ? "InactiveColor" : "TitleColor"] as SolidColorBrush;
                        ul.Content = $"{outAmount.BytesLenghtToString()}/s";
                        ul.Opacity = outAmount == 0 ? 0.5 : 1;
                        //ul.Foreground = System.Windows.Application.Current.Resources[inAmount == 0 ? "InactiveColor" : "TitleColor"] as SolidColorBrush;
                    });
                }
                catch (Exception e)
                {
                    Logger.Error($"Connection panel ({p.Key.Name}): {e}");
                }
            }
        }
    }
}
