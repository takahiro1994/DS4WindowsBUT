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
using System.Numerics;

namespace DS4Windows
{
    public enum MotionGestureType
    {
        ShakeHorizontal,
        ShakeVertical,
        Twist,
        Flip,
        Tilt,
        Circle,
        Figure8,
        Tap,
        DoubleTap,
        Custom
    }

    public class MotionGesture
    {
        public string Name { get; set; }
        public MotionGestureType Type { get; set; }
        public float Threshold { get; set; }
        public TimeSpan Duration { get; set; }
        public bool IsActive { get; set; }
        public DateTime LastDetected { get; set; }
        public string AssignedAction { get; set; } // Action to trigger when detected
    }

    public class MotionDataPoint
    {
        public DateTime Timestamp { get; set; }
        public Vector3 Acceleration { get; set; }
        public Vector3 Gyroscope { get; set; }
        public Vector3 Orientation { get; set; }
    }

    public class MotionGestureDetectedEventArgs : EventArgs
    {
        public MotionGesture Gesture { get; }
        public float Confidence { get; }
        public MotionDataPoint TriggerData { get; }

        public MotionGestureDetectedEventArgs(MotionGesture gesture, float confidence, MotionDataPoint triggerData)
        {
            Gesture = gesture;
            Confidence = confidence;
            TriggerData = triggerData;
        }
    }

    public class AdvancedMotionFilter
    {
        private readonly Queue<MotionDataPoint> motionHistory;
        private readonly List<MotionGesture> registeredGestures;
        private readonly Dictionary<MotionGestureType, Func<List<MotionDataPoint>, float>> gestureDetectors;
        
        private Vector3 lastAcceleration;
        private Vector3 lastGyroscope;
        private DateTime lastUpdateTime;
        private bool isCalibrated;
        private Vector3 gyroOffset;
        private Vector3 accelOffset;

        // Motion thresholds and parameters
        private const int MOTION_HISTORY_SIZE = 100; // Store last 100 data points (~3 seconds at 30Hz)
        private const float SHAKE_THRESHOLD = 2.0f; // g-force threshold for shake detection
        private const float TWIST_THRESHOLD = 180.0f; // degrees/sec threshold for twist
        private const float GESTURE_CONFIDENCE_THRESHOLD = 0.7f; // Minimum confidence to trigger gesture
        private const double CALIBRATION_DURATION_SECONDS = 2.0; // Time to collect calibration data

        public event EventHandler<MotionGestureDetectedEventArgs> GestureDetected;

        public bool IsCalibrated => isCalibrated;
        public int HistorySize => motionHistory.Count;
        public IReadOnlyList<MotionGesture> RegisteredGestures => registeredGestures.AsReadOnly();

        public AdvancedMotionFilter()
        {
            motionHistory = new Queue<MotionDataPoint>();
            registeredGestures = new List<MotionGesture>();
            gestureDetectors = new Dictionary<MotionGestureType, Func<List<MotionDataPoint>, float>>();
            
            InitializeGestureDetectors();
            RegisterDefaultGestures();
        }

        private void InitializeGestureDetectors()
        {
            gestureDetectors[MotionGestureType.ShakeHorizontal] = DetectHorizontalShake;
            gestureDetectors[MotionGestureType.ShakeVertical] = DetectVerticalShake;
            gestureDetectors[MotionGestureType.Twist] = DetectTwist;
            gestureDetectors[MotionGestureType.Flip] = DetectFlip;
            gestureDetectors[MotionGestureType.Tilt] = DetectTilt;
            gestureDetectors[MotionGestureType.Circle] = DetectCircle;
            gestureDetectors[MotionGestureType.Tap] = DetectTap;
            gestureDetectors[MotionGestureType.DoubleTap] = DetectDoubleTap;
        }

