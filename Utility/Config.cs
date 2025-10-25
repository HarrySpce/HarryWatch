using Harry.Models;
using Harry.Utility;
using Harry.Interception;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Harry.Interception.Modules;
using System.Xml.Linq;
using System.Reflection;
using System.Diagnostics;

namespace Harry
{
    public static class Config
    {
        static string ConfigPath = "harry.cfg";
        public static ConfigModel Instance { get; set; }

        static Config()
        {
            ConfigPath = Path.Combine(App.ExeDirectory, ConfigPath);
            Instance = File.Exists(ConfigPath)
                ? File.ReadAllText(ConfigPath).Deserialize<ConfigModel>()
                : Instance ?? new ConfigModel() { Volume = 30 };
        }

        public static ModuleSettingsBase GetNamed(string name)
        {
            if (Instance == null)
                Load();

            if (Instance.Modules.TryGetValue(name, out var module))
                return module;

            Instance.Modules[name] = new ModuleSettingsBase();
            return Instance.Modules[name];
        }
        public static void Load()
        {
            Instance = File.Exists(ConfigPath)
                ? File.ReadAllText(ConfigPath).Deserialize<ConfigModel>()
                : Instance ?? new ConfigModel() { Volume = 30 };

            App.snow = Instance.Settings.Window_Snow;
            Logger.Info("Config loaded");
        }

        public static void Save()
        {
            if (Instance == null || InterceptionManager.Modules.Count < 8)
                return;

            try
            {
                GetNamed("PVE").Settings["Buffer"] = PveModule.Buffer;
                GetNamed("PVE").Settings["AutoResync"] = PveModule.AutoResync;
                GetNamed("PVE").Settings["OutboundKeybind"] = PveModule.OutboundKeybind;
                GetNamed("PVE").Settings["SlowInboundKeybind"] = PveModule.SlowInboundKeybind;
                GetNamed("PVE").Settings["SlowOutboundKeybind"] = PveModule.SlowOutboundKeybind;

                GetNamed("PVP").Settings["OutboundKeybind"] = PvpModule.OutboundKeybind;
                GetNamed("PVP").Settings["Buffer"] = PvpModule.Buffer;
                GetNamed("PVP").Settings["AutoResync"] = PvpModule.AutoResync;

                GetNamed("API Block").Settings["SelfDisable"] = ApiModule.Disable;
                GetNamed("API Block").Settings["Buffer"] = ApiModule.Buffer;
                GetNamed("З0K").Settings["Buffer"] = InstanceModule.Buffer;

                GetNamed("Multishot").Settings["Inbound"] = MultishotModule.Inbound;
                GetNamed("Multishot").Settings["Outbound"] = MultishotModule.Outbound;
                GetNamed("Multishot").Settings["TimeLimit"] = MultishotModule.MaxTime;
                GetNamed("Multishot").Settings["PlayersMode"] = MultishotModule.PlayersMode;
                GetNamed("Multishot").Settings["ShotDetection"] = MultishotModule.WaitShot;
                GetNamed("Multishot").Settings["Togglable"] = InterceptionManager.GetModule("Multishot").Togglable;
                GetNamed("Multishot").Settings["PlayersKeybind"] = MultishotModule.PlayersKeybind;
                File.WriteAllText(ConfigPath, Instance.Serialize(true));
                Logger.Info($"Config saved");
            }
            catch (Exception e)
            {
                Logger.Error(e, additionalInfo: "Config save");
            }
        }
    }
}
