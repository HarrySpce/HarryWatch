using OxyPlot.Wpf;

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
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Harry.Controls
{
    public partial class Checkbox : UserControl
    {
        public Geometry Geometry
        {
            get { return (Geometry)GetValue(GeometryProperty); }
            set { SetValue(GeometryProperty, value); }
        }
        public static readonly DependencyProperty GeometryProperty =
            DependencyProperty.Register("Geometry", typeof(Geometry), typeof(Checkbox),
                new PropertyMetadata(Geometry.Parse("M 0 20 A 1 1 0 0 0 40 20 A 1 1 0 0 0 0 20")));


        public bool Checked { get; set; } = false;
        public Checkbox()
        {
            DataContext = this;
            InitializeComponent();
        }

        public void SetState(bool enabled)
        {
            Checked = enabled;
            //DisabledCover.Visibility = Checked ? Visibility.Hidden : Visibility.Visible;
            Icon.Data = Geometry;
            Icon.Opacity = enabled ? 1.0 : 0.35;
            ButtonBorder.Background = (SolidColorBrush)Application.Current.FindResource(Checked ? "InactiveColor" : "LightBackColor");
            ButtonBorder.Effect = enabled ? new DropShadowEffect()
            {
                ShadowDepth = 0,
                Color = Colors.White,
                BlurRadius = 6
            } : null;
        }

        private void Border_MouseEnter(object sender, MouseEventArgs e)
        {
            var lightOn = new DoubleAnimation(ButtonBorder.Opacity, 1.0, TimeSpan.FromSeconds(0.35)) { EasingFunction = new BackEase() { EasingMode = EasingMode.EaseOut } };
            ButtonBorder.BeginAnimation(OpacityProperty, lightOn);
        }

        private void Border_MouseLeave(object sender, MouseEventArgs e)
        {
            var lightOff = new DoubleAnimation(ButtonBorder.Opacity, 0.7, TimeSpan.FromSeconds(0.35)) { EasingFunction = new BackEase() { EasingMode = EasingMode.EaseOut } };
            ButtonBorder.BeginAnimation(OpacityProperty, lightOff);
        }

        public event RoutedEventHandler Click;
        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                SetState(!Checked);
                if (Click != null)
                    Click(this, e);
            }
        }
    }
}
