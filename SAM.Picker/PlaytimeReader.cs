using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace SAM.Picker
{
    internal struct AppLocalData
    {
        public int PlaytimeMinutes;
        public long LastPlayedTimestamp;
    }

    /// <summary>
    /// Reads playtime and last-played data from Steam's localconfig.vdf file.
    /// </summary>
    internal static class PlaytimeReader
    {
        /// <summary>
        /// Returns a dictionary of AppId → AppLocalData (playtime + last played).
        /// </summary>
        public static Dictionary<uint, AppLocalData> Read(ulong steamId64)
        {
            var result = new Dictionary<uint, AppLocalData>();

            string steamPath = API.Steam.GetInstallPath();
            if (string.IsNullOrEmpty(steamPath))
                return result;

            uint accountId = (uint)(steamId64 & 0xFFFFFFFF);

            string configPath = Path.Combine(steamPath, "userdata",
                accountId.ToString(), "config", "localconfig.vdf");

            if (!File.Exists(configPath))
                return result;

            try
            {
                string content = File.ReadAllText(configPath);
                ParseAppsSection(content, result);
            }
            catch (Exception)
            {
            }

            return result;
        }

        private static void ParseAppsSection(string content, Dictionary<uint, AppLocalData> result)
        {
            var match = Regex.Match(content, @"""apps""\s*\{", RegexOptions.IgnoreCase);
            if (!match.Success) return;

            int i = match.Index;
            while (i < content.Length && content[i] != '{') i++;
            if (i >= content.Length) return;
            i++;
            int braceDepth = 1;

            while (i < content.Length && braceDepth > 0)
            {
                char c = content[i];
                if (c == '}') { braceDepth--; i++; continue; }

                if (c == '"' && braceDepth == 1)
                {
                    string appIdStr = ReadQuotedString(content, ref i);
                    if (uint.TryParse(appIdStr, out uint appId))
                    {
                        var data = ParseAppBlock(content, ref i);
                        if (data.PlaytimeMinutes >= 0 || data.LastPlayedTimestamp > 0)
                        {
                            result[appId] = data;
                        }
                    }
                    else
                    {
                        SkipBlock(content, ref i);
                    }
                }
                else
                {
                    i++;
                }
            }
        }

        private static AppLocalData ParseAppBlock(string content, ref int i)
        {
            var data = new AppLocalData { PlaytimeMinutes = 0, LastPlayedTimestamp = 0 };

            while (i < content.Length && content[i] != '{' && content[i] != '"') i++;
            if (i >= content.Length || content[i] != '{') return data;
            i++;

            int depth = 1;
            while (i < content.Length && depth > 0)
            {
                char c = content[i];
                if (c == '{') { depth++; i++; continue; }
                if (c == '}') { depth--; i++; continue; }

                if (c == '"' && depth == 1)
                {
                    string key = ReadQuotedString(content, ref i);
                    while (i < content.Length && (content[i] == ' ' || content[i] == '\t' || content[i] == '\r' || content[i] == '\n')) i++;

                    if (i < content.Length && content[i] == '"')
                    {
                        string value = ReadQuotedString(content, ref i);
                        if (string.Equals(key, "Playtime", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(key, "playtime_forever", StringComparison.OrdinalIgnoreCase))
                        {
                            int.TryParse(value, out int pt);
                            data.PlaytimeMinutes = pt;
                        }
                        else if (string.Equals(key, "LastPlayed", StringComparison.OrdinalIgnoreCase))
                        {
                            long.TryParse(value, out long lp);
                            data.LastPlayedTimestamp = lp;
                        }
                    }
                    else if (i < content.Length && content[i] == '{')
                    {
                        SkipBlock(content, ref i);
                    }
                }
                else
                {
                    i++;
                }
            }

            return data;
        }

        private static string ReadQuotedString(string content, ref int i)
        {
            if (i >= content.Length || content[i] != '"') return "";
            i++;
            int start = i;
            while (i < content.Length && content[i] != '"') i++;
            string result = content.Substring(start, i - start);
            if (i < content.Length) i++;
            return result;
        }

        private static void SkipBlock(string content, ref int i)
        {
            while (i < content.Length && content[i] != '{') i++;
            if (i >= content.Length) return;
            i++;
            int depth = 1;
            while (i < content.Length && depth > 0)
            {
                if (content[i] == '{') depth++;
                else if (content[i] == '}') depth--;
                i++;
            }
        }

        public static string FormatPlaytime(int minutes)
        {
            if (minutes <= 0) return "—";
            if (minutes < 60)
                return $"{minutes} min";
            double hours = minutes / 60.0;
            return $"{hours:F1} hrs";
        }

        public static string FormatLastPlayed(long unixTimestamp)
        {
            if (unixTimestamp <= 0) return "—";
            try
            {
                var dt = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).LocalDateTime;
                var diff = DateTime.Now - dt;
                if (diff.TotalMinutes < 60) return "Just now";
                if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
                if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
                if (diff.TotalDays < 365) return dt.ToString("dd MMM");
                return dt.ToString("dd.MM.yyyy");
            }
            catch { return "—"; }
        }
    }
}