        private void RegisterDefaultGestures()
        {
            // Register common gestures with default thresholds
            RegisterGesture("Horizontal Shake", MotionGestureType.ShakeHorizontal, SHAKE_THRESHOLD, TimeSpan.FromMilliseconds(500));
            RegisterGesture("Vertical Shake", MotionGestureType.ShakeVertical, SHAKE_THRESHOLD, TimeSpan.FromMilliseconds(500));
            RegisterGesture("Twist Left", MotionGestureType.Twist, TWIST_THRESHOLD, TimeSpan.FromMilliseconds(800));
            RegisterGesture("Controller Flip", MotionGestureType.Flip, 90.0f, TimeSpan.FromMilliseconds(1000));
            RegisterGesture("Single Tap", MotionGestureType.Tap, 3.0f, TimeSpan.FromMilliseconds(200));
            RegisterGesture("Double Tap", MotionGestureType.DoubleTap, 3.0f, TimeSpan.FromMilliseconds(600));
        }

        public void RegisterGesture(string name, MotionGestureType type, float threshold, TimeSpan duration, string action = null)
        {
            var gesture = new MotionGesture
            {
                Name = name,
                Type = type,
                Threshold = threshold,
                Duration = duration,
                IsActive = true,
                AssignedAction = action
            };

            registeredGestures.Add(gesture);
            AppLogger.LogToGui($"Registered motion gesture: {name}", false);
        }

        public void ProcessMotionData(DS4Sixaxis sixAxis, DateTime timestamp)
        {
            if (sixAxis == null) return;

            var acceleration = new Vector3(sixAxis.accelXG, sixAxis.accelYG, sixAxis.accelZG);
            var gyroscope = new Vector3(sixAxis.angVelPitch, sixAxis.angVelYaw, sixAxis.angVelRoll);

            // Apply calibration offsets
            if (isCalibrated)
            {
                acceleration -= accelOffset;
                gyroscope -= gyroOffset;
            }

            var dataPoint = new MotionDataPoint
            {
                Timestamp = timestamp,
                Acceleration = acceleration,
                Gyroscope = gyroscope,
                Orientation = CalculateOrientation(acceleration, gyroscope)
            };

            // Add to history
            motionHistory.Enqueue(dataPoint);
            
            // Keep history size manageable
            while (motionHistory.Count > MOTION_HISTORY_SIZE)
            {
                motionHistory.Dequeue();
            }

            // Process gestures if we have enough data
            if (motionHistory.Count >= 10)
            {
                ProcessGestureDetection(dataPoint);
            }

            lastAcceleration = acceleration;
            lastGyroscope = gyroscope;
            lastUpdateTime = timestamp;
        }

        private Vector3 CalculateOrientation(Vector3 acceleration, Vector3 gyroscope)
        {
            // Simple orientation calculation from accelerometer
            // In production, this would use a more sophisticated filter like Madgwick or Mahony
            var pitch = (float)Math.Atan2(acceleration.Y, Math.Sqrt(acceleration.X * acceleration.X + acceleration.Z * acceleration.Z));
            var roll = (float)Math.Atan2(-acceleration.X, acceleration.Z);
            var yaw = 0f; // Would need magnetometer for accurate yaw

            return new Vector3(pitch, yaw, roll) * (180f / (float)Math.PI); // Convert to degrees
        }

        private void ProcessGestureDetection(MotionDataPoint currentData)
        {
            var recentHistory = motionHistory.TakeLast(50).ToList(); // Last ~1.5 seconds
            
            foreach (var gesture in registeredGestures.Where(g => g.IsActive))
            {
                // Skip if gesture was recently detected (prevent spam)
                if ((DateTime.UtcNow - gesture.LastDetected).TotalMilliseconds < gesture.Duration.TotalMilliseconds)
                    continue;

                if (gestureDetectors.TryGetValue(gesture.Type, out var detector))
                {
                    var confidence = detector(recentHistory);
                    
                    if (confidence >= GESTURE_CONFIDENCE_THRESHOLD)
                    {
                        gesture.LastDetected = DateTime.UtcNow;
                        GestureDetected?.Invoke(this, new MotionGestureDetectedEventArgs(gesture, confidence, currentData));
                        
                        AppLogger.LogToGui($"Motion gesture detected: {gesture.Name} (confidence: {confidence:P1})", false);
                        
                        // Execute assigned action if any
                        if (!string.IsNullOrEmpty(gesture.AssignedAction))
                        {
                            ExecuteGestureAction(gesture.AssignedAction);
                        }
                    }
                }
            }
        }

