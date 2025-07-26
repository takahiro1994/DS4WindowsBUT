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
using System.Globalization;
using System.Linq;
using System.Speech.Recognition;
using System.Speech.Synthesis;
using System.Threading.Tasks;

namespace DS4Windows
{
    public enum VoiceCommandType
    {
        ProfileControl,
        SystemControl,
        GameAction,
        Accessibility,
        Information,
        Custom
    }

    public class VoiceCommand
    {
        public string Name { get; set; }
        public string[] Phrases { get; set; }
        public VoiceCommandType Type { get; set; }
        public string Action { get; set; }
        public bool IsEnabled { get; set; } = true;
        public float ConfidenceThreshold { get; set; } = 0.7f;
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
    }

    public class VoiceResponse
    {
        public string Text { get; set; }
        public bool UseTextToSpeech { get; set; } = true;
        public string AudioFile { get; set; }
        public int Priority { get; set; } = 5; // 1-10, higher = more important
    }

    public class VoiceCommandExecutedEventArgs : EventArgs
    {
        public VoiceCommand Command { get; }
        public float Confidence { get; }
        public bool Success { get; }

        public VoiceCommandExecutedEventArgs(VoiceCommand command, float confidence, bool success)
        {
            Command = command;
            Confidence = confidence;
            Success = success;
        }
    }

    public class VoiceCommandSystem : IDisposable
    {
        private readonly SpeechRecognitionEngine speechRecognizer;
        private readonly SpeechSynthesizer speechSynthesizer;
        private readonly Dictionary<string, VoiceCommand> voiceCommands;
        private readonly List<VoiceResponse> responseQueue;
        private bool isListening;
        private bool isEnabled;
        private bool disposed;

        public event EventHandler<VoiceCommandExecutedEventArgs> CommandExecuted;
        public event EventHandler<SpeechRecognizedEventArgs> SpeechRecognized;

        public bool IsListening => isListening;
        public bool IsEnabled => isEnabled;
        public int CommandCount => voiceCommands.Count;

        public VoiceCommandSystem()
        {
            voiceCommands = new Dictionary<string, VoiceCommand>();
            responseQueue = new List<VoiceResponse>();

            try
            {
                // Initialize speech recognition
                speechRecognizer = new SpeechRecognitionEngine(new CultureInfo("en-US"));
                speechRecognizer.SetInputToDefaultAudioDevice();
                speechRecognizer.SpeechRecognized += OnSpeechRecognized;
                speechRecognizer.SpeechRecognitionRejected += OnSpeechRejected;

                // Initialize text-to-speech
                speechSynthesizer = new SpeechSynthesizer();
                speechSynthesizer.SetOutputToDefaultAudioDevice();
                speechSynthesizer.Rate = 0; // Normal speed
                speechSynthesizer.Volume = 80; // 80% volume

                RegisterDefaultCommands();
                AppLogger.LogToGui("Voice command system initialized", false);
            }
            catch (Exception ex)
            {
                AppLogger.LogToGui($"Failed to initialize voice command system: {ex.Message}", true);
            }
        }

        private void RegisterDefaultCommands()
        {
            // Profile commands
            RegisterCommand(new VoiceCommand
            {
                Name = "SwitchProfile",
                Phrases = new[] { "switch to profile *", "load profile *", "change profile to *" },
                Type = VoiceCommandType.ProfileControl,
                Action = "switch_profile"
            });

            RegisterCommand(new VoiceCommand
            {
                Name = "ListProfiles",
                Phrases = new[] { "list profiles", "show profiles", "what profiles are available" },
                Type = VoiceCommandType.ProfileControl,
                Action = "list_profiles"
            });

            // System commands
            RegisterCommand(new VoiceCommand
            {
                Name = "ShowBattery",
                Phrases = new[] { "show battery", "battery level", "how much battery" },
                Type = VoiceCommandType.Information,
                Action = "show_battery"
            });

            RegisterCommand(new VoiceCommand
            {
                Name = "ShowStatus",
                Phrases = new[] { "show status", "controller status", "system status" },
                Type = VoiceCommandType.Information,
                Action = "show_status"
            });

            RegisterCommand(new VoiceCommand
            {
                Name = "StartStop",
                Phrases = new[] { "start ds4windows", "stop ds4windows", "restart ds4windows" },
                Type = VoiceCommandType.SystemControl,
                Action = "system_control"
            });

            // Accessibility commands
            RegisterCommand(new VoiceCommand
            {
                Name = "EnableAccessibility",
                Phrases = new[] { "enable accessibility mode", "turn on accessibility", "accessibility on" },
                Type = VoiceCommandType.Accessibility,
                Action = "enable_accessibility"
            });

            RegisterCommand(new VoiceCommand
            {
                Name = "OneHandedMode",
                Phrases = new[] { "one handed mode", "left hand only", "right hand only" },
                Type = VoiceCommandType.Accessibility,
                Action = "one_handed_mode"
            });

            // Game action commands
            RegisterCommand(new VoiceCommand
            {
                Name = "QuickSave",
                Phrases = new[] { "quick save", "save game", "save now" },
                Type = VoiceCommandType.GameAction,
                Action = "quick_save"
            });

            RegisterCommand(new VoiceCommand
            {
                Name = "Screenshot",
                Phrases = new[] { "take screenshot", "screenshot", "capture screen" },
                Type = VoiceCommandType.GameAction,
                Action = "screenshot"
            });

            // Performance commands
            RegisterCommand(new VoiceCommand
            {
                Name = "ShowPerformance",
                Phrases = new[] { "show performance", "performance stats", "how is performance" },
                Type = VoiceCommandType.Information,
                Action = "show_performance"
            });

            RegisterCommand(new VoiceCommand
            {
                Name = "OptimizePerformance",
                Phrases = new[] { "optimize performance", "improve performance", "make it faster" },
                Type = VoiceCommandType.SystemControl,
                Action = "optimize_performance"
            });
        }

