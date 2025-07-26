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

namespace DS4Windows
{
    public enum SetupStep
    {
        Welcome,
        BasicFeatures,
        AdvancedFeatures,
        AccessibilitySetup,
        PerformanceSettings,
        GameDetection,
        VoiceCommands,
        Completion
    }

    public class SetupWizardConfiguration
    {
        // Basic Features
        public bool EnablePerformanceMonitoring { get; set; } = true;
        public bool EnableSmartGameDetection { get; set; } = true;
        public bool EnableAdvancedLightbar { get; set; } = true;
        public bool EnablePowerManagement { get; set; } = true;

        // Advanced Features
        public bool EnableMotionGestures { get; set; } = false;
        public bool EnableMacroSystem { get; set; } = false;
        public bool EnableProfileScripting { get; set; } = false;
        public bool EnableAPIServer { get; set; } = false;

        // Accessibility
        public AccessibilityMode AccessibilityMode { get; set; } = AccessibilityMode.None;
        public ColorBlindType ColorBlindType { get; set; } = ColorBlindType.None;
        public bool EnableVisualFeedback { get; set; } = false;
        public bool EnableVoiceCommands { get; set; } = false;

        // Performance
        public PowerMode DefaultPowerMode { get; set; } = PowerMode.Balanced;
        public bool EnableAdvancedDebugging { get; set; } = false;
        public DebugLevel DebuggingLevel { get; set; } = DebugLevel.Warning;

        // API & Integration
        public int APIServerPort { get; set; } = 8080;
        public bool StartAPIAutomatically { get; set; } = false;
    }

    public class SetupRecommendation
    {
        public string FeatureName { get; set; }
        public bool Recommended { get; set; }
        public string Reason { get; set; }
        public string Description { get; set; }
        public List<string> Pros { get; set; } = new List<string>();
        public List<string> Cons { get; set; } = new List<string>();
    }

    public class SetupWizard
    {
        private readonly List<SetupRecommendation> recommendations;
        private SetupWizardConfiguration configuration;
        private SetupStep currentStep;

        public event EventHandler<SetupStepChangedEventArgs> StepChanged;
        public event EventHandler<SetupCompletedEventArgs> SetupCompleted;

        public SetupStep CurrentStep => currentStep;
        public SetupWizardConfiguration Configuration => configuration;
        public IReadOnlyList<SetupRecommendation> Recommendations => recommendations.AsReadOnly();

        public SetupWizard()
        {
            recommendations = new List<SetupRecommendation>();
            configuration = new SetupWizardConfiguration();
            currentStep = SetupStep.Welcome;

            GenerateRecommendations();
        }

        private void GenerateRecommendations()
        {
            // Analyze system and usage patterns to generate recommendations
            AnalyzeSystemCapabilities();
            AnalyzeUsagePatterns();
            AnalyzeAccessibilityNeeds();
        }

