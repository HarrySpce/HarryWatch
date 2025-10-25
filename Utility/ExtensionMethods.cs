using Harry.Database;
using Harry.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;

namespace Harry
{
    public static class ExtensionMethods
    {
        public static string Serialize(this object obj, bool spaces = false, bool ignore_default = false)
        {
            return JsonSerializer.Serialize(obj, new JsonSerializerOptions() 
            { 
                IncludeFields = true, 
                WriteIndented = spaces, 
                DefaultIgnoreCondition = ignore_default 
                    ? System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault 
                    : System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });
        }
        public static T Deserialize<T>(this string obj)
        {
            return JsonSerializer.Deserialize<T>(obj, new JsonSerializerOptions() { IncludeFields = true });
        }


        private static TimeSpan animationTime = TimeSpan.FromSeconds(0.5);
        private static List<UIElement> inAppear = new List<UIElement>();
        private static List<UIElement> inDisappear = new List<UIElement>();
        public static void ElementDisappear(this UIElement e, TimeSpan timeOverride = default, bool shrink = true, bool checkOverride = false)
        {
            if (e.Visibility == Visibility.Collapsed) return;
            if (timeOverride == default)
                timeOverride = animationTime;

            if (inDisappear.Contains(e) && !checkOverride) return;
            inDisappear.Add(e);

            var ease = new QuadraticEase() { EasingMode = EasingMode.EaseInOut };
            e.Opacity = 1;
            var opacity_anim = new DoubleAnimation(1, 0, timeOverride) { EasingFunction = ease };
            opacity_anim.Completed += (s, a) => e.Visibility = Visibility.Collapsed;
            e.BeginAnimation(UIElement.OpacityProperty, opacity_anim);

            if (shrink)
            {
                ScaleTransform trans = new ScaleTransform();
                if (e is FrameworkElement ee)
                {
                    ee.LayoutTransform = trans;
                }
                else
                {
                    e.RenderTransform = trans;
                    e.RenderTransformOrigin = new Point(0.5, 0.5);
                }

                DoubleAnimation anim = new DoubleAnimation(1, 0, timeOverride);
                anim.Completed += (s, a) =>
                {
                    inDisappear.Remove(e);
                    e.Visibility = Visibility.Collapsed;
                };
                trans.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
                trans.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
            }

            return;
        }
        public static void ElementAppear(this UIElement e, TimeSpan timeOverride = default, bool shrink = true, bool checkOverride = false)
        {
            if (e.Visibility == Visibility.Visible) return;
            if (timeOverride == default)
                timeOverride = animationTime;

            if (inDisappear.Contains(e) && !checkOverride) return;
            if (inAppear.Contains(e) && !checkOverride) return;
            inAppear.Add(e);

            var ease = new QuadraticEase() { EasingMode = EasingMode.EaseInOut };
            e.Visibility = Visibility.Visible;
            e.Opacity = 0;
            var opacity_anim = new DoubleAnimation(0, 1, timeOverride) { EasingFunction = ease };
            opacity_anim.Completed += (s, a) => inAppear.Remove(e);
            e.BeginAnimation(UIElement.OpacityProperty, opacity_anim);

            if (shrink)
            {
                ScaleTransform trans = new ScaleTransform();
                if (e is FrameworkElement ee)
                {
                    ee.LayoutTransform = trans;
                }
                else
                {
                    e.RenderTransform = trans;
                    e.RenderTransformOrigin = new Point(0.5, 0.5);
                }

                DoubleAnimation anim = new DoubleAnimation(0, 1, timeOverride);
                trans.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
                trans.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
            } 

            return;
        }

