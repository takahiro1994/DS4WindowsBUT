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
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DS4Windows
{
    public enum APIEndpoint
    {
        Controllers,
        Profiles,
        Settings,
        Macros,
        Performance,
        Battery,
        Games,
        System
    }

    public class APIResponse<T>
    {
        public bool Success { get; set; }
        public T Data { get; set; }
        public string Message { get; set; }
        public int StatusCode { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class ControllerInfo
    {
        public int Index { get; set; }
        public string Name { get; set; }
        public string MacAddress { get; set; }
        public bool IsConnected { get; set; }
        public int BatteryLevel { get; set; }
        public bool IsCharging { get; set; }
        public string ConnectionType { get; set; }
        public double Latency { get; set; }
        public string CurrentProfile { get; set; }
        public BatteryHealth BatteryHealth { get; set; }
        public ControllerHealthStatus HealthStatus { get; set; }
    }

    public class SystemInfo
    {
        public string Version { get; set; }
        public DateTime Uptime { get; set; }
        public int ActiveControllers { get; set; }
        public bool ViGEmInstalled { get; set; }
        public bool HidHideInstalled { get; set; }
        public string Platform { get; set; }
        public PerformanceDashboard Performance { get; set; }
    }

    public class DS4WindowsAPI
    {
        private readonly HttpListener httpListener;
        private readonly Dictionary<string, Func<HttpListenerContext, Task>> endpoints;
        private bool isRunning;
        private readonly int port;

        public event EventHandler<APIRequestEventArgs> APIRequestReceived;
        public event EventHandler<APIErrorEventArgs> APIError;

        public bool IsRunning => isRunning;
        public int Port => port;

        public DS4WindowsAPI(int port = 8080)
        {
            this.port = port;
            this.httpListener = new HttpListener();
            this.endpoints = new Dictionary<string, Func<HttpListenerContext, Task>>();
            
            InitializeEndpoints();
        }

        private void InitializeEndpoints()
        {
            // Controller endpoints
            endpoints["/api/controllers"] = HandleControllersEndpoint;
            endpoints["/api/controllers/{id}"] = HandleControllerByIdEndpoint;
            endpoints["/api/controllers/{id}/battery"] = HandleControllerBatteryEndpoint;
            endpoints["/api/controllers/{id}/performance"] = HandleControllerPerformanceEndpoint;
            
            // Profile endpoints
            endpoints["/api/profiles"] = HandleProfilesEndpoint;
            endpoints["/api/profiles/{name}"] = HandleProfileByNameEndpoint;
            endpoints["/api/profiles/{name}/apply"] = HandleApplyProfileEndpoint;
            
            // Macro endpoints
            endpoints["/api/macros"] = HandleMacrosEndpoint;
            endpoints["/api/macros/{name}"] = HandleMacroByNameEndpoint;
            endpoints["/api/macros/{name}/execute"] = HandleExecuteMacroEndpoint;
            
            // System endpoints
            endpoints["/api/system"] = HandleSystemEndpoint;
            endpoints["/api/system/performance"] = HandleSystemPerformanceEndpoint;
            endpoints["/api/games"] = HandleGamesEndpoint;
            endpoints["/api/games/current"] = HandleCurrentGameEndpoint;
            
            // Settings endpoints
            endpoints["/api/settings"] = HandleSettingsEndpoint;
            endpoints["/api/settings/{category}"] = HandleSettingsCategoryEndpoint;
        }

        public async Task StartAsync()
        {
            if (isRunning) return;

            try
            {
                httpListener.Prefixes.Add($"http://localhost:{port}/");
                httpListener.Prefixes.Add($"http://127.0.0.1:{port}/");
                httpListener.Start();
                isRunning = true;

                AppLogger.LogToGui($"DS4Windows API started on port {port}", false);

                // Start listening for requests
                while (isRunning)
                {
                    try
                    {
                        var context = await httpListener.GetContextAsync();
                        _ = Task.Run(() => ProcessRequestAsync(context));
                    }
                    catch (HttpListenerException)
                    {
                        // Expected when stopping the listener
                        break;
                    }
                    catch (Exception ex)
                    {
                        APIError?.Invoke(this, new APIErrorEventArgs(ex));
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogToGui($"Failed to start DS4Windows API: {ex.Message}", true);
                throw;
            }
        }

        public void Stop()
        {
            if (!isRunning) return;

            isRunning = false;
            httpListener?.Stop();
            AppLogger.LogToGui("DS4Windows API stopped", false);
        }

        private async Task ProcessRequestAsync(HttpListenerContext context)
        {
            try
            {
                var request = context.Request;
                var response = context.Response;

                // Enable CORS
                response.Headers.Add("Access-Control-Allow-Origin", "*");
                response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
                response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");

                if (request.HttpMethod == "OPTIONS")
                {
                    response.StatusCode = 200;
                    response.Close();
                    return;
                }

                APIRequestReceived?.Invoke(this, new APIRequestEventArgs(request));

                var path = request.Url.AbsolutePath.ToLower();
                var handler = FindEndpointHandler(path, request.HttpMethod);

                if (handler != null)
                {
                    await handler(context);
                }
                else
                {
                    await SendErrorResponse(context, 404, "Endpoint not found");
                }
            }
            catch (Exception ex)
            {
                await SendErrorResponse(context, 500, $"Internal server error: {ex.Message}");
                APIError?.Invoke(this, new APIErrorEventArgs(ex));
            }
        }

        private Func<HttpListenerContext, Task> FindEndpointHandler(string path, string method)
        {
            // Direct match first
            if (endpoints.TryGetValue(path, out var handler))
                return handler;

            // Pattern matching for parameterized endpoints
            foreach (var endpoint in endpoints.Keys)
            {
                if (MatchesPattern(path, endpoint))
                    return endpoints[endpoint];
            }

            return null;
        }

        private bool MatchesPattern(string path, string pattern)
        {
            var pathParts = path.Split('/');
            var patternParts = pattern.Split('/');

            if (pathParts.Length != patternParts.Length)
                return false;

            for (int i = 0; i < pathParts.Length; i++)
            {
                if (patternParts[i].StartsWith("{") && patternParts[i].EndsWith("}"))
                    continue; // Parameter placeholder
                
                if (pathParts[i] != patternParts[i])
                    return false;
            }

            return true;
        }

        private async Task HandleControllersEndpoint(HttpListenerContext context)
        {
            var controllers = new List<ControllerInfo>();
            
            for (int i = 0; i < ControlService.MAX_DS4_CONTROLLER_COUNT; i++)
            {
                var device = App.rootHub?.DS4Controllers[i];
                if (device != null)
                {
                    controllers.Add(new ControllerInfo
                    {
                        Index = i,
                        Name = device.DisplayName,
                        MacAddress = device.getMacAddress(),
                        IsConnected = device.IsAlive(),
                        BatteryLevel = device.getBattery(),
                        IsCharging = device.isCharging(),
                        ConnectionType = device.getConnectionType().ToString(),
                        Latency = device.Latency,
                        CurrentProfile = Global.ProfilePath[i],
                        BatteryHealth = device.BatteryManager?.CurrentStats.Health ?? BatteryHealth.Unknown,
                        HealthStatus = device.HealthMonitor?.GenerateHealthReport().OverallHealth ?? ControllerHealthStatus.Excellent
                    });
                }
            }
            
            await SendJsonResponse(context, new APIResponse<List<ControllerInfo>>
            {
                Success = true,
                Data = controllers,
                Message = $"Found {controllers.Count} controllers"
            });
        }

        private async Task HandleControllerByIdEndpoint(HttpListenerContext context)
        {
            var id = ExtractIdFromPath(context.Request.Url.AbsolutePath);
            if (id < 0 || id >= ControlService.MAX_DS4_CONTROLLER_COUNT)
            {
                await SendErrorResponse(context, 400, "Invalid controller ID");
                return;
            }

            var device = App.rootHub?.DS4Controllers[id];
            if (device == null)
            {
                await SendErrorResponse(context, 404, "Controller not found");
                return;
            }

            var controllerInfo = new ControllerInfo
            {
                Index = id,
                Name = device.DisplayName,
                MacAddress = device.getMacAddress(),
                IsConnected = device.IsAlive(),
                BatteryLevel = device.getBattery(),
                IsCharging = device.isCharging(),
                ConnectionType = device.getConnectionType().ToString(),
                Latency = device.Latency,
                CurrentProfile = Global.ProfilePath[id],
                BatteryHealth = device.BatteryManager?.CurrentStats.Health ?? BatteryHealth.Unknown,
                HealthStatus = device.HealthMonitor?.GenerateHealthReport().OverallHealth ?? ControllerHealthStatus.Excellent
            };

            await SendJsonResponse(context, new APIResponse<ControllerInfo>
            {
                Success = true,
                Data = controllerInfo,
                Message = "Controller information retrieved"
            });
        }

        private async Task HandleSystemEndpoint(HttpListenerContext context)
        {
            var systemInfo = new SystemInfo
            {
                Version = Global.exeversion,
                Uptime = DateTime.UtcNow - Process.GetCurrentProcess().StartTime,
                ActiveControllers = App.rootHub?.activeControllers ?? 0,
                ViGEmInstalled = Global.IsViGEmBusInstalled,
                HidHideInstalled = Global.hidHideInstalled,
                Platform = Environment.Is64BitProcess ? "x64" : "x86"
            };

            await SendJsonResponse(context, new APIResponse<SystemInfo>
            {
                Success = true,
                Data = systemInfo,
                Message = "System information retrieved"
            });
        }

        private async Task HandleProfilesEndpoint(HttpListenerContext context)
        {
            var profiles = new List<string>();
            
            try
            {
                var profilesPath = Path.Combine(Global.appdatapath, "Profiles");
                if (Directory.Exists(profilesPath))
                {
                    var profileFiles = Directory.GetFiles(profilesPath, "*.xml");
                    profiles = profileFiles.Select(f => Path.GetFileNameWithoutExtension(f)).ToList();
                }
            }
            catch (Exception ex)
            {
                await SendErrorResponse(context, 500, $"Error reading profiles: {ex.Message}");
                return;
            }

            await SendJsonResponse(context, new APIResponse<List<string>>
            {
                Success = true,
                Data = profiles,
                Message = $"Found {profiles.Count} profiles"
            });
        }

        private async Task HandleExecuteMacroEndpoint(HttpListenerContext context)
        {
            var macroName = ExtractNameFromPath(context.Request.Url.AbsolutePath);
            
            // This would integrate with the macro system
            var success = false; // await macroSystem.ExecuteMacroAsync(macroName);
            
            await SendJsonResponse(context, new APIResponse<bool>
            {
                Success = success,
                Data = success,
                Message = success ? $"Macro '{macroName}' executed successfully" : $"Failed to execute macro '{macroName}'"
            });
        }

        private async Task SendJsonResponse<T>(HttpListenerContext context, T data)
        {
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });
            
            var buffer = Encoding.UTF8.GetBytes(json);
            
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = buffer.Length;
            context.Response.StatusCode = 200;
            
            await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            context.Response.Close();
        }

        private async Task SendErrorResponse(HttpListenerContext context, int statusCode, string message)
        {
            var errorResponse = new APIResponse<object>
            {
                Success = false,
                Data = null,
                Message = message,
                StatusCode = statusCode
            };

            context.Response.StatusCode = statusCode;
            await SendJsonResponse(context, errorResponse);
        }

        private int ExtractIdFromPath(string path)
        {
            var parts = path.Split('/');
            for (int i = 0; i < parts.Length; i++)
            {
                if ((parts[i] == "controllers" || parts[i] == "controller") && i + 1 < parts.Length)
                {
                    if (int.TryParse(parts[i + 1], out var id))
                        return id;
                }
            }
            return -1;
        }

        private string ExtractNameFromPath(string path)
        {
            var parts = path.Split('/');
            if (parts.Length >= 4)
                return parts[3]; // /api/endpoint/name
            return null;
        }

        // Additional endpoint handlers would be implemented here...
        private async Task HandleControllerBatteryEndpoint(HttpListenerContext context) { /* Implementation */ }
        private async Task HandleControllerPerformanceEndpoint(HttpListenerContext context) { /* Implementation */ }
        private async Task HandleProfileByNameEndpoint(HttpListenerContext context) { /* Implementation */ }
        private async Task HandleApplyProfileEndpoint(HttpListenerContext context) { /* Implementation */ }
        private async Task HandleMacrosEndpoint(HttpListenerContext context) { /* Implementation */ }
        private async Task HandleMacroByNameEndpoint(HttpListenerContext context) { /* Implementation */ }
        private async Task HandleSystemPerformanceEndpoint(HttpListenerContext context) { /* Implementation */ }
        private async Task HandleGamesEndpoint(HttpListenerContext context) { /* Implementation */ }
        private async Task HandleCurrentGameEndpoint(HttpListenerContext context) { /* Implementation */ }
        private async Task HandleSettingsEndpoint(HttpListenerContext context) { /* Implementation */ }
        private async Task HandleSettingsCategoryEndpoint(HttpListenerContext context) { /* Implementation */ }

        public void Dispose()
        {
            Stop();
            httpListener?.Close();
        }
    }

    public class APIRequestEventArgs : EventArgs
    {
        public HttpListenerRequest Request { get; }

        public APIRequestEventArgs(HttpListenerRequest request)
        {
            Request = request;
        }
    }

    public class APIErrorEventArgs : EventArgs
    {
        public Exception Error { get; }

        public APIErrorEventArgs(Exception error)
        {
            Error = error;
        }
    }
}
