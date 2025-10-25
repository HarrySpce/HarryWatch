using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Effects;

namespace Harry.Windows
{
    public class PincushionEffect : ShaderEffect
    {
        static PincushionEffect()
        {
            _pixelShader.UriSource = MakePackUri("Windows/Converters/PincushionDistortion.ps");
        }

        private static PixelShader _pixelShader = new PixelShader();
        public PincushionEffect()
        {
            PixelShader = _pixelShader;
            UpdateShaderValue(InputProperty);
        }


        public Brush Input
        {
            get { return (Brush)GetValue(InputProperty); }
            set { SetValue(InputProperty, value); }
        }
        public static readonly DependencyProperty InputProperty = RegisterPixelShaderSamplerProperty("Input", typeof(PincushionEffect), 0);


        public float Width
        {
            get { return (float)GetValue(WidthProperty); }
            set { SetValue(WidthProperty, value); }
        }
        public static readonly DependencyProperty WidthProperty = DependencyProperty.Register("Width", typeof(float), typeof(PincushionEffect), new UIPropertyMetadata(0.0f, PixelShaderConstantCallback(0)));

        public float Height
        {
            get { return (float)GetValue(HeightProperty); }
            set { SetValue(HeightProperty, value); }
        }
        public static readonly DependencyProperty HeightProperty = DependencyProperty.Register("Height", typeof(float), typeof(PincushionEffect), new UIPropertyMetadata(0.0f, PixelShaderConstantCallback(1)));

        public float Power
        {
            get { return (float)GetValue(PowerProperty); }
            set { SetValue(PowerProperty, value); }
        }
        public static readonly DependencyProperty PowerProperty = DependencyProperty.Register("Power", typeof(float), typeof(PincushionEffect), new UIPropertyMetadata(0.0f, PixelShaderConstantCallback(2)));


        public static Uri MakePackUri(string relativeFile)
        {
            System.Reflection.Assembly a = typeof(PincushionEffect).Assembly;
            string assemblyShortName = a.ToString().Split(',')[0];
            string uriString = "pack://application:,,,/" + assemblyShortName + ";component/" + relativeFile;
            return new Uri(uriString);
        }
    }
}
