/* Copyright (c) 2024 Rick (rick 'at' gibbed 'dot' us)
 *
 * This software is provided 'as-is', without any express or implied
 * warranty. In no event will the authors be held liable for any damages
 * arising from the use of this software.
 *
 * Permission is granted to anyone to use this software for any purpose,
 * including commercial applications, and to alter it and redistribute it
 * freely, subject to the following restrictions:
 *
 * 1. The origin of this software must not be misrepresented; you must not
 *    claim that you wrote the original software. If you use this software
 *    in a product, an acknowledgment in the product documentation would
 *    be appreciated but is not required.
 *
 * 2. Altered source versions must be plainly marked as such, and must not
 *    be misrepresented as being the original software.
 *
 * 3. This notice may not be removed or altered from any source
 *    distribution.
 */

using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;
using System.Security.AccessControl;
using System.Security.Principal;

namespace SAM.Game
{
    internal static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            long appId;

            if (args.Length == 0)
            {
                Process.Start("SAM.Picker.exe");
                return;
            }

            if (long.TryParse(args[0], out appId) == false)
            {
                MessageBox.Show(
                    "Could not parse application ID from command line argument.",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            if (API.Steam.GetInstallPath() == Application.StartupPath)
            {
                MessageBox.Show(
                    "This tool declines to being run from the Steam directory.",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            bool unlockAll = args.Length > 1 && args[1] == "--unlock-all";
            bool idle = args.Length > 1 && args[1] == "--idle";
            double idleHours = 0;
            if (idle && args.Length > 2 && args[2].StartsWith("--hours="))
            {
                double.TryParse(args[2].Substring(8),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out idleHours);
            }
            bool headless = unlockAll || idle;

            using (API.Client client = new())
            {
                try
                {
                    client.Initialize(appId);
                }
                catch (API.ClientInitializeException e)
                {
                    if (headless)
                    {
                        Console.Error.WriteLine($"Failed to initialize: {e.Message}");
                        Environment.ExitCode = 1;
                        return;
                    }

                    if (e.Failure == API.ClientInitializeFailure.ConnectToGlobalUser)
                    {
                        MessageBox.Show(
                            "Steam is not running. Please start Steam then run this tool again.\n\n" +
                            "If you have the game through Family Share, the game may be locked due to\n" +
                            "the Family Share account actively playing a game.\n\n" +
                            "(" + e.Message + ")",
                            "Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                    else if (string.IsNullOrEmpty(e.Message) == false)
                    {
                        MessageBox.Show(
                            "Steam is not running. Please start Steam then run this tool again.\n\n" +
                            "(" + e.Message + ")",
                            "Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                    else
                    {
                        MessageBox.Show(
                            "Steam is not running. Please start Steam then run this tool again.",
                            "Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                    return;
                }
                catch (DllNotFoundException)
                {
                    if (headless)
                    {
                        Console.Error.WriteLine("DLL not found error.");
                        Environment.ExitCode = 1;
                        return;
                    }

                    MessageBox.Show(
                        "You've caused an exceptional error!",
                        "Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }

                if (unlockAll)
                {
                    Environment.ExitCode = RunHeadlessUnlockAll(appId, client) ? 0 : 1;
                    return;
                }

                if (idle)
                {
                    Environment.ExitCode = RunIdle(appId, client, idleHours) ? 0 : 1;
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new Manager(appId, client));
            }
        }

        private static bool RunHeadlessUnlockAll(long appId, API.Client client)
        {
            // Request user stats
            var steamId = client.SteamUser.GetSteamId();
            var callHandle = client.SteamUserStats.RequestUserStats(steamId);
            if (callHandle == API.CallHandle.Invalid)
            {
                return false;
            }

            // Poll callbacks until stats are received (max 10 seconds)
            bool statsReceived = false;
            int statsResult = -1;
            var callback = client.CreateAndRegisterCallback<API.Callbacks.UserStatsReceived>();
            callback.OnRun += (param) =>
            {
                statsResult = param.Result;
                statsReceived = true;
            };

            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (!statsReceived && DateTime.UtcNow < deadline)
            {
                client.RunCallbacks(false);
                Thread.Sleep(100);
            }

            if (!statsReceived || statsResult != 1)
            {
                return false;
            }

            // Enumerate achievements using API
            uint numAchievements = client.SteamUserStats.GetNumAchievements();
            if (numAchievements == 0)
            {
                return true; // no achievements = success
            }

            int unlocked = 0;
            for (uint i = 0; i < numAchievements; i++)
            {
                string name = client.SteamUserStats.GetAchievementName(i);
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                if (client.SteamUserStats.GetAchievement(name, out bool isAchieved) && isAchieved)
                {
                    continue; // already unlocked
                }

                if (client.SteamUserStats.SetAchievement(name, true))
                {
                    unlocked++;
                }
            }

            if (unlocked > 0)
            {
                return client.SteamUserStats.StoreStats();
            }

            return true;
        }

        private static bool RunIdle(long appId, API.Client client, double hours)
        {
            Console.WriteLine($"Idling app {appId}...");
            if (hours > 0)
            {
                Console.WriteLine($"Will idle for {hours:F1} hour(s).");
            }
            else
            {
                Console.WriteLine("Idling indefinitely. Press Ctrl+C or kill process to stop.");
            }

            var startTime = DateTime.UtcNow;
            var endTime = hours > 0
                ? startTime.AddHours(hours)
                : DateTime.MaxValue;

            bool cancelled = false;
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                cancelled = true;
            };

            // Named event for graceful shutdown from ActiveGamesForm
            string eventName = $"Local\\SAM_Idle_Stop_{appId}";
            using var stopEvent = new EventWaitHandle(false, EventResetMode.ManualReset, eventName);

            while (!cancelled && DateTime.UtcNow < endTime)
            {
                // Wait 5 seconds or until stop event is signaled
                if (stopEvent.WaitOne(5000))
                {
                    Console.WriteLine("\nReceived stop signal.");
                    break;
                }

                client.RunCallbacks(false);

                if (hours > 0)
                {
                    var elapsed = DateTime.UtcNow - startTime;
                    var remaining = endTime - DateTime.UtcNow;
                    if (remaining.TotalSeconds > 0)
                    {
                        Console.Write($"\rElapsed: {elapsed.TotalHours:F2}h / {hours:F1}h   ");
                    }
                }
            }

            Console.WriteLine();
            var total = DateTime.UtcNow - startTime;
            Console.WriteLine($"Idle complete. Total time: {total.TotalHours:F2} hours.");
            return true;
        }
    }
}
