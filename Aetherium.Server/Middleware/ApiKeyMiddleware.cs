using System;
using System.Net;
using System.Security.Cryptography;
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
    /// All /api/management traffic (including reads) requires a valid key in production.
    /// In Development the middleware is bypassed only when no key is configured.
    /// </summary>
    public class ApiKeyMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly byte[]? _apiKeyBytes;
        private const string ApiKeyHeaderName = "X-Dashboard-ApiKey";

        public ApiKeyMiddleware(RequestDelegate next, IConfiguration configuration)
        {
            _next = next;
            var key = configuration["Dashboard:ApiKey"];
            _apiKeyBytes = string.IsNullOrEmpty(key) ? null : Encoding.UTF8.GetBytes(key);
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value ?? "";

            // Only gate /api/management routes.
            if (!path.StartsWith("/api/management", StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            // No key configured: allow only in Development. Production must refuse rather
            // than implicitly opening the management surface.
            if (_apiKeyBytes == null)
            {
                var env = context.RequestServices.GetRequiredService<IHostEnvironment>();
                if (env.IsDevelopment())
                {
                    await _next(context);
                    return;
                }

                context.Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{\"error\":\"API key authentication not configured\"}", Encoding.UTF8);
                return;
            }

            // Gate every method (including GET) so management metadata isn't leaked anonymously.
            if (!context.Request.Headers.TryGetValue(ApiKeyHeaderName, out var providedKey) ||
                !ConstantTimeEquals(providedKey.ToString(), _apiKeyBytes))
            {
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{\"error\":\"Invalid or missing API key\"}", Encoding.UTF8);
                return;
            }

            await _next(context);
        }

        private static bool ConstantTimeEquals(string provided, byte[] expectedBytes)
        {
            // FixedTimeEquals requires equal-length spans, so hash both sides to a stable
            // 32-byte form first. SHA-256 is overkill for byte comparison but eliminates
            // both length-disclosure and short-circuit timing channels.
            var providedBytes = Encoding.UTF8.GetBytes(provided);
            Span<byte> providedHash = stackalloc byte[32];
            Span<byte> expectedHash = stackalloc byte[32];
            SHA256.HashData(providedBytes, providedHash);
            SHA256.HashData(expectedBytes, expectedHash);
            return CryptographicOperations.FixedTimeEquals(providedHash, expectedHash);
        }
    }
}

