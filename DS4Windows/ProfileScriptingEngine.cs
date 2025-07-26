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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace DS4Windows
{
    public enum ScriptLanguage
    {
        CSharp,
        JavaScript,
        Python,
        Lua
    }

    public class ScriptContext
    {
        public DS4Device Device { get; set; }
        public DS4State CurrentState { get; set; }
        public DS4State PreviousState { get; set; }
        public int ControllerIndex { get; set; }
        public Dictionary<string, object> Variables { get; set; } = new Dictionary<string, object>();
        public IScriptingAPI API { get; set; }
    }

    public interface IScriptingAPI
    {
        void Log(string message);
        void LogWarning(string message);
        void LogError(string message);
        void SetLightbarColor(byte r, byte g, byte b);
        void SetVibration(byte left, byte right);
        void SendKey(string key);
        void SendKeyUp(string key);
        void SendKeyDown(string key);
        void MouseClick(string button);
        void MouseMove(int x, int y);
        void ExecuteMacro(string macroName);
        void SwitchProfile(string profileName);
        void ShowNotification(string message);
        void PlayAudio(string audioFile);
        object GetVariable(string name);
        void SetVariable(string name, object value);
        bool IsButtonPressed(string button);
        bool IsButtonReleased(string button);
        float GetTriggerValue(string trigger);
        float GetStickValue(string stick, string axis);
        int GetBatteryLevel();
        bool IsCharging();
        double GetLatency();
        void Wait(int milliseconds);
    }

    public class DS4WindowsScriptingAPI : IScriptingAPI
    {
        private readonly ScriptContext context;
        private readonly Dictionary<string, object> globalVariables;

        public DS4WindowsScriptingAPI(ScriptContext context)
        {
            this.context = context;
            this.globalVariables = new Dictionary<string, object>();
        }

        public void Log(string message) => AppLogger.LogToGui($"Script: {message}", false);
        public void LogWarning(string message) => AppLogger.LogToGui($"Script Warning: {message}", false);
        public void LogError(string message) => AppLogger.LogToGui($"Script Error: {message}", true);

        public void SetLightbarColor(byte r, byte g, byte b)
        {
            if (context.Device != null)
            {
                context.Device.LightBarColor = new DS4Color(r, g, b);
            }
        }

        public void SetVibration(byte left, byte right)
        {
            // This would integrate with the vibration system
            Log($"Setting vibration: Left={left}, Right={right}");
        }

        public void SendKey(string key)
        {
            // This would integrate with the keyboard input system
            Log($"Sending key: {key}");
        }

        public void SendKeyUp(string key) => Log($"Key up: {key}");
        public void SendKeyDown(string key) => Log($"Key down: {key}");
        public void MouseClick(string button) => Log($"Mouse click: {button}");
        public void MouseMove(int x, int y) => Log($"Mouse move: {x}, {y}");
        public void ExecuteMacro(string macroName) => Log($"Executing macro: {macroName}");
        public void SwitchProfile(string profileName) => Log($"Switching to profile: {profileName}");
        public void ShowNotification(string message) => AppLogger.LogToGui(message, false);
        public void PlayAudio(string audioFile) => Log($"Playing audio: {audioFile}");

        public object GetVariable(string name)
        {
            return context.Variables.TryGetValue(name, out var value) ? value : 
                   globalVariables.TryGetValue(name, out value) ? value : null;
        }

        public void SetVariable(string name, object value)
        {
            context.Variables[name] = value;
        }

        public bool IsButtonPressed(string button)
        {
            if (context.CurrentState == null) return false;
            
            return button.ToLower() switch
            {
                "cross" => context.CurrentState.Cross,
                "triangle" => context.CurrentState.Triangle,
                "circle" => context.CurrentState.Circle,
                "square" => context.CurrentState.Square,
                "l1" => context.CurrentState.L1,
                "r1" => context.CurrentState.R1,
                "l2" => context.CurrentState.L2Btn,
                "r2" => context.CurrentState.R2Btn,
                "l3" => context.CurrentState.L3,
                "r3" => context.CurrentState.R3,
                "options" => context.CurrentState.Options,
                "share" => context.CurrentState.Share,
                "ps" => context.CurrentState.PS,
                "touchpad" => context.CurrentState.TouchButton,
                _ => false
            };
        }

        public bool IsButtonReleased(string button)
        {
            return !IsButtonPressed(button) && 
                   (context.PreviousState != null && WasButtonPressed(button, context.PreviousState));
        }

        private bool WasButtonPressed(string button, DS4State state)
        {
            return button.ToLower() switch
            {
                "cross" => state.Cross,
                "triangle" => state.Triangle,
                "circle" => state.Circle,
                "square" => state.Square,
                "l1" => state.L1,
                "r1" => state.R1,
                "l2" => state.L2Btn,
                "r2" => state.R2Btn,
                "l3" => state.L3,
                "r3" => state.R3,
                "options" => state.Options,
                "share" => state.Share,
                "ps" => state.PS,
                "touchpad" => state.TouchButton,
                _ => false
            };
        }

        public float GetTriggerValue(string trigger)
        {
            if (context.CurrentState == null) return 0f;
            
            return trigger.ToLower() switch
            {
                "l2" => context.CurrentState.L2 / 255f,
                "r2" => context.CurrentState.R2 / 255f,
                _ => 0f
            };
        }

        public float GetStickValue(string stick, string axis)
        {
            if (context.CurrentState == null) return 0f;
            
            return (stick.ToLower(), axis.ToLower()) switch
            {
                ("left", "x") => (context.CurrentState.LX - 128) / 128f,
                ("left", "y") => (context.CurrentState.LY - 128) / 128f,
                ("right", "x") => (context.CurrentState.RX - 128) / 128f,
                ("right", "y") => (context.CurrentState.RY - 128) / 128f,
                _ => 0f
            };
        }

        public int GetBatteryLevel()
        {
            return context.Device?.getBattery() ?? 0;
        }

        public bool IsCharging()
        {
            return context.Device?.isCharging() ?? false;
        }

        public double GetLatency()
        {
            return context.Device?.Latency ?? 0.0;
        }

        public void Wait(int milliseconds)
        {
            System.Threading.Thread.Sleep(milliseconds);
        }
    }

    public class ProfileScript
    {
        public string Name { get; set; }
        public string Code { get; set; }
        public ScriptLanguage Language { get; set; } = ScriptLanguage.CSharp;
        public bool IsEnabled { get; set; } = true;
        public string Description { get; set; }
        public List<string> TriggerEvents { get; set; } = new List<string>();
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
        public DateTime LastModified { get; set; } = DateTime.UtcNow;
        public DateTime LastExecuted { get; set; }
        public int ExecutionCount { get; set; }
        public TimeSpan TotalExecutionTime { get; set; }
    }

    public class ScriptExecutionResult
    {
        public bool Success { get; set; }
        public object Result { get; set; }
        public string ErrorMessage { get; set; }
        public Exception Exception { get; set; }
        public TimeSpan ExecutionTime { get; set; }
    }

    public class ProfileScriptingEngine : IDisposable
    {
        private readonly Dictionary<string, ProfileScript> scripts;
        private readonly Dictionary<string, Script<object>> compiledScripts;
        private readonly ScriptOptions scriptOptions;
        private bool disposed;

        public event EventHandler<ScriptExecutedEventArgs> ScriptExecuted;
        public event EventHandler<ScriptErrorEventArgs> ScriptError;

        public IReadOnlyDictionary<string, ProfileScript> Scripts => scripts;

        public ProfileScriptingEngine()
        {
            scripts = new Dictionary<string, ProfileScript>();
            compiledScripts = new Dictionary<string, Script<object>>();

            // Configure script options
            scriptOptions = ScriptOptions.Default
                .WithReferences(
                    typeof(object).Assembly,
                    typeof(Console).Assembly,
                    typeof(System.Linq.Enumerable).Assembly,
                    typeof(System.Math).Assembly,
                    Assembly.GetExecutingAssembly()
                )
                .WithImports(
                    "System",
                    "System.Linq",
                    "System.Collections.Generic",
                    "System.Math",
                    "DS4Windows"
                );

            LoadDefaultScripts();
        }

        private void LoadDefaultScripts()
        {
            // Create example scripts
            CreateBatteryMonitorScript();
            CreateAdaptiveLightbarScript();
            CreateGameDetectionScript();
            CreatePerformanceMonitorScript();
        }

        private void CreateBatteryMonitorScript()
        {
            var script = new ProfileScript
            {
                Name = "BatteryMonitor",
                Description = "Changes lightbar color based on battery level",
                Language = ScriptLanguage.CSharp,
                TriggerEvents = new List<string> { "StateUpdate" },
                Code = @"
// Battery Monitor Script
var batteryLevel = API.GetBatteryLevel();
var isCharging = API.IsCharging();

if (isCharging) {
    // Pulse green when charging
    var intensity = (Math.Sin(DateTime.Now.Millisecond / 100.0) + 1) / 2;
    API.SetLightbarColor(0, (byte)(255 * intensity), 0);
} else {
    // Color based on battery level
    if (batteryLevel > 50) {
        API.SetLightbarColor(0, 255, 0); // Green
    } else if (batteryLevel > 20) {
        API.SetLightbarColor(255, 255, 0); // Yellow
    } else {
        API.SetLightbarColor(255, 0, 0); // Red
    }
}

// Log battery status every 60 seconds
var lastLog = API.GetVariable(""lastBatteryLog"") as DateTime?;
if (!lastLog.HasValue || (DateTime.Now - lastLog.Value).TotalSeconds > 60) {
    API.Log($""Battery: {batteryLevel}% {(isCharging ? ""(Charging)"" : """")}"");
    API.SetVariable(""lastBatteryLog"", DateTime.Now);
}"
            };

            RegisterScript(script);
        }

        private void CreateAdaptiveLightbarScript()
        {
            var script = new ProfileScript
            {
                Name = "AdaptiveLightbar",
                Description = "Adaptive lightbar that responds to controller input",
                Language = ScriptLanguage.CSharp,
                TriggerEvents = new List<string> { "StateUpdate" },
                Code = @"
// Adaptive Lightbar Script
var baseColor = new { R = 0, G = 100, B = 255 }; // Blue base
var intensity = 1.0;

// Increase intensity based on trigger pressure
var leftTrigger = API.GetTriggerValue(""L2"");
var rightTrigger = API.GetTriggerValue(""R2"");
intensity += (leftTrigger + rightTrigger) * 0.5;

// Add stick movement influence
var leftStickX = Math.Abs(API.GetStickValue(""left"", ""x""));
var leftStickY = Math.Abs(API.GetStickValue(""left"", ""y""));
var rightStickX = Math.Abs(API.GetStickValue(""right"", ""x""));
var rightStickY = Math.Abs(API.GetStickValue(""right"", ""y""));

var stickActivity = (leftStickX + leftStickY + rightStickX + rightStickY) / 4.0;
intensity += stickActivity * 0.3;

// Flash on button press
if (API.IsButtonPressed(""cross"") || API.IsButtonPressed(""triangle"") || 
    API.IsButtonPressed(""circle"") || API.IsButtonPressed(""square"")) {
    intensity = 2.0;
}

// Apply intensity with bounds
intensity = Math.Min(intensity, 2.0);
var r = (byte)(baseColor.R * intensity);
var g = (byte)(baseColor.G * intensity);
var b = (byte)(baseColor.B * intensity);

API.SetLightbarColor(r, g, b);"
            };

            RegisterScript(script);
        }

        private void CreateGameDetectionScript()
        {
            var script = new ProfileScript
            {
                Name = "GameDetection",
                Description = "Automatically adjusts settings based on running games",
                Language = ScriptLanguage.CSharp,
                TriggerEvents = new List<string> { "StateUpdate" },
                Code = @"
// Game Detection Script
var lastCheck = API.GetVariable(""lastGameCheck"") as DateTime?;
if (!lastCheck.HasValue || (DateTime.Now - lastCheck.Value).TotalSeconds > 10) {
    API.SetVariable(""lastGameCheck"", DateTime.Now);
    
    // Check for common games (this would be enhanced with actual process detection)
    var processes = System.Diagnostics.Process.GetProcesses();
    var gameFound = false;
    
    foreach (var process in processes.Take(50)) { // Limit to avoid performance issues
        try {
            var processName = process.ProcessName.ToLower();
            
            if (processName.Contains(""steam"") || processName.Contains(""game"")) {
                // Gaming mode detected
                API.SetLightbarColor(255, 0, 255); // Purple for gaming
                API.SetVariable(""gamingMode"", true);
                gameFound = true;
                break;
            }
        } catch {
            // Ignore process access errors
        }
    }
    
    if (!gameFound && (API.GetVariable(""gamingMode"") as bool? ?? false)) {
        // Return to normal mode
        API.SetLightbarColor(0, 100, 255); // Blue for normal
        API.SetVariable(""gamingMode"", false);
        API.Log(""Returned to normal mode"");
    }
}"
            };

            RegisterScript(script);
        }

        private void CreatePerformanceMonitorScript()
        {
            var script = new ProfileScript
            {
                Name = "PerformanceMonitor",
                Description = "Monitors controller performance and alerts on issues",
                Language = ScriptLanguage.CSharp,
                TriggerEvents = new List<string> { "StateUpdate" },
                Code = @"
// Performance Monitor Script
var latency = API.GetLatency();
var highLatencyThreshold = 20.0; // milliseconds

// Track latency history
var latencyHistory = API.GetVariable(""latencyHistory"") as List<double>;
if (latencyHistory == null) {
    latencyHistory = new List<double>();
    API.SetVariable(""latencyHistory"", latencyHistory);
}

latencyHistory.Add(latency);
if (latencyHistory.Count > 100) {
    latencyHistory.RemoveAt(0);
}

// Check for high latency
if (latency > highLatencyThreshold) {
    var alertCount = API.GetVariable(""highLatencyAlerts"") as int? ?? 0;
    alertCount++;
    API.SetVariable(""highLatencyAlerts"", alertCount);
    
    // Flash red for high latency
    if (alertCount % 5 == 1) { // Alert every 5th occurrence
        API.SetLightbarColor(255, 0, 0);
        API.LogWarning($""High latency detected: {latency:F1}ms"");
    }
} else {
    // Reset alert count on normal latency
    API.SetVariable(""highLatencyAlerts"", 0);
}

// Calculate average latency
if (latencyHistory.Count >= 10) {
    var avgLatency = latencyHistory.Average();
    API.SetVariable(""averageLatency"", avgLatency);
    
    // Log performance stats every 60 seconds
    var lastPerfLog = API.GetVariable(""lastPerfLog"") as DateTime?;
    if (!lastPerfLog.HasValue || (DateTime.Now - lastPerfLog.Value).TotalSeconds > 60) {
        API.Log($""Performance - Avg Latency: {avgLatency:F1}ms, Current: {latency:F1}ms"");
        API.SetVariable(""lastPerfLog"", DateTime.Now);
    }
}"
            };

            RegisterScript(script);
        }

        public void RegisterScript(ProfileScript script)
        {
            if (script == null || string.IsNullOrEmpty(script.Name))
                return;

            scripts[script.Name] = script;
            
            // Pre-compile the script if it's C#
            if (script.Language == ScriptLanguage.CSharp)
            {
                try
                {
                    var compiledScript = CSharpScript.Create(script.Code, scriptOptions, typeof(ScriptContext));
                    compiledScripts[script.Name] = compiledScript;
                }
                catch (Exception ex)
                {
                    AppLogger.LogToGui($"Script compilation error for '{script.Name}': {ex.Message}", true);
                }
            }

            AppLogger.LogToGui($"Script registered: {script.Name}", false);
        }

        public void UnregisterScript(string scriptName)
        {
            if (scripts.Remove(scriptName))
            {
                compiledScripts.Remove(scriptName);
                AppLogger.LogToGui($"Script unregistered: {scriptName}", false);
            }
        }

        public async Task<ScriptExecutionResult> ExecuteScriptAsync(string scriptName, ScriptContext context)
        {
            if (!scripts.TryGetValue(scriptName, out var script) || !script.IsEnabled)
            {
                return new ScriptExecutionResult
                {
                    Success = false,
                    ErrorMessage = $"Script '{scriptName}' not found or disabled"
                };
            }

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                context.API = new DS4WindowsScriptingAPI(context);
                object result = null;

                switch (script.Language)
                {
                    case ScriptLanguage.CSharp:
                        result = await ExecuteCSharpScript(script, context);
                        break;
                    
                    default:
                        throw new NotSupportedException($"Script language '{script.Language}' is not supported yet");
                }

                stopwatch.Stop();
                
                // Update script statistics
                script.LastExecuted = DateTime.UtcNow;
                script.ExecutionCount++;
                script.TotalExecutionTime = script.TotalExecutionTime.Add(stopwatch.Elapsed);

                var executionResult = new ScriptExecutionResult
                {
                    Success = true,
                    Result = result,
                    ExecutionTime = stopwatch.Elapsed
                };

                ScriptExecuted?.Invoke(this, new ScriptExecutedEventArgs(script, executionResult));
                return executionResult;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                
                var errorResult = new ScriptExecutionResult
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    Exception = ex,
                    ExecutionTime = stopwatch.Elapsed
                };

                ScriptError?.Invoke(this, new ScriptErrorEventArgs(script, ex));
                AppLogger.LogToGui($"Script execution error in '{scriptName}': {ex.Message}", true);
                
                return errorResult;
            }
        }

        private async Task<object> ExecuteCSharpScript(ProfileScript script, ScriptContext context)
        {
            if (compiledScripts.TryGetValue(script.Name, out var compiledScript))
            {
                // Use pre-compiled script
                var state = await compiledScript.RunAsync(context);
                return state.ReturnValue;
            }
            else
            {
                // Compile and execute on-the-fly
                var result = await CSharpScript.EvaluateAsync(script.Code, scriptOptions, context);
                return result;
            }
        }

        public void ProcessControllerState(DS4State currentState, DS4State previousState, DS4Device device, int controllerIndex)
        {
            var context = new ScriptContext
            {
                Device = device,
                CurrentState = currentState,
                PreviousState = previousState,
                ControllerIndex = controllerIndex
            };

            // Execute scripts that are triggered by state updates
            foreach (var script in scripts.Values.Where(s => s.IsEnabled && s.TriggerEvents.Contains("StateUpdate")))
            {
                try
                {
                    // Execute asynchronously without waiting (fire and forget)
                    _ = Task.Run(() => ExecuteScriptAsync(script.Name, context));
                }
                catch (Exception ex)
                {
                    AppLogger.LogToGui($"Error starting script '{script.Name}': {ex.Message}", true);
                }
            }
        }

        public void TriggerScriptEvent(string eventName, ScriptContext context)
        {
            var triggeredScripts = scripts.Values.Where(s => s.IsEnabled && s.TriggerEvents.Contains(eventName));
            
            foreach (var script in triggeredScripts)
            {
                _ = Task.Run(() => ExecuteScriptAsync(script.Name, context));
            }
        }

        public async Task<bool> SaveScriptAsync(ProfileScript script, string filePath = null)
        {
            try
            {
                filePath = filePath ?? Path.Combine(Global.appdatapath, "Scripts", $"{script.Name}.json");
                
                // Ensure directory exists
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                
                var json = System.Text.Json.JsonSerializer.Serialize(script, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                });
                
                await File.WriteAllTextAsync(filePath, json);
                script.LastModified = DateTime.UtcNow;
                
                AppLogger.LogToGui($"Script saved: {script.Name}", false);
                return true;
            }
            catch (Exception ex)
            {
                AppLogger.LogToGui($"Error saving script '{script.Name}': {ex.Message}", true);
                return false;
            }
        }

        public async Task<ProfileScript> LoadScriptAsync(string filePath)
        {
            try
            {
                var json = await File.ReadAllTextAsync(filePath);
                var script = System.Text.Json.JsonSerializer.Deserialize<ProfileScript>(json, new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                });
                
                if (script != null)
                {
                    RegisterScript(script);
                    AppLogger.LogToGui($"Script loaded: {script.Name}", false);
                }
                
                return script;
            }
            catch (Exception ex)
            {
                AppLogger.LogToGui($"Error loading script from '{filePath}': {ex.Message}", true);
                return null;
            }
        }

        public List<string> GetScriptNames()
        {
            return scripts.Keys.ToList();
        }

        public ProfileScript GetScript(string name)
        {
            return scripts.TryGetValue(name, out var script) ? script : null;
        }

        public void EnableScript(string scriptName, bool enabled)
        {
            if (scripts.TryGetValue(scriptName, out var script))
            {
                script.IsEnabled = enabled;
                AppLogger.LogToGui($"Script '{scriptName}' {(enabled ? "enabled" : "disabled")}", false);
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            
            disposed = true;
            scripts.Clear();
            compiledScripts.Clear();
        }
    }

    // Event argument classes
    public class ScriptExecutedEventArgs : EventArgs
    {
        public ProfileScript Script { get; }
        public ScriptExecutionResult Result { get; }

        public ScriptExecutedEventArgs(ProfileScript script, ScriptExecutionResult result)
        {
            Script = script;
            Result = result;
        }
    }

    public class ScriptErrorEventArgs : EventArgs
    {
        public ProfileScript Script { get; }
        public Exception Error { get; }

        public ScriptErrorEventArgs(ProfileScript script, Exception error)
        {
            Script = script;
            Error = error;
        }
    }
}
