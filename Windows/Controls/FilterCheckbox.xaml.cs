using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace Harry.Controls
{
    public partial class FilterCheckbox : UserControl
    {
        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(FilterCheckbox), new PropertyMetadata(""));

        public Geometry Geometry
        {
            get { return (Geometry)GetValue(GeometryProperty); }
            set { SetValue(GeometryProperty, value); }
        }
        public static readonly DependencyProperty GeometryProperty =
            DependencyProperty.Register("Geometry", typeof(Geometry), typeof(WindowControlButton),
                new PropertyMetadata(Geometry.Parse("M 0 0")));

        public Brush Color
        {
            get { return (Brush)GetValue(ColorProperty); }
            set { SetValue(ColorProperty, value); }
        }
        public static readonly DependencyProperty ColorProperty =
            DependencyProperty.Register("Color", typeof(Brush), typeof(FilterCheckbox), new PropertyMetadata(Brushes.White));

        
        public CornerRadius CornerRadius
        {
            get { return (CornerRadius)GetValue(CornerRadiusProperty); }
            set { SetValue(CornerRadiusProperty, value); }
        }
        public static readonly DependencyProperty CornerRadiusProperty = 
            DependencyProperty.Register("CornerRadius", typeof(CornerRadius), typeof(FilterCheckbox), new PropertyMetadata(new CornerRadius(11)));



        public bool Checked
        {
            get { return (bool)GetValue(CheckedProperty); }
            set { SetValue(CheckedProperty, value); }
        }

        public static readonly DependencyProperty CheckedProperty =
            DependencyProperty.Register("Checked", typeof(bool), typeof(FilterCheckbox), new PropertyMetadata(false));



        public double AnimationTime = 0.3;
        public FilterCheckbox()
        {
            InitializeComponent();
            Loaded += FilterCheckbox_Loaded;
        }

        private void FilterCheckbox_Loaded(object sender, RoutedEventArgs e)
        {
            Border.BorderBrush = Color;
            Border.CornerRadius = CornerRadius;

            Label.Content = Text;
            
            Icon.Data = Geometry;
            Icon.Fill = Color;
            Icon.StrokeThickness = 5;

            ScaleTransform defaultScale = new ScaleTransform(1, 1);
            Icon.RenderTransformOrigin = new Point(0.5, 0.5);
            Icon.RenderTransform = defaultScale;

            Border.Background = Checked ? Color : tinted;
        }

        public event RoutedEventHandler CheckboxChecked;
        DateTime latestTrigger = DateTime.MinValue;
        double brightness = 0.35;
        SolidColorBrush tinted => new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x77,
            (byte)((Color as SolidColorBrush).Color.R * brightness),
            (byte)((Color as SolidColorBrush).Color.G * brightness),
            (byte)((Color as SolidColorBrush).Color.B * brightness)));
        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && DateTime.Now - latestTrigger > TimeSpan.FromSeconds(0.5))
            {
                latestTrigger = DateTime.Now;
                Checked = !Checked;

                var border = FindName("Border") as Border;
                var label = FindName("Label") as Label;

                // #7718122B
                var cur = (Color as SolidColorBrush).Color;
                var finishBackgroundColor = Checked ? new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x77, cur.R, cur.G, cur.B)) : tinted;
                //var finishTextColor = Checked ? new SolidColorBrush(System.Windows.Media.Color.FromArgb(0xff, 0x00, 0x00, 0x00)) : new SolidColorBrush((Color as SolidColorBrush).Color);

                var colorAnim = new ColorAnimation((border.Background as SolidColorBrush).Color, finishBackgroundColor.Color, TimeSpan.FromSeconds(AnimationTime));
                //var textAnim = new ColorAnimation((label.Foreground as SolidColorBrush).Color, finishTextColor.Color, TimeSpan.FromSeconds(AnimationTime));

                var currentBackgroundColor = new SolidColorBrush((border.Background as SolidColorBrush).Color);
                var currentTextColor = new SolidColorBrush((label.Foreground as SolidColorBrush).Color);

                border.Background = currentBackgroundColor;
                label.Foreground = currentTextColor;
                label.FontWeight = Checked ? FontWeights.Bold : FontWeights.Normal;

                currentBackgroundColor.BeginAnimation(SolidColorBrush.ColorProperty, colorAnim);
                //currentTextColor.BeginAnimation(SolidColorBrush.ColorProperty, textAnim);

                if (CheckboxChecked != null)
                    CheckboxChecked(this, e);
            }
        }

        public void FixInitState(bool ischecked)
        {
            Checked = ischecked;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var cur = new SolidColorBrush((Color as SolidColorBrush).Color);
                Border.Background = cur;
                Border.Background = Checked ? new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x77, cur.Color.R, cur.Color.G, cur.Color.B)) : tinted;
                //Icon.Fill = Color;
                //Label.Foreground = Checked ? new SolidColorBrush(System.Windows.Media.Color.FromArgb(0xff, 0x00, 0x00, 0x00)) : new SolidColorBrush(cur.Color);
                Label.FontWeight = Checked ? FontWeights.Bold : FontWeights.Normal;
            }));
        }



        private void Border_MouseEnter(object sender, MouseEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var border = sender as Border;
                var anim = new DoubleAnimation(border.Opacity, 0.95, TimeSpan.FromSeconds(AnimationTime));
                border.BeginAnimation(OpacityProperty, anim);
            }));

        }
        private void Border_MouseLeave(object sender, MouseEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var border = sender as Border;
                var anim = new DoubleAnimation(border.Opacity, 0.7, TimeSpan.FromSeconds(AnimationTime));
                border.BeginAnimation(OpacityProperty, anim);
            }));
        }
    }
}
