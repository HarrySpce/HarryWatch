using Harry.Utility;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using UserControl = System.Windows.Controls.UserControl;

namespace Harry.Controls
{
    public partial class Button : UserControl
    {
        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(Button), new PropertyMetadata("None"));

        public TextBlock TextBlock => ButtonText;

        new public SolidColorBrush Foreground
        {
            get { return (SolidColorBrush)GetValue(ForegroundProperty); }
            set { SetValue(ForegroundProperty, value); }
        }

        public static readonly DependencyProperty ForegroundProperty =
            DependencyProperty.Register("Foreground", typeof(SolidColorBrush), typeof(Button), new PropertyMetadata(Brushes.White));


        new public SolidColorBrush Background 
        {
            get { return (SolidColorBrush)GetValue(BackgroundProperty); }
            set { SetValue(BackgroundProperty, value); }
        }

        public static readonly DependencyProperty BackgroundProperty =
            DependencyProperty.Register("Background", typeof(SolidColorBrush), typeof(Button), new PropertyMetadata(new SolidColorBrush(Color.FromArgb(0x10, 0xFF, 0xFF, 0xFF))));


        public CornerRadius CornerRadius
        {
            get { return (CornerRadius)GetValue(CornerRadiusProperty); }
            set { SetValue(CornerRadiusProperty, value); }
        }

        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register("CornerRadius", typeof(CornerRadius), typeof(Button), new PropertyMetadata(new CornerRadius { BottomLeft = 9, BottomRight = 9, TopLeft = 9, TopRight = 9}));




        public Button()
        {
            if (Background == Brushes.White) Background = System.Windows.Application.Current.Resources["AccentColor"] as SolidColorBrush;
            InitializeComponent();
            DataContext = this;
        }

        private void Border_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var lightOn = new DoubleAnimation(ButtonBorder.Opacity, 1.0, TimeSpan.FromSeconds(0.35)) { EasingFunction = new BackEase() { EasingMode = EasingMode.EaseOut } };
            ButtonBorder.BeginAnimation(OpacityProperty, lightOn);
        }

        private void Border_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var lightOff = new DoubleAnimation(ButtonBorder.Opacity, 0.7, TimeSpan.FromSeconds(0.35)) { EasingFunction = new BackEase() { EasingMode = EasingMode.EaseOut } };
            ButtonBorder.BeginAnimation(OpacityProperty, lightOff);
        }


        public event RoutedEventHandler Click;
        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (Click != null)
                    Click(this, e);
            }
        }
    }
}