        private float DetectHorizontalShake(List<MotionDataPoint> history)
        {
            if (history.Count < 10) return 0f;

            var xAccelValues = history.Select(h => h.Acceleration.X).ToArray();
            var peakCount = CountPeaks(xAccelValues, SHAKE_THRESHOLD);
            var frequency = CalculateFrequency(history, h => h.Acceleration.X);

            // Shake should have multiple peaks and appropriate frequency (2-8 Hz)
            var confidence = Math.Min(1.0f, (peakCount / 4.0f) * (frequency >= 2 && frequency <= 8 ? 1.0f : 0.5f));
            return confidence;
        }

        private float DetectVerticalShake(List<MotionDataPoint> history)
        {
            if (history.Count < 10) return 0f;

            var yAccelValues = history.Select(h => h.Acceleration.Y).ToArray();
            var peakCount = CountPeaks(yAccelValues, SHAKE_THRESHOLD);
            var frequency = CalculateFrequency(history, h => h.Acceleration.Y);

            var confidence = Math.Min(1.0f, (peakCount / 4.0f) * (frequency >= 2 && frequency <= 8 ? 1.0f : 0.5f));
            return confidence;
        }

        private float DetectTwist(List<MotionDataPoint> history)
        {
            if (history.Count < 5) return 0f;

            var maxAngularVel = history.Max(h => Math.Abs(h.Gyroscope.Z));
            var totalRotation = history.Sum(h => Math.Abs(h.Gyroscope.Z)) / history.Count;

            if (maxAngularVel > TWIST_THRESHOLD)
            {
                return Math.Min(1.0f, totalRotation / TWIST_THRESHOLD);
            }

            return 0f;
        }

        private float DetectFlip(List<MotionDataPoint> history)
        {
            if (history.Count < 10) return 0f;

            var orientationChange = Math.Abs(history.Last().Orientation.X - history.First().Orientation.X);
            
            if (orientationChange > 90) // 90 degree flip
            {
                return Math.Min(1.0f, orientationChange / 180f);
            }

            return 0f;
        }

        private float DetectTilt(List<MotionDataPoint> history)
        {
            if (history.Count < 5) return 0f;

            var currentTilt = Math.Abs(history.Last().Orientation.Y); // Roll angle
            var sustained = history.TakeLast(5).All(h => Math.Abs(h.Orientation.Y) > 30);

            if (currentTilt > 45 && sustained)
            {
                return Math.Min(1.0f, currentTilt / 90f);
            }

            return 0f;
        }

        private float DetectCircle(List<MotionDataPoint> history)
        {
            if (history.Count < 20) return 0f;

            // Detect circular motion in gyroscope data
            var gyroX = history.Select(h => h.Gyroscope.X).ToArray();
            var gyroY = history.Select(h => h.Gyroscope.Y).ToArray();

            // Simple circular motion detection - would need more sophisticated analysis
            var xRange = gyroX.Max() - gyroX.Min();
            var yRange = gyroY.Max() - gyroY.Min();
            var avgMagnitude = history.Average(h => h.Gyroscope.Length());

            if (xRange > 100 && yRange > 100 && avgMagnitude > 50)
            {
                return Math.Min(1.0f, avgMagnitude / 200f);
            }

            return 0f;
        }

        private float DetectTap(List<MotionDataPoint> history)
        {
            if (history.Count < 5) return 0f;

            var recentData = history.TakeLast(5).ToList();
            var maxAccel = recentData.Max(h => h.Acceleration.Length());
            var avgAccel = recentData.Average(h => h.Acceleration.Length());

            // Sharp acceleration spike followed by return to normal
            if (maxAccel > 3.0f && avgAccel < maxAccel * 0.7f)
            {
                return Math.Min(1.0f, maxAccel / 6.0f);
            }

            return 0f;
        }

        private float DetectDoubleTap(List<MotionDataPoint> history)
        {
            if (history.Count < 15) return 0f;

            var accelMagnitudes = history.Select(h => h.Acceleration.Length()).ToArray();
            var peaks = FindPeaks(accelMagnitudes, 2.5f);

            // Look for two peaks within appropriate time window
            if (peaks.Count >= 2)
            {
                var timeBetweenPeaks = (history[peaks[1]].Timestamp - history[peaks[0]].Timestamp).TotalMilliseconds;
                if (timeBetweenPeaks >= 100 && timeBetweenPeaks <= 400)
                {
                    return 0.9f;
                }
            }

            return 0f;
        }

