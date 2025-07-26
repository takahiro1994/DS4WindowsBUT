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
using System.Threading;
using System.Threading.Tasks;

namespace DS4Windows
{
    public enum PowerMode
    {
        Performance,
        Balanced,
        PowerSaver,
        Adaptive,
        Custom
    }

    public enum IdleAction
    {
        Nothing,
        DimLightbar,
        DisableLightbar,
        DisableVibration,
        Sleep,
        Disconnect
    }

    public class PowerProfile
    {
        public string Name { get; set; }
        public PowerMode Mode { get; set; }
        public int LightbarBrightness { get; set; } = 100; // 0-100%
        public int VibrationStrength { get; set; } = 100; // 0-100%
        public int PollingRate { get; set; } = 1000; // Hz
        public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromMinutes(5);
        public IdleAction IdleAction { get; set; } = IdleAction.DimLightbar;
        public bool AutoAdjustBrightness { get; set; } = false;
        public bool ReduceLatencyMode { get; set; } = false;
        public int LowBatteryThreshold { get; set; } = 20; // %
        public bool AggressivePowerSaving { get; set; } = false;
    }

    public class PowerManagement
    {
        private readonly DS4Device device;
        private readonly BatteryManager batteryManager;
        private PowerProfile currentProfile;
        private DateTime lastActivityTime;
        private Timer idleTimer;
        private bool isIdleMode;
        private int originalLightbarBrightness;
        private int originalVibrationStrength;
        private bool disposed;

        // Predefined power profiles
        public static readonly PowerProfile PerformanceProfile = new PowerProfile
        {
            Name = "Performance",
            Mode = PowerMode.Performance,
            LightbarBrightness = 100,
            VibrationStrength = 100,
            PollingRate = 1000,
            IdleTimeout = TimeSpan.FromMinutes(10),
            IdleAction = IdleAction.DimLightbar,
            ReduceLatencyMode = true
        };

        public static readonly PowerProfile BalancedProfile = new PowerProfile
        {
            Name = "Balanced",
            Mode = PowerMode.Balanced,
            LightbarBrightness = 70,
            VibrationStrength = 80,
            PollingRate = 500,
            IdleTimeout = TimeSpan.FromMinutes(5),
            IdleAction = IdleAction.DimLightbar,
            AutoAdjustBrightness = true
        };

        public static readonly PowerProfile PowerSaverProfile = new PowerProfile
        {
            Name = "Power Saver",
            Mode = PowerMode.PowerSaver,
            LightbarBrightness = 30,
            VibrationStrength = 50,
            PollingRate = 250,
            IdleTimeout = TimeSpan.FromMinutes(2),
            IdleAction = IdleAction.DisableLightbar,
            AggressivePowerSaving = true,
            LowBatteryThreshold = 30
        };

        public static readonly PowerProfile AdaptiveProfile = new PowerProfile
        {
            Name = "Adaptive",
            Mode = PowerMode.Adaptive,
            LightbarBrightness = 80,
            VibrationStrength = 90,
            PollingRate = 750,
            IdleTimeout = TimeSpan.FromMinutes(3),
            IdleAction = IdleAction.DimLightbar,
            AutoAdjustBrightness = true,
            LowBatteryThreshold = 25
        };

        public event EventHandler<PowerModeChangedEventArgs> PowerModeChanged;
        public event EventHandler<IdleModeEventArgs> IdleModeChanged;

        public PowerProfile CurrentProfile => currentProfile;
        public bool IsIdleMode => isIdleMode;
        public TimeSpan TimeSinceLastActivity => DateTime.UtcNow - lastActivityTime;

