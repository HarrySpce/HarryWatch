using RateLimiter;
using Harry.Models;
using Harry.Utility;
using Harry.Windows;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Harry.Interception.Modules 
{
    public class MultishotModule : PacketModuleBase
    {
        PacketProviderBase playersProvider;
        PacketProviderBase xboxProvider;

        public static double MaxTime;
        public static bool PlayersMode;
        public static bool WaitShot;
        public static bool Inbound;
        public static bool Outbound;

        public MultishotModule() : base("Multishot", false, InterceptionManager.GetProvider("Players"), InterceptionManager.GetProvider("Xbox"))
        {
            Icon = System.Windows.Application.Current.FindResource("CrosshairIcon") as Geometry;
            Description = "[PATCHED]";

            playersProvider = InterceptionManager.GetProvider("Players");
            xboxProvider = InterceptionManager.GetProvider("Xbox");

            MaxTime = Math.Clamp(Config.GetNamed(Name).GetSettings<double>("TimeLimit"), 0.5, 1.8);
            PlayersMode = Config.GetNamed(Name).GetSettings<bool>("PlayersMode");
            WaitShot = Config.GetNamed(Name).GetSettings<bool>("ShotDetection");
            Inbound = Config.GetNamed(Name).GetSettings<bool>("Inbound");
            Outbound = Config.GetNamed(Name).GetSettings<bool>("Outbound");
            Togglable = Config.GetNamed(Name).GetSettings<bool>("Togglable");

            KeyListener.KeysPressed += PlayersKeybindHandler;
            KeyListener.KeysPressed += ShootButtonTracker;

            PlayersKeybind.AddRange(Config.GetNamed(Name).GetSettings<List<Keycode>>("PlayersKeybind"));
        }

        private void ShootButtonTracker(LinkedList<Keycode> keycode)
        {
            if (keycode.Any(x => x == Keycode.VK_LMB))
            {
                lastLMB = DateTime.Now;

                if (!block && IsActivated && (Outbound || Inbound) &&
                    (DateTime.Now - correction < TimeSpan.FromSeconds(0.5) || !WaitShot) &&
                    OverlayWindow.CheckGameFocus())
                {
                    lastShot = StartTime = DateTime.Now;
                    block = true;
                    if (Togglable) MainWindow.Instance.Dispatcher.BeginInvoke(() => EnableSound.Play());
                    Logger.Debug($"{Name}: Recent start found");
                }
            }
        }

        public static List<Keycode> PlayersKeybind = new List<Keycode>();
        private void PlayersKeybindHandler(LinkedList<Keycode> keycodes)
        {
            if (!KeybindChecks()) return;

            if (!PlayersKeybind.Any() || keycodes.Count < PlayersKeybind.Count) return;

            if (PlayersKeybind.All(x => keycodes.Contains(x)))
            {
                PlayersMode = !PlayersMode;
                
                MainWindow.Instance.Dispatcher.BeginInvoke(() =>
                {
                    EnableSound.Play();
                    MainWindow.Instance.MS_PVP.SetState(PlayersMode);
                });
            }
        }

        public override void Toggle()
        {
            IsActivated = !IsActivated;
            if (!IsActivated)
            {
                Disable();
                return;
            }

            addrMap.Clear();
            StartTime = DateTime.Now;

            if (!WaitShot) // no validation
            {
                if (DateTime.Now - lastLMB < shotTimeout)
                {
                    block = true;
                    lastShot = DateTime.Now;

                    Logger.Debug($"{Name}: Recent lmb found");
                }
                else
                {
                    lastShot = DateTime.Now;
                    correction = DateTime.Now;
                }

                return;
            }

            correction = DateTime.Now;

            var date = DateTime.Now - shotTimeout;
            var shots = playersProvider.CurrentQueue.Where(x => x.CreatedAt > date).ToArray().Where(x => x.IsOutboundShot());
            if (shots.Any())
            {
                foreach (var addr in shots.Select(x => x.RemoteAddress).Distinct())
                    addrMap[addr] = 0;

                lastShot = DateTime.Now;
                block = true;

                Logger.Debug($"{Name}: Found recent shot, starting immediately");
            }
        }

        void Disable()
        {
            addrMap.Clear();
            block = false;
            lastLMB = DateTime.MinValue;
            correction = DateTime.MinValue;

            StartTime = DateTime.Now;
            cooldown = DateTime.Now + TimeSpan.FromSeconds(0.1);

            if (PlayersMode)
            {
                playersProvider.ClearDelayQueue(null, true, 0);
            }
            else
            {
                xboxProvider.ClearDelayQueue(null, true, 0);
            }

            if (!Togglable)
            {
                ForceDisable();
                return;
            }
        }

        bool block;
        DateTime cooldown;
        DateTime lastLMB;
        DateTime lastShot;
        Dictionary<string, int> addrMap = new();

        TimeSpan shotTimeout = TimeSpan.FromSeconds(0.75);
        TimeSpan timeLimit => TimeSpan.FromSeconds(MaxTime);
        public override bool AllowPacket(Packet p)
        {
            if (!base.AllowPacket(p)) return false;

            // DISABLE TRIGGERS
            if (block)
            {
                // attempt of resync?
                if (PlayersMode && p.SourceProvider == playersProvider && p.Outbound && p.Length == 1185)
                {
                    Logger.Debug($"{Name}: Encountered reconnect packet [{DateTime.Now - StartTime}]");
                    Disable();
                    return false;
                }

                if (PlayersMode && DateTime.Now - lastShot > shotTimeout)
                {
                    Logger.Debug($"{Name}: No shots are being made [{DateTime.Now - StartTime}]");
                    Disable();
                    return false;
                }
                // add check to skip at least 10 out packets

                if (DateTime.Now - StartTime > timeLimit)
                {
                    Logger.Debug($"{Name}: Timelimit hit [{DateTime.Now - StartTime}]");
                    Disable();
                    return false;
                }
            }


            return p.SourceProvider.Name switch
            {
                "Players" => Handle27K(p),
                "Xbox" => Handle3074(p),
                _ => true
            };
        }

        const int pveDropPacketsAmount = 20; // 5/10 - x2 / 12/15 - x3
        bool Handle3074(Packet p)
        {
            if (!IsActivated || !block || PlayersMode) return true;

            if (p.IsPveQuestionableReconnectRequest() || p.IsPveReconnectRequest())
            {
                Disable();
                Logger.Debug($"{Name}: Pve reconnect");
                return true;
            }

            if (p.Inbound) return !Inbound;

            if (cooldown - DateTime.Now > TimeSpan.Zero) return true;

            var r = p.RemoteAddress;
            if (!addrMap.ContainsKey(r)) addrMap[r] = 0;
            addrMap[r]++;

            if (addrMap[r] >= pveDropPacketsAmount) //  + (Outbound ? 20 : 0)
            {
                Disable();
                Logger.Debug($"{Name}: Drop amount exceeded");
            }

            if (Outbound) xboxProvider.DelayPacket(p, TimeSpan.FromSeconds(10));
            return !Outbound;
        }

        DateTime correction = DateTime.MinValue;
        bool Handle27K(Packet p)
        {
            if (!PlayersMode && !WaitShot) return true;

            if (p.Inbound && !block) return true;

            var r = p.RemoteAddress;
            var con = playersProvider.Connections[r];
            if (con.IsReadonlyPlayerConnection()) return true;

            // CHECKS
            var shot = p.IsOutboundShot();
            if (shot)
            {
                lastShot = DateTime.Now;

                // Recent activation
                if (IsActivated && !block &&
                    DateTime.Now - lastLMB < TimeSpan.FromSeconds(1.5) &&
                    cooldown <= DateTime.Now)
                {
                    StartTime = DateTime.Now;
                    if (Togglable) MainWindow.Instance.Dispatcher.BeginInvoke(() => EnableSound.Play());
                    block = WaitShot || Togglable;
                }
                // Note time for future activation
                else
                {
                    correction = DateTime.Now;
                }

                // Adds address to count shots
                if (!addrMap.ContainsKey(r)) addrMap[r] = 0;

                if (PlayersMode && block) addrMap[r]++;
                Logger.Debug($"{Name}: Shot x{addrMap[r]}");
            }
            // RESULT
            if (PlayersMode && block)
            {
                if (p.Inbound && Inbound)
                {
                    return false;
                }

                if (p.Outbound && Outbound)
                {
                    playersProvider.DelayPacket(p, TimeSpan.FromSeconds(10));
                    return false;
                }
            }

            return true;
        }
    }
}
