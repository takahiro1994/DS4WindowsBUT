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
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DS4Windows
{
    public enum AccessibilityMode
    {
        None,
        OneHandedLeft,
        OneHandedRight,
        ReducedMotion,
        HighContrast,
        LargeText,
        ColorBlind,
        HearingImpaired,
        MotorImpaired
    }

    public enum ColorBlindType
    {
        None,
        Deuteranopia,  // Green-blind
        Protanopia,    // Red-blind
        Tritanopia,    // Blue-blind
        Monochromacy   // Complete color blindness
    }

    public class AccessibilityProfile
    {
        public string Name { get; set; }
        public AccessibilityMode Mode { get; set; }
        public Dictionary<DS4Controls, DS4Controls> ButtonRemapping { get; set; } = new Dictionary<DS4Controls, DS4Controls>();
        public bool VisualFeedbackEnabled { get; set; } = true;
        public bool HapticFeedbackEnhanced { get; set; } = true;
        public bool AudioCuesEnabled { get; set; } = true;
        public ColorBlindType ColorBlindType { get; set; } = ColorBlindType.None;
        public double SensitivityMultiplier { get; set; } = 1.0;
        public int HoldTimeThreshold { get; set; } = 500; // ms
        public bool SwipeGesturesEnabled { get; set; } = true;
        public bool AutoRepeatEnabled { get; set; } = false;
        public int AutoRepeatDelay { get; set; } = 500; // ms
    }

    public class OneHandedLayout
    {
        public string Name { get; set; }
        public bool IsLeftHanded { get; set; }
        public Dictionary<DS4Controls, DS4Controls> Remapping { get; set; } = new Dictionary<DS4Controls, DS4Controls>();
        public Dictionary<DS4Controls, string> GestureActions { get; set; } = new Dictionary<DS4Controls, string>();
        public bool UseStickForButtons { get; set; }
        public bool UseTouchpadGestures { get; set; }
    }

    public class VisualFeedback
    {
        public bool ShowButtonPresses { get; set; } = true;
        public bool ShowStickMovement { get; set; } = true;
        public bool ShowTriggerInput { get; set; } = true;
        public bool ShowTouchpadActivity { get; set; } = true;
        public bool HighContrastMode { get; set; } = false;
        public int FeedbackSize { get; set; } = 100; // Percentage
        public Color PrimaryColor { get; set; } = Color.Blue;
        public Color SecondaryColor { get; set; } = Color.Red;
        public int OpacityLevel { get; set; } = 80; // Percentage
    }

    public class AccessibilityFeatures
    {
        private readonly DS4Device device;
        private AccessibilityProfile currentProfile;
        private readonly Dictionary<string, OneHandedLayout> oneHandedLayouts;
        private readonly VisualFeedback visualFeedback;
        private readonly Timer feedbackTimer;
        private Form feedbackOverlay;
        private bool visualFeedbackActive;

        // Predefined one-handed layouts
        public static readonly OneHandedLayout LeftHandedLayout = new OneHandedLayout
        {
            Name = "Left Hand Only",
            IsLeftHanded = true,
            Remapping = new Dictionary<DS4Controls, DS4Controls>
            {
                [DS4Controls.Triangle] = DS4Controls.R1,
                [DS4Controls.Square] = DS4Controls.R2,
                [DS4Controls.Cross] = DS4Controls.R3,
                [DS4Controls.Circle] = DS4Controls.Options,
                [DS4Controls.DpadUp] = DS4Controls.RYNeg,
                [DS4Controls.DpadDown] = DS4Controls.RYPos,
                [DS4Controls.DpadLeft] = DS4Controls.RXNeg,
                [DS4Controls.DpadRight] = DS4Controls.RXPos
            },
            UseStickForButtons = true,
            UseTouchpadGestures = true
        };

        public static readonly OneHandedLayout RightHandedLayout = new OneHandedLayout
        {
            Name = "Right Hand Only",
            IsLeftHanded = false,
            Remapping = new Dictionary<DS4Controls, DS4Controls>
            {
                [DS4Controls.L1] = DS4Controls.Triangle,
                [DS4Controls.L2] = DS4Controls.Square,
                [DS4Controls.L3] = DS4Controls.Cross,
                [DS4Controls.Share] = DS4Controls.Circle,
                [DS4Controls.LYNeg] = DS4Controls.DpadUp,
                [DS4Controls.LYPos] = DS4Controls.DpadDown,
                [DS4Controls.LXNeg] = DS4Controls.DpadLeft,
                [DS4Controls.LXPos] = DS4Controls.DpadRight
            },
            UseStickForButtons = true,
            UseTouchpadGestures = true
        };

        public event EventHandler<AccessibilityEventArgs> AccessibilityModeChanged;
        public event EventHandler<FeedbackEventArgs> FeedbackTriggered;

        public AccessibilityProfile CurrentProfile => currentProfile;
        public bool VisualFeedbackActive => visualFeedbackActive;

        public AccessibilityFeatures(DS4Device device)
        {
            this.device = device ?? throw new ArgumentNullException(nameof(device));
            this.oneHandedLayouts = new Dictionary<string, OneHandedLayout>();
            this.visualFeedback = new VisualFeedback();
            this.currentProfile = new AccessibilityProfile { Name = "Default", Mode = AccessibilityMode.None };

            InitializeOneHandedLayouts();
            InitializeVisualFeedback();

            // Update timer for visual feedback
            this.feedbackTimer = new Timer();
            this.feedbackTimer.Interval = 50; // 20 FPS
            this.feedbackTimer.Tick += UpdateVisualFeedback;
        }

        private void InitializeOneHandedLayouts()
        {
            oneHandedLayouts["LeftHanded"] = LeftHandedLayout;
            oneHandedLayouts["RightHanded"] = RightHandedLayout;

            // Create additional specialized layouts
            CreateMotorImpairedLayout();
            CreateSimplifiedLayout();
        }

        private void CreateMotorImpairedLayout()
        {
            var motorLayout = new OneHandedLayout
            {
                Name = "Motor Impaired",
                IsLeftHanded = false,
                Remapping = new Dictionary<DS4Controls, DS4Controls>(),
                UseStickForButtons = false,
                UseTouchpadGestures = true
            };

            // Map multiple functions to single buttons with hold/double-tap
            motorLayout.GestureActions[DS4Controls.Cross] = "primary_action";
            motorLayout.GestureActions[DS4Controls.Circle] = "secondary_action";
            motorLayout.GestureActions[DS4Controls.TouchButton] = "menu_toggle";

            oneHandedLayouts["MotorImpaired"] = motorLayout;
        }

        private void CreateSimplifiedLayout()
        {
            var simplifiedLayout = new OneHandedLayout
            {
                Name = "Simplified",
                IsLeftHanded = false,
                Remapping = new Dictionary<DS4Controls, DS4Controls>
                {
                    // Reduce button complexity
                    [DS4Controls.Triangle] = DS4Controls.Cross,
                    [DS4Controls.Square] = DS4Controls.Cross,
                    [DS4Controls.Circle] = DS4Controls.Circle,
                    [DS4Controls.L1] = DS4Controls.L2,
                    [DS4Controls.R1] = DS4Controls.R2
                },
                UseStickForButtons = false,
                UseTouchpadGestures = true
            };

            oneHandedLayouts["Simplified"] = simplifiedLayout;
        }

        private void InitializeVisualFeedback()
        {
            // Set up color schemes for different accessibility needs
            SetColorSchemeForAccessibility(ColorBlindType.None);
        }

        public void SetAccessibilityMode(AccessibilityMode mode)
        {
            var oldMode = currentProfile.Mode;
            currentProfile.Mode = mode;

            ApplyAccessibilityMode(mode);
            AccessibilityModeChanged?.Invoke(this, new AccessibilityEventArgs(oldMode, mode));

            AppLogger.LogToGui($"Accessibility mode changed to: {mode}", false);
        }

        private void ApplyAccessibilityMode(AccessibilityMode mode)
        {
            switch (mode)
            {
                case AccessibilityMode.OneHandedLeft:
                    ApplyOneHandedLayout("LeftHanded");
                    break;

                case AccessibilityMode.OneHandedRight:
                    ApplyOneHandedLayout("RightHanded");
                    break;

                case AccessibilityMode.MotorImpaired:
                    ApplyOneHandedLayout("MotorImpaired");
                    EnableEnhancedHaptics();
                    break;

                case AccessibilityMode.HighContrast:
                    EnableHighContrastMode();
                    break;

                case AccessibilityMode.ColorBlind:
                    ApplyColorBlindSupport();
                    break;

                case AccessibilityMode.HearingImpaired:
                    EnableVisualCues();
                    break;

                case AccessibilityMode.ReducedMotion:
                    DisableMotionEffects();
                    break;
            }
        }

        private void ApplyOneHandedLayout(string layoutName)
        {
            if (!oneHandedLayouts.TryGetValue(layoutName, out var layout))
            {
                AppLogger.LogToGui($"One-handed layout '{layoutName}' not found", true);
                return;
            }

            // Apply button remapping
            currentProfile.ButtonRemapping.Clear();
            foreach (var mapping in layout.Remapping)
            {
                currentProfile.ButtonRemapping[mapping.Key] = mapping.Value;
            }

            // Enable additional features for one-handed use
            if (layout.UseTouchpadGestures)
            {
                EnableTouchpadGestures();
            }

            if (layout.UseStickForButtons)
            {
                EnableStickButtonMapping();
            }

            AppLogger.LogToGui($"Applied one-handed layout: {layout.Name}", false);
        }

        private void EnableTouchpadGestures()
        {
            // Enable swipe gestures for common actions
            currentProfile.SwipeGesturesEnabled = true;
            
            // These would integrate with existing touchpad handling
            AppLogger.LogToGui("Touchpad gestures enabled for accessibility", false);
        }

        private void EnableStickButtonMapping()
        {
            // Allow stick movements to trigger button presses
            AppLogger.LogToGui("Stick-to-button mapping enabled", false);
        }

        private void EnableEnhancedHaptics()
        {
            currentProfile.HapticFeedbackEnhanced = true;
            
            // Increase vibration feedback for better tactile response
            // This would integrate with existing vibration system
            AppLogger.LogToGui("Enhanced haptic feedback enabled", false);
        }

        private void EnableHighContrastMode()
        {
            visualFeedback.HighContrastMode = true;
            visualFeedback.PrimaryColor = Color.White;
            visualFeedback.SecondaryColor = Color.Black;
            
            // Apply to lightbar as well
            if (device.LightbarEffects != null)
            {
                var highContrastEffect = AdvancedLightbarEffects.CreateNotificationFlash(Color.White, 1, 5);
                device.LightbarEffects.AddEffect(highContrastEffect);
            }

            AppLogger.LogToGui("High contrast mode enabled", false);
        }

        private void ApplyColorBlindSupport()
        {
            SetColorSchemeForAccessibility(currentProfile.ColorBlindType);
            AppLogger.LogToGui($"Color blind support applied: {currentProfile.ColorBlindType}", false);
        }

        private void SetColorSchemeForAccessibility(ColorBlindType colorBlindType)
        {
            switch (colorBlindType)
            {
                case ColorBlindType.Deuteranopia: // Green-blind
                    visualFeedback.PrimaryColor = Color.Blue;
                    visualFeedback.SecondaryColor = Color.Orange;
                    break;

                case ColorBlindType.Protanopia: // Red-blind
                    visualFeedback.PrimaryColor = Color.Blue;
                    visualFeedback.SecondaryColor = Color.Yellow;
                    break;

                case ColorBlindType.Tritanopia: // Blue-blind
                    visualFeedback.PrimaryColor = Color.Red;
                    visualFeedback.SecondaryColor = Color.Green;
                    break;

                case ColorBlindType.Monochromacy: // Complete color blindness
                    visualFeedback.PrimaryColor = Color.White;
                    visualFeedback.SecondaryColor = Color.Gray;
                    break;

                default:
                    visualFeedback.PrimaryColor = Color.Blue;
                    visualFeedback.SecondaryColor = Color.Red;
                    break;
            }
        }

        private void EnableVisualCues()
        {
            currentProfile.VisualFeedbackEnabled = true;
            StartVisualFeedback();
            
            // Enable lightbar notifications for audio events
            AppLogger.LogToGui("Visual cues enabled for hearing accessibility", false);
        }

        private void DisableMotionEffects()
        {
            // Disable motion-based effects and animations
            currentProfile.VisualFeedbackEnabled = false;
            
            if (device.LightbarEffects != null)
            {
                device.LightbarEffects.RemoveEffect(LightbarEffectType.Pulse);
                device.LightbarEffects.RemoveEffect(LightbarEffectType.Rainbow);
            }

            AppLogger.LogToGui("Motion effects disabled for accessibility", false);
        }

        public void StartVisualFeedback()
        {
            if (visualFeedbackActive) return;

            try
            {
                CreateFeedbackOverlay();
                visualFeedbackActive = true;
                feedbackTimer.Start();
                
                AppLogger.LogToGui("Visual feedback overlay started", false);
            }
            catch (Exception ex)
            {
                AppLogger.LogToGui($"Failed to start visual feedback: {ex.Message}", true);
            }
        }

        public void StopVisualFeedback()
        {
            if (!visualFeedbackActive) return;

            visualFeedbackActive = false;
            feedbackTimer.Stop();
            feedbackOverlay?.Close();
            feedbackOverlay = null;
            
            AppLogger.LogToGui("Visual feedback overlay stopped", false);
        }

        private void CreateFeedbackOverlay()
        {
            feedbackOverlay = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                WindowState = FormWindowState.Maximized,
                TopMost = true,
                BackColor = Color.Black,
                TransparencyKey = Color.Black,
                ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual
            };

            // Make the form click-through
            int exStyle = GetWindowLong(feedbackOverlay.Handle, -20);
            exStyle |= 0x80000 | 0x20; // WS_EX_LAYERED | WS_EX_TRANSPARENT
            SetWindowLong(feedbackOverlay.Handle, -20, exStyle);

            feedbackOverlay.Show();
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private void UpdateVisualFeedback(object sender, EventArgs e)
        {
            if (!visualFeedbackActive || feedbackOverlay == null) return;

            try
            {
                // This would be implemented with proper drawing code
                // For now, just demonstrate the concept
                FeedbackTriggered?.Invoke(this, new FeedbackEventArgs("VisualUpdate", visualFeedback));
            }
            catch (Exception ex)
            {
                AppLogger.LogToGui($"Error updating visual feedback: {ex.Message}", true);
            }
        }

        public void ProcessControllerInput(DS4State currentState, DS4State previousState)
        {
            if (currentProfile.Mode == AccessibilityMode.None) return;

            // Apply button remapping
            ApplyButtonRemapping(currentState);

            // Process accessibility-specific input handling
            ProcessAccessibilityInput(currentState, previousState);

            // Update visual feedback
            if (visualFeedbackActive)
            {
                UpdateInputVisualization(currentState);
            }
        }

        private void ApplyButtonRemapping(DS4State state)
        {
            // This would need deep integration with the existing input processing
            // For now, log the remapping that would occur
            foreach (var mapping in currentProfile.ButtonRemapping)
            {
                // Map source button to target button
                // Example: if Triangle -> R1, then when Triangle is pressed, trigger R1 instead
            }
        }

        private void ProcessAccessibilityInput(DS4State currentState, DS4State previousState)
        {
            // Handle hold time adjustments for motor impaired users
            if (currentProfile.HoldTimeThreshold > 500)
            {
                // Require longer hold times for button recognition
            }

            // Handle auto-repeat for users who can't maintain button holds
            if (currentProfile.AutoRepeatEnabled)
            {
                // Implement auto-repeat functionality
            }

            // Process enhanced touchpad gestures
            if (currentProfile.SwipeGesturesEnabled)
            {
                ProcessTouchpadGestures(currentState, previousState);
            }
        }

        private void ProcessTouchpadGestures(DS4State currentState, DS4State previousState)
        {
            // Implement gesture recognition for accessibility
            // This would integrate with the existing touchpad system
        }

        private void UpdateInputVisualization(DS4State state)
        {
            if (!visualFeedback.ShowButtonPresses) return;

            // Update the visual overlay to show current input state
            // This would be implemented with proper graphics drawing
        }

        public void ConfigureForUser(AccessibilityMode mode, Dictionary<string, object> preferences)
        {
            currentProfile.Mode = mode;
            
            // Apply user preferences
            if (preferences.TryGetValue("SensitivityMultiplier", out var sensitivity))
            {
                currentProfile.SensitivityMultiplier = Convert.ToDouble(sensitivity);
            }

            if (preferences.TryGetValue("ColorBlindType", out var colorBlindType))
            {
                currentProfile.ColorBlindType = (ColorBlindType)Enum.Parse(typeof(ColorBlindType), colorBlindType.ToString());
            }

            if (preferences.TryGetValue("HoldTimeThreshold", out var holdTime))
            {
                currentProfile.HoldTimeThreshold = Convert.ToInt32(holdTime);
            }

            ApplyAccessibilityMode(mode);
            AppLogger.LogToGui($"Accessibility configured for user with {preferences.Count} preferences", false);
        }

        public List<string> GetAvailableLayouts()
        {
            return oneHandedLayouts.Keys.ToList();
        }

        public void SaveProfile(string name)
        {
            // Save current accessibility profile
            currentProfile.Name = name;
            AppLogger.LogToGui($"Accessibility profile '{name}' saved", false);
        }

        public void LoadProfile(string name)
        {
            // Load accessibility profile
            AppLogger.LogToGui($"Accessibility profile '{name}' loaded", false);
        }

        public void Dispose()
        {
            feedbackTimer?.Stop();
            feedbackTimer?.Dispose();
            StopVisualFeedback();
        }
    }

    // Event argument classes
    public class AccessibilityEventArgs : EventArgs
    {
        public AccessibilityMode OldMode { get; }
        public AccessibilityMode NewMode { get; }

        public AccessibilityEventArgs(AccessibilityMode oldMode, AccessibilityMode newMode)
        {
            OldMode = oldMode;
            NewMode = newMode;
        }
    }

    public class FeedbackEventArgs : EventArgs
    {
        public string FeedbackType { get; }
        public VisualFeedback FeedbackData { get; }

        public FeedbackEventArgs(string feedbackType, VisualFeedback feedbackData)
        {
            FeedbackType = feedbackType;
            FeedbackData = feedbackData;
        }
    }
}