        private int CountPeaks(float[] values, float threshold)
        {
            int peaks = 0;
            for (int i = 1; i < values.Length - 1; i++)
            {
                if (values[i] > values[i - 1] && values[i] > values[i + 1] && Math.Abs(values[i]) > threshold)
                {
                    peaks++;
                }
            }
            return peaks;
        }

        private List<int> FindPeaks(float[] values, float threshold)
        {
            var peaks = new List<int>();
            for (int i = 1; i < values.Length - 1; i++)
            {
                if (values[i] > values[i - 1] && values[i] > values[i + 1] && values[i] > threshold)
                {
                    peaks.Add(i);
                }
            }
            return peaks;
        }

        private float CalculateFrequency(List<MotionDataPoint> history, Func<MotionDataPoint, float> selector)
        {
            if (history.Count < 4) return 0f;

            var values = history.Select(selector).ToArray();
            var peaks = CountPeaks(values, 1.0f);
            var duration = (history.Last().Timestamp - history.First().Timestamp).TotalSeconds;

            return duration > 0 ? (float)(peaks / duration) : 0f;
        }

        public void CalibrateMotionSensors()
        {
            AppLogger.LogToGui("Starting motion sensor calibration. Keep controller still...", false);
            
            // Clear existing calibration
            gyroOffset = Vector3.Zero;
            accelOffset = Vector3.Zero;
            isCalibrated = false;

            // Start calibration process (would be done over time with multiple samples)
            Task.Run(async () =>
            {
                await Task.Delay((int)(CALIBRATION_DURATION_SECONDS * 1000));
                
                if (motionHistory.Count > 10)
                {
                    var recentData = motionHistory.TakeLast(30).ToList();
                    gyroOffset = new Vector3(
                        recentData.Average(d => d.Gyroscope.X),
                        recentData.Average(d => d.Gyroscope.Y),
                        recentData.Average(d => d.Gyroscope.Z)
                    );

                    // Don't calibrate accelerometer Z-axis as it should read ~1g due to gravity
                    accelOffset = new Vector3(
                        recentData.Average(d => d.Acceleration.X),
                        recentData.Average(d => d.Acceleration.Y),
                        0
                    );

                    isCalibrated = true;
                    AppLogger.LogToGui("Motion sensor calibration complete", false);
                }
                else
                {
                    AppLogger.LogToGui("Motion sensor calibration failed - insufficient data", true);
                }
            });
        }

        private void ExecuteGestureAction(string action)
        {
            try
            {
                // Parse and execute gesture action
                // This would integrate with the existing DS4Windows action system
                AppLogger.LogToGui($"Executing gesture action: {action}", false);
                
                // Example action parsing:
                if (action.StartsWith("profile:"))
                {
                    var profileName = action.Substring(8);
                    // Switch to profile: Global.LoadProfile(0, profileName);
                }
                else if (action.StartsWith("key:"))
                {
                    var keyName = action.Substring(4);
                    // Send key press: SendKey(keyName);
                }
                else if (action == "show_battery")
                {
                    // Show battery level via lightbar
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogToGui($"Error executing gesture action '{action}': {ex.Message}", true);
            }
        }

        public void SetGestureAction(string gestureName, string action)
        {
            var gesture = registeredGestures.FirstOrDefault(g => g.Name == gestureName);
            if (gesture != null)
            {
                gesture.AssignedAction = action;
                AppLogger.LogToGui($"Assigned action '{action}' to gesture '{gestureName}'", false);
            }
        }

        public void EnableGesture(string gestureName, bool enabled)
        {
            var gesture = registeredGestures.FirstOrDefault(g => g.Name == gestureName);
            if (gesture != null)
            {
                gesture.IsActive = enabled;
                AppLogger.LogToGui($"Gesture '{gestureName}' {(enabled ? "enabled" : "disabled")}", false);
            }
        }

        public List<string> GetAvailableGestures()
        {
            return registeredGestures.Select(g => $"{g.Name} ({g.Type})").ToList();
        }

        public void ClearHistory()
        {
            motionHistory.Clear();
        }
    }
}