        public static void ElementFadeOut(this UIElement e, TimeSpan timeOverride = default, bool checkOverride = false)
        {
            if (e.Visibility == Visibility.Collapsed) return;
            if (timeOverride == default)
                timeOverride = animationTime;

            if (inDisappear.Contains(e) && !checkOverride) return;
            inDisappear.Add(e);

            var ease = new QuadraticEase() { EasingMode = EasingMode.EaseOut };
            var disappear = new DoubleAnimation(1, 0, timeOverride) { EasingFunction = ease };

            disappear.Completed += (s, a) =>
            {
                inDisappear.Remove(e);
                e.Visibility = Visibility.Collapsed;
            };
            e.BeginAnimation(UIElement.OpacityProperty, disappear);
        }
        public static void ElementFadeIn(this UIElement e, TimeSpan timeOverride = default, bool checkOverride = false)
        {
            if (e.Visibility == Visibility.Visible && e.Opacity != 0) return;
            if (timeOverride == default)
                timeOverride = animationTime;

            if (inDisappear.Contains(e) && !checkOverride) return;
            if (inAppear.Contains(e) && !checkOverride) return;
            inAppear.Add(e);

            e.Opacity = 0;
            e.Visibility = Visibility.Visible;

            var ease = new QuadraticEase() { EasingMode = EasingMode.EaseOut };
            var appear = new DoubleAnimation(0, 1, timeOverride) { EasingFunction = ease };
            appear.Completed += (s, a) => inAppear.Remove(e);
            e.BeginAnimation(UIElement.OpacityProperty, appear);
        }


        public static string[] SplitLinesOnLimit(this string input, int limit = 2048)
        {
            var result = new List<string>();
            var lines = input.Split('\n');
            var currentString = string.Empty;
            for (int i = 0; i < lines.Length; i++)
            {
                if (currentString.Length + lines[i].Length >= limit)
                {
                    result.Add(currentString);
                    currentString = lines[i] + '\n';
                }
                else
                {
                    currentString += lines[i] + '\n';
                }
            }

            if (currentString.Length > 0)
                result.Add(currentString);

            return result.ToArray();
        }
        public static string BytesLenghtToString(this int len)
        {
            if (len >= 1048576 / 2) // 0.5 MB
            {
                return (len / 1048576).ToString("0.##") + "MB";
            }
            else if (len >= 1024) //1 KB
            {
                return (len / 1024).ToString("0.##") + "KB";
            }
            else
            {
                return len + "B";
            }
        }


        public static string ToHexString(this byte[] bytes)
        {
            return string.Join("", bytes.Select(x => x.ToString("x2")));
        }
        public static byte[] ToBytes(this string bytes)
        {
            return Enumerable.Range(0, bytes.Length)
                                 .Where(x => x % 2 == 0)
                                 .Select(x => Convert.ToByte(bytes.Substring(x, 2), 16))
                                 .ToArray();
        }


        public static string ResilientDownloadString(this WebClient wc, string url)
        {
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    return wc.DownloadString(url);
                }
                catch (WebException)
                {
                    if (i == 2)
                        throw;

                    continue;
                }
            }
            return String.Empty;
        }



        static public Color ColorFromHSL(float h, float s, float v)
        {
            if (s == 0)
            { 
                byte L = (byte)v; 
                return Color.FromArgb(255, L, L, L); 
            }

            double min, max, hh;
            hh = h / 360d;

            max = v < 0.5d ? v * (1 + s) : (v + s) - (v * s);
            min = (v * 2d) - max;

            Color c = Color.FromArgb(255, (byte)(255 * RGBChannelFromHue(min, max, hh + 1 / 3d)),
                                          (byte)(255 * RGBChannelFromHue(min, max, hh)),
                                          (byte)(255 * RGBChannelFromHue(min, max, hh - 1 / 3d)));
            return c;
        }

        static double RGBChannelFromHue(double m1, double m2, double h)
        {
            h = (h + 1d) % 1d;
            if (h < 0) h += 1;
            if (h * 6 < 1) return m1 + (m2 - m1) * 6 * h;
            else if (h * 2 < 1) return m2;
            else if (h * 3 < 2) return m1 + (m2 - m1) * 6 * (2d / 3d - h);
            else return m1;

        }
    }
}
