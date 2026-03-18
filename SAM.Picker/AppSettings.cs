using System;
using System.IO;

namespace SAM.Picker
{
    internal static class AppSettings
    {
        private static readonly string BasePath =
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

        private static readonly string SettingsPath = Path.Combine(BasePath, "lib", "sam_settings.ini");

        private static readonly string LegacySettingsPath = Path.Combine(BasePath, "sam_settings.ini");

        public static string SteamApiKey { get; set; } = "";

        public static void Load()
        {
            try
            {
                string path = File.Exists(SettingsPath)
                    ? SettingsPath
                    : LegacySettingsPath;

                if (!File.Exists(path)) return;
                foreach (var line in File.ReadAllLines(path))
                {
                    var parts = line.Split(new[] { '=' }, 2);
                    if (parts.Length == 2 && parts[0].Trim() == "SteamApiKey")
                    {
                        SteamApiKey = parts[1].Trim();
                    }
                }
            }
            catch { }
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
                File.WriteAllText(SettingsPath, $"SteamApiKey={SteamApiKey}\n");
            }
            catch { }
        }
    }
}
