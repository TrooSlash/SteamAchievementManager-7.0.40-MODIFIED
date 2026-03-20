using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SAM.Picker
{
    internal struct PlayerSummary
    {
        public string PersonaName;
        public string AvatarFullUrl;
        public int PersonaState;
        public string CountryCode;
        public string RealName;
    }

    internal struct BadgeInfo
    {
        public int PlayerLevel;
        public int PlayerXp;
        public int PlayerXpNeededToLevelUp;
        public int PlayerXpNeededCurrentLevel;
        public int BadgeCount;
    }

    internal struct GameAchievementData
    {
        public int TotalAchievements;
        public int UnlockedAchievements;
        public List<string> UnlockedNames;
    }

    internal struct AchievementSchema
    {
        public string Name;
        public string DisplayName;
        public string Description;
        public string IconUrl;
        public string IconGrayUrl;
    }

    internal static class SteamWebApi
    {
        private static string DownloadString(string url)
        {
            using (var client = new WebClient())
            {
                client.Encoding = System.Text.Encoding.UTF8;
                return client.DownloadString(url);
            }
        }

        public static PlayerSummary? GetPlayerSummary(string apiKey, ulong steamId)
        {
            if (string.IsNullOrEmpty(apiKey))
                return null;

            try
            {
                string url = $"https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0002/?key={apiKey}&steamids={steamId}";
                string json = DownloadString(url);

                var root = JObject.Parse(json);
                var players = root["response"]?["players"] as JArray;
                if (players == null || players.Count == 0)
                    return null;

                var player = players[0];
                return new PlayerSummary
                {
                    PersonaName = player["personaname"]?.ToString(),
                    AvatarFullUrl = player["avatarfull"]?.ToString(),
                    PersonaState = player["personastate"]?.Value<int>() ?? 0,
                    CountryCode = player["loccountrycode"]?.ToString(),
                    RealName = player["realname"]?.ToString()
                };
            }
            catch
            {
                return null;
            }
        }

        public static int? GetSteamLevel(string apiKey, ulong steamId)
        {
            if (string.IsNullOrEmpty(apiKey))
                return null;

            try
            {
                string url = $"https://api.steampowered.com/IPlayerService/GetSteamLevel/v1/?key={apiKey}&steamid={steamId}";
                string json = DownloadString(url);

                var root = JObject.Parse(json);
                return root["response"]?["player_level"]?.Value<int>();
            }
            catch
            {
                return null;
            }
        }

        public static BadgeInfo? GetBadges(string apiKey, ulong steamId)
        {
            if (string.IsNullOrEmpty(apiKey))
                return null;

            try
            {
                string url = $"https://api.steampowered.com/IPlayerService/GetBadges/v1/?key={apiKey}&steamid={steamId}";
                string json = DownloadString(url);

                var root = JObject.Parse(json);
                var response = root["response"];
                if (response == null)
                    return null;

                var badges = response["badges"] as JArray;

                return new BadgeInfo
                {
                    PlayerLevel = response["player_level"]?.Value<int>() ?? 0,
                    PlayerXp = response["player_xp"]?.Value<int>() ?? 0,
                    PlayerXpNeededToLevelUp = response["player_xp_needed_to_level_up"]?.Value<int>() ?? 0,
                    PlayerXpNeededCurrentLevel = response["player_xp_needed_current_level"]?.Value<int>() ?? 0,
                    BadgeCount = badges?.Count ?? 0
                };
            }
            catch
            {
                return null;
            }
        }

        public static GameAchievementData? GetPlayerAchievements(string apiKey, ulong steamId, uint appId)
        {
            if (string.IsNullOrEmpty(apiKey))
                return null;

            try
            {
                string url = $"https://api.steampowered.com/ISteamUserStats/GetPlayerAchievements/v0001/?appid={appId}&key={apiKey}&steamid={steamId}";
                string json = DownloadString(url);

                var root = JObject.Parse(json);
                var achievements = root["playerstats"]?["achievements"] as JArray;
                if (achievements == null)
                    return null;

                int total = achievements.Count;
                int unlocked = 0;
                var unlockedNames = new List<string>();

                foreach (var achievement in achievements)
                {
                    if (achievement["achieved"]?.Value<int>() == 1)
                    {
                        unlocked++;
                        if (unlockedNames.Count < 5)
                        {
                            string name = achievement["apiname"]?.ToString();
                            if (!string.IsNullOrEmpty(name))
                                unlockedNames.Add(name);
                        }
                    }
                }

                return new GameAchievementData
                {
                    TotalAchievements = total,
                    UnlockedAchievements = unlocked,
                    UnlockedNames = unlockedNames
                };
            }
            catch
            {
                return null;
            }
        }

        public static Dictionary<string, float> GetGlobalAchievementPercentages(uint appId)
        {
            try
            {
                string url = $"https://api.steampowered.com/ISteamUserStats/GetGlobalAchievementPercentagesForApp/v0002/?gameid={appId}";
                string json = DownloadString(url);

                var root = JObject.Parse(json);
                var achievements = root["achievementpercentages"]?["achievements"] as JArray;
                if (achievements == null)
                    return null;

                var result = new Dictionary<string, float>();
                foreach (var achievement in achievements)
                {
                    string name = achievement["name"]?.ToString();
                    float percent = achievement["percent"]?.Value<float>() ?? 0f;
                    if (!string.IsNullOrEmpty(name) && !result.ContainsKey(name))
                        result[name] = percent;
                }

                return result;
            }
            catch
            {
                return null;
            }
        }

        public static List<AchievementSchema> GetSchemaForGame(string apiKey, uint appId)
        {
            if (string.IsNullOrEmpty(apiKey))
                return null;

            try
            {
                string url = $"https://api.steampowered.com/ISteamUserStats/GetSchemaForGame/v2/?appid={appId}&key={apiKey}";
                string json = DownloadString(url);

                var root = JObject.Parse(json);
                var achievements = root["game"]?["availableGameStats"]?["achievements"] as JArray;
                if (achievements == null)
                    return null;

                var result = new List<AchievementSchema>();
                foreach (var achievement in achievements)
                {
                    result.Add(new AchievementSchema
                    {
                        Name = achievement["name"]?.ToString(),
                        DisplayName = achievement["displayName"]?.ToString(),
                        Description = achievement["description"]?.ToString(),
                        IconUrl = achievement["icon"]?.ToString(),
                        IconGrayUrl = achievement["icongray"]?.ToString()
                    });
                }

                return result;
            }
            catch
            {
                return null;
            }
        }

        public static Bitmap DownloadImage(string url)
        {
            if (string.IsNullOrEmpty(url))
                return null;

            try
            {
                using (var client = new WebClient())
                {
                    byte[] data = client.DownloadData(url);
                    using (var stream = new MemoryStream(data))
                    {
                        return new Bitmap(stream);
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        public static string GetPersonaStateString(int state)
        {
            switch (state)
            {
                case 0: return "Offline";
                case 1: return "Online";
                case 2: return "Busy";
                case 3: return "Away";
                case 4: return "Snooze";
                case 5: return "Looking to trade";
                case 6: return "Looking to play";
                default: return "Unknown";
            }
        }
    }
}
