using Microsoft.EntityFrameworkCore.Metadata;
using Harry.Database;
using Harry.Models;
using Harry.Utility;
using Harry.Windows;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Security.Policy;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using WindivertDotnet;

namespace Harry.Interception.Modules
{
    public abstract class PacketModuleBase : IDisposable
    {
        public MediaPlayer EnableSound;
        public MediaPlayer DisableSound;
        public HashSet<PacketProviderBase> PacketProviders { get; set; } = new HashSet<PacketProviderBase>();

        public Geometry Icon { get; set; }
        public SolidColorBrush Color { get; set; }

        public bool IsEnabled { get; set; } = false;
        public bool IsActivated { get; set; }

        public string Name { get; set; }
        public string Description { get; set; }

        public bool Togglable {get; set; } = false;
        public DateTime StartTime { get; set; }

        public PacketModuleBase(string name, bool togglable = false, params PacketProviderBase[] providers) 
        {
            Name = name;
            StartTime = DateTime.MinValue;

            Icon = Application.Current.FindResource("DefaultIcon") as Geometry;
            Color = Brushes.White;

            Togglable = togglable;

            EnableSound = new MediaPlayer();
            EnableSound.Volume = 0;
            EnableSound.Open(new Uri(Path.Combine("Sound", Togglable ? "enable.mp3" : "activate.mp3"), UriKind.Relative));
            EnableSound.MediaOpened += async (s, e) => 
            { 
                EnableSound.Stop(); 
                EnableSound.Position = TimeSpan.Zero;
                await Task.Delay(1000);
                EnableSound.Volume = Config.Instance.Volume / 100d; 
            };
            EnableSound.MediaEnded += (s, e) => 
            { 
                EnableSound.Pause(); 
                EnableSound.Position = TimeSpan.Zero; 
            };
            EnableSound.MediaFailed += (s, e) =>
            {
                Logger.Error(e.ErrorException);
            };

            DisableSound = new MediaPlayer();
            DisableSound.Volume = 0;
            DisableSound.Open(new Uri(Path.Combine("Sound", Togglable ? "disable.mp3" : "deactivate.mp3"), UriKind.Relative));
            DisableSound.MediaOpened += async (s, e) => 
            { 
                EnableSound.Stop(); 
                EnableSound.Position = TimeSpan.Zero;
                await Task.Delay(1000);
                DisableSound.Volume = Config.Instance.Volume / 100d; 
            };
            DisableSound.MediaEnded += (s, e) =>
            { 
                DisableSound.Pause(); 
                DisableSound.Position = TimeSpan.Zero; 
            };
            DisableSound.MediaFailed += (s, e) =>
            {
                Logger.Error(e.ErrorException);
            };

            if (providers is not null && providers.Length > 0)
                providers.ToList().ForEach(provider => PacketProviders.Add(provider));
        }

        protected bool hooked = false;
        public void HookKeybind()
        {
            if (hooked) return;
            KeyListener.KeysPressed += KeybindPressedHandler;
            hooked = true;
        }

        public void UnhookKeybind()
        {
            if (!hooked) return;
            KeyListener.KeysPressed -= KeybindPressedHandler;
            hooked = false;
        }

        public virtual void StartListening()
        {
            if (IsEnabled)
                return;

            IsEnabled = true;
            Config.GetNamed(Name).Enabled = true;

            HookKeybind();
            foreach (var p in PacketProviders)
                p.Subscribe(this);

            Logger.Info($"{Name}: Started");
        }

        public virtual void StopListening()
        {
            if (!IsEnabled)
                return;

            UnhookKeybind();
            IsEnabled = false;
            if (IsActivated && Togglable)
                ForceDisable();

            Config.GetNamed(Name).Enabled = false;

            foreach (var p in PacketProviders)
                p.Unsubscribe(this);

            Logger.Info($"{Name}: Finished");
        }

        public abstract void Toggle();
        public virtual bool AllowPacket(Packet packet)
        {
            return !packet.IsSent;
        }

        protected DateTime latestTrigger = DateTime.MinValue;
        protected bool KeybindChecks()
        {
            if (!IsEnabled || !hooked) return false;

            if (DateTime.Now - latestTrigger < TimeSpan.FromSeconds(0.25)) return false;

            if (Config.Instance.Settings.AltTabSupressKeybinds && !OverlayWindow.CheckGameFocus()) return false;

            return true;
        }

        private void KeybindPressedHandler(LinkedList<Keycode> keycodes)
        {
            if (!KeybindChecks()) return;

            var bind = Config.GetNamed(Name).Keybind;
            if (!bind.Any() || 
                keycodes.Count < bind.Count || 
                !bind.All(x => keycodes.Contains(x))) return;

            Task.Run(() =>
            {
                Logger.Info($"{Name}: Hotkey fired");
                latestTrigger = DateTime.Now;

                var savedState = IsActivated;
                var savedTime = StartTime;
                Toggle();
                var newState = IsActivated;

                if (savedState == newState) return; 

                if (Togglable)
                {
                    if (!IsActivated) Logger.Info($"{Name}: Worked for {DateTime.Now - savedTime}");

                    StartTime = latestTrigger;
                    MainWindow.Instance.Dispatcher.BeginInvoke(() => (IsActivated ? EnableSound : DisableSound).Play());
                }
                else if (!Togglable && IsActivated)
                {
                    StartTime = latestTrigger;
                    MainWindow.Instance.Dispatcher.BeginInvoke(() => EnableSound.Play());
                }
            });
        }

        public void ForceDisable()
        {
            if (!IsActivated)
                return;
            Toggle();
            if (Togglable)
            {
                MainWindow.Instance.Dispatcher.BeginInvoke(() => DisableSound.Play());
                Logger.Info($"{Name}: Worked for {DateTime.Now - StartTime}");
                StartTime = DateTime.Now;
            }
        }

        public void ForceEnable()
        {
            if (IsActivated)
                return;
            Toggle();

            if (Togglable)
            {
                MainWindow.Instance.Dispatcher.BeginInvoke(() => EnableSound.Play());
                StartTime = DateTime.Now;
            }
        }

        public void Dispose()
        {
            StopListening();
            EnableSound.Close();
            DisableSound.Close();
        }
        public override string ToString()
        {
            return Name ?? "null";
        }
    }
}
