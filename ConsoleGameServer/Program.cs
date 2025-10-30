using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orleans;
using Orleans.Hosting;
using ConsoleGameServer.Agents;

namespace ConsoleGameServer
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services
            builder.Services.AddSignalR();
            builder.Services.AddSingleton<GameSessionManager>();
            
            // Add PromptRegistry for loading agent prompts
            builder.Services.AddSingleton<PromptRegistry>(sp =>
            {
                var registry = new PromptRegistry();
                registry.LoadTemplates();
                return registry;
            });

            // Add Orleans co-hosting (can be disabled for tests via env var)
            var disableOrleans = Environment.GetEnvironmentVariable("DISABLE_ORLEANS") == "1";
            if (!disableOrleans)
            {
                builder.Host.UseOrleans(siloBuilder =>
                {
                    siloBuilder.UseLocalhostClustering();
                    // Ensure grains can resolve the same PromptRegistry singleton
                    siloBuilder.Services.AddSingleton(sp => sp.GetRequiredService<PromptRegistry>());
                });
            }

            // Configure URLs
            builder.WebHost.UseUrls("http://localhost:5000");

            var app = builder.Build();

            // Configure middleware
            app.MapHub<GameHub>("/gamehub");
            
            // Dashboard endpoint for viewing active agents
            app.MapGet("/dashboard", () =>
            {
                // TODO: Query Orleans for active agent grains and return status
                return Microsoft.AspNetCore.Http.Results.Json(new { message = "Agent dashboard - TODO: Implement agent listing" });
            });

            Console.WriteLine("Console Game Server starting on http://localhost:5000");
            Console.WriteLine("Orleans silo co-hosted with ASP.NET Core");
            Console.WriteLine("Waiting for client connections...");
            Console.WriteLine("Agent dashboard: http://localhost:5000/dashboard");

            await app.RunAsync();
        }
    }
}