        public PowerManagement(DS4Device device, BatteryManager batteryManager)
        {
            this.device = device ?? throw new ArgumentNullException(nameof(device));
            this.batteryManager = batteryManager;
            this.currentProfile = BalancedProfile;
            this.lastActivityTime = DateTime.UtcNow;
            
            // Initialize idle timer
            this.idleTimer = new Timer(CheckIdleState, null, 
                TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

            // Subscribe to battery events
            if (batteryManager != null)
            {
                batteryManager.LowBatteryWarning += OnLowBatteryWarning;
                batteryManager.BatteryHealthChanged += OnBatteryHealthChanged;
            }

            ApplyPowerProfile(currentProfile);
        }

        public void SetPowerProfile(PowerProfile profile)
        {
            if (profile == null) return;

            var oldProfile = currentProfile;
            currentProfile = profile;
            
            ApplyPowerProfile(profile);
            PowerModeChanged?.Invoke(this, new PowerModeChangedEventArgs(oldProfile, profile));
            
            AppLogger.LogToGui($"Power profile changed to: {profile.Name}", false);
        }

        public void SetPowerMode(PowerMode mode)
        {
            var profile = mode switch
            {
                PowerMode.Performance => PerformanceProfile,
                PowerMode.Balanced => BalancedProfile,
                PowerMode.PowerSaver => PowerSaverProfile,
                PowerMode.Adaptive => AdaptiveProfile,
                _ => currentProfile
            };

            if (profile != currentProfile)
            {
                SetPowerProfile(profile);
            }
        }

        private void ApplyPowerProfile(PowerProfile profile)
        {
            try
            {
                // Store original settings if not in idle mode
                if (!isIdleMode)
                {
                    // Would need to integrate with existing lightbar and vibration systems
                    // originalLightbarBrightness = device.getLightbarBrightness();
                    // originalVibrationStrength = device.getVibrationStrength();
                }

                // Apply profile settings
                ApplyLightbarSettings(profile);
                ApplyVibrationSettings(profile);
                ApplyPollingRate(profile);
                
                // Update idle timer interval
                if (idleTimer != null)
                {
                    idleTimer.Change(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
                }

                AppLogger.LogToGui($"Applied power profile: {profile.Name}", false);
            }
            catch (Exception ex)
            {
                AppLogger.LogToGui($"Error applying power profile: {ex.Message}", true);
            }
        }

        private void ApplyLightbarSettings(PowerProfile profile)
        {
            // This would integrate with existing lightbar system
            // device.setLightbarBrightness(profile.LightbarBrightness);
            
            if (profile.AutoAdjustBrightness)
            {
                // Adjust brightness based on time of day or ambient light
                var brightness = CalculateAdaptiveBrightness(profile.LightbarBrightness);
                // device.setLightbarBrightness(brightness);
            }
        }

        private void ApplyVibrationSettings(PowerProfile profile)
        {
            // This would integrate with existing vibration system
            // device.setVibrationStrength(profile.VibrationStrength);
        }

        private void ApplyPollingRate(PowerProfile profile)
        {
            // This would integrate with device polling system
            // device.setPollingRate(profile.PollingRate);
        }

        private int CalculateAdaptiveBrightness(int baseBrightness)
        {
            // Adjust brightness based on time of day
            var now = DateTime.Now.TimeOfDay;
            double factor = 1.0;
            
            // Reduce brightness during night hours
            if (now >= TimeSpan.FromHours(22) || now <= TimeSpan.FromHours(6))
            {
                factor = 0.3; // 30% brightness at night
            }
            else if (now >= TimeSpan.FromHours(18) || now <= TimeSpan.FromHours(8))
            {
                factor = 0.6; // 60% brightness in evening/early morning
            }
            
            return (int)(baseBrightness * factor);
        }

        public void RegisterActivity()
        {
            lastActivityTime = DateTime.UtcNow;
            
            if (isIdleMode)
            {
                ExitIdleMode();
            }
        }

        private void CheckIdleState(object state)
        {
            if (disposed) return;

            var timeSinceActivity = DateTime.UtcNow - lastActivityTime;
            
            if (!isIdleMode && timeSinceActivity >= currentProfile.IdleTimeout)
            {
                EnterIdleMode();
            }
        }

        private void EnterIdleMode()
        {
            if (isIdleMode) return;
            
            isIdleMode = true;
            
            try
            {
                switch (currentProfile.IdleAction)
                {
                    case IdleAction.DimLightbar:
                        // Reduce lightbar brightness to 20%
                        ApplyLightbarSettings(new PowerProfile 
                        { 
                            LightbarBrightness = Math.Max(20, currentProfile.LightbarBrightness / 5) 
                        });
                        break;
                        
                    case IdleAction.DisableLightbar:
                        // Turn off lightbar completely
                        ApplyLightbarSettings(new PowerProfile { LightbarBrightness = 0 });
                        break;
                        
                    case IdleAction.DisableVibration:
                        // Disable vibration
                        ApplyVibrationSettings(new PowerProfile { VibrationStrength = 0 });
                        break;
                        
                    case IdleAction.Sleep:
                        // Put controller in low-power mode
                        EnterSleepMode();
                        break;
                        
                    case IdleAction.Disconnect:
                        // Disconnect controller
                        RequestDisconnect();
                        break;
                }
                
                IdleModeChanged?.Invoke(this, new IdleModeEventArgs(true));
                AppLogger.LogToGui($"Controller entered idle mode: {currentProfile.IdleAction}", false);
            }
            catch (Exception ex)
            {
                AppLogger.LogToGui($"Error entering idle mode: {ex.Message}", true);
            }
        }

        private void ExitIdleMode()
        {
            if (!isIdleMode) return;
            
            isIdleMode = false;
            
            try
            {
                // Restore original settings
                ApplyPowerProfile(currentProfile);
                
                IdleModeChanged?.Invoke(this, new IdleModeEventArgs(false));
                AppLogger.LogToGui("Controller exited idle mode", false);
            }
            catch (Exception ex)
            {
                AppLogger.LogToGui($"Error exiting idle mode: {ex.Message}", true);
            }
        }

        private void EnterSleepMode()
        {
            // Minimize all controller activity
            ApplyLightbarSettings(new PowerProfile { LightbarBrightness = 0 });
            ApplyVibrationSettings(new PowerProfile { VibrationStrength = 0 });
            ApplyPollingRate(new PowerProfile { PollingRate = 100 }); // Reduce to 100Hz
            
            AppLogger.LogToGui("Controller entered sleep mode", false);
        }

        private void RequestDisconnect()
        {
            // Request graceful disconnect
            AppLogger.LogToGui("Requesting controller disconnect due to idle timeout", false);
            // device.RequestDisconnect();
        }

        private void OnLowBatteryWarning(object sender, LowBatteryEventArgs e)
        {
            if (e.WarningLevel == BatteryWarningLevel.Critical)
            {
                // Automatically switch to power saver mode
                if (currentProfile.Mode != PowerMode.PowerSaver)
                {
                    AppLogger.LogToGui("Critical battery - switching to Power Saver mode", true);
                    SetPowerMode(PowerMode.PowerSaver);
                }
            }
            else if (e.WarningLevel == BatteryWarningLevel.VeryLow)
            {
                // Apply aggressive power saving
                var emergencyProfile = new PowerProfile
                {
                    Name = "Emergency",
                    Mode = PowerMode.Custom,
                    LightbarBrightness = 10,
                    VibrationStrength = 0,
                    PollingRate = 125,
                    IdleTimeout = TimeSpan.FromMinutes(1),
                    IdleAction = IdleAction.Sleep,
                    AggressivePowerSaving = true
                };
                
                SetPowerProfile(emergencyProfile);
            }
        }

        private void OnBatteryHealthChanged(object sender, BatteryHealthChangedEventArgs e)
        {
            if (e.NewHealth == BatteryHealth.Poor || e.NewHealth == BatteryHealth.VeryPoor)
            {
                // Recommend power saving measures for degraded batteries
                AppLogger.LogToGui("Poor battery health detected - consider using Power Saver mode", false);
            }
        }

        public void OptimizeForGame(string gameName)
        {
            // Game-specific power optimization
            var gameProfile = gameName.ToLower() switch
            {
                var name when name.Contains("competitive") || name.Contains("fps") => PerformanceProfile,
                var name when name.Contains("casual") || name.Contains("puzzle") => PowerSaverProfile,
                _ => currentProfile.Mode == PowerMode.Adaptive ? BalancedProfile : currentProfile
            };

            if (gameProfile != currentProfile)
            {
                AppLogger.LogToGui($"Optimizing power profile for {gameName}", false);
                SetPowerProfile(gameProfile);
            }
        }

        public PowerUsageStats GetPowerUsageStats()
        {
            return new PowerUsageStats
            {
                CurrentMode = currentProfile.Mode,
                BatteryLevel = device?.getBattery() ?? 0,
                EstimatedBatteryLife = batteryManager?.CurrentStats.EstimatedTimeRemaining ?? TimeSpan.Zero,
                PowerSavingActive = isIdleMode || currentProfile.AggressivePowerSaving,
                TimeSinceLastActivity = TimeSinceLastActivity,
                LightbarBrightness = currentProfile.LightbarBrightness,
                VibrationStrength = currentProfile.VibrationStrength,
                PollingRate = currentProfile.PollingRate
            };
        }

        public void Dispose()
        {
            if (disposed) return;
            
            disposed = true;
            idleTimer?.Dispose();
            
            if (batteryManager != null)
            {
                batteryManager.LowBatteryWarning -= OnLowBatteryWarning;
                batteryManager.BatteryHealthChanged -= OnBatteryHealthChanged;
            }
        }
    }

    public class PowerUsageStats
    {
        public PowerMode CurrentMode { get; set; }
        public int BatteryLevel { get; set; }
        public TimeSpan EstimatedBatteryLife { get; set; }
        public bool PowerSavingActive { get; set; }
        public TimeSpan TimeSinceLastActivity { get; set; }
        public int LightbarBrightness { get; set; }
        public int VibrationStrength { get; set; }
        public int PollingRate { get; set; }
    }

    public class PowerModeChangedEventArgs : EventArgs
    {
        public PowerProfile OldProfile { get; }
        public PowerProfile NewProfile { get; }

        public PowerModeChangedEventArgs(PowerProfile oldProfile, PowerProfile newProfile)
        {
            OldProfile = oldProfile;
            NewProfile = newProfile;
        }
    }

    public class IdleModeEventArgs : EventArgs
    {
        public bool IsIdle { get; }

        public IdleModeEventArgs(bool isIdle)
        {
            IsIdle = isIdle;
        }
    }
}
