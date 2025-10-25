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
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Harry.Controls
{
    public partial class CurrentTimeLabel : UserControl
    {
        public CurrentTimeLabel()
        {
            InitializeComponent();
            this.Loaded += OnLoad;
            var dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
            dispatcherTimer.Tick += new EventHandler(Tick);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
            dispatcherTimer.Start();

            
        }

        private void OnLoad(object sender, RoutedEventArgs e)
        {
            var current = DateTime.Now.ToString("HHmmss");
            for (int j = 0; j < 6; j++)
            {
                var curDigitLabel = FindName($"time1{j}") as Label;
                var newDigitLabel = FindName($"time0{j}") as Label;
                newDigitLabel.Opacity = 0;
                newDigitLabel.Content = "7";
                curDigitLabel.MaxWidth = curDigitLabel.MinWidth = newDigitLabel.MaxWidth = newDigitLabel.MinWidth = newDigitLabel.ActualWidth;
                curDigitLabel.HorizontalContentAlignment = newDigitLabel.HorizontalContentAlignment = curDigitLabel.HorizontalAlignment = newDigitLabel.HorizontalAlignment = HorizontalAlignment.Center;
                curDigitLabel.Content = newDigitLabel.Content = current[j].ToString();
                curDigitLabel.RenderTransform = new TranslateTransform();
                newDigitLabel.RenderTransform = new TranslateTransform();
            }
        }

        private void Tick(object sender, EventArgs e)
        {
            var current = DateTime.Now.ToString("HHmmss");
            float animationTime = 0.7f;

            for (int i = 0; i < 6; i++)
            {
                var curDigit = int.Parse(current[i].ToString());

                var curDigitLabel = FindName($"time1{i}") as Label;
                var newDigitLabel = FindName($"time0{i}") as Label;

                if (curDigit != int.Parse(curDigitLabel.Content as string))
                {
                    IEasingFunction easeInOut = new BackEase() { EasingMode = EasingMode.EaseInOut };
                    IEasingFunction easeOut = new BackEase() { EasingMode = EasingMode.EaseOut };
                    DoubleAnimation appearMove = new DoubleAnimation(0, newDigitLabel.ActualHeight, TimeSpan.FromSeconds(animationTime - 0.1)) { EasingFunction = easeInOut };
                    DoubleAnimation disappearMove = new DoubleAnimation(curDigitLabel.RenderTransform.Value.OffsetY, newDigitLabel.ActualHeight, TimeSpan.FromSeconds(animationTime)) { EasingFunction = easeOut };

                    DoubleAnimation appear = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromSeconds(animationTime - 0.1))) { EasingFunction = easeInOut };
                    DoubleAnimation disappear = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromSeconds(animationTime))) { EasingFunction = easeOut };

                    Dispatcher.BeginInvoke(async () =>
                    {
                        //curDigitLabel.Content = "O";

                        newDigitLabel.Content = curDigit.ToString();
                        newDigitLabel.RenderTransform.BeginAnimation(TranslateTransform.YProperty, appearMove);
                        newDigitLabel.BeginAnimation(OpacityProperty, appear);

                        //await Task.Delay(500);

                        curDigitLabel.RenderTransform.BeginAnimation(TranslateTransform.YProperty, disappearMove);
                        curDigitLabel.BeginAnimation(OpacityProperty, disappear);
                        
                        await Task.Delay(TimeSpan.FromSeconds(animationTime));
                        curDigitLabel.Content = curDigit.ToString();
                        curDigitLabel.RenderTransform = new TranslateTransform();
                        //newDigitLabel.RenderTransform = new TranslateTransform();
                    });
                }
            }
        }
    }
}
