using OxyPlot.Wpf;

using Harry.Utility;

using System;
using System.Collections.Generic;
using System.Globalization;
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
    public partial class WindowControlButton : UserControl
    {
        public double MaxScale
        {
            get { return (double)GetValue(MaxScaleProperty); }
            set { SetValue(MaxScaleProperty, value); }
        }
        public static readonly DependencyProperty MaxScaleProperty =
            DependencyProperty.Register("MaxScale", typeof(double), typeof(WindowControlButton), new PropertyMetadata(1.25));



        public double Scale
        {
            get { return (double)GetValue(ScaleProperty); }
            set { SetValue(ScaleProperty, value); }
        }
        public static readonly DependencyProperty ScaleProperty =
            DependencyProperty.Register("Scale", typeof(double), typeof(WindowControlButton), new PropertyMetadata(0.9));




        public Geometry PathData
        {
            get { return (Geometry)GetValue(PathDataProperty); }
            set { SetValue(PathDataProperty, value); }
        }
        public static readonly DependencyProperty PathDataProperty =
            DependencyProperty.Register("PathData", typeof(Geometry), typeof(WindowControlButton),
                new PropertyMetadata(Geometry.Parse("M 0 0 L 10 0 L 10 10 L 0 10 Z")));


        public Brush GlowColor
        {
            get { return (Brush)GetValue(GlowColorProperty); }
            set { SetValue(GlowColorProperty, value); }
        }
        public static readonly DependencyProperty GlowColorProperty =
            DependencyProperty.Register("GlowColor", typeof(Brush), typeof(WindowControlButton),
                new PropertyMetadata(Brushes.White));


        public Brush FillColor
        {
            get { return (Brush)GetValue(FillColorProperty); }
            set { SetValue(FillColorProperty, value); }
        }
        public static readonly DependencyProperty FillColorProperty =
            DependencyProperty.Register("FillColor", typeof(Brush), typeof(WindowControlButton),
                new PropertyMetadata(Brushes.LightGray));


        
        public WindowControlButton() 
        {
            InitializeComponent();
            this.Loaded += RefreshAppearance;
        }

        public void RefreshAppearance(object sender, RoutedEventArgs e)
        {
            Icon.Data = PathData;
            Icon.Fill = FillColor;
            Icon.Effect = new DropShadowEffect()
            {
                ShadowDepth = 0,
                BlurRadius = 10,
                Color = GlowColor.ToOxyColor().ToColor(),
                Opacity = 0.0,
            };

            ScaleTransform defaultScale = new ScaleTransform(Scale, Scale);
            Grid.RenderTransformOrigin = new Point(0.5, 0.5);
            Grid.RenderTransform = defaultScale;

            if (stayActive)
                Border_MouseEnter(null, null);
        }


        public void Border_MouseEnter(object sender = null, MouseEventArgs e = null)
        {
            DoubleAnimation sizeIncrease = new DoubleAnimation((Grid.RenderTransform as ScaleTransform).ScaleX, MaxScale, new Duration(TimeSpan.FromSeconds(0.2)));

            Grid.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, sizeIncrease);
            Grid.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, sizeIncrease);

            Icon.Fill = new SolidColorBrush((Icon.Fill as SolidColorBrush).Color);
            Icon.Fill.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation((GlowColor as SolidColorBrush).Color, new Duration(TimeSpan.FromSeconds(0.3))));

            DoubleAnimation shadowOpacity = new DoubleAnimation(0.4, new Duration(TimeSpan.FromSeconds(0.3)));
            (Icon.Effect as DropShadowEffect).BeginAnimation(DropShadowEffect.OpacityProperty, shadowOpacity);
        }

        public bool stayActive = false;
        public void Border_MouseLeave(object sender = null, MouseEventArgs e = null)
        {
            if (stayActive)
                return;

            DoubleAnimation sizeDecrease = new DoubleAnimation((Grid.RenderTransform as ScaleTransform).ScaleX, Scale, new Duration(TimeSpan.FromSeconds(0.1)));

            Grid.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, sizeDecrease);
            Grid.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, sizeDecrease);

            DoubleAnimation shadowOpacity = new DoubleAnimation(0, new Duration(TimeSpan.FromSeconds(0.5)));
            (Icon.Effect as DropShadowEffect).BeginAnimation(DropShadowEffect.OpacityProperty, shadowOpacity);

            Icon.Fill = new SolidColorBrush((Icon.Fill as SolidColorBrush).Color);
            Icon.Fill.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation((FillColor as SolidColorBrush).Color, new Duration(TimeSpan.FromSeconds(0.3))));
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
