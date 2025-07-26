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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DS4Windows
{
    public enum PerformanceAlertType
    {
        HighLatency,
        PacketLoss,
        HighCpuUsage,
        MemoryLeak,
        FrameDrops,
        OverheatingWarning
    }

    public class PerformanceAlert
    {
        public PerformanceAlertType Type { get; set; }
        public double Value { get; set; }
        public DateTime Timestamp { get; set; }
        public string Description { get; set; }
        public int Severity { get; set; } // 1-10, 10 being most severe
    }

    public class LatencyMetrics
    {
        public double CurrentLatency { get; set; }
        public double AverageLatency { get; set; }
        public double MinLatency { get; set; }
        public double MaxLatency { get; set; }
        public double LatencyVariance { get; set; }
        public int PacketsProcessed { get; set; }
        public DateTime LastUpdate { get; set; }
    }

    public class ThroughputMetrics
    {
        public long BytesPerSecond { get; set; }
        public long PacketsPerSecond { get; set; }
        public long TotalBytesProcessed { get; set; }
        public long TotalPacketsProcessed { get; set; }
        public double AveragePacketSize { get; set; }
        public DateTime MeasurementStart { get; set; }
    }

    public class SystemResourceMetrics
    {
        public double CpuUsagePercent { get; set; }
        public long MemoryUsageBytes { get; set; }
        public long MemoryUsageMB => MemoryUsageBytes / (1024 * 1024);
        public double ThreadCount { get; set; }
        public double HandleCount { get; set; }
        public TimeSpan ProcessUptime { get; set; }
    }

    public class PerformanceDashboard
    {
        public LatencyMetrics Latency { get; set; } = new LatencyMetrics();
        public ThroughputMetrics Throughput { get; set; } = new ThroughputMetrics();
        public SystemResourceMetrics SystemResources { get; set; } = new SystemResourceMetrics();
        public List<PerformanceAlert> RecentAlerts { get; set; } = new List<PerformanceAlert>();
        public double OverallPerformanceScore { get; set; }
        public DateTime LastUpdate { get; set; }
    }

    public class PerformanceAlertEventArgs : EventArgs
    {
        public PerformanceAlert Alert { get; }

        public PerformanceAlertEventArgs(PerformanceAlert alert)
        {
            Alert = alert;
        }
    }

    public class PerformanceAnalytics : IDisposable
    {
        private readonly DS4Device device;
        private readonly Stopwatch latencyStopwatch;
        private readonly List<double> latencyHistory;
        private readonly Queue<DateTime> packetTimestamps;
        private readonly Queue<int> packetSizes;
        private readonly PerformanceCounter cpuCounter;
        private readonly Process currentProcess;
        private readonly Timer performanceTimer;
        
        private long totalBytesProcessed;
        private long totalPacketsProcessed;
        private DateTime measurementStart;
        private bool disposed;

        // Thresholds for alerts
        private const double HIGH_LATENCY_THRESHOLD = 20.0; // ms
        private const double HIGH_CPU_THRESHOLD = 80.0; // %
        private const long HIGH_MEMORY_THRESHOLD = 500 * 1024 * 1024; // 500MB
        private const int LATENCY_HISTORY_SIZE = 1000;
        private const int PACKET_HISTORY_SECONDS = 60;

        public event EventHandler<PerformanceAlertEventArgs> PerformanceAlert;
        public event EventHandler<EventArgs> DashboardUpdated;

        public PerformanceDashboard Dashboard { get; private set; }

        public PerformanceAnalytics(DS4Device device)
        {
            this.device = device ?? throw new ArgumentNullException(nameof(device));
            this.latencyStopwatch = new Stopwatch();
            this.latencyHistory = new List<double>();
            this.packetTimestamps = new Queue<DateTime>();
            this.packetSizes = new Queue<int>();
            this.currentProcess = Process.GetCurrentProcess();
            this.measurementStart = DateTime.UtcNow;
            this.Dashboard = new PerformanceDashboard();

            // Initialize CPU counter
            try
            {
                this.cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                this.cpuCounter.NextValue(); // First call always returns 0
            }
            catch (Exception ex)
            {
                AppLogger.LogToGui($"Failed to initialize CPU counter: {ex.Message}", true);
            }

            // Start performance monitoring timer (update every 5 seconds)
            this.performanceTimer = new Timer(UpdatePerformanceMetrics, null, 
                TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));

            latencyStopwatch.Start();
        }

        public void RecordPacketData(int packetSize)
        {
            if (disposed) return;

            var now = DateTime.UtcNow;
            packetTimestamps.Enqueue(now);
            packetSizes.Enqueue(packetSize);
            totalBytesProcessed += packetSize;
            totalPacketsProcessed++;

            // Remove old entries (older than PACKET_HISTORY_SECONDS)
            while (packetTimestamps.Count > 0 && 
                   (now - packetTimestamps.Peek()).TotalSeconds > PACKET_HISTORY_SECONDS)
            {
                packetTimestamps.Dequeue();
                packetSizes.Dequeue();
            }

            // Record latency if we have timing information
            if (latencyStopwatch.IsRunning)
            {
                double latency = latencyStopwatch.Elapsed.TotalMilliseconds;
                RecordLatency(latency);
                latencyStopwatch.Restart();
            }
        }

        public void RecordLatency(double latencyMs)
        {
            if (disposed) return;

            lock (latencyHistory)
            {
                latencyHistory.Add(latencyMs);
                
                // Keep only recent history
                if (latencyHistory.Count > LATENCY_HISTORY_SIZE)
                {
                    latencyHistory.RemoveAt(0);
                }
            }

            // Check for high latency alert
            if (latencyMs > HIGH_LATENCY_THRESHOLD)
            {
                var alert = new PerformanceAlert
                {
                    Type = PerformanceAlertType.HighLatency,
                    Value = latencyMs,
                    Timestamp = DateTime.UtcNow,
                    Description = $"High input latency detected: {latencyMs:F1}ms",
                    Severity = latencyMs > HIGH_LATENCY_THRESHOLD * 2 ? 8 : 5
                };
                
                TriggerAlert(alert);
            }
        }

        private void UpdatePerformanceMetrics(object state)
        {
            if (disposed) return;

            try
            {
                UpdateLatencyMetrics();
                UpdateThroughputMetrics();
                UpdateSystemResourceMetrics();
                CalculatePerformanceScore();

                Dashboard.LastUpdate = DateTime.UtcNow;
                DashboardUpdated?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                AppLogger.LogToGui($"Error updating performance metrics: {ex.Message}", true);
            }
        }

        private void UpdateLatencyMetrics()
        {
            lock (latencyHistory)
            {
                if (latencyHistory.Count == 0) return;

                Dashboard.Latency.CurrentLatency = latencyHistory.LastOrDefault();
                Dashboard.Latency.AverageLatency = latencyHistory.Average();
                Dashboard.Latency.MinLatency = latencyHistory.Min();
                Dashboard.Latency.MaxLatency = latencyHistory.Max();
                Dashboard.Latency.PacketsProcessed = latencyHistory.Count;
                Dashboard.Latency.LastUpdate = DateTime.UtcNow;

                // Calculate variance
                double mean = Dashboard.Latency.AverageLatency;
                Dashboard.Latency.LatencyVariance = latencyHistory
                    .Select(x => Math.Pow(x - mean, 2))
                    .Average();
            }
        }

        private void UpdateThroughputMetrics()
        {
            var now = DateTime.UtcNow;
            var elapsed = (now - measurementStart).TotalSeconds;
            
            if (elapsed > 0)
            {
                Dashboard.Throughput.BytesPerSecond = (long)(totalBytesProcessed / elapsed);
                Dashboard.Throughput.PacketsPerSecond = (long)(totalPacketsProcessed / elapsed);
            }

            Dashboard.Throughput.TotalBytesProcessed = totalBytesProcessed;
            Dashboard.Throughput.TotalPacketsProcessed = totalPacketsProcessed;
            Dashboard.Throughput.MeasurementStart = measurementStart;

            if (totalPacketsProcessed > 0)
            {
                Dashboard.Throughput.AveragePacketSize = (double)totalBytesProcessed / totalPacketsProcessed;
            }
        }

        private void UpdateSystemResourceMetrics()
        {
            try
            {
                // Update CPU usage
                if (cpuCounter != null)
                {
                    Dashboard.SystemResources.CpuUsagePercent = cpuCounter.NextValue();
                }

                // Update memory usage
                currentProcess.Refresh();
                Dashboard.SystemResources.MemoryUsageBytes = currentProcess.WorkingSet64;
                Dashboard.SystemResources.ThreadCount = currentProcess.Threads.Count;
                Dashboard.SystemResources.HandleCount = currentProcess.HandleCount;
                Dashboard.SystemResources.ProcessUptime = DateTime.UtcNow - currentProcess.StartTime;

                // Check for resource alerts
                CheckResourceAlerts();
            }
            catch (Exception ex)
            {
                AppLogger.LogToGui($"Error updating system resource metrics: {ex.Message}", true);
            }
        }

        private void CheckResourceAlerts()
        {
            // Check CPU usage
            if (Dashboard.SystemResources.CpuUsagePercent > HIGH_CPU_THRESHOLD)
            {
                var alert = new PerformanceAlert
                {
                    Type = PerformanceAlertType.HighCpuUsage,
                    Value = Dashboard.SystemResources.CpuUsagePercent,
                    Timestamp = DateTime.UtcNow,
                    Description = $"High CPU usage: {Dashboard.SystemResources.CpuUsagePercent:F1}%",
                    Severity = Dashboard.SystemResources.CpuUsagePercent > 95 ? 9 : 6
                };
                
                TriggerAlert(alert);
            }

            // Check memory usage
            if (Dashboard.SystemResources.MemoryUsageBytes > HIGH_MEMORY_THRESHOLD)
            {
                var alert = new PerformanceAlert
                {
                    Type = PerformanceAlertType.MemoryLeak,
                    Value = Dashboard.SystemResources.MemoryUsageMB,
                    Timestamp = DateTime.UtcNow,
                    Description = $"High memory usage: {Dashboard.SystemResources.MemoryUsageMB:F0}MB",
                    Severity = Dashboard.SystemResources.MemoryUsageBytes > HIGH_MEMORY_THRESHOLD * 2 ? 8 : 5
                };
                
                TriggerAlert(alert);
            }
        }

        private void CalculatePerformanceScore()
        {
            double score = 100.0;

            // Deduct points for high latency
            if (Dashboard.Latency.AverageLatency > HIGH_LATENCY_THRESHOLD)
            {
                score -= Math.Min(30, Dashboard.Latency.AverageLatency - HIGH_LATENCY_THRESHOLD);
            }

            // Deduct points for high CPU usage
            if (Dashboard.SystemResources.CpuUsagePercent > HIGH_CPU_THRESHOLD)
            {
                score -= Math.Min(25, Dashboard.SystemResources.CpuUsagePercent - HIGH_CPU_THRESHOLD);
            }

            // Deduct points for high memory usage
            if (Dashboard.SystemResources.MemoryUsageBytes > HIGH_MEMORY_THRESHOLD)
            {
                double excessMB = (Dashboard.SystemResources.MemoryUsageBytes - HIGH_MEMORY_THRESHOLD) / (1024.0 * 1024.0);
                score -= Math.Min(20, excessMB / 100.0);
            }

            // Deduct points for latency variance (jitter)
            if (Dashboard.Latency.LatencyVariance > 100) // High jitter
            {
                score -= Math.Min(15, Dashboard.Latency.LatencyVariance / 50.0);
            }

            Dashboard.OverallPerformanceScore = Math.Max(0, score);
        }

        private void TriggerAlert(PerformanceAlert alert)
        {
            // Add to recent alerts list
            Dashboard.RecentAlerts.Add(alert);
            
            // Keep only recent alerts (last 50)
            if (Dashboard.RecentAlerts.Count > 50)
            {
                Dashboard.RecentAlerts.RemoveAt(0);
            }

            // Trigger event
            PerformanceAlert?.Invoke(this, new PerformanceAlertEventArgs(alert));
        }

        public PerformanceDashboard GetCurrentDashboard()
        {
            return Dashboard;
        }

        public void OptimizePerformance()
        {
            try
            {
                // Force garbage collection if memory usage is high
                if (Dashboard.SystemResources.MemoryUsageBytes > HIGH_MEMORY_THRESHOLD)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                    
                    AppLogger.LogToGui("Performed memory cleanup due to high usage", false);
                }

                // Clear old performance data
                lock (latencyHistory)
                {
                    if (latencyHistory.Count > LATENCY_HISTORY_SIZE / 2)
                    {
                        latencyHistory.RemoveRange(0, latencyHistory.Count / 4);
                    }
                }

                // Reset measurement counters if they're getting too large
                if (totalPacketsProcessed > long.MaxValue / 2)
                {
                    ResetCounters();
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogToGui($"Error during performance optimization: {ex.Message}", true);
            }
        }

        public void ResetCounters()
        {
            totalBytesProcessed = 0;
            totalPacketsProcessed = 0;
            measurementStart = DateTime.UtcNow;
            
            lock (latencyHistory)
            {
                latencyHistory.Clear();
            }
            
            packetTimestamps.Clear();
            packetSizes.Clear();
            
            AppLogger.LogToGui("Performance counters reset", false);
        }

        public void Dispose()
        {
            if (disposed) return;
            
            disposed = true;
            performanceTimer?.Dispose();
            cpuCounter?.Dispose();
            currentProcess?.Dispose();
            latencyStopwatch?.Stop();
        }
    }
}
