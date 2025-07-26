/*
DS4Windows
Copyright (C) 2023  Travis Nickles

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace DS4Windows
{
    public enum GamePlatform
    {
        Steam,
        EpicGames,
        GOG,
        UbisoftConnect,
        EAOrigin,
        BattleNet,
        MicrosoftStore,
        Standalone,
        Unknown
    }

    public class GameInfo
    {
        public string Name { get; set; }
        public string ExecutablePath { get; set; }
        public string ProcessName { get; set; }
        public GamePlatform Platform { get; set; }
        public string ProfileName { get; set; }
        public bool AutoSwitchProfile { get; set; }
        public DateTime LastPlayed { get; set; }
        public TimeSpan PlayTime { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }

    public class GameDetectedEventArgs : EventArgs
    {
        public GameInfo Game { get; }
        public bool IsNewSession { get; }

        public GameDetectedEventArgs(GameInfo game, bool isNewSession)
        {
            Game = game;
            IsNewSession = isNewSession;
        }
    }

    public class GameExitedEventArgs : EventArgs
    {
        public GameInfo Game { get; }
        public TimeSpan SessionDuration { get; }

        public GameExitedEventArgs(GameInfo game, TimeSpan sessionDuration)
        {
            Game = game;
            SessionDuration = sessionDuration;
        }
    }

    public class SmartGameDetection : IDisposable
    {
        private readonly Dictionary<string, GameInfo> knownGames;
        private readonly Dictionary<string, DateTime> activeGameSessions;
        private readonly Timer detectionTimer;
        private readonly List<string> monitoredProcesses;
        private bool disposed;

        // Platform-specific registry and folder paths
        private readonly Dictionary<GamePlatform, List<string>> platformPaths = new()
        {
            [GamePlatform.Steam] = new List<string>
            {
                @"SOFTWARE\Valve\Steam",
                @"SOFTWARE\WOW6432Node\Valve\Steam"
            },
            [GamePlatform.EpicGames] = new List<string>
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Epic")
            },
            [GamePlatform.GOG] = new List<string>
            {
                @"SOFTWARE\GOG.com\Games",
                @"SOFTWARE\WOW6432Node\GOG.com\Games"
            }
        };

        public event EventHandler<GameDetectedEventArgs> GameDetected;
        public event EventHandler<GameExitedEventArgs> GameExited;

        public IReadOnlyDictionary<string, GameInfo> KnownGames => knownGames;
        public IReadOnlyDictionary<string, DateTime> ActiveSessions => activeGameSessions;

        public SmartGameDetection()
        {
            knownGames = new Dictionary<string, GameInfo>();
            activeGameSessions = new Dictionary<string, DateTime>();
            monitoredProcesses = new List<string>();

            // Scan for games on initialization
            Task.Run(ScanForGames);

            // Start monitoring timer (check every 5 seconds)
            detectionTimer = new Timer(CheckRunningProcesses, null, 
                TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        }

        private async Task ScanForGames()
        {
            try
            {
                await Task.Run(() =>
                {
                    ScanSteamGames();
                    ScanEpicGames();
                    ScanGOGGames();
                    ScanStandaloneGames();
                });

                AppLogger.LogToGui($"Game detection: Found {knownGames.Count} games", false);
            }
            catch (Exception ex)
            {
                AppLogger.LogToGui($"Error scanning for games: {ex.Message}", true);
            }
        }

        private void ScanSteamGames()
        {
            try
            {
                // Try to find Steam installation
                string steamPath = GetSteamInstallPath();
                if (string.IsNullOrEmpty(steamPath)) return;

                // Scan common Steam library folders
                var libraryFolders = GetSteamLibraryFolders(steamPath);
                
                foreach (var folder in libraryFolders)
                {
                    ScanSteamLibraryFolder(folder);
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogToGui($"Error scanning Steam games: {ex.Message}", true);
            }
        }

        private string GetSteamInstallPath()
        {
            foreach (var regPath in platformPaths[GamePlatform.Steam])
            {
                try
                {
                    using var key = Registry.LocalMachine.OpenSubKey(regPath);
                    if (key?.GetValue("InstallPath") is string path && Directory.Exists(path))
                    {
                        return path;
                    }
                }
                catch { }
            }
            return null;
        }

        private List<string> GetSteamLibraryFolders(string steamPath)
        {
            var folders = new List<string> { Path.Combine(steamPath, "steamapps") };

            try
            {
                var configPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
                if (File.Exists(configPath))
                {
                    var content = File.ReadAllText(configPath);
                    // Simple VDF parsing - would need more sophisticated parsing for production
                    var lines = content.Split('\n');
                    foreach (var line in lines)
                    {
                        if (line.Contains("\"path\"") && line.Contains(":\\"))
                        {
                            var pathStart = line.IndexOf("\"", line.IndexOf("\"path\"") + 6) + 1;
                            var pathEnd = line.IndexOf("\"", pathStart);
                            if (pathEnd > pathStart)
                            {
                                var libraryPath = line.Substring(pathStart, pathEnd - pathStart);
                                var steamappsPath = Path.Combine(libraryPath, "steamapps");
                                if (Directory.Exists(steamappsPath))
                                {
                                    folders.Add(steamappsPath);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogToGui($"Error reading Steam library folders: {ex.Message}", true);
            }

            return folders;
        }

        private void ScanSteamLibraryFolder(string steamappsPath)
        {
            try
            {
                var commonPath = Path.Combine(steamappsPath, "common");
                if (!Directory.Exists(commonPath)) return;

                foreach (var gameDir in Directory.GetDirectories(commonPath))
                {
                    var gameName = Path.GetFileName(gameDir);
                    var exeFiles = Directory.GetFiles(gameDir, "*.exe", SearchOption.TopDirectoryOnly);
                    
                    if (exeFiles.Length > 0)
                    {
                        var mainExe = exeFiles.FirstOrDefault(exe => 
                            Path.GetFileNameWithoutExtension(exe).Equals(gameName, StringComparison.OrdinalIgnoreCase)) 
                            ?? exeFiles[0];

                        var gameInfo = new GameInfo
                        {
                            Name = gameName,
                            ExecutablePath = mainExe,
                            ProcessName = Path.GetFileNameWithoutExtension(mainExe),
                            Platform = GamePlatform.Steam,
                            ProfileName = $"{gameName}_Steam",
                            AutoSwitchProfile = true
                        };

                        knownGames[gameInfo.ProcessName.ToLower()] = gameInfo;
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogToGui($"Error scanning Steam library folder {steamappsPath}: {ex.Message}", true);
            }
        }

        private void ScanEpicGames()
        {
            try
            {
                var epicDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "Epic", "EpicGamesLauncher", "Data", "Manifests");

                if (!Directory.Exists(epicDataPath)) return;

                foreach (var manifestFile in Directory.GetFiles(epicDataPath, "*.item"))
                {
                    // Parse Epic Games manifest files (JSON format)
                    // This would need proper JSON parsing in production
                    var content = File.ReadAllText(manifestFile);
                    if (content.Contains("\"InstallLocation\"") && content.Contains("\"DisplayName\""))
                    {
                        // Simple extraction - would use proper JSON parser in production
                        var displayNameMatch = System.Text.RegularExpressions.Regex.Match(content, "\"DisplayName\":\\s*\"([^\"]+)\"");
                        var installLocationMatch = System.Text.RegularExpressions.Regex.Match(content, "\"InstallLocation\":\\s*\"([^\"]+)\"");

                        if (displayNameMatch.Success && installLocationMatch.Success)
                        {
                            var gameName = displayNameMatch.Groups[1].Value;
                            var installPath = installLocationMatch.Groups[1].Value.Replace("\\\\", "\\");

                            if (Directory.Exists(installPath))
                            {
                                var exeFiles = Directory.GetFiles(installPath, "*.exe", SearchOption.AllDirectories)
                                    .Where(exe => !Path.GetFileName(exe).StartsWith("UE4PrereqSetup", StringComparison.OrdinalIgnoreCase))
                                    .ToArray();

                                if (exeFiles.Length > 0)
                                {
                                    var mainExe = exeFiles[0];
                                    var gameInfo = new GameInfo
                                    {
                                        Name = gameName,
                                        ExecutablePath = mainExe,
                                        ProcessName = Path.GetFileNameWithoutExtension(mainExe),
                                        Platform = GamePlatform.EpicGames,
                                        ProfileName = $"{gameName}_Epic",
                                        AutoSwitchProfile = true
                                    };

                                    knownGames[gameInfo.ProcessName.ToLower()] = gameInfo;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogToGui($"Error scanning Epic Games: {ex.Message}", true);
            }
        }

        private void ScanGOGGames()
        {
            try
            {
                foreach (var regPath in platformPaths[GamePlatform.GOG])
                {
                    using var key = Registry.LocalMachine.OpenSubKey(regPath);
                    if (key == null) continue;

                    foreach (var subKeyName in key.GetSubKeyNames())
                    {
                        using var gameKey = key.OpenSubKey(subKeyName);
                        if (gameKey == null) continue;

                        var gameName = gameKey.GetValue("GAMENAME") as string;
                        var exePath = gameKey.GetValue("EXE") as string;
                        var workingDir = gameKey.GetValue("WORKINGDIR") as string;

                        if (!string.IsNullOrEmpty(gameName) && !string.IsNullOrEmpty(exePath))
                        {
                            var fullExePath = Path.IsPathRooted(exePath) ? exePath : Path.Combine(workingDir ?? "", exePath);
                            
                            if (File.Exists(fullExePath))
                            {
                                var gameInfo = new GameInfo
                                {
                                    Name = gameName,
                                    ExecutablePath = fullExePath,
                                    ProcessName = Path.GetFileNameWithoutExtension(fullExePath),
                                    Platform = GamePlatform.GOG,
                                    ProfileName = $"{gameName}_GOG",
                                    AutoSwitchProfile = true
                                };

                                knownGames[gameInfo.ProcessName.ToLower()] = gameInfo;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogToGui($"Error scanning GOG games: {ex.Message}", true);
            }
        }

        private void ScanStandaloneGames()
        {
            // Scan common game installation directories
            var commonGameDirs = new[]
            {
                @"C:\Program Files\",
                @"C:\Program Files (x86)\",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Games"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Games")
            };

            foreach (var baseDir in commonGameDirs.Where(Directory.Exists))
            {
                try
                {
                    foreach (var gameDir in Directory.GetDirectories(baseDir))
                    {
                        ScanDirectoryForGames(gameDir, GamePlatform.Standalone);
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.LogToGui($"Error scanning directory {baseDir}: {ex.Message}", true);
                }
            }
        }

        private void ScanDirectoryForGames(string directory, GamePlatform platform)
        {
            try
            {
                var exeFiles = Directory.GetFiles(directory, "*.exe", SearchOption.TopDirectoryOnly);
                var gameExes = exeFiles.Where(IsLikelyGameExecutable).ToArray();

                foreach (var exe in gameExes)
                {
                    var gameName = Path.GetFileNameWithoutExtension(exe);
                    var processName = gameName.ToLower();

                    if (!knownGames.ContainsKey(processName))
                    {
                        var gameInfo = new GameInfo
                        {
                            Name = gameName,
                            ExecutablePath = exe,
                            ProcessName = gameName,
                            Platform = platform,
                            ProfileName = $"{gameName}_Standalone",
                            AutoSwitchProfile = true
                        };

                        knownGames[processName] = gameInfo;
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogToGui($"Error scanning game directory {directory}: {ex.Message}", true);
            }
        }

        private bool IsLikelyGameExecutable(string exePath)
        {
            var fileName = Path.GetFileNameWithoutExtension(exePath).ToLower();
            
            // Skip common non-game executables
            var skipPatterns = new[]
            {
                "unins", "setup", "install", "update", "launcher", "crash", "error",
                "config", "settings", "tool", "editor", "server", "dedicated"
            };

            return !skipPatterns.Any(pattern => fileName.Contains(pattern));
        }

        private void CheckRunningProcesses(object state)
        {
            if (disposed) return;

            try
            {
                var runningProcesses = Process.GetProcesses()
                    .Where(p => !string.IsNullOrEmpty(p.ProcessName))
                    .ToLookup(p => p.ProcessName.ToLower());

                var currentActiveGames = new HashSet<string>();

                foreach (var processGroup in runningProcesses)
                {
                    var processName = processGroup.Key;
                    if (knownGames.TryGetValue(processName, out var gameInfo))
                    {
                        currentActiveGames.Add(processName);

                        if (!activeGameSessions.ContainsKey(processName))
                        {
                            // New game session detected
                            activeGameSessions[processName] = DateTime.UtcNow;
                            gameInfo.LastPlayed = DateTime.UtcNow;

                            GameDetected?.Invoke(this, new GameDetectedEventArgs(gameInfo, true));
                            AppLogger.LogToGui($"Game detected: {gameInfo.Name} ({gameInfo.Platform})", false);

                            // Auto-switch profile if enabled
                            if (gameInfo.AutoSwitchProfile && !string.IsNullOrEmpty(gameInfo.ProfileName))
                            {
                                AutoSwitchProfile(gameInfo);
                            }
                        }
                    }
                }

                // Check for ended game sessions
                var endedSessions = activeGameSessions.Keys.Except(currentActiveGames).ToList();
                foreach (var processName in endedSessions)
                {
                    var sessionStart = activeGameSessions[processName];
                    var sessionDuration = DateTime.UtcNow - sessionStart;
                    activeGameSessions.Remove(processName);

                    if (knownGames.TryGetValue(processName, out var gameInfo))
                    {
                        gameInfo.PlayTime = gameInfo.PlayTime.Add(sessionDuration);
                        GameExited?.Invoke(this, new GameExitedEventArgs(gameInfo, sessionDuration));
                        AppLogger.LogToGui($"Game session ended: {gameInfo.Name} (Duration: {sessionDuration:hh\\:mm\\:ss})", false);
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogToGui($"Error checking running processes: {ex.Message}", true);
            }
        }

        private void AutoSwitchProfile(GameInfo gameInfo)
        {
            try
            {
                // This would integrate with the existing profile system
                AppLogger.LogToGui($"Auto-switching to profile: {gameInfo.ProfileName}", false);
                
                // Implementation would depend on existing DS4Windows profile switching mechanism
                // Global.LoadProfile(0, gameInfo.ProfileName);
            }
            catch (Exception ex)
            {
                AppLogger.LogToGui($"Error auto-switching profile for {gameInfo.Name}: {ex.Message}", true);
            }
        }

        public void AddCustomGame(string name, string executablePath, string profileName = null)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(executablePath))
                return;

            if (!File.Exists(executablePath))
            {
                AppLogger.LogToGui($"Executable not found: {executablePath}", true);
                return;
            }

            var processName = Path.GetFileNameWithoutExtension(executablePath).ToLower();
            var gameInfo = new GameInfo
            {
                Name = name,
                ExecutablePath = executablePath,
                ProcessName = Path.GetFileNameWithoutExtension(executablePath),
                Platform = GamePlatform.Standalone,
                ProfileName = profileName ?? $"{name}_Custom",
                AutoSwitchProfile = !string.IsNullOrEmpty(profileName)
            };

            knownGames[processName] = gameInfo;
            AppLogger.LogToGui($"Added custom game: {name}", false);
        }

        public void RemoveGame(string processName)
        {
            if (knownGames.Remove(processName.ToLower()))
            {
                AppLogger.LogToGui($"Removed game: {processName}", false);
            }
        }

        public GameInfo GetCurrentGame()
        {
            var currentProcess = activeGameSessions.Keys.FirstOrDefault();
            return currentProcess != null && knownGames.TryGetValue(currentProcess, out var gameInfo) ? gameInfo : null;
        }

        public List<GameInfo> GetGamesByPlatform(GamePlatform platform)
        {
            return knownGames.Values.Where(g => g.Platform == platform).ToList();
        }

        public void Dispose()
        {
            if (disposed) return;

            disposed = true;
            detectionTimer?.Dispose();
        }
    }
}
