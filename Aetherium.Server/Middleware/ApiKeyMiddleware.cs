using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Aetherium.Server.Middleware
{
    /// <summary>
    /// Middleware for API key authentication on control-plane endpoints.
    /// </summary>
    public class ApiKeyMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly string? _apiKey;
        private const string ApiKeyHeaderName = "X-Dashboard-ApiKey";

        public ApiKeyMiddleware(RequestDelegate next, IConfiguration configuration)
        {
            _next = next;
            _apiKey = configuration["Dashboard:ApiKey"];
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Only apply to control-plane routes (POST, PUT, DELETE, PATCH)
            var method = context.Request.Method;
            var isControlAction = method == "POST" || method == "PUT" || method == "DELETE" || method == "PATCH";
            var path = context.Request.Path.Value ?? "";

            // Only apply to /api/management routes
            if (!path.StartsWith("/api/management", StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            // For control actions, require API key
            if (isControlAction)
            {
                if (string.IsNullOrEmpty(_apiKey))
                {
                    // No API key configured - allow if in development mode
                    var env = context.RequestServices.GetRequiredService<IHostEnvironment>();
                    if (env.IsDevelopment())
                    {
                        await _next(context);
                        return;
                    }
                    
                    // Production mode without API key configured - deny control actions
                    context.Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync("{\"error\":\"API key authentication not configured\"}", Encoding.UTF8);
                    return;
                }

                // Check for API key in header
                if (!context.Request.Headers.TryGetValue(ApiKeyHeaderName, out var providedKey) || providedKey != _apiKey)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync("{\"error\":\"Invalid or missing API key\"}", Encoding.UTF8);
                    return;
                }
            }

            // Allow GET requests without API key, and authenticated control actions
            await _next(context);
        }
    }
}

