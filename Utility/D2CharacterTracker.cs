using DotNetBungieAPI;
using DotNetBungieAPI.Models.Destiny;
using DotNetBungieAPI.Models;
using DotNetBungieAPI.Models.User;
using DotNetBungieAPI.Service.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using DotNetBungieAPI.Models.Destiny.Definitions.ActivityModes;
using DotNetBungieAPI.Models.Destiny.Components;
using DotNetBungieAPI.Models.Destiny.HistoricalStats;
using Microsoft.VisualBasic.ApplicationServices;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using Harry.Interception.PacketProviders;
using System.Runtime.InteropServices;

namespace Harry.Utility
{
    public static class D2CharacterTracker
    {
        static D2CharacterTracker()
        {
            client = BungieApiBuilder.GetApiClient((c) => c.ClientConfiguration.ApiKey = "2d5a4599c47f48aebeb25465ba8a21dd");
        }

        static IBungieClient client;

        public static UserInfoCard User { get; private set; }
        public static DestinyCharacterActivitiesComponent ActiveCharacter { get; set; }
        public static DestinyCharacterComponent[] UserChars { get; private set; }
        public static List<DestinyHistoricalStatsPeriodGroup> UserActivities { get; private set; }
        public static Dictionary<DestinyCharacterComponent, List<DestinyHistoricalStatsPeriodGroup>> CharacterActivities { get; private set; }

        public static int RaidsCount => 
            User is null || UserActivities is null ? 0 : 
            UserActivities.ToArray()
                .Count(x => 
                    ((Config.Instance.Settings.Tracker_CountRaids && x.ActivityDetails.Mode == DestinyActivityModeType.Raid) || 
                    (Config.Instance.Settings.Tracker_CountDungeons && x.ActivityDetails.Mode == DestinyActivityModeType.Dungeon)) &&
                x.Values.TryGetValue("completed", out var c) && c.BasicValue.Value > 0);

        public static string BungieName { get; private set; }
        public static short Delimeter { get; private set; }
        static DateTime reset;

        public static async Task<bool> TryLoadFromConfig()
        {
            if (Config.Instance.Settings.Tracker_BungieName is null) return false;

            Regex validation = new Regex(@"(.+?)#(\d{1,4})");
            var m = validation.Match(Config.Instance.Settings.Tracker_BungieName);
            if (!m.Success) return false;

            return await TrySetUser(m.Groups[1].Value, short.Parse(m.Groups[2].Value));
        }
        public static async Task<bool> TrySetUser(string name, short delimeter)
        {
            var playerResult = await client.ApiAccess.Destiny2.SearchDestinyPlayerByBungieName(BungieMembershipType.All, new DotNetBungieAPI.Models.Requests.ExactSearchRequest() { DisplayName = name, DisplayNameCode = delimeter });
            if (!playerResult.Response.Any())
            {
                Logger.Debug("user not found");
                return false;
            }

            var user = playerResult.Response.First();
            var profileData = await client.ApiAccess.Destiny2.GetProfile(user.MembershipType, user.MembershipId, 
                new DestinyComponentType[] { DestinyComponentType.Characters, DestinyComponentType.CharacterActivities });

            if (!profileData.Response.CharacterActivities.Data.Any())
            {
                Logger.Debug("private profile");
                return false;
            }

            User = user;
            BungieName = name;
            Delimeter = delimeter;
            UserChars = new DestinyCharacterComponent[profileData.Response.Characters.Data.Count];
            CharacterActivities = new();

            reset = DateTime.UtcNow.Date.AddHours(17);
            if (reset > DateTime.UtcNow)
                reset -= TimeSpan.FromDays(1);

            int i = 0;
            foreach (var character in profileData.Response.Characters.Data.Values)
            {
                UserChars[i++] = character;
                var activitiesResponse = await client.ApiAccess.Destiny2.GetActivityHistory(user.MembershipType, user.MembershipId, character.CharacterId, 100);
                CharacterActivities[character] = activitiesResponse.Response.Activities
                    .Where(x => x.Period > reset)
                    .OrderByDescending(x => x.Period).ToList();
            }

            UserActivities = CharacterActivities
                .SelectMany(x => x.Value)
                .OrderByDescending(x => x.Period).ToList();

            Config.Instance.Settings.Tracker_BungieName = BungieName + "#" + delimeter;
            return true;
        }

        static List<DateTime> usedStarts = new List<DateTime>();
        public static async Task Update()
        {
            await UpdateHistory();
            await UpdateCurrent();

            if (ActiveCharacter is null)
            {
                Logger.Debug("no active char");
                return; 
            }

            var savedTime = UserActivities.Any() ? UserActivities.First().Period.ToLocalTime().AddSeconds(5) : DateTime.MinValue;
            var apiTime = ActiveCharacter.DateActivityStarted.ToLocalTime();

            if (savedTime > apiTime)
            {
                Logger.Debug("saved > api");
                return;
            }

            if (XboxProvider.InstanceStarted != DateTime.MinValue && XboxProvider.InstanceStarted != apiTime && !usedStarts.Contains(apiTime))
            {
                XboxProvider.InstanceStarted = apiTime;
                Logger.Debug($"Saved: {savedTime}");
                usedStarts.Add(apiTime);
            }
        }

        static DateTime lastActivityHistoryUpdate = DateTime.MinValue;
        static async Task UpdateHistory()
        {
            if (User is null && !await TryLoadFromConfig())
                return;

            if (lastActivityHistoryUpdate + TimeSpan.FromMinutes(0.5) > DateTime.Now) return;
            lastActivityHistoryUpdate = DateTime.Now;

            try
            {
                foreach (var character in UserChars)
                {
                    var activitiesResponse = await client.ApiAccess.Destiny2.GetActivityHistory(User.MembershipType, User.MembershipId, character.CharacterId, 15);
                    CharacterActivities[character].InsertRange(0, activitiesResponse.Response.Activities);
                    CharacterActivities[character] = CharacterActivities[character]
                        .Where(x => x.Period > reset)
                        .DistinctBy(x => x.ActivityDetails.InstanceId)
                        .OrderByDescending(x => x.Period).ToList();
                }

                UserActivities = CharacterActivities
                    .SelectMany(x => x.Value)
                    .OrderByDescending(x => x.Period).ToList();
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }
        }

        static DateTime lastCurrentActivityUpdate = DateTime.MinValue;
        static async Task UpdateCurrent()
        {
            if (User is null && !await TryLoadFromConfig())
                return;

            if (lastCurrentActivityUpdate + TimeSpan.FromMinutes(0.25) > DateTime.Now) return;
            lastCurrentActivityUpdate = DateTime.Now;

            try
            {
                var profileData = await client.ApiAccess.Destiny2.GetProfile(User.MembershipType, User.MembershipId, new DestinyComponentType[] { DestinyComponentType.CharacterActivities });
                ActiveCharacter = profileData.Response.CharacterActivities.Data.Values.FirstOrDefault(x => x.CurrentActivity.HasValidHash);
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }
        }
    }
}
