using Harry.Interception.Modules;
using Harry.Interception.PacketProviders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Harry.Interception
{
    public static class InterceptionManager
    {
        public static List<PacketProviderBase> Providers = new List<PacketProviderBase>();
        public static List<PacketModuleBase> Modules = new List<PacketModuleBase>();

        public static PacketModuleBase GetModule(string name)
        {
            return Modules.FirstOrDefault(x => x.Name == name) ?? Modules.FirstOrDefault(x => x.Name.StartsWith(name));
        }

        public static PacketProviderBase GetProvider(string name)
        {
            return Providers.FirstOrDefault(x => x.Name.StartsWith(name));
        }

        private static bool init = false;
        public static void Init()
        {
            if (init) return;
            init = true;

            Providers.Add(new PlayersProvider());
            Providers.Add(new XboxProvider());
            Providers.Add(new _30000_Provider());
            Providers.Add(new _7500_Provider());

            Modules.Add(new PveModule());
            Modules.Add(new MultishotModule());
            Modules.Add(new ReconnectModule());
            Modules.Add(new ResModule());
            Modules.Add(new ApiModule());
            Modules.Add(new TimerModule());
            Modules.Add(new PauserModule());
            Modules.Add(new SoloModule());
            Modules.Add(new PvpModule());
            Modules.Add(new InstanceModule());

            var enabledNames = Config.Instance.Modules.Where(x => x.Value.Enabled).Select(x => x.Key);
            foreach (var disable in Modules.Where(x => x.IsEnabled && !enabledNames.Contains(x.Name)).ToArray())
                disable.StopListening();

            foreach (var enable in Modules.Where(x => enabledNames.Contains(x.Name)).ToArray())
                enable.StartListening();

            if (Config.Instance.CurrentModule is null)
                Config.Instance.CurrentModule = Modules[0].Name;
        }
    }
}