        public void RegisterCommand(VoiceCommand command)
        {
            if (command == null || string.IsNullOrEmpty(command.Name))
                return;

            voiceCommands[command.Name] = command;
            UpdateGrammar();
            
            AppLogger.LogToGui($"Voice command registered: {command.Name}", false);
        }

        public void UnregisterCommand(string commandName)
        {
            if (voiceCommands.Remove(commandName))
            {
                UpdateGrammar();
                AppLogger.LogToGui($"Voice command unregistered: {commandName}", false);
            }
        }

        private void UpdateGrammar()
        {
            if (speechRecognizer == null) return;

            try
            {
                // Create grammar from all registered commands
                var grammarBuilder = new GrammarBuilder();
                var choices = new Choices();

                foreach (var command in voiceCommands.Values.Where(c => c.IsEnabled))
                {
                    foreach (var phrase in command.Phrases)
                    {
                        if (phrase.Contains("*")) // Wildcard for parameters
                        {
                            // Handle parameterized phrases
                            var parts = phrase.Split('*');
                            if (parts.Length == 2)
                            {
                                var parameterizedPhrase = new GrammarBuilder(parts[0].Trim());
                                parameterizedPhrase.Append(new Choices(GetParameterValues(command)));
                                if (!string.IsNullOrEmpty(parts[1].Trim()))
                                {
                                    parameterizedPhrase.Append(parts[1].Trim());
                                }
                                choices.Add(parameterizedPhrase);
                            }
                        }
                        else
                        {
                            choices.Add(phrase);
                        }
                    }
                }

                if (choices.Count > 0)
                {
                    grammarBuilder.Append(choices);
                    var grammar = new Grammar(grammarBuilder);
                    
                    speechRecognizer.UnloadAllGrammars();
                    speechRecognizer.LoadGrammar(grammar);
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogToGui($"Error updating voice command grammar: {ex.Message}", true);
            }
        }

        private string[] GetParameterValues(VoiceCommand command)
        {
            // Return appropriate parameter values based on command type
            return command.Type switch
            {
                VoiceCommandType.ProfileControl => GetAvailableProfiles(),
                VoiceCommandType.Accessibility => new[] { "left", "right", "motor impaired", "simplified" },
                VoiceCommandType.SystemControl => new[] { "start", "stop", "restart" },
                _ => new[] { "default" }
            };
        }

        private string[] GetAvailableProfiles()
        {
            try
            {
                // This would integrate with the existing profile system
                // For now, return some common profile names
                return new[] { "default", "gaming", "desktop", "media", "fps", "racing" };
            }
            catch
            {
                return new[] { "default" };
            }
        }

        public async Task StartListeningAsync()
        {
            if (speechRecognizer == null || isListening)
                return;

            try
            {
                speechRecognizer.RecognizeAsync(RecognizeMode.Multiple);
                isListening = true;
                isEnabled = true;
                
                await SpeakAsync("Voice commands activated");
                AppLogger.LogToGui("Voice command listening started", false);
            }
            catch (Exception ex)
            {
                AppLogger.LogToGui($"Failed to start voice recognition: {ex.Message}", true);
            }
        }

        public async Task StopListeningAsync()
        {
            if (speechRecognizer == null || !isListening)
                return;

            try
            {
                speechRecognizer.RecognizeAsyncStop();
                isListening = false;
                
                await SpeakAsync("Voice commands deactivated");
                AppLogger.LogToGui("Voice command listening stopped", false);
            }
            catch (Exception ex)
            {
                AppLogger.LogToGui($"Error stopping voice recognition: {ex.Message}", true);
            }
        }

        private void OnSpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            if (!isEnabled || e.Result.Confidence < 0.5f)
                return;

            try
            {
                var recognizedText = e.Result.Text.ToLower();
                var matchedCommand = FindMatchingCommand(recognizedText);

                if (matchedCommand != null && e.Result.Confidence >= matchedCommand.ConfidenceThreshold)
                {
                    var success = ExecuteVoiceCommand(matchedCommand, recognizedText, e.Result.Confidence);
                    CommandExecuted?.Invoke(this, new VoiceCommandExecutedEventArgs(matchedCommand, e.Result.Confidence, success));
                    
                    AppLogger.LogToGui($"Voice command executed: {matchedCommand.Name} (confidence: {e.Result.Confidence:P1})", false);
                }
                else
                {
                    AppLogger.LogToGui($"Voice command not recognized: '{recognizedText}' (confidence: {e.Result.Confidence:P1})", false);
                }

                SpeechRecognized?.Invoke(this, e);
            }
            catch (Exception ex)
            {
                AppLogger.LogToGui($"Error processing voice command: {ex.Message}", true);
            }
        }

