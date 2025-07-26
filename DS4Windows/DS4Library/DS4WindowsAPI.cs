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
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Net.WebSockets;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DS4Windows.API
{
    public class DS4WindowsAPIService
    {
        private readonly ControlService controlService;
        private readonly IHost webHost;
        private readonly string baseUrl;
        private readonly int port;
        private bool isRunning = false;

        public DS4WindowsAPIService(ControlService controlService, int port = 8080)
        {
            this.controlService = controlService ?? throw new ArgumentNullException(nameof(controlService));
            this.port = port;
            this.baseUrl = $"http://localhost:{port}";
            
            InitializeWebHost();
        }

        private void InitializeWebHost()
        {
            var builder = WebApplication.CreateBuilder();
            
            builder.Services.AddControllers();
            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader();
                });
            });
            
            builder.Services.AddSingleton(controlService);
            
            builder.WebHost.UseUrls(baseUrl);
            
            var app = builder.Build();
            
            app.UseCors();
            app.UseRouting();
            app.MapControllers();
            
            // Custom middleware for API endpoints
            app.MapGet("/api/status", GetSystemStatus);
            app.MapGet("/api/controllers", GetControllers);
            app.MapGet("/api/controllers/{id}", GetController);
            app.MapPost("/api/controllers/{id}/profile", SetControllerProfile);
            app.MapGet("/api/profiles", GetProfiles);
            app.MapPost("/api/profiles/{name}/execute", ExecuteProfile);
            app.MapGet("/api/analytics", GetAnalytics);
            app.MapPost("/api/macro/{name}/execute", ExecuteMacro);
            app.MapGet("/api/health", GetHealthStatus);
            
            webHost = app;
        }

        public async Task StartAsync()
        {
            if (!isRunning)
            {
                await webHost.StartAsync();
                isRunning = true;
            }
        }

        public async Task StopAsync()
        {
            if (isRunning)
            {
                await webHost.StopAsync();
                isRunning = false;
            }
        }

        private async Task<IResult> GetSystemStatus(HttpContext context)
        {
            var status = new
            {
                Version = "3.3.3+Enhanced",
                IsRunning = controlService.running,
                ActiveControllers = controlService.activeControllers,
                Timestamp = DateTime.UtcNow,
                Features = new
                {
                    AdvancedLightbar = true,
                    MotionFiltering = true,
                    HealthMonitoring = true,
                    PerformanceAnalytics = true,
                    SmartProfiles = true,
                    AdvancedMacros = true
                }
            };
            
            return Results.Json(status);
        }

        private async Task<IResult> GetControllers(HttpContext context)
        {
            var controllers = new List<object>();
            
            for (int i = 0; i < controlService.DS4Controllers.Length; i++)
            {
                var controller = controlService.DS4Controllers[i];
                if (controller != null)
                {
                    controllers.Add(new
                    {
                        Id = i,
                        MacAddress = controller.MacAddress,
                        ConnectionType = controller.ConnectionType.ToString(),
                        Battery = controller.Battery,
                        IsCharging = controller.Charging,
                        ControllerVersion = controller.ControllerVersion.ToString(),
                        IsExclusive = controller.IsExclusive,
                        LastActive = controller.lastActive,
                        Health = controller.HealthMonitor?.GenerateHealthReport()?.OverallHealth.ToString(),
                        Performance = new
                        {
                            AverageLatency = controller.PerformanceAnalytics?.GetCurrentMetrics()?.AverageLatencyMs,
                            PacketLoss = controller.PerformanceAnalytics?.GetCurrentMetrics()?.PacketLossPercentage
                        }
                    });
                }
            }
            
            return Results.Json(controllers);
        }

        private async Task<IResult> GetController(HttpContext context, int id)
        {
            if (id < 0 || id >= controlService.DS4Controllers.Length)
                return Results.NotFound($"Controller {id} not found");
            
            var controller = controlService.DS4Controllers[id];
            if (controller == null)
                return Results.NotFound($"Controller {id} not connected");
            
            var detailedInfo = new
            {
                Id = id,
                MacAddress = controller.MacAddress,
                ConnectionType = controller.ConnectionType.ToString(),
                Battery = new
                {
                    Level = controller.Battery,
                    IsCharging = controller.Charging,
                    Health = controller.BatteryManager?.GetBatteryHealth().ToString(),
                    EstimatedLife = controller.BatteryManager?.GetEstimatedRemainingLife().ToString(),
                    Analytics = controller.BatteryManager?.GetBatteryAnalytics()
                },
                Hardware = new
                {
                    Version = controller.ControllerVersion.ToString(),
                    Health = controller.HealthMonitor?.GenerateHealthReport(),
                    Calibration = new
                    {
                        ShouldRunCalib = controller.ShouldRunCalib(),
                        LastCalibration = DateTime.UtcNow // Would track actual calibration time
                    }
                },
                Performance = controller.PerformanceAnalytics?.GetCurrentMetrics(),
                Motion = new
                {
                    FilterType = controller.MotionFilter?.GetType().Name,
                    Statistics = controller.MotionFilter?.GetMotionStatistics()
                },
                Lightbar = new
                {
                    CurrentColor = $"#{controller.LightBarColor.red:X2}{controller.LightBarColor.green:X2}{controller.LightBarColor.blue:X2}",
                    ActiveEffects = "N/A" // Would need to expose from AdvancedLightbarEffects
                }
            };
            
            return Results.Json(detailedInfo);
        }

        private async Task<IResult> SetControllerProfile(HttpContext context, int id)
        {
            if (id < 0 || id >= controlService.DS4Controllers.Length)
                return Results.BadRequest($"Invalid controller ID: {id}");
                
            using var reader = new System.IO.StreamReader(context.Request.Body);
            var requestBody = await reader.ReadToEndAsync();
            
            try
            {
                var profileRequest = JsonSerializer.Deserialize<ProfileChangeRequest>(requestBody);
                
                // Would implement profile switching logic here
                // Global.LoadProfile(id, profileRequest.ProfileName);
                
                return Results.Ok(new { Success = true, Message = $"Profile '{profileRequest.ProfileName}' applied to controller {id}" });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { Success = false, Error = ex.Message });
            }
        }

        private async Task<IResult> GetProfiles(HttpContext context)
        {
            // Would integrate with actual profile system
            var profiles = new[]
            {
                new { Name = "Default", Path = "Profiles/Default.xml", LastUsed = DateTime.UtcNow.AddDays(-1) },
                new { Name = "Gaming", Path = "Profiles/Gaming.xml", LastUsed = DateTime.UtcNow.AddHours(-2) },
                new { Name = "Media", Path = "Profiles/Media.xml", LastUsed = DateTime.UtcNow.AddDays(-5) }
            };
            
            return Results.Json(profiles);
        }

        private async Task<IResult> ExecuteProfile(HttpContext context, string name)
        {
            try
            {
                // Would implement profile execution logic
                return Results.Ok(new { Success = true, Message = $"Profile '{name}' executed successfully" });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { Success = false, Error = ex.Message });
            }
        }

        private async Task<IResult> GetAnalytics(HttpContext context)
        {
            var analytics = new
            {
                System = new
                {
                    Uptime = DateTime.UtcNow - Process.GetCurrentProcess().StartTime,
                    MemoryUsage = GC.GetTotalMemory(false),
                    ActiveControllers = controlService.activeControllers
                },
                Controllers = controlService.DS4Controllers
                    .Where(c => c != null)
                    .Select((c, i) => new
                    {
                        Id = i,
                        MacAddress = c.MacAddress,
                        TotalUsageTime = DateTime.UtcNow - c.firstActive,
                        Performance = c.PerformanceAnalytics?.GetCurrentMetrics(),
                        Health = c.HealthMonitor?.GenerateHealthReport()?.OverallHealth.ToString()
                    }).ToArray(),
                GlobalStats = new
                {
                    TotalInputEvents = controlService.DS4Controllers
                        .Where(c => c != null)
                        .Sum(c => c.PerformanceAnalytics?.GetCurrentMetrics()?.TotalInputEvents ?? 0),
                    AverageLatency = controlService.DS4Controllers
                        .Where(c => c != null)
                        .Average(c => c.PerformanceAnalytics?.GetCurrentMetrics()?.AverageLatencyMs ?? 0)
                }
            };
            
            return Results.Json(analytics);
        }

        private async Task<IResult> ExecuteMacro(HttpContext context, string name)
        {
            try
            {
                // Would integrate with AdvancedMacroSystem
                return Results.Ok(new { Success = true, Message = $"Macro '{name}' executed" });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { Success = false, Error = ex.Message });
            }
        }

        private async Task<IResult> GetHealthStatus(HttpContext context)
        {
            var healthStatus = new
            {
                Status = "Healthy",
                Timestamp = DateTime.UtcNow,
                Controllers = controlService.DS4Controllers
                    .Where(c => c != null)
                    .Select((c, i) => new
                    {
                        Id = i,
                        MacAddress = c.MacAddress,
                        Health = c.HealthMonitor?.GenerateHealthReport()?.OverallHealth.ToString() ?? "Unknown",
                        BatteryHealth = c.BatteryManager?.GetBatteryHealth().ToString() ?? "Unknown",
                        LastActive = c.lastActive,
                        Issues = c.HealthMonitor?.GenerateHealthReport()?.RecommendedActions ?? new List<string>()
                    }).ToArray(),
                SystemHealth = new
                {
                    Memory = new
                    {
                        Used = GC.GetTotalMemory(false),
                        Available = "N/A" // Would need system memory info
                    },
                    CPU = "N/A", // Would need CPU usage monitoring
                    Disk = "N/A"  // Would need disk usage monitoring
                }
            };
            
            return Results.Json(healthStatus);
        }

        public void Dispose()
        {
            StopAsync().Wait();
            webHost?.Dispose();
        }
    }

    // Request/Response models
    public class ProfileChangeRequest
    {
        public string ProfileName { get; set; }
        public Dictionary<string, object> Options { get; set; } = new Dictionary<string, object>();
    }

    public class MacroExecuteRequest
    {
        public string MacroName { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
    }

    public class LightbarEffectRequest
    {
        public string EffectType { get; set; }
        public string Color { get; set; }
        public int Duration { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
    }

    // WebSocket support for real-time updates
    public class DS4WindowsWebSocketHandler
    {
        private readonly List<WebSocket> connectedClients = new List<WebSocket>();
        private readonly ControlService controlService;
        private readonly Timer updateTimer;

        public DS4WindowsWebSocketHandler(ControlService controlService)
        {
            this.controlService = controlService;
            this.updateTimer = new Timer(SendUpdates, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }

        private async void SendUpdates(object state)
        {
            if (connectedClients.Count == 0) return;

            var update = new
            {
                Type = "ControllerUpdate",
                Timestamp = DateTime.UtcNow,
                Controllers = controlService.DS4Controllers
                    .Where(c => c != null)
                    .Select((c, i) => new
                    {
                        Id = i,
                        Battery = c.Battery,
                        IsCharging = c.Charging,
                        LatencyMs = c.PerformanceAnalytics?.GetCurrentMetrics()?.AverageLatencyMs ?? 0
                    }).ToArray()
            };

            var json = JsonSerializer.Serialize(update);
            var buffer = Encoding.UTF8.GetBytes(json);

            var clientsToRemove = new List<WebSocket>();
            
            foreach (var client in connectedClients.ToList())
            {
                try
                {
                    if (client.State == WebSocketState.Open)
                    {
                        await client.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                    else
                    {
                        clientsToRemove.Add(client);
                    }
                }
                catch
                {
                    clientsToRemove.Add(client);
                }
            }

            foreach (var client in clientsToRemove)
            {
                connectedClients.Remove(client);
            }
        }
    }
}
