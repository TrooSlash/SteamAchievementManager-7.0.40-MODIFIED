# SAM Evolved (Steam Achievement Manager)

**[EN](README.md)** | **[RU](README.ru.md)**

A heavily modified version of [Steam Achievement Manager](https://github.com/gibbed/SteamAchievementManager) by gibbed. This fork adds Steam Web API integration, idle modes, VAC protection, profile panel, parallel loading, localization, and many quality-of-life improvements while preserving full compatibility with the original Steam API layer.

[![Download Latest Release](https://img.shields.io/github/v/release/TrooSlash/SAM-Evolved_Steam_Achievement_Manager?label=Download&style=for-the-badge)](https://github.com/TrooSlash/SAM-Evolved_Steam_Achievement_Manager/releases/latest)

---

## Table of Contents

- [Requirements](#requirements)
- [Build](#build)
- [Changes from Original](#changes-from-original)
  - [New Features](#new-features)
  - [Improvements and Fixes](#improvements-and-fixes)
  - [Unchanged](#unchanged)
- [Feature Details](#feature-details)
  - [Steam Web API Integration](#steam-web-api-integration)
  - [Profile Panel](#profile-panel)
  - [Achievement Progress](#achievement-progress)
  - [VAC Protection](#vac-protection)
  - [Three-Phase Loading](#three-phase-loading)
  - [Idle Modes](#idle-modes)
  - [Active Games Manager](#active-games-manager)
  - [View Modes](#view-modes)
  - [Playtime Data](#playtime-data)
  - [Localization](#localization)
  - [Protected Achievement Detection](#protected-achievement-detection)
  - [Logging](#logging)
  - [Keyboard Shortcuts](#keyboard-shortcuts)
  - [Achievement Editor](#achievement-editor)
- [Command Line Arguments](#command-line-arguments)
- [Project Structure](#project-structure)
- [Screenshots](#screenshots)
- [Attribution](#attribution)

---

## Requirements

- Windows 7 or later
- .NET Framework 4.8
- Steam client running with an authenticated account
- Platform: x86 (32-bit)
- Steam Web API key (optional, enables additional features)

## Build

```
dotnet build SAM.sln -c Release -p:Platform=x86
```

Output: `upload\SAM.Picker.exe`, `upload\SAM.Game.exe`

---

## Changes from Original

### New Features

| Feature | Description |
|---------|-------------|
| **Steam Web API** | Optional API key enables full game library detection, achievement stats, profile display, and VAC protection |
| **Profile Panel** | Avatar, nickname, online status, country, Steam level, XP progress, badge count |
| **Achievement Progress** | Per-game unlocked/total achievement counter in the game list (requires API key) |
| **Global Achievement %** | Each achievement shows what percentage of all players unlocked it |
| **VAC Protection** | Automatic detection of VAC-protected games with warning before achievement modification |
| **Three-Phase Loading** | Local games shown instantly, game types and API data loaded in background |
| **6 Idle Modes** | Simple, Sequential, Round-Robin, Target Hours, Schedule, Anti-Idle |
| **Active Games Manager** | Centralized window to monitor, pause, resume, and stop idle processes |
| **Playtime Display** | Hours played and last played date from local Steam files (no API key needed) |
| **Tile / List View** | Switch between card-style tile view and detailed list view with sortable columns |
| **Batch Game Selection** | Checkboxes in list view for selecting up to 32 games |
| **Localization** | English and Russian UI with runtime language switching |
| **Parallel Downloads** | Up to 6 concurrent logo downloads and 8 concurrent achievement API requests |
| **Image Caching** | Game icons cached in memory, persist across refresh without re-downloading |
| **Graceful Idle Shutdown** | Named EventWaitHandle signals for clean SteamAPI disconnection |
| **Manifest Cleanup** | Automatic removal of orphaned `appmanifest_*.acf` files after idle |
| **Batch Unlock via CLI** | `--unlock-all` argument to unlock all achievements without GUI |
| **Protected Achievement Detection** | Automatic background scan of Steam schema files; lock icon in game list, visual markers in editor, "Hide protected" filter |
| **File Logging** | Serilog-based logging to `lib/logs/` with daily rotation, configurable log level, "Open Logs" button in Settings |
| **API Key Encryption** | Steam API key encrypted via Windows DPAPI (`ENC:` prefix in settings file) |
| **Keyboard Shortcuts** | F5/Ctrl+R (refresh), Ctrl+F (search), Ctrl+S (settings/save) |

### Improvements and Fixes

| Area | Change |
|------|--------|
| **Startup** | Three-phase parallel loading: local scan, XML types, API enrichment |
| **Logo Downloads** | 6 concurrent Task.Run downloads instead of single BackgroundWorker |
| **Achievement Loading** | 8 concurrent API requests via SemaphoreSlim with incremental UI updates |
| **Image Cache** | Logos persist across refresh -- no re-downloading within the same session |
| **Progress Indicator** | Status bar shows loading phase and progress counters |
| **BeginInvoke Safety** | `IsHandleCreated` guard before all cross-thread BeginInvoke calls |
| **Exception Handling** | Broad `catch(Exception)` prevents silent crashes during downloads |
| **Statistics Tab** | Automatically hidden when a game has 0 statistics |
| **Column Sorting** | Clickable headers with ascending/descending toggle and arrow indicators |
| **Achievement Icons** | Reduced from 64x64 to 32x32 for compact display |
| **Protected Achievements** | Visual highlight for non-modifiable achievements |
| **Thread Safety** | Thread-safe collections for concurrent download tracking |
| **Memory** | Proper WebClient disposal and Bitmap stream handling |
| **HTTP Timeouts** | 10-second timeout on all WebClient calls (was infinite) |
| **Pipe Safety** | Steam pipe handle released on partial Client.Initialize() failure |
| **Native Memory** | AllocHGlobal/FreeHGlobal wrapped in try-finally |
| **Design System** | All colors centralized in shared DarkPalette; semantic tokens replace hardcoded values |
| **Architecture** | Logo download extracted to service class; AssemblyResolve handler shared via Bootstrap |
| **Unit Tests** | 56 NUnit tests covering XP formula, schedule window, playtime formatting, VDF parser, permission flags |

### Unchanged

- **SAM.API** -- The entire Steam API interop layer is untouched. Native vtable calls and pipe management are identical to the original.
- **Game detection logic** -- Steam game enumeration, ownership checks, and app data retrieval are original code.
- **Achievement read/write** -- The core `SetAchievement`, `GetAchievement`, and `StoreStats` calls are unmodified.

---

## Feature Details

### Steam Web API Integration

An optional Steam Web API key unlocks additional features. The key is configured in Settings (gear icon) and stored locally in `sam_settings.ini`.

What the API key enables:
- Full game library detection (finds uninstalled and free-to-play games)
- Achievement progress column in the game list
- Profile panel with avatar, level, and badges
- VAC protection warnings in the achievement editor
- Global achievement unlock percentages

How to get a key:
1. Open [steamcommunity.com/dev/apikey](https://steamcommunity.com/dev/apikey)
2. Enter any domain name (e.g., `localhost`)
3. Copy the generated key into Settings

The key is free, requires no approval, and is stored locally until the application is reinstalled.

### Profile Panel

When an API key is configured, a profile panel appears at the top of the main window:

- Avatar (64x64)
- Persona name with colored online status indicator
- Country code
- Steam level displayed in an accent-colored badge
- XP progress bar (current / needed for next level)
- Total badge count

Profile data is loaded asynchronously on startup.

### Achievement Progress

With an API key, the game list gains an "Achievements" column showing the unlock ratio per game (e.g., `12/50`). Achievements are loaded in parallel (8 concurrent requests) with incremental UI updates. Games without achievements display a dash. The column is automatically hidden when no API key is configured.

### VAC Protection

The achievement editor automatically checks whether a game uses VAC (Valve Anti-Cheat) or third-party anti-cheat systems (EAC, BattlEye). If detected:

- A warning panel is displayed at the top of the editor
- Achievement modification is blocked by default
- An override button allows the user to proceed at their own risk with an explicit confirmation dialog
- The "Unlock All" button shows a separate VAC-specific warning

This protection does not require an API key -- it uses the Steam store page category data.

### Three-Phase Loading

Game loading is split into three phases for faster startup:

| Phase | Source | Speed | Purpose |
|-------|--------|-------|---------|
| 1 | Local Steam files | Instant | Scan appmanifest files, registry, localconfig.vdf. Games appear immediately. |
| 2 | Remote XML | Network | Download game type database. Update types (normal/demo/mod/junk). |
| 3 | Steam Web API | Network | Fetch additional owned games not found locally. Requires API key. |

Playtime data is applied after Phase 1. Logo downloads start after Phase 1. Achievement loading starts after Phase 3 completes.

### Idle Modes

Six modes available through the Idle Games button on the toolbar:

| Mode | Description |
|------|-------------|
| **Simple** | Launch all selected games simultaneously. Optional hour limit. |
| **Sequential** | Launch games one at a time, each for a specified number of hours. |
| **Round-Robin** | Rotate between games at a configurable interval (minutes). |
| **Target Hours** | Run games until each reaches a target total playtime. |
| **Schedule** | Run only during specified hours (e.g., 02:00 to 08:00). |
| **Anti-Idle** | Periodic restart of idle processes to prevent Steam timeout. |

When no games are checked, all currently displayed games are used. Maximum 32 games per session (Steam limit). A counter at the bottom shows how many games are selected (e.g., `5/32`).

### Active Games Manager

A dedicated window that opens when idle sessions start:

- Real-time list of all running idle processes
- Per-game controls: Pause / Resume / Stop
- Elapsed time counter for each game
- "Stop All" button with confirmation dialog
- Graceful shutdown via named events, falls back to process kill after 3-second timeout
- Cleans up orphaned Steam manifest files on close

### View Modes

**List View** (default):
- Columns: Game, AppId, Type, Hours, Last Played, Achievements (with API key)
- Sortable by clicking column headers (ascending/descending toggle)
- Checkboxes for batch selection (up to 32 games)
- Small 36x36 game icons with alternating row backgrounds

**Tile View**:
- Card-style grid with game cover images (184x69)
- Custom OwnerDraw rendering with hover highlight
- Virtual mode for smooth scrolling with large libraries

Toggle between views through the Settings dialog.

### Playtime Data

Hours played and last played date are read from Steam's local file:

```
Steam/userdata/<AccountId>/config/localconfig.vdf
```

No Steam Web API key is required. AccountId is computed as the lower 32 bits of SteamID64.

### Localization

Two languages: **English** (default) and **Russian**.

Switch language through the Settings dialog (gear icon on toolbar). The language setting is propagated to the Achievement Editor window via the `SAM_LANGUAGE` environment variable.

Localized elements:
- All toolbar buttons and tooltips
- Status bar messages and progress indicators
- Dialog boxes and error messages
- Column headers
- Idle mode names and descriptions
- Achievement editor labels
- VAC warning messages
- API key instructions

Game names are not translated (they come from Steam).

### Protected Achievement Detection

SAM automatically scans Steam's local schema files (`appcache/stats/UserGameStatsSchema_*.bin`) in the background to detect games with server-validated achievements.

- Games with protected achievements show a lock icon in the game list (e.g., `12/50`)
- Inside the editor, protected achievements have a lock overlay, golden text, and a tooltip
- Title bar shows count: `Game Name | 3 protected`
- "Hide protected" filter button in the toolbar
- Warning dialog when all achievements in a game are protected
- Results cached in `lib/protected_cache.txt` with 7-day expiry and cross-process safety

### Logging

File-based diagnostics using Serilog. All errors and key actions are recorded automatically.

- Log files: `lib/logs/sam-picker-{date}.log`, `lib/logs/sam-game-{appId}-{date}.log`
- Log level configurable in Settings: Debug / Information / Warning / Error
- Changes apply instantly without restart
- "Open Logs" button in Settings for quick access
- Automatic cleanup: daily rotation, 10 MB size limit, keeps last 7 files
- Crash protection: unhandled exceptions logged before exit

### Keyboard Shortcuts

| Shortcut | SAM.Picker | SAM.Game |
|----------|------------|----------|
| **F5** / **Ctrl+R** | Refresh game list | Refresh achievements |
| **Ctrl+F** | Focus search box | — |
| **Ctrl+S** | Open Settings | Save changes |

### Achievement Editor

When opening a game, the editor shows:

**Achievements tab:**
- Achievement list with OwnerDraw rendering (icon, name, description, unlock time)
- Global unlock percentage per achievement (fetched from Steam API, no key required)
- Toolbar: Lock All / Invert / Unlock All / Show Locked Only / Show Unlocked Only / Filter
- Custom checkboxes (checked = teal fill with white checkmark)
- Protected achievements highlighted and blocked from modification
- VAC warning panel with override option (when anti-cheat detected)

**Statistics tab:**
- Game statistics with editable values
- Automatically hidden when a game has 0 statistics

**Commit Changes** button to save modifications to Steam.

---

## Command Line Arguments

### SAM.Picker.exe

Standard launch, no arguments required.

### SAM.Game.exe

```
SAM.Game.exe <AppId>                    -- Open achievement editor for game
SAM.Game.exe <AppId> --idle             -- Idle mode (no GUI, runs indefinitely)
SAM.Game.exe <AppId> --idle --hours=10  -- Idle for 10 hours then exit
SAM.Game.exe <AppId> --unlock-all       -- Unlock all achievements (no GUI)
```

---

## Project Structure

```
SAM.sln
SAM.API/                           -- Steam API library
  Steam/                           -- Steam client connection and pipe management
  Wrappers/                        -- Interface wrappers (SteamUserStats, SteamApps, etc.)
  Types/                           -- Data types (UserStatsReceived, AchievementInfo, etc.)
  Bootstrap.cs                     -- Shared AssemblyResolve handler for lib/ DLLs
  Logging/ApiLogger.cs             -- Logging abstraction (no Serilog dependency)

SAM.Picker/                        -- Main application (game library browser)
  GamePicker.cs                    -- Main form: game list, filters, sorting, parallel loading
  GamePicker.Designer.cs           -- Form layout and control definitions
  GameInfo.cs                      -- Game data model (id, type, playtime, achievements)
  MyListView.cs                    -- Custom ListView with double-buffering
  ProfilePanel.cs                  -- Steam profile display panel
  PlaytimeReader.cs                -- localconfig.vdf parser for playtime data
  SteamWebApi.cs                   -- Steam Web API client (achievements, profile, badges)
  AppSettings.cs                   -- Persistent settings (API key, encryption)
  ActiveGamesForm.cs               -- Active idle games monitor
  IdleSettingsDialog.cs            -- Idle mode configuration dialog
  SettingsDialog.cs                -- Language, view, API key, and log level settings
  Localization.cs                  -- English/Russian localization system
  DarkTheme.cs                     -- Theme engine with custom renderers
  DarkPalette.cs                   -- Shared color palette (compile-linked to SAM.Game)
  LogSetup.cs                      -- Serilog initialization and log level switching
  LogoDownloadService.cs           -- Event-based parallel logo download service

SAM.Game/                          -- Achievement and statistics editor
  Program.cs                       -- Entry point, headless modes, graceful shutdown
  Manager.cs                       -- Achievement/stats form, VAC detection, global %
  Manager.Designer.cs              -- Form layout
  Stats/AchievementInfo.cs         -- Achievement data model
  DarkTheme.cs                     -- Theme for editor window (delegates to DarkPalette)
  GameLocalization.cs              -- Editor localization (reads SAM_LANGUAGE env var)
  LogSetup.cs                      -- Serilog initialization for SAM.Game

SAM.Tests/                         -- Unit tests (NUnit)
  PlaytimeFormattingTests.cs       -- FormatPlaytime, FormatLastPlayed
  XpProgressTests.cs               -- XP progress formula
  ScheduleWindowTests.cs           -- Idle schedule time window
  VdfParserTests.cs                -- localconfig.vdf parser
  PermissionFlagsTests.cs          -- StatFlags bitmask logic
```

---

## Screenshots

### Main Window -- Without API Key
![Main Window without API](screenshots/main-no-api.png)

### Main Window -- With API Key
![Main Window with API](screenshots/main-with-api.png)

### Main Window -- Tile View
![Main Window - Tile View](screenshots/main-tiles.png)

### Achievement Editor
![Achievement Editor](screenshots/achievements.png)

### VAC Protection Warning
![VAC Protection](screenshots/vac-warning.png)

### Idle Settings
![Idle Settings](screenshots/idle-settings.png)

### Active Games Manager
![Active Games Manager](screenshots/active-games.png)

### Settings
![Settings](screenshots/settings.png)

---

## Attribution

Based on [SteamAchievementManager](https://github.com/gibbed/SteamAchievementManager) by gibbed.

Icons from the [Fugue Icons](https://p.yusukekamiyamane.com/) set by Yusuke Kamiyamane.
