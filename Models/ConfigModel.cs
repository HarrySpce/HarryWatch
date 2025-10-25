using Harry.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Harry.Models
{
    public class ConfigModel
    {
        public string CurrentModule { get; set; }
        public double Volume { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }

        public Settings Settings { get; set; } = new Settings();
        public Dictionary<string, ModuleSettingsBase> Modules { get; set; } = new Dictionary<string, ModuleSettingsBase>();
        public List<string> LastOpenAhks { get; set; } = new List<string>();
    }

    public class Settings
    {
        public string? Tracker_BungieName { get; set; } = null;
        public bool Tracker_CountRaids { get; set; } = true;
        public bool Tracker_CountDungeons { get; set; } = true;
        public bool AltTabSupressKeybinds { get; set; } = false;

        public bool Overlay_StartOnLaunch { get; set; } = true;
        public bool Overlay_ShowTime { get; set; } = false;
        public bool Overlay_ShowTimer { get; set; } = true;
        public bool Overlay_DisableOnInactivity { get; set; } = true;
        public bool Overlay_DisplayOnlyTogglable { get; set; } = true;
        public int Overlay_LeftOffset { get; set; } = 0;
        public int Overlay_BottomOffset { get; set; } = 0;

        public bool Window_Snow { get; set; } = true;
        public bool Window_DisplayClock { get; set; } = true;
        public bool Window_DisplaySpeed { get; set; } = true;
        public int Window_TimerDecaySeconds { get; set; } = 10;

        public bool DB_SavePackets { get; set; } = true;
        public bool DB_KeyPresses { get; set; } = true;

        public bool AHK_AutoClose { get; set; } = true;
        public bool AHK_AutoOpen { get; set; } = true;
    }

    public class ModuleSettingsBase
    {
        public bool Enabled = false;
        public List<Keycode> Keybind = new List<Keycode>();
        public Dictionary<string, object> Settings = new Dictionary<string, object>();

        public T GetSettings<T>(string name)
        {
            if (!Settings.ContainsKey(name))
            {
                Settings[name] = default(T);
                if (name == "Inbound" || name == "SelfDisable" || name == "Buffer" || name == "AutoResync")
                    Settings[name] = true;
                else if (name.Contains("Keybind"))
                    Settings[name] = new List<Keycode>();
                else if (name == "TimeLimit")
                    Settings[name] = 1.8d;

                Config.Save();
            }

            if (Settings[name] is JsonElement e)
            {
                if (name.Contains("Keybind"))
                {
                    return JsonSerializer.Deserialize<T>(e);
                }

                if (typeof(T).Equals(typeof(bool)))
                {
                    return (T)Convert.ChangeType(e.GetBoolean(), typeof(T));
                }

                if (typeof(T).Equals(typeof(double)))
                {
                    return (T)Convert.ChangeType(e.GetDouble(), typeof(T));
                }
            }

            return (T)Convert.ChangeType(Settings[name], typeof(T));
        }
    }
}
