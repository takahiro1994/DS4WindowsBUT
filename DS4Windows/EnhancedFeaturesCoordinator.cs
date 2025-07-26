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
using System.Linq;
using System.Threading.Tasks;

namespace DS4Windows
{
    public class EnhancedFeaturesConfiguration
    {
        public bool PerformanceAnalyticsEnabled { get; set; } = true;
        public bool SmartGameDetectionEnabled { get; set; } = true;
        public bool AdvancedLightbarEnabled { get; set; } = true;
        public bool MotionGesturesEnabled { get; set; } = true;
        public bool MacroSystemEnabled { get; set; } = true;
        public bool PowerManagementEnabled { get; set; } = true;
        public bool VoiceCommandsEnabled { get; set; } = false; // Disabled by default
        public bool AccessibilityFeaturesEnabled { get; set; } = false; // Enabled when needed
        public bool APIServerEnabled { get; set; } = false; // Disabled by default
        public bool AdvancedDebuggingEnabled { get; set; } = false; // Disabled by default
        public bool ProfileScriptingEnabled { get; set; } = false; // Disabled by default
        
        // Feature-specific settings
        public int APIServerPort { get; set; } = 8080;
        public AccessibilityMode DefaultAccessibilityMode { get; set; } = AccessibilityMode.None;
        public PowerMode DefaultPowerMode { get; set; } = PowerMode.Balanced;
        public DebugLevel DebuggingLevel { get; set; } = DebugLevel.Warning;
    }

    public class EnhancedFeaturesCoordinator : IDisposable
    {
        private readonly Dictionary<int, ControllerEnhancedFeatures> controllerFeatures;
        private readonly EnhancedFeaturesConfiguration configuration;
        private readonly SmartGameDetection gameDetection;
        private readonly DS4WindowsAPI apiServer;
        private readonly AdvancedDebugging debugSystem;
        private readonly VoiceCommandSystem voiceCommands;
        private bool disposed;

        public event EventHandler<FeatureStatusChangedEventArgs> FeatureStatusChanged;
        public event EventHandler<ControllerConnectedEventArgs> ControllerEnhancedFeaturesReady;

        public EnhancedFeaturesConfiguration Configuration => configuration;
        public SmartGameDetection GameDetection => gameDetection;
        public DS4WindowsAPI APIServer => apiServer;
        public AdvancedDebugging DebugSystem => debugSystem;
        public VoiceCommandSystem VoiceCommands => voiceCommands;

        public EnhancedFeaturesCoordinator()
        {
            controllerFeatures = new Dictionary<int, ControllerEnhancedFeatures>();
            configuration = LoadConfiguration();

            // Initialize global systems
            if (configuration.SmartGameDetectionEnabled)
            {
                gameDetection = new SmartGameDetection();
                gameDetection.GameDetected += OnGameDetected;
                gameDetection.GameExited += OnGameExited;
            }

            if (configuration.APIServerEnabled)
            {
                apiServer = new DS4WindowsAPI(configuration.APIServerPort);
                _ = Task.Run(() => apiServer.StartAsync());
            }

            if (configuration.AdvancedDebuggingEnabled)
            {
                debugSystem = new AdvancedDebugging();
                debugSystem.SetLogLevel(configuration.DebuggingLevel);
            }

            if (configuration.VoiceCommandsEnabled)
            {
                voiceCommands = new VoiceCommandSystem();
                _ = Task.Run(() => voiceCommands.StartListeningAsync());
            }

            AppLogger.LogToGui("Enhanced Features Coordinator initialized", false);
        }

        public void RegisterController(DS4Device device, int index)
        {
            if (device == null || controllerFeatures.ContainsKey(index))
                return;

            try
            {
                var features = new ControllerEnhancedFeatures(device, index, configuration);
                controllerFeatures[index] = features;

                // Wire up inter-system communications
                SetupControllerIntegrations(features, device, index);

                ControllerEnhancedFeaturesReady?.Invoke(this, new ControllerConnectedEventArgs(device, index));
                AppLogger.LogToGui($"Enhanced features registered for controller {index}", false);
            }
            catch (Exception ex)
            {
                AppLogger.LogToGui($"Error registering enhanced features for controller {index}: {ex.Message}", true);
                debugSystem?.LogEvent(DebugLevel.Error, "Coordinator", 
                    $"Failed to register controller {index}", "EnhancedFeaturesCoordinator", ex);
            }
        }

        public void UnregisterController(int index)
        {
            if (controllerFeatures.TryGetValue(index, out var features))
            {
                features.Dispose();
                controllerFeatures.Remove(index);
                AppLogger.LogToGui($"Enhanced features unregistered for controller {index}", false);
            }
        }

