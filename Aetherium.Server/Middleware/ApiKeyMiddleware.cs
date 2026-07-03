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
    /// All /api/management traffic (including reads) requires a valid key in production,
    /// and every MUTATING request (POST/PUT/PATCH/DELETE) to the other control-plane
    /// controllers (adaptation, benchmark, cluster, curriculum, meta-progression,
    /// agent telemetry) requires it too — those were previously fully anonymous, so
    /// anyone who could reach the port could tick the economy, rewrite adaptation
    /// rules, or write curriculum files. Reads on those controllers stay open (the
    /// Dashboard's monitoring pages call them without a key).
    /// In Development the middleware is bypassed only when no key is configured.
    /// </summary>
    public class ApiKeyMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly byte[]? _apiKeyBytes;
        private const string ApiKeyHeaderName = "X-Dashboard-ApiKey";

        /// <summary>Prefix protected for every HTTP method, including reads.</summary>
        private const string FullyProtectedPrefix = "/api/management";

        /// <summary>Prefixes protected for mutating methods only.</summary>
        private static readonly string[] MutationProtectedPrefixes =
        {
            "/api/adaptation",
            "/api/agenttelemetry",
            "/api/benchmark",
            "/api/cluster",
            "/api/curriculum",
            "/api/metaprogression",
        };

        public ApiKeyMiddleware(RequestDelegate next, IConfiguration configuration)
        {
            _next = next;
            var key = configuration["Dashboard:ApiKey"];
            _apiKeyBytes = string.IsNullOrEmpty(key) ? null : Encoding.UTF8.GetBytes(key);
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (!RequiresApiKey(context.Request))
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

        private static bool RequiresApiKey(HttpRequest request)
        {
            var path = request.Path.Value ?? "";

            if (path.StartsWith(FullyProtectedPrefix, StringComparison.OrdinalIgnoreCase))
                return true;

            if (IsMutatingMethod(request.Method))
            {
                foreach (var prefix in MutationProtectedPrefixes)
                {
                    if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }

        private static bool IsMutatingMethod(string method) =>
            HttpMethods.IsPost(method) ||
            HttpMethods.IsPut(method) ||
            HttpMethods.IsPatch(method) ||
            HttpMethods.IsDelete(method);

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

