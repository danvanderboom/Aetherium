using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orleans;
using Orleans.Hosting;
using Aetherium.Server.Agents;
using Aetherium.Server.Events;
using Aetherium.Server.Management;
using Aetherium.Server.Persistence;
using Aetherium.Server.Simulation;
using Aetherium.WorldGen;
using Aetherium.WorldGen.Prefabs;
using Aetherium.WorldBuilders.Validation;
using Microsoft.AspNetCore.SignalR;

namespace Aetherium.Server
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services
            builder.Services.AddSignalR();
            builder.Services.AddControllers(); // Add API controllers
            builder.Services.AddSingleton<GameSessionManager>();
            
            // Update PerceptionService to use WorldClock
            builder.Services.AddSingleton<PerceptionService>(sp =>
            {
                var clock = sp.GetService<WorldClock>();
                var weather = sp.GetService<WeatherSystem>();
                var season = sp.GetService<SeasonManager>();
                return new PerceptionService(clock, weather, season);
            });
            
            // Add simulation configuration
            builder.Services.Configure<SimulationOptions>(
                builder.Configuration.GetSection("Simulation"));
            
            // Add simulation services
            builder.Services.AddSingleton<WorldClock>();
            builder.Services.AddSingleton<IWorldSnapshotStore, MemoryWorldSnapshotStore>();
                builder.Services.AddSingleton<SeasonManager>();
                builder.Services.AddSingleton<WeatherSystem>();
                builder.Services.AddSingleton<SpawnManager>();
                builder.Services.AddSingleton<BuilderAI>(sp =>
                {
                    var prefabLibrary = sp.GetRequiredService<PrefabLibrary>();
                    return new BuilderAI(prefabLibrary);
                });
                builder.Services.AddSingleton<TemporalModifierRegistry>(sp =>
                {
                    var registry = new TemporalModifierRegistry();
                    
                    // Register SpawnModifier for time/weather-weighted creature spawning
                    var spawnManager = sp.GetService<SpawnManager>();
                    if (spawnManager != null)
                    {
                        var spawnModifier = new SpawnModifier(spawnManager, spawnProbabilityPerTick: 0.01);
                        registry.Register(spawnModifier);
                    }
                    
                    // Register BuilderModifier if BuilderAI is available
                    var builderAI = sp.GetService<BuilderAI>();
                    if (builderAI != null)
                    {
                        // Note: World access is not available from IMapRegionGrain directly
                        // For now, BuilderModifier will be skipped until World access is available
                        // var builderModifier = new BuilderModifier(builderAI, ...);
                        // registry.Register(builderModifier);
                    }
                    return registry;
                });
            builder.Services.AddSingleton<IEventScheduler, EventScheduler>();
            
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
            
            // Add AgentToolRegistry for discovering and managing agent tools
            builder.Services.AddSingleton<Aetherium.Server.Agents.Tools.AgentToolRegistry>(sp =>
            {
                var toolRegistry = new Aetherium.Server.Agents.Tools.AgentToolRegistry(sp);
                toolRegistry.DiscoverTools(System.Reflection.Assembly.GetExecutingAssembly());
                return toolRegistry;
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
                    siloBuilder.Services.AddSingleton(sp => sp.GetRequiredService<Aetherium.Server.Agents.Tools.AgentToolRegistry>());
                    
                    // Register simulation services for grains
                    siloBuilder.Services.AddSingleton(sp => sp.GetRequiredService<WorldClock>());
                    siloBuilder.Services.AddSingleton(sp => sp.GetRequiredService<SeasonManager>());
                    siloBuilder.Services.AddSingleton(sp => sp.GetRequiredService<WeatherSystem>());
                    siloBuilder.Services.AddSingleton(sp => sp.GetRequiredService<SpawnManager>());
                    siloBuilder.Services.AddSingleton(sp => sp.GetRequiredService<IEventScheduler>());
                    
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
                    
                    // Register IHubContext<AgentDashboardHub> for AgentRunnerGrain
                    siloBuilder.Services.AddSingleton<IHubContext<Hubs.AgentDashboardHub>>(sp =>
                    {
                        var host = sp.GetRequiredService<IHost>();
                        return host.Services.GetRequiredService<IHubContext<Hubs.AgentDashboardHub>>();
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
            app.UseRouting();
            app.MapHub<GameHub>("/gamehub");
            app.MapHub<Hubs.AgentDashboardHub>("/agentDashboardHub"); // Map dashboard hub
            app.MapControllers(); // Map API controllers
            
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


