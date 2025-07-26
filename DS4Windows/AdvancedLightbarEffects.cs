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
using System.Threading;
using System.Threading.Tasks;

namespace DS4Windows
{
    public enum LightbarEffectType
    {
        Solid,
        Flash,
        Pulse,
        Rainbow,
        Gradient,
        Notification,
        BatteryIndicator,
        HealthStatus,
        GameSpecific,
        Custom
    }

    public abstract class LightbarEffect
    {
        public string Name { get; set; }
        public LightbarEffectType Type { get; set; }
        public int Priority { get; set; } = 5; // 1-10, 10 being highest
        public TimeSpan Duration { get; set; } = TimeSpan.FromSeconds(5);
        public DateTime StartTime { get; set; }
        public bool IsActive { get; set; }
        public bool Loop { get; set; }

        public abstract DS4Color GetColor(double progress);
        
        public virtual bool ShouldEnd()
        {
            return !Loop && DateTime.UtcNow >= StartTime.Add(Duration);
        }

        public virtual void Start()
        {
            StartTime = DateTime.UtcNow;
            IsActive = true;
        }

        public virtual void Stop()
        {
            IsActive = false;
        }

        protected double GetProgress()
        {
            if (!IsActive) return 1.0;
            
            var elapsed = DateTime.UtcNow - StartTime;
            if (elapsed >= Duration)
            {
                return Loop ? (elapsed.TotalMilliseconds % Duration.TotalMilliseconds) / Duration.TotalMilliseconds : 1.0;
            }
            
            return elapsed.TotalMilliseconds / Duration.TotalMilliseconds;
        }
    }

    public class SolidColorEffect : LightbarEffect
    {
        public Color Color { get; set; }

        public SolidColorEffect(Color color, TimeSpan? duration = null)
        {
            Type = LightbarEffectType.Solid;
            Color = color;
            Duration = duration ?? TimeSpan.MaxValue;
            Loop = duration == null;
        }

        public override DS4Color GetColor(double progress)
        {
            return new DS4Color(Color);
        }
    }

    public class FlashEffect : LightbarEffect
    {
        public Color FlashColor { get; set; }
        public Color BaseColor { get; set; }
        public double FlashRate { get; set; } = 2.0; // Flashes per second

        public FlashEffect(Color flashColor, Color baseColor, int flashCount = 3, double flashRate = 2.0)
        {
            Type = LightbarEffectType.Flash;
            FlashColor = flashColor;
            BaseColor = baseColor;
            FlashRate = flashRate;
            Duration = TimeSpan.FromSeconds(flashCount / flashRate);
        }

        public override DS4Color GetColor(double progress)
        {
            var cycle = Math.Sin(progress * Duration.TotalSeconds * FlashRate * 2 * Math.PI);
            return cycle > 0 ? new DS4Color(FlashColor) : new DS4Color(BaseColor);
        }
    }

    public class PulseEffect : LightbarEffect
    {
        public Color Color { get; set; }
        public double MinIntensity { get; set; } = 0.2;
        public double MaxIntensity { get; set; } = 1.0;

        public PulseEffect(Color color, TimeSpan duration, double minIntensity = 0.2)
        {
            Type = LightbarEffectType.Pulse;
            Color = color;
            Duration = duration;
            MinIntensity = minIntensity;
            Loop = true;
        }

        public override DS4Color GetColor(double progress)
        {
            var intensity = MinIntensity + (MaxIntensity - MinIntensity) * (Math.Sin(progress * 2 * Math.PI) + 1) / 2;
            return new DS4Color(
                (byte)(Color.R * intensity),
                (byte)(Color.G * intensity),
                (byte)(Color.B * intensity)
            );
        }
    }

    public class RainbowEffect : LightbarEffect
    {
        public double Speed { get; set; } = 1.0;

        public RainbowEffect(TimeSpan duration, double speed = 1.0)
        {
            Type = LightbarEffectType.Rainbow;
            Duration = duration;
            Speed = speed;
            Loop = true;
        }

        public override DS4Color GetColor(double progress)
        {
            var hue = (progress * Speed * 360) % 360;
            var color = ColorFromHSV(hue, 1.0, 1.0);
            return new DS4Color(color);
        }

        private Color ColorFromHSV(double hue, double saturation, double value)
        {
            int hi = Convert.ToInt32(Math.Floor(hue / 60)) % 6;
            double f = hue / 60 - Math.Floor(hue / 60);

            value = value * 255;
            int v = Convert.ToInt32(value);
            int p = Convert.ToInt32(value * (1 - saturation));
            int q = Convert.ToInt32(value * (1 - f * saturation));
            int t = Convert.ToInt32(value * (1 - (1 - f) * saturation));

            if (hi == 0)
                return Color.FromArgb(255, v, t, p);
            else if (hi == 1)
                return Color.FromArgb(255, q, v, p);
            else if (hi == 2)
                return Color.FromArgb(255, p, v, t);
            else if (hi == 3)
                return Color.FromArgb(255, p, q, v);
            else if (hi == 4)
                return Color.FromArgb(255, t, p, v);
            else
                return Color.FromArgb(255, v, p, q);
        }
    }

