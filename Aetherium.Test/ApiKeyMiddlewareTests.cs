using System.Collections.Generic;
using System.Threading.Tasks;
using Aetherium.Server.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Aetherium.Test
{
    /// <summary>
    /// Tests for the control-plane API-key gate (P0-7 in docs/audits/RECOMMENDATIONS.md):
    /// /api/management is protected for every method, and mutating requests
    /// (POST/PUT/PATCH/DELETE) to the other control-plane controllers require the
    /// key too — previously anyone who could reach the port could tick the economy
    /// or rewrite adaptation rules anonymously. Reads on those controllers stay open.
    /// </summary>
    public class ApiKeyMiddlewareTests
    {
        private const string HeaderName = "X-Dashboard-ApiKey";

        private static (ApiKeyMiddleware middleware, System.Func<bool> nextCalled) Create(string? apiKey)
        {
            var reached = false;
            RequestDelegate next = _ => { reached = true; return Task.CompletedTask; };

            var values = new Dictionary<string, string?>();
            if (apiKey != null)
                values["Dashboard:ApiKey"] = apiKey;

            var config = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
            return (new ApiKeyMiddleware(next, config), () => reached);
        }

        private static HttpContext Request(string method, string path, string? key = null)
        {
            var context = new DefaultHttpContext();
            context.Request.Method = method;
            context.Request.Path = path;
            if (key != null)
                context.Request.Headers[HeaderName] = key;
            return context;
        }

        [Theory]
        [InlineData("/api/cluster/abc/economy/tick")]
        [InlineData("/api/adaptation/rules/reload")]
        [InlineData("/api/benchmark")]
        [InlineData("/api/curriculum")]
        [InlineData("/api/metaprogression/p1/discoveries")]
        public async Task Mutating_ControlPlane_Request_Without_Key_Is_Rejected(string path)
        {
            var (middleware, nextCalled) = Create("secret");
            var context = Request("POST", path);

            await middleware.InvokeAsync(context);

            Assert.False(nextCalled());
            Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        }

        [Fact]
        public async Task Mutating_ControlPlane_Request_With_Valid_Key_Passes()
        {
            var (middleware, nextCalled) = Create("secret");
            var context = Request("POST", "/api/cluster/abc/economy/tick", key: "secret");

            await middleware.InvokeAsync(context);

            Assert.True(nextCalled());
        }

        [Fact]
        public async Task Mutating_Request_With_Wrong_Key_Is_Rejected()
        {
            var (middleware, nextCalled) = Create("secret");
            var context = Request("POST", "/api/benchmark", key: "not-the-secret");

            await middleware.InvokeAsync(context);

            Assert.False(nextCalled());
            Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        }

        [Theory]
        [InlineData("/api/adaptation/behavior/agent-1")]
        [InlineData("/api/cluster")]
        [InlineData("/api/agenttelemetry/agent-1/analysis")]
        public async Task Read_On_ControlPlane_Controllers_Stays_Open(string path)
        {
            var (middleware, nextCalled) = Create("secret");
            var context = Request("GET", path);

            await middleware.InvokeAsync(context);

            Assert.True(nextCalled());
        }

        [Fact]
        public async Task Management_Read_Still_Requires_Key()
        {
            // /api/management is fully protected — even GETs leak management metadata.
            var (middleware, nextCalled) = Create("secret");
            var context = Request("GET", "/api/management/worlds");

            await middleware.InvokeAsync(context);

            Assert.False(nextCalled());
            Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        }

        [Fact]
        public async Task Unrelated_Paths_Are_Not_Gated()
        {
            var (middleware, nextCalled) = Create("secret");
            var context = Request("POST", "/gamehub/negotiate");

            await middleware.InvokeAsync(context);

            Assert.True(nextCalled());
        }
    }
}