        private void AnalyzeSystemCapabilities()
        {
            // Performance monitoring - recommended for all users
            recommendations.Add(new SetupRecommendation
            {
                FeatureName = "Performance Monitoring",
                Recommended = true,
                Reason = "Helps optimize controller performance and identify issues",
                Description = "Real-time monitoring of controller latency, battery health, and system performance",
                Pros = new List<string>
                {
                    "Identifies performance issues early",
                    "Optimizes battery life",
                    "Provides detailed diagnostics",
                    "Low system impact"
                },
                Cons = new List<string>
                {
                    "Slight memory usage increase"
                }
            });

            // Game detection - recommended for gamers
            recommendations.Add(new SetupRecommendation
            {
                FeatureName = "Smart Game Detection",
                Recommended = true,
                Reason = "Automatically optimizes settings for different games",
                Description = "Detects running games and applies game-specific controller profiles and optimizations",
                Pros = new List<string>
                {
                    "Automatic profile switching",
                    "Game-specific optimizations",
                    "Supports Steam, Epic, GOG, and standalone games",
                    "Tracks gaming statistics"
                },
                Cons = new List<string>
                {
                    "May slow down during game scanning",
                    "Requires access to process information"
                }
            });

            // Advanced lightbar - recommended for DS4 users
            recommendations.Add(new SetupRecommendation
            {
                FeatureName = "Advanced Lightbar Effects",
                Recommended = true,
                Reason = "Enhanced visual feedback and customization options",
                Description = "Advanced lightbar effects including notifications, battery indicators, and game-specific themes",
                Pros = new List<string>
                {
                    "Rich visual feedback",
                    "Battery level indication",
                    "Game-specific themes",
                    "Notification system"
                },
                Cons = new List<string>
                {
                    "Slightly increases battery usage",
                    "May be distracting in dark environments"
                }
            });

            // Motion gestures - recommended for advanced users
            var motionRecommended = Environment.ProcessorCount >= 4; // Multi-core systems handle better
            recommendations.Add(new SetupRecommendation
            {
                FeatureName = "Motion Gestures",
                Recommended = motionRecommended,
                Reason = motionRecommended ? 
                    "Your system can handle motion processing well" : 
                    "May impact performance on single-core systems",
                Description = "Recognize controller movements as gestures (shake, twist, tilt) for additional input options",
                Pros = new List<string>
                {
                    "Additional input methods",
                    "Accessibility benefits",
                    "Customizable gesture actions",
                    "Works with existing games"
                },
                Cons = new List<string>
                {
                    "Requires learning curve",
                    "May trigger accidentally",
                    "Additional CPU usage"
                }
            });

            // API Server - recommended for power users and developers
            recommendations.Add(new SetupRecommendation
            {
                FeatureName = "API Server",
                Recommended = false,
                Reason = "Only needed for third-party integration or remote control",
                Description = "REST API server for third-party applications and remote control capabilities",
                Pros = new List<string>
                {
                    "Third-party app integration",
                    "Remote control capabilities",
                    "Automation possibilities",
                    "Real-time data access"
                },
                Cons = new List<string>
                {
                    "Security considerations",
                    "Additional network port usage",
                    "Not needed by most users"
                }
            });
        }

        private void AnalyzeUsagePatterns()
        {
            // This would analyze existing DS4Windows usage patterns
            // For now, provide general recommendations

            // Macro system - for users with complex input needs
            recommendations.Add(new SetupRecommendation
            {
                FeatureName = "Advanced Macro System",
                Recommended = false,
                Reason = "Only needed for complex automation and specialized gaming",
                Description = "Advanced macro system with conditional logic, variables, and complex sequences",
                Pros = new List<string>
                {
                    "Complex automation possibilities",
                    "Conditional macro execution",
                    "Time-saving for repetitive tasks",
                    "Gaming advantage in supported games"
                },
                Cons = new List<string>
                {
                    "Learning curve required",
                    "May be considered cheating in competitive games",
                    "Additional complexity"
                }
            });

            // Profile scripting - for advanced users only
            recommendations.Add(new SetupRecommendation
            {
                FeatureName = "Profile Scripting",
                Recommended = false,
                Reason = "Advanced feature for power users with programming knowledge",
                Description = "C# scripting engine for creating custom profile behaviors and advanced automations",
                Pros = new List<string>
                {
                    "Unlimited customization possibilities",
                    "Full access to DS4Windows API",
                    "Custom behaviors and automations",
                    "Community script sharing potential"
                },
                Cons = new List<string>
                {
                    "Requires programming knowledge",
                    "Potential security risks with untrusted scripts",
                    "High complexity",
                    "May impact performance"
                }
            });
        }