    public class BatteryIndicatorEffect : LightbarEffect
    {
        public int BatteryLevel { get; set; }

        public BatteryIndicatorEffect(int batteryLevel)
        {
            Type = LightbarEffectType.BatteryIndicator;
            BatteryLevel = Math.Clamp(batteryLevel, 0, 100);
            Duration = TimeSpan.FromSeconds(3);
        }

        public override DS4Color GetColor(double progress)
        {
            Color color;
            
            if (BatteryLevel > 75)
                color = Color.Green;
            else if (BatteryLevel > 50)
                color = Color.Yellow;
            else if (BatteryLevel > 25)
                color = Color.Orange;
            else
                color = Color.Red;

            // Pulse effect based on battery level
            var intensity = 0.3 + 0.7 * (Math.Sin(progress * 6 * Math.PI) + 1) / 2;
            
            return new DS4Color(
                (byte)(color.R * intensity),
                (byte)(color.G * intensity),
                (byte)(color.B * intensity)
            );
        }
    }

    public class NotificationEffect : LightbarEffect
    {
        public Color NotificationColor { get; set; }
        public int FlashCount { get; set; }

        public NotificationEffect(Color color, int flashCount = 3, int priority = 8)
        {
            Type = LightbarEffectType.Notification;
            NotificationColor = color;
            FlashCount = flashCount;
            Priority = priority;
            Duration = TimeSpan.FromSeconds(flashCount * 0.5);
        }

        public override DS4Color GetColor(double progress)
        {
            var flashPhase = progress * FlashCount;
            var isOn = (flashPhase % 1.0) < 0.5;
            
            return isOn ? new DS4Color(NotificationColor) : new DS4Color(Color.Black);
        }
    }

    public class GradientEffect : LightbarEffect
    {
        public List<Color> Colors { get; set; }
        public bool Smooth { get; set; } = true;

        public GradientEffect(List<Color> colors, TimeSpan duration, bool smooth = true)
        {
            Type = LightbarEffectType.Gradient;
            Colors = colors ?? throw new ArgumentNullException(nameof(colors));
            Duration = duration;
            Smooth = smooth;
            Loop = true;
        }

        public override DS4Color GetColor(double progress)
        {
            if (Colors.Count == 0) return new DS4Color(Color.White);
            if (Colors.Count == 1) return new DS4Color(Colors[0]);

            var scaledProgress = progress * (Colors.Count - 1);
            var index = (int)Math.Floor(scaledProgress);
            var fraction = scaledProgress - index;

            if (index >= Colors.Count - 1)
                return new DS4Color(Colors.Last());

            var color1 = Colors[index];
            var color2 = Colors[index + 1];

            if (!Smooth)
                return new DS4Color(fraction < 0.5 ? color1 : color2);

            // Smooth interpolation
            var r = (byte)(color1.R + (color2.R - color1.R) * fraction);
            var g = (byte)(color1.G + (color2.G - color1.G) * fraction);
            var b = (byte)(color1.B + (color2.B - color1.B) * fraction);

            return new DS4Color(r, g, b);
        }
    }

    public class AdvancedLightbarEffects : IDisposable
    {
        private readonly DS4Device device;
        private readonly List<LightbarEffect> activeEffects;
        private readonly Timer updateTimer;
        private bool disposed;
        private DS4Color baseColor;
        private DS4Color currentColor;

        public event EventHandler<LightbarColorChangedEventArgs> ColorChanged;

        public AdvancedLightbarEffects(DS4Device device)
        {
            this.device = device ?? throw new ArgumentNullException(nameof(device));
            this.activeEffects = new List<LightbarEffect>();
            this.baseColor = new DS4Color(Color.Blue); // Default color
            this.currentColor = baseColor;

            // Update lightbar at 30 FPS
            this.updateTimer = new Timer(UpdateLightbar, null, 
                TimeSpan.FromMilliseconds(33), TimeSpan.FromMilliseconds(33));
        }

        public void SetBaseColor(DS4Color color)
        {
            baseColor = color;
        }

        public void AddEffect(LightbarEffect effect)
        {
            if (effect == null) return;

            lock (activeEffects)
            {
                // Remove existing effects with same type and lower priority
                activeEffects.RemoveAll(e => e.Type == effect.Type && e.Priority <= effect.Priority);
                
                effect.Start();
                activeEffects.Add(effect);
                
                // Sort by priority (highest first)
                activeEffects.Sort((a, b) => b.Priority.CompareTo(a.Priority));
            }
        }