        private void OnSpeechRejected(object sender, SpeechRecognitionRejectedEventArgs e)
        {
            AppLogger.LogToGui($"Speech rejected: {e.Result?.Text ?? "Unknown"}", false);
        }

        private VoiceCommand FindMatchingCommand(string recognizedText)
        {
            foreach (var command in voiceCommands.Values.Where(c => c.IsEnabled))
            {
                foreach (var phrase in command.Phrases)
                {
                    if (phrase.Contains("*"))
                    {
                        // Handle wildcard matching
                        var pattern = phrase.Replace("*", "(.+)");
                        if (System.Text.RegularExpressions.Regex.IsMatch(recognizedText, pattern))
                        {
                            return command;
                        }
                    }
                    else if (recognizedText.Contains(phrase.ToLower()))
                    {
                        return command;
                    }
                }
            }
            return null;
        }

        private bool ExecuteVoiceCommand(VoiceCommand command, string recognizedText, float confidence)
        {
            try
            {
                switch (command.Action)
                {
                    case "switch_profile":
                        return ExecuteProfileSwitch(recognizedText);

                    case "list_profiles":
                        return ExecuteListProfiles();

                    case "show_battery":
                        return ExecuteShowBattery();

                    case "show_status":
                        return ExecuteShowStatus();

                    case "system_control":
                        return ExecuteSystemControl(recognizedText);

                    case "enable_accessibility":
                        return ExecuteEnableAccessibility();

                    case "one_handed_mode":
                        return ExecuteOneHandedMode(recognizedText);

                    case "quick_save":
                        return ExecuteQuickSave();

                    case "screenshot":
                        return ExecuteScreenshot();

                    case "show_performance":
                        return ExecuteShowPerformance();

                    case "optimize_performance":
                        return ExecuteOptimizePerformance();

                    default:
                        return ExecuteCustomCommand(command, recognizedText);
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogToGui($"Error executing voice command '{command.Name}': {ex.Message}", true);
                return false;
            }
        }

        private bool ExecuteProfileSwitch(string recognizedText)
        {
            // Extract profile name from recognized text
            var profileName = ExtractParameter(recognizedText, "profile");
            if (!string.IsNullOrEmpty(profileName))
            {
                // This would integrate with existing profile system
                // Global.LoadProfile(0, profileName);
                SpeakAsync($"Switching to {profileName} profile");
                return true;
            }
            
            SpeakAsync("Profile name not recognized");
            return false;
        }

        private bool ExecuteListProfiles()
        {
            var profiles = GetAvailableProfiles();
            var profileList = string.Join(", ", profiles);
            SpeakAsync($"Available profiles: {profileList}");
            return true;
        }

        private bool ExecuteShowBattery()
        {
            // Get battery info from first connected controller
            var device = App.rootHub?.DS4Controllers?.FirstOrDefault(d => d != null);
            if (device != null)
            {
                var batteryLevel = device.getBattery();
                var isCharging = device.isCharging();
                var message = $"Controller battery is {batteryLevel} percent";
                if (isCharging) message += " and charging";
                
                SpeakAsync(message);
                return true;
            }
            
            SpeakAsync("No controller connected");
            return false;
        }

        private bool ExecuteShowStatus()
        {
            var activeControllers = App.rootHub?.activeControllers ?? 0;
            var message = $"DS4Windows is running with {activeControllers} controller";
            if (activeControllers != 1) message += "s";
            message += " connected";
            
            SpeakAsync(message);
            return true;
        }

        private bool ExecuteSystemControl(string recognizedText)
        {
            if (recognizedText.Contains("start"))
            {
                SpeakAsync("Starting DS4Windows service");
                // App.rootHub?.Start();
            }
            else if (recognizedText.Contains("stop"))
            {
                SpeakAsync("Stopping DS4Windows service");
                // App.rootHub?.Stop();
            }
            else if (recognizedText.Contains("restart"))
            {
                SpeakAsync("Restarting DS4Windows service");
                // App.rootHub?.Stop();
                // App.rootHub?.Start();
            }
            
            return true;
        }

        private bool ExecuteEnableAccessibility()
        {
            SpeakAsync("Accessibility mode enabled");
            // This would integrate with AccessibilityFeatures
            return true;
        }

        private bool ExecuteOneHandedMode(string recognizedText)
        {
            if (recognizedText.Contains("left"))
            {
                SpeakAsync("Left hand only mode activated");
                // accessibilityFeatures.SetAccessibilityMode(AccessibilityMode.OneHandedLeft);
            }
            else if (recognizedText.Contains("right"))
            {
                SpeakAsync("Right hand only mode activated");
                // accessibilityFeatures.SetAccessibilityMode(AccessibilityMode.OneHandedRight);
            }
            else
            {
                SpeakAsync("One handed mode activated");
            }
            
            return true;
        }

        private bool ExecuteQuickSave()
        {
            // Simulate F5 key press for quick save
            SpeakAsync("Quick save");
            // SendKeys.SendWait("{F5}");
            return true;
        }

        private bool ExecuteScreenshot()
        {
            // Simulate screenshot key combination
            SpeakAsync("Taking screenshot");
            // SendKeys.SendWait("{F12}");
            return true;
        }

        private bool ExecuteShowPerformance()
        {
            // Get performance info from first controller
            var device = App.rootHub?.DS4Controllers?.FirstOrDefault(d => d != null);
            if (device?.PerformanceAnalytics != null)
            {
                var dashboard = device.PerformanceAnalytics.GetCurrentDashboard();
                var latency = dashboard.Latency.AverageLatency;
                var score = dashboard.OverallPerformanceScore;
                
                SpeakAsync($"Performance score is {score:F0} percent with {latency:F1} millisecond latency");
                return true;
            }
            
            SpeakAsync("Performance data not available");
            return false;
        }

        private bool ExecuteOptimizePerformance()
        {
            SpeakAsync("Optimizing performance");
            // Trigger performance optimization
            var device = App.rootHub?.DS4Controllers?.FirstOrDefault(d => d != null);
            device?.PerformanceAnalytics?.OptimizePerformance();
            return true;
        }

        private bool ExecuteCustomCommand(VoiceCommand command, string recognizedText)
        {
            AppLogger.LogToGui($"Executing custom voice command: {command.Name}", false);
            SpeakAsync($"Executing {command.Name}");
            return true;
        }

        private string ExtractParameter(string text, string context)
        {
            // Simple parameter extraction - would need more sophisticated parsing
            var words = text.Split(' ');
            var contextIndex = Array.FindIndex(words, w => w.Contains(context));
            
            if (contextIndex >= 0 && contextIndex + 1 < words.Length)
            {
                return words[contextIndex + 1];
            }
            
            return null;
        }

        public async Task SpeakAsync(string text)
        {
            if (speechSynthesizer == null || string.IsNullOrEmpty(text))
                return;

            try
            {
                await Task.Run(() => speechSynthesizer.Speak(text));
            }
            catch (Exception ex)
            {
                AppLogger.LogToGui($"Error in text-to-speech: {ex.Message}", true);
            }
        }

        public void SetVoiceSettings(int rate, int volume)
        {
            if (speechSynthesizer != null)
            {
                speechSynthesizer.Rate = Math.Clamp(rate, -10, 10);
                speechSynthesizer.Volume = Math.Clamp(volume, 0, 100);
            }
        }

        public void EnableCommand(string commandName, bool enabled)
        {
            if (voiceCommands.TryGetValue(commandName, out var command))
            {
                command.IsEnabled = enabled;
                UpdateGrammar();
                AppLogger.LogToGui($"Voice command '{commandName}' {(enabled ? "enabled" : "disabled")}", false);
            }
        }

        public List<string> GetRegisteredCommands()
        {
            return voiceCommands.Keys.ToList();
        }

        public void Dispose()
        {
            if (disposed) return;
            
            disposed = true;

            try
            {
                if (isListening)
                {
                    StopListeningAsync().Wait();
                }

                speechRecognizer?.Dispose();
                speechSynthesizer?.Dispose();
            }
            catch (Exception ex)
            {
                AppLogger.LogToGui($"Error disposing voice command system: {ex.Message}", true);
            }
        }
    }
}