        private void AnalyzeAccessibilityNeeds()
        {
            // Accessibility features - ask user about needs
            recommendations.Add(new SetupRecommendation
            {
                FeatureName = "Accessibility Features",
                Recommended = false,
                Reason = "Enable if you have specific accessibility needs",
                Description = "Comprehensive accessibility support including one-handed layouts, visual feedback, and motor assistance",
                Pros = new List<string>
                {
                    "One-handed controller layouts",
                    "Color blind support",
                    "Visual feedback for hearing impaired",
                    "Motor impairment assistance",
                    "High contrast modes"
                },
                Cons = new List<string>
                {
                    "Additional screen overlays",
                    "Slight performance impact",
                    "May change familiar interface"
                }
            });

            // Voice commands - accessibility and convenience
            recommendations.Add(new SetupRecommendation
            {
                FeatureName = "Voice Commands",
                Recommended = false,
                Reason = "Useful for accessibility or hands-free control",
                Description = "Voice recognition system for hands-free DS4Windows control and game actions",
                Pros = new List<string>
                {
                    "Hands-free control",
                    "Accessibility benefits",
                    "Gaming convenience",
                    "Natural language commands"
                },
                Cons = new List<string>
                {
                    "Requires microphone access",
                    "May not work well in noisy environments",
                    "Privacy considerations",
                    "Additional system resources"
                }
            });
        }

        public void NextStep()
        {
            if (currentStep < SetupStep.Completion)
            {
                currentStep++;
                StepChanged?.Invoke(this, new SetupStepChangedEventArgs(currentStep));
            }
        }

        public void PreviousStep()
        {
            if (currentStep > SetupStep.Welcome)
            {
                currentStep--;
                StepChanged?.Invoke(this, new SetupStepChangedEventArgs(currentStep));
            }
        }

        public void GoToStep(SetupStep step)
        {
            currentStep = step;
            StepChanged?.Invoke(this, new SetupStepChangedEventArgs(currentStep));
        }

        public List<SetupRecommendation> GetRecommendationsForStep(SetupStep step)
        {
            return step switch
            {
                SetupStep.BasicFeatures => recommendations.Where(r => 
                    r.FeatureName.Contains("Performance") || 
                    r.FeatureName.Contains("Game Detection") || 
                    r.FeatureName.Contains("Lightbar")).ToList(),

                SetupStep.AdvancedFeatures => recommendations.Where(r => 
                    r.FeatureName.Contains("Motion") || 
                    r.FeatureName.Contains("Macro") || 
                    r.FeatureName.Contains("Scripting") || 
                    r.FeatureName.Contains("API")).ToList(),

                SetupStep.AccessibilitySetup => recommendations.Where(r => 
                    r.FeatureName.Contains("Accessibility") || 
                    r.FeatureName.Contains("Voice")).ToList(),

                _ => new List<SetupRecommendation>()
            };
        }

        public void ApplyRecommendations()
        {
            foreach (var recommendation in recommendations.Where(r => r.Recommended))
            {
                switch (recommendation.FeatureName)
                {
                    case "Performance Monitoring":
                        configuration.EnablePerformanceMonitoring = true;
                        break;
                    case "Smart Game Detection":
                        configuration.EnableSmartGameDetection = true;
                        break;
                    case "Advanced Lightbar Effects":
                        configuration.EnableAdvancedLightbar = true;
                        break;
                    case "Motion Gestures":
                        configuration.EnableMotionGestures = true;
                        break;
                    case "Advanced Macro System":
                        configuration.EnableMacroSystem = true;
                        break;
                    case "API Server":
                        configuration.EnableAPIServer = true;
                        break;
                    case "Accessibility Features":
                        configuration.AccessibilityMode = AccessibilityMode.ReducedMotion; // Safe default
                        break;
                    case "Voice Commands":
                        configuration.EnableVoiceCommands = true;
                        break;
                    case "Profile Scripting":
                        configuration.EnableProfileScripting = true;
                        break;
                }
            }
        }