        public void RemoveEffect(LightbarEffectType type)
        {
            lock (activeEffects)
            {
                activeEffects.RemoveAll(e => e.Type == type);
            }
        }

        public void RemoveAllEffects()
        {
            lock (activeEffects)
            {
                foreach (var effect in activeEffects)
                {
                    effect.Stop();
                }
                activeEffects.Clear();
            }
        }

        private void UpdateLightbar(object state)
        {
            if (disposed) return;

            try
            {
                DS4Color newColor = baseColor;
                LightbarEffect activeEffect = null;

                lock (activeEffects)
                {
                    // Remove expired effects
                    activeEffects.RemoveAll(e => e.ShouldEnd());

                    // Get highest priority active effect
                    activeEffect = activeEffects.FirstOrDefault(e => e.IsActive);
                }

                if (activeEffect != null)
                {
                    var progress = activeEffect.GetProgress();
                    newColor = activeEffect.GetColor(progress);
                }

                // Only update if color changed
                if (!newColor.Equals(currentColor))
                {
                    currentColor = newColor;
                    device.LightBarColor = currentColor;
                    ColorChanged?.Invoke(this, new LightbarColorChangedEventArgs(currentColor));
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogToGui($"Error updating lightbar effects: {ex.Message}", true);
            }
        }

        // Static factory methods for common effects
        public static NotificationEffect CreateNotificationFlash(Color color, int flashCount = 3, int priority = 8)
        {
            return new NotificationEffect(color, flashCount, priority);
        }

        public static BatteryIndicatorEffect CreateBatteryIndicatorEffect(int batteryLevel, int priority = 9)
        {
            return new BatteryIndicatorEffect(batteryLevel) { Priority = priority };
        }

        public static FlashEffect CreateWarningFlash(Color warningColor, int flashCount = 5)
        {
            return new FlashEffect(warningColor, Color.Black, flashCount) { Priority = 10 };
        }

        public static PulseEffect CreateHealthStatusPulse(ControllerHealthStatus health)
        {
            var color = health switch
            {
                ControllerHealthStatus.Excellent => Color.Green,
                ControllerHealthStatus.Good => Color.LightGreen,
                ControllerHealthStatus.Fair => Color.Yellow,
                ControllerHealthStatus.Poor => Color.Orange,
                ControllerHealthStatus.Critical => Color.Red,
                _ => Color.Blue
            };

            return new PulseEffect(color, TimeSpan.FromSeconds(2)) { Priority = 6 };
        }

        public static RainbowEffect CreateRainbowEffect(TimeSpan duration, double speed = 1.0)
        {
            return new RainbowEffect(duration, speed) { Priority = 3 };
        }

        public static GradientEffect CreateGameSpecificGradient(string gameName)
        {
            // Predefined color schemes for popular games
            var colors = gameName.ToLower() switch
            {
                "cyberpunk" => new List<Color> { Color.Cyan, Color.Magenta, Color.Yellow },
                "witcher" => new List<Color> { Color.White, Color.Silver, Color.Gray },
                "gta" => new List<Color> { Color.Orange, Color.White, Color.Green },
                "fifa" => new List<Color> { Color.Green, Color.White },
                "cod" => new List<Color> { Color.Olive, Color.Black, Color.Orange },
                _ => new List<Color> { Color.Blue, Color.Purple, Color.Cyan }
            };

            return new GradientEffect(colors, TimeSpan.FromSeconds(10)) { Priority = 4 };
        }

        public void ApplyGameProfile(string gameName)
        {
            // Apply game-specific lighting effects
            var gameEffect = CreateGameSpecificGradient(gameName);
            AddEffect(gameEffect);

            AppLogger.LogToGui($"Applied lighting profile for {gameName}", false);
        }

        public void ShowPerformanceAlert(PerformanceAlertType alertType)
        {
            var color = alertType switch
            {
                PerformanceAlertType.HighLatency => Color.Red,
                PerformanceAlertType.PacketLoss => Color.Orange,
                PerformanceAlertType.HighCpuUsage => Color.Yellow,
                PerformanceAlertType.MemoryLeak => Color.Purple,
                _ => Color.Red
            };

            var effect = CreateWarningFlash(color, 3);
            AddEffect(effect);
        }

        public void Dispose()
        {
            if (disposed) return;

            disposed = true;
            updateTimer?.Dispose();
            RemoveAllEffects();
        }
    }

    public class LightbarColorChangedEventArgs : EventArgs
    {
        public DS4Color NewColor { get; }

        public LightbarColorChangedEventArgs(DS4Color newColor)
        {
            NewColor = newColor;
        }
    }
}