        private void SetupControllerIntegrations(ControllerEnhancedFeatures features, DS4Device device, int index)
        {
            // Performance Analytics <-> Power Management
            if (features.PerformanceAnalytics != null && features.PowerManagement != null)
            {
                features.PerformanceAnalytics.PerformanceAlert += (sender, e) =>
                {
                    if (e.Alert.Type == PerformanceAlertType.HighLatency && 
                        features.PowerManagement.CurrentProfile.Mode != PowerMode.Performance)
                    {
                        features.PowerManagement.SetPowerMode(PowerMode.Performance);
                        AppLogger.LogToGui($"Controller {index}: Switched to Performance mode due to high latency", false);
                    }
                };
            }

            // Battery Manager <-> Power Management
            if (features.BatteryManager != null && features.PowerManagement != null)
            {
                features.BatteryManager.LowBatteryWarning += (sender, e) =>
                {
                    if (e.WarningLevel == BatteryWarningLevel.Critical)
                    {
                        features.PowerManagement.SetPowerMode(PowerMode.PowerSaver);
                    }
                };
            }

            // Game Detection <-> Lightbar Effects
            if (gameDetection != null && features.LightbarEffects != null)
            {
                gameDetection.GameDetected += (sender, e) =>
                {
                    features.LightbarEffects.ApplyGameProfile(e.Game.Name);
                };
            }

            // Motion Gestures <-> Macro System
            if (features.MotionFilter != null && features.MacroSystem != null)
            {
                features.MotionFilter.GestureDetected += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Gesture.AssignedAction))
                    {
                        _ = Task.Run(() => features.MacroSystem.ExecuteMacroAsync(e.Gesture.AssignedAction));
                    }
                };
            }

            // Health Monitor <-> Accessibility Features
            if (features.HealthMonitor != null && features.AccessibilityFeatures != null)
            {
                features.HealthMonitor.WearWarning += (sender, e) =>
                {
                    if (e.WarningType == WearWarningType.StickDrift)
                    {
                        // Suggest recalibration or accessibility assistance
                        AppLogger.LogToGui($"Controller {index}: Consider accessibility features due to hardware wear", false);
                    }
                };
            }

            // Voice Commands integration
            if (voiceCommands != null)
            {
                voiceCommands.CommandExecuted += (sender, e) =>
                {
                    HandleVoiceCommand(e.Command, features, index);
                };
            }

            // Debugging integration
            if (debugSystem != null)
            {
                var sessionId = debugSystem.StartDebugSession($"Controller {index} Enhanced Features");
                features.SetDebugSession(sessionId);
            }
        }

        private void HandleVoiceCommand(VoiceCommand command, ControllerEnhancedFeatures features, int controllerIndex)
        {
            try
            {
                switch (command.Action)
                {
                    case "switch_profile":
                        // Handle profile switching for specific controller
                        break;

                    case "show_battery":
                        if (features.BatteryManager != null)
                        {
                            var stats = features.BatteryManager.CurrentStats;
                            voiceCommands?.SpeakAsync($"Controller {controllerIndex + 1} battery is {stats.CurrentLevel} percent");
                        }
                        break;

                    case "show_performance":
                        if (features.PerformanceAnalytics != null)
                        {
                            var dashboard = features.PerformanceAnalytics.GetCurrentDashboard();
                            voiceCommands?.SpeakAsync($"Controller {controllerIndex + 1} performance score is {dashboard.OverallPerformanceScore:F0} percent");
                        }
                        break;

                    case "enable_accessibility":
                        if (features.AccessibilityFeatures != null)
                        {
                            features.AccessibilityFeatures.SetAccessibilityMode(configuration.DefaultAccessibilityMode);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                debugSystem?.LogEvent(DebugLevel.Error, "VoiceCommand", 
                    $"Error handling voice command for controller {controllerIndex}", "EnhancedFeaturesCoordinator", ex);
            }
        }

        private void OnGameDetected(object sender, GameDetectedEventArgs e)
        {
            AppLogger.LogToGui($"Game detected: {e.Game.Name} ({e.Game.Platform})", false);

            // Apply game-specific optimizations to all controllers
            foreach (var features in controllerFeatures.Values)
            {
                try
                {
                    // Optimize power profile for game type
                    if (features.PowerManagement != null)
                    {
                        features.PowerManagement.OptimizeForGame(e.Game.Name);
                    }

                    // Apply game-specific lightbar profile
                    if (features.LightbarEffects != null)
                    {
                        features.LightbarEffects.ApplyGameProfile(e.Game.Name);
                    }

                    // Adjust motion sensitivity for game genre
                    if (features.MotionFilter != null && IsActionGame(e.Game))
                    {
                        // Could adjust sensitivity based on game type
                    }
                }
                catch (Exception ex)
                {
                    debugSystem?.LogEvent(DebugLevel.Warning, "GameDetection", 
                        $"Error applying game optimizations", "EnhancedFeaturesCoordinator", ex);
                }
            }
        }

        private void OnGameExited(object sender, GameExitedEventArgs e)
        {
            AppLogger.LogToGui($"Game session ended: {e.Game.Name} (Duration: {e.SessionDuration:hh\\:mm\\:ss})", false);

            // Return controllers to balanced mode
            foreach (var features in controllerFeatures.Values)
            {
                features.PowerManagement?.SetPowerMode(PowerMode.Balanced);
            }
        }

        private bool IsActionGame(GameInfo game)
        {
            var actionGameKeywords = new[] { "fps", "shooter", "action", "combat", "fighting", "racing" };
            return actionGameKeywords.Any(keyword => game.Name.ToLower().Contains(keyword));
        }

        public void ProcessControllerState(DS4State currentState, DS4State previousState, DS4Device device, int index)
        {
            if (controllerFeatures.TryGetValue(index, out var features))
            {
                features.ProcessControllerState(currentState, previousState);
            }
        }

        public void EnableFeature(string featureName, bool enabled)
        {
            try
            {
                switch (featureName.ToLower())
                {
                    case "gamedetection":
                        configuration.SmartGameDetectionEnabled = enabled;
                        if (!enabled && gameDetection != null)
                        {
                            gameDetection.Dispose();
                        }
                        break;

                    case "voicecommands":
                        configuration.VoiceCommandsEnabled = enabled;
                        if (enabled && voiceCommands == null)
                        {
                            // Would reinitialize voice commands
                        }
                        else if (!enabled && voiceCommands != null)
                        {
                            _ = Task.Run(() => voiceCommands.StopListeningAsync());
                        }
                        break;

                    case "apiserver":
                        configuration.APIServerEnabled = enabled;
                        if (enabled && apiServer == null)
                        {
                            // Would reinitialize API server
                        }
                        else if (!enabled && apiServer != null)
                        {
                            apiServer.Stop();
                        }
                        break;
                }

                FeatureStatusChanged?.Invoke(this, new FeatureStatusChangedEventArgs(featureName, enabled));
                SaveConfiguration();
            }
            catch (Exception ex)
            {
                AppLogger.LogToGui($"Error toggling feature '{featureName}': {ex.Message}", true);
            }
        }

        public Dictionary<string, bool> GetFeatureStatus()
        {
            return new Dictionary<string, bool>
            {
                ["PerformanceAnalytics"] = configuration.PerformanceAnalyticsEnabled,
                ["SmartGameDetection"] = configuration.SmartGameDetectionEnabled,
                ["AdvancedLightbar"] = configuration.AdvancedLightbarEnabled,
                ["MotionGestures"] = configuration.MotionGesturesEnabled,
                ["MacroSystem"] = configuration.MacroSystemEnabled,
                ["PowerManagement"] = configuration.PowerManagementEnabled,
                ["VoiceCommands"] = configuration.VoiceCommandsEnabled,
                ["AccessibilityFeatures"] = configuration.AccessibilityFeaturesEnabled,
                ["APIServer"] = configuration.APIServerEnabled,
                ["AdvancedDebugging"] = configuration.AdvancedDebuggingEnabled,
                ["ProfileScripting"] = configuration.ProfileScriptingEnabled
            };
        }

        private EnhancedFeaturesConfiguration LoadConfiguration()
        {
            try
            {
                var configPath = System.IO.Path.Combine(Global.appdatapath, "EnhancedFeaturesConfig.json");
                if (System.IO.File.Exists(configPath))
                {
                    var json = System.IO.File.ReadAllText(configPath);
                    return System.Text.Json.JsonSerializer.Deserialize<EnhancedFeaturesConfiguration>(json) ?? new EnhancedFeaturesConfiguration();
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogToGui($"Error loading enhanced features configuration: {ex.Message}", true);
            }

            return new EnhancedFeaturesConfiguration();
        }

        private void SaveConfiguration()
        {
            try
            {
                var configPath = System.IO.Path.Combine(Global.appdatapath, "EnhancedFeaturesConfig.json");
                var json = System.Text.Json.JsonSerializer.Serialize(configuration, new System.Text.Json.JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                System.IO.File.WriteAllText(configPath, json);
            }
            catch (Exception ex)
            {
                AppLogger.LogToGui($"Error saving enhanced features configuration: {ex.Message}", true);
            }
        }

        public void Dispose()
        {
            if (disposed) return;

            disposed = true;

            try
            {
                // Dispose all controller features
                foreach (var features in controllerFeatures.Values)
                {
                    features.Dispose();
                }
                controllerFeatures.Clear();

                // Dispose global systems
                gameDetection?.Dispose();
                apiServer?.Dispose();
                debugSystem?.Dispose();
                voiceCommands?.Dispose();

                AppLogger.LogToGui("Enhanced Features Coordinator disposed", false);
            }
            catch (Exception ex)
            {
                AppLogger.LogToGui($"Error disposing EnhancedFeaturesCoordinator: {ex.Message}", true);
            }
        }
    }

    // Individual controller's enhanced features manager
    public class ControllerEnhancedFeatures : IDisposable
    {
        public PerformanceAnalytics PerformanceAnalytics { get; private set; }
        public BatteryManager BatteryManager { get; private set; }
        public HardwareHealthMonitor HealthMonitor { get; private set; }
        public AdvancedLightbarEffects LightbarEffects { get; private set; }
        public AdvancedMotionFilter MotionFilter { get; private set; }
        public AdvancedMacroSystem MacroSystem { get; private set; }
        public PowerManagement PowerManagement { get; private set; }
        public AccessibilityFeatures AccessibilityFeatures { get; private set; }
        public ProfileScriptingEngine ScriptingEngine { get; private set; }

        private readonly DS4Device device;
        private readonly int controllerIndex;
        private string debugSessionId;

        public ControllerEnhancedFeatures(DS4Device device, int index, EnhancedFeaturesConfiguration config)
        {
            this.device = device;
            this.controllerIndex = index;

            InitializeFeatures(config);
        }

        private void InitializeFeatures(EnhancedFeaturesConfiguration config)
        {
            try
            {
                if (config.PerformanceAnalyticsEnabled)
                {
                    PerformanceAnalytics = new PerformanceAnalytics(device);
                }

                BatteryManager = new BatteryManager(device);
                HealthMonitor = new HardwareHealthMonitor(device);

                if (config.AdvancedLightbarEnabled)
                {
                    LightbarEffects = new AdvancedLightbarEffects(device);
                }

                if (config.MotionGesturesEnabled)
                {
                    MotionFilter = new AdvancedMotionFilter();
                }

                if (config.MacroSystemEnabled)
                {
                    MacroSystem = new AdvancedMacroSystem(device);
                }

                if (config.PowerManagementEnabled)
                {
                    PowerManagement = new PowerManagement(device, BatteryManager);
                    PowerManagement.SetPowerMode(config.DefaultPowerMode);
                }

                if (config.AccessibilityFeaturesEnabled)
                {
                    AccessibilityFeatures = new AccessibilityFeatures(device);
                    if (config.DefaultAccessibilityMode != AccessibilityMode.None)
                    {
                        AccessibilityFeatures.SetAccessibilityMode(config.DefaultAccessibilityMode);
                    }
                }

                if (config.ProfileScriptingEnabled)
                {
                    ScriptingEngine = new ProfileScriptingEngine();
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogToGui($"Error initializing enhanced features for controller {controllerIndex}: {ex.Message}", true);
            }
        }

        public void ProcessControllerState(DS4State currentState, DS4State previousState)
        {
            // Update all systems with current controller state
            BatteryManager?.UpdateBatteryStats();
            HealthMonitor?.UpdateHealthMonitoring(currentState);
            PerformanceAnalytics?.RecordPacketData(64); // Typical DS4 packet size

            if (device.SixAxis != null)
            {
                MotionFilter?.ProcessMotionData(device.SixAxis, DateTime.UtcNow);
            }

            MacroSystem?.ProcessControllerState(currentState);
            PowerManagement?.RegisterActivity();
            AccessibilityFeatures?.ProcessControllerInput(currentState, previousState);
            ScriptingEngine?.ProcessControllerState(currentState, previousState, device, controllerIndex);
        }

        public void SetDebugSession(string sessionId)
        {
            debugSessionId = sessionId;
        }

        public void Dispose()
        {
            PerformanceAnalytics?.Dispose();
            LightbarEffects?.Dispose();
            PowerManagement?.Dispose();
            AccessibilityFeatures?.Dispose();
            ScriptingEngine?.Dispose();
        }
    }

    // Event argument classes
    public class FeatureStatusChangedEventArgs : EventArgs
    {
        public string FeatureName { get; }
        public bool IsEnabled { get; }

        public FeatureStatusChangedEventArgs(string featureName, bool isEnabled)
        {
            FeatureName = featureName;
            IsEnabled = isEnabled;
        }
    }

    public class ControllerConnectedEventArgs : EventArgs
    {
        public DS4Device Device { get; }
        public int Index { get; }

        public ControllerConnectedEventArgs(DS4Device device, int index)
        {
            Device = device;
            Index = index;
        }
    }
}