        public EnhancedFeaturesConfiguration GenerateEnhancedConfiguration()
        {
            return new EnhancedFeaturesConfiguration
            {
                PerformanceAnalyticsEnabled = configuration.EnablePerformanceMonitoring,
                SmartGameDetectionEnabled = configuration.EnableSmartGameDetection,
                AdvancedLightbarEnabled = configuration.EnableAdvancedLightbar,
                MotionGesturesEnabled = configuration.EnableMotionGestures,
                MacroSystemEnabled = configuration.EnableMacroSystem,
                PowerManagementEnabled = configuration.EnablePowerManagement,
                VoiceCommandsEnabled = configuration.EnableVoiceCommands,
                AccessibilityFeaturesEnabled = configuration.AccessibilityMode != AccessibilityMode.None,
                APIServerEnabled = configuration.EnableAPIServer,
                AdvancedDebuggingEnabled = configuration.EnableAdvancedDebugging,
                ProfileScriptingEnabled = configuration.EnableProfileScripting,
                APIServerPort = configuration.APIServerPort,
                DefaultAccessibilityMode = configuration.AccessibilityMode,
                DefaultPowerMode = configuration.DefaultPowerMode,
                DebuggingLevel = configuration.DebuggingLevel
            };
        }

        public void CompleteSetup()
        {
            var enhancedConfig = GenerateEnhancedConfiguration();
            
            SetupCompleted?.Invoke(this, new SetupCompletedEventArgs(enhancedConfig));
            
            AppLogger.LogToGui("Enhanced DS4Windows setup completed!", false);
            
            LogSetupSummary();
        }

        private void LogSetupSummary()
        {
            var enabledFeatures = new List<string>();
            
            if (configuration.EnablePerformanceMonitoring) enabledFeatures.Add("Performance Monitoring");
            if (configuration.EnableSmartGameDetection) enabledFeatures.Add("Smart Game Detection");
            if (configuration.EnableAdvancedLightbar) enabledFeatures.Add("Advanced Lightbar");
            if (configuration.EnableMotionGestures) enabledFeatures.Add("Motion Gestures");
            if (configuration.EnableMacroSystem) enabledFeatures.Add("Macro System");
            if (configuration.EnableVoiceCommands) enabledFeatures.Add("Voice Commands");
            if (configuration.AccessibilityMode != AccessibilityMode.None) enabledFeatures.Add("Accessibility Features");
            if (configuration.EnableAPIServer) enabledFeatures.Add("API Server");
            if (configuration.EnableProfileScripting) enabledFeatures.Add("Profile Scripting");
            if (configuration.EnableAdvancedDebugging) enabledFeatures.Add("Advanced Debugging");

            AppLogger.LogToGui($"Enhanced features enabled: {string.Join(", ", enabledFeatures)}", false);
            AppLogger.LogToGui($"Power mode: {configuration.DefaultPowerMode}", false);
            
            if (configuration.AccessibilityMode != AccessibilityMode.None)
            {
                AppLogger.LogToGui($"Accessibility mode: {configuration.AccessibilityMode}", false);
            }
        }

        public string GetStepDescription(SetupStep step)
        {
            return step switch
            {
                SetupStep.Welcome => "Welcome to Enhanced DS4Windows! This wizard will help you configure the new advanced features.",
                SetupStep.BasicFeatures => "Configure basic enhanced features that improve your controller experience.",
                SetupStep.AdvancedFeatures => "Enable advanced features for power users and specialized use cases.",
                SetupStep.AccessibilitySetup => "Configure accessibility features to make DS4Windows work better for your needs.",
                SetupStep.PerformanceSettings => "Optimize performance and power management settings.",
                SetupStep.GameDetection => "Configure automatic game detection and profile switching.",
                SetupStep.VoiceCommands => "Set up voice commands for hands-free control.",
                SetupStep.Completion => "Review your configuration and complete the setup.",
                _ => "Unknown step"
            };
        }
    }

    // Event argument classes
    public class SetupStepChangedEventArgs : EventArgs
    {
        public SetupStep NewStep { get; }

        public SetupStepChangedEventArgs(SetupStep newStep)
        {
            NewStep = newStep;
        }
    }

    public class SetupCompletedEventArgs : EventArgs
    {
        public EnhancedFeaturesConfiguration Configuration { get; }

        public SetupCompletedEventArgs(EnhancedFeaturesConfiguration configuration)
        {
            Configuration = configuration;
        }
    }
}
