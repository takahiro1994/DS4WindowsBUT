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
    public enum BatteryHealth
    {
        Excellent,
        Good,
        Fair,
        Poor,
        VeryPoor,
        Unknown
    }

    public enum BatteryWarningLevel
    {
        Normal,
        Low,
        VeryLow,
        Critical
    }

    public class BatteryStats
    {
        public int CurrentLevel { get; set; }
        public bool IsCharging { get; set; }
        public BatteryHealth Health { get; set; }
        public TimeSpan EstimatedTimeRemaining { get; set; }
        public double AverageDischargeRate { get; set; } // % per hour
        public int ChargeDischargeCount { get; set; }
        public DateTime LastFullCharge { get; set; }
        public TimeSpan TotalUsageTime { get; set; }
        public BatteryWarningLevel WarningLevel { get; set; }
        public List<BatteryReading> RecentReadings { get; set; } = new List<BatteryReading>();
    }

    public class BatteryReading
    {
        public DateTime Timestamp { get; set; }
        public int Level { get; set; }
        public bool IsCharging { get; set; }
        public double Voltage { get; set; }
        public double Temperature { get; set; }
    }

    public class BatteryHealthChangedEventArgs : EventArgs
    {
        public BatteryHealth OldHealth { get; }
        public BatteryHealth NewHealth { get; }

        public BatteryHealthChangedEventArgs(BatteryHealth oldHealth, BatteryHealth newHealth)
        {
            OldHealth = oldHealth;
            NewHealth = newHealth;
        }
    }

    public class LowBatteryEventArgs : EventArgs
    {
        public BatteryWarningLevel WarningLevel { get; }
        public int BatteryLevel { get; }
        public TimeSpan EstimatedTimeRemaining { get; }

        public LowBatteryEventArgs(BatteryWarningLevel warningLevel, int batteryLevel, TimeSpan estimatedTime)
        {
            WarningLevel = warningLevel;
            BatteryLevel = batteryLevel;
            EstimatedTimeRemaining = estimatedTime;
        }
    }

    public class BatteryManager
    {
        private readonly DS4Device device;
        private readonly List<BatteryReading> batteryHistory;
        private BatteryStats currentStats;
        private DateTime lastUpdateTime;
        private bool wasCharging;
        private int lastBatteryLevel;
        private int chargeDischargeCount;
        private DateTime lastFullChargeTime;
        private BatteryWarningLevel lastWarningLevel;

        // Battery health thresholds
        private const int HEALTH_HISTORY_HOURS = 24;
        private const int LOW_BATTERY_THRESHOLD = 20;
        private const int VERY_LOW_BATTERY_THRESHOLD = 10;
        private const int CRITICAL_BATTERY_THRESHOLD = 5;
        private const double POOR_HEALTH_DISCHARGE_RATE = 25.0; // % per hour
        private const int POOR_HEALTH_CYCLE_COUNT = 500;

        public event EventHandler<BatteryHealthChangedEventArgs> BatteryHealthChanged;
        public event EventHandler<LowBatteryEventArgs> LowBatteryWarning;

        public BatteryStats CurrentStats => currentStats;

        public BatteryManager(DS4Device device)
        {
            this.device = device ?? throw new ArgumentNullException(nameof(device));
            this.batteryHistory = new List<BatteryReading>();
            this.currentStats = new BatteryStats();
            this.lastUpdateTime = DateTime.UtcNow;
            this.lastBatteryLevel = device.getBattery();
            this.wasCharging = device.isCharging();
            this.lastWarningLevel = BatteryWarningLevel.Normal;
            
            InitializeBatteryStats();
        }

        private void InitializeBatteryStats()
        {
            currentStats.CurrentLevel = device.getBattery();
            currentStats.IsCharging = device.isCharging();
            currentStats.Health = BatteryHealth.Unknown;
            currentStats.WarningLevel = BatteryWarningLevel.Normal;
            currentStats.LastFullCharge = DateTime.UtcNow.AddDays(-1); // Default to 1 day ago
            
            // Add initial reading
            var initialReading = new BatteryReading
            {
                Timestamp = DateTime.UtcNow,
                Level = currentStats.CurrentLevel,
                IsCharging = currentStats.IsCharging,
                Voltage = 0, // Would need hardware support to get actual voltage
                Temperature = 0 // Would need hardware support to get actual temperature
            };
            
            batteryHistory.Add(initialReading);
            currentStats.RecentReadings.Add(initialReading);
        }

        public void UpdateBatteryStats()
        {
            var now = DateTime.UtcNow;
            var currentLevel = device.getBattery();
            var isCharging = device.isCharging();
            
            // Create new reading
            var reading = new BatteryReading
            {
                Timestamp = now,
                Level = currentLevel,
                IsCharging = isCharging,
                Voltage = 0, // Would need hardware support
                Temperature = 0 // Would need hardware support
            };
            
            // Add to history
            batteryHistory.Add(reading);
            currentStats.RecentReadings.Add(reading);
            
            // Keep only recent readings
            var cutoff = now.AddHours(-HEALTH_HISTORY_HOURS);
            batteryHistory.RemoveAll(r => r.Timestamp < cutoff);
            currentStats.RecentReadings = currentStats.RecentReadings
                .Where(r => r.Timestamp >= cutoff)
                .ToList();

            // Detect charge/discharge cycles
            if (wasCharging != isCharging)
            {
                if (!wasCharging && isCharging)
                {
                    // Started charging
                    AppLogger.LogToGui($"Controller {device.Mac} started charging at {currentLevel}%", false);
                }
                else if (wasCharging && !isCharging)
                {
                    // Stopped charging
                    chargeDischargeCount++;
                    if (currentLevel >= 95)
                    {
                        lastFullChargeTime = now;
                        currentStats.LastFullCharge = now;
                        AppLogger.LogToGui($"Controller {device.Mac} fully charged", false);
                    }
                }
                wasCharging = isCharging;
            }

            // Update current stats
            currentStats.CurrentLevel = currentLevel;
            currentStats.IsCharging = isCharging;
            currentStats.ChargeDischargeCount = chargeDischargeCount;
            
            // Calculate discharge rate and estimated time remaining
            CalculateDischargeRate();
            CalculateEstimatedTimeRemaining();
            
            // Update battery health
            var oldHealth = currentStats.Health;
            currentStats.Health = CalculateBatteryHealth();
            
            if (oldHealth != currentStats.Health)
            {
                BatteryHealthChanged?.Invoke(this, new BatteryHealthChangedEventArgs(oldHealth, currentStats.Health));
            }
            
            // Check for low battery warnings
            CheckLowBatteryWarnings();
            
            lastUpdateTime = now;
            lastBatteryLevel = currentLevel;
        }

        private void CalculateDischargeRate()
        {
            if (batteryHistory.Count < 2) return;
            
            // Calculate average discharge rate over the last few hours
            var recentReadings = batteryHistory
                .Where(r => !r.IsCharging && r.Timestamp >= DateTime.UtcNow.AddHours(-4))
                .OrderBy(r => r.Timestamp)
                .ToList();
            
            if (recentReadings.Count < 2) return;
            
            var firstReading = recentReadings.First();
            var lastReading = recentReadings.Last();
            
            var timeDiff = (lastReading.Timestamp - firstReading.Timestamp).TotalHours;
            var levelDiff = firstReading.Level - lastReading.Level; // Positive for discharge
            
            if (timeDiff > 0 && levelDiff > 0)
            {
                currentStats.AverageDischargeRate = levelDiff / timeDiff;
            }
        }

        private void CalculateEstimatedTimeRemaining()
        {
            if (currentStats.IsCharging)
            {
                currentStats.EstimatedTimeRemaining = TimeSpan.Zero;
                return;
            }
            
            if (currentStats.AverageDischargeRate <= 0)
            {
                currentStats.EstimatedTimeRemaining = TimeSpan.MaxValue;
                return;
            }
            
            var hoursRemaining = currentStats.CurrentLevel / currentStats.AverageDischargeRate;
            currentStats.EstimatedTimeRemaining = TimeSpan.FromHours(Math.Max(0, hoursRemaining));
        }

        private BatteryHealth CalculateBatteryHealth()
        {
            if (batteryHistory.Count < 10) return BatteryHealth.Unknown;
            
            int healthScore = 100;
            
            // Deduct points for high discharge rate
            if (currentStats.AverageDischargeRate > POOR_HEALTH_DISCHARGE_RATE)
            {
                healthScore -= (int)((currentStats.AverageDischargeRate - POOR_HEALTH_DISCHARGE_RATE) * 2);
            }
            
            // Deduct points for high cycle count
            if (chargeDischargeCount > POOR_HEALTH_CYCLE_COUNT)
            {
                healthScore -= (chargeDischargeCount - POOR_HEALTH_CYCLE_COUNT) / 10;
            }
            
            // Deduct points for inconsistent readings (battery level jumps)
            var levelJumps = 0;
            for (int i = 1; i < batteryHistory.Count; i++)
            {
                var diff = Math.Abs(batteryHistory[i].Level - batteryHistory[i - 1].Level);
                if (diff > 10 && batteryHistory[i].IsCharging == batteryHistory[i - 1].IsCharging)
                {
                    levelJumps++;
                }
            }
            healthScore -= levelJumps * 5;
            
            // Determine health category
            if (healthScore >= 90) return BatteryHealth.Excellent;
            if (healthScore >= 75) return BatteryHealth.Good;
            if (healthScore >= 60) return BatteryHealth.Fair;
            if (healthScore >= 40) return BatteryHealth.Poor;
            return BatteryHealth.VeryPoor;
        }

        private void CheckLowBatteryWarnings()
        {
            if (currentStats.IsCharging) 
            {
                currentStats.WarningLevel = BatteryWarningLevel.Normal;
                return;
            }
            
            var newWarningLevel = BatteryWarningLevel.Normal;
            
            if (currentStats.CurrentLevel <= CRITICAL_BATTERY_THRESHOLD)
            {
                newWarningLevel = BatteryWarningLevel.Critical;
            }
            else if (currentStats.CurrentLevel <= VERY_LOW_BATTERY_THRESHOLD)
            {
                newWarningLevel = BatteryWarningLevel.VeryLow;
            }
            else if (currentStats.CurrentLevel <= LOW_BATTERY_THRESHOLD)
            {
                newWarningLevel = BatteryWarningLevel.Low;
            }
            
            if (newWarningLevel != lastWarningLevel && newWarningLevel != BatteryWarningLevel.Normal)
            {
                currentStats.WarningLevel = newWarningLevel;
                LowBatteryWarning?.Invoke(this, new LowBatteryEventArgs(
                    newWarningLevel, currentStats.CurrentLevel, currentStats.EstimatedTimeRemaining));
                lastWarningLevel = newWarningLevel;
            }
        }

        public void OptimizePowerUsage()
        {
            if (currentStats.WarningLevel == BatteryWarningLevel.Critical ||
                currentStats.WarningLevel == BatteryWarningLevel.VeryLow)
            {
                // Suggest power saving measures
                AppLogger.LogToGui($"Controller {device.Mac}: Consider reducing lightbar brightness and vibration to save power", false);
                
                // Auto-reduce lightbar brightness if enabled
                if (Global.PowerSavingMode)
                {
                    // This would need integration with lightbar control
                    AppLogger.LogToGui($"Controller {device.Mac}: Auto-reducing lightbar brightness for power saving", false);
                }
            }
        }

        public string GetBatteryHealthDescription()
        {
            return currentStats.Health switch
            {
                BatteryHealth.Excellent => "Battery is in excellent condition",
                BatteryHealth.Good => "Battery is in good condition",
                BatteryHealth.Fair => "Battery is showing signs of wear but still functional",
                BatteryHealth.Poor => "Battery is degraded and may need replacement soon",
                BatteryHealth.VeryPoor => "Battery is severely degraded and should be replaced",
                BatteryHealth.Unknown => "Battery health cannot be determined yet",
                _ => "Unknown battery status"
            };
        }

        public List<string> GetPowerSavingRecommendations()
        {
            var recommendations = new List<string>();
            
            if (currentStats.Health == BatteryHealth.Poor || currentStats.Health == BatteryHealth.VeryPoor)
            {
                recommendations.Add("Consider replacing the controller battery");
                recommendations.Add("Reduce lightbar brightness to extend battery life");
                recommendations.Add("Disable vibration when not needed");
            }
            
            if (currentStats.AverageDischargeRate > 20)
            {
                recommendations.Add("Battery is draining faster than normal - check for background processes");
                recommendations.Add("Consider using wired connection when possible");
            }
            
            if (currentStats.WarningLevel != BatteryWarningLevel.Normal)
            {
                recommendations.Add("Charge controller when convenient");
                recommendations.Add("Enable power saving mode in DS4Windows settings");
            }
            
            return recommendations;
        }
    }
}
