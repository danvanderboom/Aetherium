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
using ConsoleGameServer.Management;
using ConsoleGame.WorldGen;
using ConsoleGame.WorldGen.Prefabs;
using ConsoleGame.WorldBuilders.Validation;
using Microsoft.AspNetCore.SignalR;

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
            
            // Add world generation services
            builder.Services.AddSingleton<MapGeneratorRegistry>();
            builder.Services.AddSingleton<MapValidator>();
            
            // Add prefab library
            var environment = builder.Environment.EnvironmentName;
            var useFileStorage = environment == "Development" || 
                                Environment.GetEnvironmentVariable("PREFAB_STORAGE") == "file";
            
            builder.Services.AddSingleton<PrefabLibrary>(sp =>
            {
                var library = new PrefabLibrary(useFileStorage);
                // Load prefabs from file system in development
                if (useFileStorage)
                {
                    var prefabPath = Environment.GetEnvironmentVariable("PREFAB_PATH") ?? "./Data/Prefabs";
                    Console.WriteLine($"Loading prefabs from: {prefabPath}");
                    // TODO: Implement file loading in PrefabLibrary
                }
                return library;
            });
            
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
                    
                    // Configure grain storage
                    var storageType = Environment.GetEnvironmentVariable("ORLEANS_STORAGE") ?? "memory";
                    
                    if (storageType == "memory")
                    {
                        Console.WriteLine("Using in-memory grain storage (development mode)");
                        siloBuilder.AddMemoryGrainStorage("narrativeStore");
                        siloBuilder.AddMemoryGrainStorage("worldStore");
                        siloBuilder.AddMemoryGrainStorage("mapStore");
                    }
                    // Azure Storage support - requires Microsoft.Orleans.Persistence.AzureStorage package
                    // Uncomment when package is added:
                    /*
                    else if (storageType == "azure")
                    {
                        // Azure Table Storage configuration (for production)
                        var connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");
                        if (string.IsNullOrEmpty(connectionString))
                        {
                            throw new InvalidOperationException("AZURE_STORAGE_CONNECTION_STRING is required for Azure storage");
                        }
                        
                        Console.WriteLine("Using Azure Table Storage for grain persistence");
                        siloBuilder.AddAzureTableGrainStorage("narrativeStore", options =>
                        {
                            options.ConfigureTableServiceClient(connectionString);
                        });
                        siloBuilder.AddAzureTableGrainStorage("worldStore", options =>
                        {
                            options.ConfigureTableServiceClient(connectionString);
                        });
                        siloBuilder.AddAzureTableGrainStorage("mapStore", options =>
                        {
                            options.ConfigureTableServiceClient(connectionString);
                        });
                    }
                    */
                    
                    // Ensure grains can resolve the same singletons from host
                    siloBuilder.Services.AddSingleton(sp => sp.GetRequiredService<PromptRegistry>());
                    siloBuilder.Services.AddSingleton(sp => sp.GetRequiredService<MapGeneratorRegistry>());
                    siloBuilder.Services.AddSingleton(sp => sp.GetRequiredService<MapValidator>());
                    siloBuilder.Services.AddSingleton(sp => sp.GetRequiredService<PrefabLibrary>());
                    
                    // Register GameSessionManager for GameManagementGrain
                    siloBuilder.Services.AddSingleton<GameSessionManager>(sp =>
                    {
                        var host = sp.GetRequiredService<IHost>();
                        return host.Services.GetRequiredService<GameSessionManager>();
                    });
                    
                    // Register IHubContext<GameHub> for GameManagementGrain
                    siloBuilder.Services.AddSingleton<IHubContext<GameHub>>(sp =>
                    {
                        var host = sp.GetRequiredService<IHost>();
                        return host.Services.GetRequiredService<IHubContext<GameHub>>();
                    });
                    
                    // Register IGrainFactory for GameManagementGrain
                    siloBuilder.Services.AddSingleton<IGrainFactory>(sp =>
                    {
                        return sp.GetRequiredService<IGrainFactory>();
                    });
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
            Console.WriteLine("Multi-world hosting enabled - use CLI to create worlds");
            Console.WriteLine("Waiting for client connections...");
            Console.WriteLine("Agent dashboard: http://localhost:5000/dashboard");

            await app.RunAsync();
        }
    }
}

