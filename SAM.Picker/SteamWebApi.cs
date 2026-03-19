using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;

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
                return client.DownloadString(url);
            }
        }

        private static string ParseString(string json, string fieldName)
        {
            var match = Regex.Match(json, "\"" + fieldName + "\"\\s*:\\s*\"([^\"]*)\"");
            return match.Success ? match.Groups[1].Value : null;
        }

        private static int ParseInt(string json, string fieldName, int defaultValue = 0)
        {
            var match = Regex.Match(json, "\"" + fieldName + "\"\\s*:\\s*(\\d+)");
            return match.Success ? int.Parse(match.Groups[1].Value) : defaultValue;
        }

        private static float ParseFloat(string json, string fieldName, float defaultValue = 0f)
        {
            var match = Regex.Match(json, "\"" + fieldName + "\"\\s*:\\s*([\\d.]+)");
            return match.Success ? float.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : defaultValue;
        }

        public static PlayerSummary? GetPlayerSummary(string apiKey, ulong steamId)
        {
            try
            {
                string url = string.Format(
                    "https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0002/?key={0}&steamids={1}",
                    apiKey, steamId);
                string json = DownloadString(url);

                return new PlayerSummary
                {
                    PersonaName = ParseString(json, "personaname"),
                    AvatarFullUrl = ParseString(json, "avatarfull"),
                    PersonaState = ParseInt(json, "personastate"),
                    CountryCode = ParseString(json, "loccountrycode"),
                    RealName = ParseString(json, "realname")
                };
            }
            catch
            {
                return null;
            }
        }

        public static int? GetSteamLevel(string apiKey, ulong steamId)
        {
            try
            {
                string url = string.Format(
                    "https://api.steampowered.com/IPlayerService/GetSteamLevel/v1/?key={0}&steamid={1}",
                    apiKey, steamId);
                string json = DownloadString(url);

                return ParseInt(json, "player_level");
            }
            catch
            {
                return null;
            }
        }

        public static BadgeInfo? GetBadges(string apiKey, ulong steamId)
        {
            try
            {
                string url = string.Format(
                    "https://api.steampowered.com/IPlayerService/GetBadges/v1/?key={0}&steamid={1}",
                    apiKey, steamId);
                string json = DownloadString(url);

                var badgeMatches = Regex.Matches(json, "\"badgeid\"\\s*:\\s*\\d+");

                return new BadgeInfo
                {
                    PlayerLevel = ParseInt(json, "player_level"),
                    PlayerXp = ParseInt(json, "player_xp"),
                    PlayerXpNeededToLevelUp = ParseInt(json, "player_xp_needed_to_level_up"),
                    PlayerXpNeededCurrentLevel = ParseInt(json, "player_xp_needed_current_level"),
                    BadgeCount = badgeMatches.Count
                };
            }
            catch
            {
                return null;
            }
        }

        public static GameAchievementData? GetPlayerAchievements(string apiKey, ulong steamId, uint appId)
        {
            try
            {
                string url = string.Format(
                    "https://api.steampowered.com/ISteamUserStats/GetPlayerAchievements/v0001/?appid={0}&key={1}&steamid={2}",
                    appId, apiKey, steamId);
                string json = DownloadString(url);

                var achievementBlocks = Regex.Matches(json, "\\{[^}]*\"apiname\"\\s*:\\s*\"([^\"]*)\"[^}]*\\}");
                int total = 0;
                int unlocked = 0;
                var unlockedNames = new List<string>();

                foreach (Match block in achievementBlocks)
                {
                    total++;
                    string blockText = block.Value;
                    int achieved = ParseInt(blockText, "achieved");
                    if (achieved == 1)
                    {
                        unlocked++;
                        if (unlockedNames.Count < 5)
                        {
                            string name = ParseString(blockText, "apiname");
                            if (name != null)
                            {
                                unlockedNames.Add(name);
                            }
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
                string url = string.Format(
                    "https://api.steampowered.com/ISteamUserStats/GetGlobalAchievementPercentagesForApp/v0002/?gameid={0}",
                    appId);
                string json = DownloadString(url);

                var result = new Dictionary<string, float>();
                var blocks = Regex.Matches(json, "\\{[^}]*\"name\"\\s*:\\s*\"([^\"]*)\"[^}]*\\}");

                foreach (Match block in blocks)
                {
                    string name = ParseString(block.Value, "name");
                    float percent = ParseFloat(block.Value, "percent");
                    if (name != null && !result.ContainsKey(name))
                    {
                        result[name] = percent;
                    }
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
            try
            {
                string url = string.Format(
                    "https://api.steampowered.com/ISteamUserStats/GetSchemaForGame/v2/?appid={0}&key={1}",
                    appId, apiKey);
                string json = DownloadString(url);

                var result = new List<AchievementSchema>();
                var blocks = Regex.Matches(json, "\\{[^{}]*\"name\"\\s*:\\s*\"[^\"]*\"[^{}]*\"icon\"\\s*:\\s*\"[^\"]*\"[^{}]*\\}");

                foreach (Match block in blocks)
                {
                    string blockText = block.Value;
                    result.Add(new AchievementSchema
                    {
                        Name = ParseString(blockText, "name"),
                        DisplayName = ParseString(blockText, "displayName"),
                        Description = ParseString(blockText, "description"),
                        IconUrl = ParseString(blockText, "icon"),
                        IconGrayUrl = ParseString(blockText, "icongray")
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
