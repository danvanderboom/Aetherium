using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Identity.Web;
using Orleans;
using Orleans.Hosting;
using Aetherium.Server.Agents;
using Aetherium.Server.Events;
using Aetherium.Server.Management;
using Aetherium.Server.Middleware;
using Aetherium.Server.Persistence;
using Aetherium.Server.Simulation;
using Aetherium.WorldGen;
using Aetherium.WorldGen.Prefabs;
using Aetherium.WorldBuilders.Validation;
using Microsoft.AspNetCore.SignalR;
using UFX.Orleans.SignalRBackplane;

namespace Aetherium.Server
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services
            // Configure SignalR (with Orleans backplane if Orleans is enabled)
            var disableOrleans = Environment.GetEnvironmentVariable("DISABLE_ORLEANS") == "1";
            var signalRBuilder = builder.Services.AddSignalR(options =>
            {
                options.EnableDetailedErrors = builder.Environment.IsDevelopment();
            });
            
            // Add Orleans backplane if Orleans is enabled
            // UFX.Orleans.SignalRBackplane automatically configures when Orleans is co-hosted
            // No explicit configuration needed - the package will detect Orleans and configure SignalR hubs

            // Add Azure AD B2C authentication
            var azureAdSection = builder.Configuration.GetSection("AzureAdB2C");
            var instance = azureAdSection["Instance"];
            var domain = azureAdSection["Domain"];
            var tenantId = azureAdSection["TenantId"];
            var clientId = azureAdSection["ClientId"];
            var signUpSignInPolicyId = azureAdSection["SignUpSignInPolicyId"];
            var scopes = azureAdSection["Scopes"];

            // Only add authentication if B2C is configured
            if (!string.IsNullOrEmpty(domain) && !string.IsNullOrEmpty(clientId) && !string.IsNullOrEmpty(tenantId))
            {
                builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAdB2C"));
                
                // Configure additional options
                builder.Services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    options.TokenValidationParameters.NameClaimType = "name";
                });

                builder.Services.AddAuthorization(options =>
                {
                    options.AddPolicy("Admin", policy => policy.RequireClaim("roles", "Admin"));
                    options.DefaultPolicy = new AuthorizationPolicyBuilder()
                        .RequireAuthenticatedUser()
                        .Build();
                });

                Console.WriteLine("Azure AD B2C authentication enabled");
            }
            else
            {
                Console.WriteLine("Azure AD B2C not configured - authentication disabled");
            }

            builder.Services.AddControllers(); // Add API controllers
            builder.Services.AddSingleton<GameSessionManager>();
            
            // Register world hosting service (only when Orleans is enabled)
            if (!disableOrleans)
            {
                builder.Services.AddSingleton<Aetherium.Server.Services.IWorldHost>(sp =>
                {
                    var grainFactory = sp.GetRequiredService<Orleans.IGrainFactory>();
                    var clusterClient = sp.GetRequiredService<Orleans.IClusterClient>();
                    var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Aetherium.Server.Services.OrleansWorldHost>>();
                    return new Aetherium.Server.Services.OrleansWorldHost(grainFactory, clusterClient, logger);
                });
            }
            
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
                        var spawnModifier = new SpawnModifier(spawnManager, spawnProbabilityPerTick: 0.01, serviceProvider: sp);
                        registry.Register(spawnModifier);
                    }
                    
                    // Register BuilderModifier if BuilderAI is available
                    var builderAI = sp.GetService<BuilderAI>();
                    if (builderAI != null)
                    {
                        // BuilderModifier currently requires World access which is not easily available
                        // without the Func-based methods. For now, pass null and let it skip building
                        // until BuildStructureAsync is fully implemented.
                        var builderModifier = new BuilderModifier(builderAI, null);
                        registry.Register(builderModifier);
                    }
                    return registry;
                });
            builder.Services.AddSingleton<IEventScheduler, EventScheduler>();
            
            // Add Orleans co-hosting (can be disabled for tests via env var)
            // Note: disableOrleans already checked above

            // Add world tick service (background service to drive world ticks)
            builder.Services.AddSingleton<WorldTickService>(sp =>
            {
                var grainFactory = sp.GetService<Orleans.IGrainFactory>();
                if (grainFactory == null && !disableOrleans)
                {
                    // If Orleans is enabled, we'll need to resolve IGrainFactory from Orleans host
                    // For now, we'll create a wrapper that gets it from the Orleans host
                    // This will be resolved when Orleans starts
                }
                var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SimulationOptions>>();
                return new WorldTickService(grainFactory!, options);
            });
            // Note: WorldTickService is registered but not started as a hosted service
            // Manual ticking can be done via API or other mechanisms
            // Automatic ticking would require a world registry to track active worlds
            
            // Add world generation services
            builder.Services.AddSingleton<MapGeneratorRegistry>();
            builder.Services.AddSingleton<MapValidator>();
            
            // Add hub world services
            builder.Services.AddSingleton<Aetherium.Server.HubWorld.HubWorldLoader>(sp =>
            {
                var hubPath = Environment.GetEnvironmentVariable("HUB_PATH") ?? "./Data/Hubs";
                var loader = new Aetherium.Server.HubWorld.HubWorldLoader(hubPath);
                // Load hubs asynchronously - this will happen when service is first accessed
                _ = loader.LoadHubsAsync().ContinueWith(t =>
                {
                    if (t.IsFaulted)
                        Console.WriteLine($"[Program] Error loading hubs: {t.Exception?.GetBaseException().Message}");
                });
                return loader;
            });
            builder.Services.AddSingleton<Aetherium.Server.HubWorld.HubWorldGenerator>(sp =>
            {
                var loader = sp.GetRequiredService<Aetherium.Server.HubWorld.HubWorldLoader>();
                return new Aetherium.Server.HubWorld.HubWorldGenerator(loader);
            });
            builder.Services.AddSingleton<Aetherium.Server.HubWorld.HubTemplateResolver>(sp =>
            {
                var generator = sp.GetRequiredService<Aetherium.Server.HubWorld.HubWorldGenerator>();
                return new Aetherium.Server.HubWorld.HubTemplateResolver(generator);
            });
            
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
                    
                    // Configure Orleans Streams for world events
                    siloBuilder.AddMemoryStreams("Default");
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
                    
                    // Register hub world services for grains
                    siloBuilder.Services.AddSingleton(sp =>
                    {
                        var host = sp.GetRequiredService<IHost>();
                        return host.Services.GetRequiredService<Aetherium.Server.HubWorld.HubWorldLoader>();
                    });
                    siloBuilder.Services.AddSingleton(sp =>
                    {
                        var host = sp.GetRequiredService<IHost>();
                        return host.Services.GetRequiredService<Aetherium.Server.HubWorld.HubWorldGenerator>();
                    });
                    siloBuilder.Services.AddSingleton(sp =>
                    {
                        var host = sp.GetRequiredService<IHost>();
                        return host.Services.GetRequiredService<Aetherium.Server.HubWorld.HubTemplateResolver>();
                    });
                    
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

                    // Register IClusterClient for ManagementHub
                    siloBuilder.Services.AddSingleton<IClusterClient>(sp =>
                    {
                        var host = sp.GetRequiredService<IHost>();
                        return host.Services.GetRequiredService<IClusterClient>();
                    });
                    
                    // Register IWorldHost for grains (GameManagementGrain needs it) - only if it exists
                    siloBuilder.Services.AddSingleton<Aetherium.Server.Services.IWorldHost>(sp =>
                    {
                        var host = sp.GetRequiredService<IHost>();
                        var worldHost = host.Services.GetService<Aetherium.Server.Services.IWorldHost>();
                        if (worldHost == null)
                            throw new InvalidOperationException("IWorldHost service not found in host services");
                        return worldHost;
                    });
                    
                    // SignalR backplane with Orleans is configured via AddOrleans() on signalRBuilder above
                });
            }

            // Configure URLs
            var aspNetCoreUrls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
            if (!string.IsNullOrWhiteSpace(aspNetCoreUrls))
            {
                builder.WebHost.UseUrls(aspNetCoreUrls);
            }
            else
            {
                builder.WebHost.UseUrls("http://localhost:5000");
            }

            var app = builder.Build();

            // Configure middleware
            app.UseMiddleware<ApiKeyMiddleware>();
            app.UseRouting();
            
            // Add authentication/authorization middleware (only if B2C is configured)
            var appAzureAdSection = app.Configuration.GetSection("AzureAdB2C");
            if (!string.IsNullOrEmpty(appAzureAdSection["Domain"]))
            {
                app.UseAuthentication();
                app.UseAuthorization();
            }
            
            // Get IClusterClient for hubs that need it
            IClusterClient? clusterClient = null;
            if (!disableOrleans)
            {
                clusterClient = app.Services.GetService<Orleans.IClusterClient>();
                // If not available yet, try getting from host
                if (clusterClient == null)
                {
                    var host = app.Services.GetRequiredService<IHost>();
                    clusterClient = host.Services.GetService<Orleans.IClusterClient>();
                }
            }
            
            app.MapHub<GameHub>("/gamehub");
            app.MapHub<Hubs.AgentDashboardHub>("/agentDashboardHub"); // Map dashboard hub
            app.MapHub<Hubs.ManagementHub>("/managementHub"); // Map management hub for CLI
            
            app.MapControllers(); // Map API controllers
            
            // Dashboard endpoint for viewing active agents
            app.MapGet("/dashboard", () =>
            {
                // TODO: Query Orleans for active agent grains and return status
                return Microsoft.AspNetCore.Http.Results.Json(new { message = "Agent dashboard - TODO: Implement agent listing" });
            });

            var effectiveUrls = aspNetCoreUrls ?? "http://localhost:5000";
            Console.WriteLine($"Console Game Server starting on {effectiveUrls}");
            Console.WriteLine("Orleans silo co-hosted with ASP.NET Core");
            Console.WriteLine("Multi-world hosting enabled - use CLI to create worlds");
            Console.WriteLine("Waiting for client connections...");
            Console.WriteLine("Agent dashboard: http://localhost:5000/dashboard");

            await app.RunAsync();
        }
    }
}


