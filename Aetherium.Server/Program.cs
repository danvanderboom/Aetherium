using System;
using System.IO;
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
            var b2cConfigured = !string.IsNullOrEmpty(domain) && !string.IsNullOrEmpty(clientId) && !string.IsNullOrEmpty(tenantId);
            if (b2cConfigured)
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
                    // GameClient policy: when B2C is configured, the game hub requires an
                    // authenticated user. The [Authorize(Policy = "GameClient")] attribute on
                    // GameHub is permanent; the policy adapts to deployment mode so dev runs
                    // without B2C still allow anonymous play.
                    options.AddPolicy("GameClient", policy => policy.RequireAuthenticatedUser());
                    options.DefaultPolicy = new AuthorizationPolicyBuilder()
                        .RequireAuthenticatedUser()
                        .Build();
                });

                Console.WriteLine("Azure AD B2C authentication enabled (GameHub gated by GameClient policy)");
            }
            else
            {
                // No B2C: register a GameClient policy that allows anonymous so the
                // [Authorize(Policy = "GameClient")] attribute on GameHub is a no-op in dev.
                // We still need authorization services registered for the attribute to resolve.
                builder.Services.AddAuthorization(options =>
                {
                    options.AddPolicy("GameClient", policy => policy.RequireAssertion(_ => true));
                });
                Console.WriteLine("Azure AD B2C not configured - GameHub running in open/dev mode");
            }

            builder.Services.AddControllers(); // Add API controllers
            builder.Services.AddSingleton<IRandomSource, DefaultRandomSource>();
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

            // Persistence configuration (snapshot compaction cadence + threshold).
            builder.Services.Configure<Aetherium.Server.Persistence.PersistenceOptions>(
                builder.Configuration.GetSection("Persistence"));
            
            // Add simulation services
            builder.Services.AddSingleton<WorldClock>();

            // Persistent world snapshot store: SQLite when ORLEANS_STORAGE=sqlite
            // (or AETHERIUM_DATA_DIR is set and ORLEANS_STORAGE is unset), else in-memory.
            var (storageMode, sqliteConnectionString) = ResolveStorageConfiguration();
            if (storageMode == "sqlite")
            {
                builder.Services.AddSingleton<IWorldSnapshotStore>(sp =>
                {
                    var serializer = sp.GetRequiredService<Orleans.Serialization.Serializer>();
                    var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SqliteWorldSnapshotStore>>();
                    return new SqliteWorldSnapshotStore(sqliteConnectionString!, serializer, logger);
                });
            }
            else
            {
                builder.Services.AddSingleton<IWorldSnapshotStore, MemoryWorldSnapshotStore>();
            }
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

            // Add world tick service. When Orleans is enabled, register it as a hosted
            // service so it actually runs. When Orleans is disabled (test mode), keep it as
            // a plain singleton so anything resolving it still gets a usable instance.
            if (!disableOrleans)
            {
                builder.Services.AddSingleton<WorldTickService>(sp =>
                {
                    var grainFactory = sp.GetRequiredService<Orleans.IGrainFactory>();
                    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SimulationOptions>>();
                    return new WorldTickService(grainFactory, options);
                });
                builder.Services.AddHostedService(sp => sp.GetRequiredService<WorldTickService>());
            }
            else
            {
                // No IGrainFactory available in disabled-Orleans test runs; expose a no-op
                // singleton placeholder so any test wiring that resolves WorldTickService
                // doesn't blow up. The hosted service is intentionally not registered.
                builder.Services.AddSingleton<WorldTickService>(sp =>
                {
                    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SimulationOptions>>();
                    return new WorldTickService(null!, options);
                });
            }
            
            // Add world generation services. Populate the registry at construction via
            // DiscoverTypes — otherwise GetGenerator returns null in the co-hosted server and
            // GameMapGrain silently falls back to AdvancedDungeonGenerator for every request,
            // ignoring the requested generator type (RoomsAndCorridors, GridCity, Maze, …).
            builder.Services.AddSingleton<MapGeneratorRegistry>(sp =>
            {
                var registry = new MapGeneratorRegistry();
                registry.DiscoverTypes(typeof(Aetherium.WorldGen.IMapGenerator).Assembly);
                return registry;
            });
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

            // Game definition bundles (add-game-definition-loader): YAML-defined games under
            // Data/Games, each instantiable as any number of concurrently-running worlds.
            builder.Services.AddSingleton<Aetherium.Server.Games.GameDefinitionRegistry>(sp =>
            {
                var gamesPath = Environment.GetEnvironmentVariable("GAMES_PATH") ?? "./Data/Games";
                var registry = new Aetherium.Server.Games.GameDefinitionRegistry(gamesPath);
                registry.LoadAll();
                foreach (var diagnostic in registry.Diagnostics)
                    Console.WriteLine($"[Program] Game definition diagnostic: {diagnostic}");
                return registry;
            });
            
            // Add prefab library
            var environment = builder.Environment.EnvironmentName;
            var useFileStorage = environment == "Development" || 
                                Environment.GetEnvironmentVariable("PREFAB_STORAGE") == "file";
            
            builder.Services.AddSingleton<PrefabLibrary>(sp =>
            {
                var library = new PrefabLibrary(useFileStorage);
                // Load prefabs from file system in development. LoadFromDirectory tolerates a
                // missing directory (logs and returns) and catches per-file errors, so this is
                // safe to call unconditionally in file mode.
                if (useFileStorage)
                {
                    var prefabPath = Environment.GetEnvironmentVariable("PREFAB_PATH") ?? "./Data/Prefabs";
                    Console.WriteLine($"Loading prefabs from: {prefabPath}");
                    library.LoadFromDirectory(prefabPath);
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
                    
                    // Configure grain storage. Resolution centralized in ResolveStorageConfiguration
                    // so the snapshot-store DI binding (above) and the grain-storage providers stay in sync.
                    // Every [PersistentState(..., "<storeName>")] declared by a grain must have a
                    // matching provider here or that grain throws on activation. The full set:
                    //   narrativeStore  — NarrativeStateGrain
                    //   worldStore      — WorldGrain / ClusterGrain
                    //   mapStore        — GameMapGrain / MapRegionGrain
                    //   metaStore       — MetaProgressionGrain (previously unregistered → activation failed)
                    var grainStoreNames = new[] { "narrativeStore", "worldStore", "mapStore", "metaStore" };
                    if (storageMode == "memory")
                    {
                        Console.WriteLine("Using in-memory grain storage (development mode)");
                        foreach (var storeName in grainStoreNames)
                        {
                            siloBuilder.AddMemoryGrainStorage(storeName);
                        }
                    }
                    else if (storageMode == "sqlite")
                    {
                        Console.WriteLine($"Using SQLite grain storage at {sqliteConnectionString}");
                        foreach (var storeName in grainStoreNames)
                        {
                            var capturedName = storeName;
                            var capturedConnString = sqliteConnectionString!;
                            siloBuilder.Services.AddKeyedSingleton<Orleans.Storage.IGrainStorage>(capturedName, (sp, _) =>
                            {
                                var serializer = sp.GetRequiredService<Orleans.Serialization.Serializer>();
                                var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SqliteGrainStorage>>();
                                return new SqliteGrainStorage(capturedName, capturedConnString, serializer, logger);
                            });
                        }
                    }

                    // Configure Orleans Streams for world events. The memory stream provider's
                    // pubsub requires a grain storage named "PubSubStore" (or "Default"); without
                    // it the silo fails its configuration validation at startup. PubSub state is
                    // transient coordination, so an in-memory store is correct even under SQLite.
                    siloBuilder.AddMemoryGrainStorage("PubSubStore");
                    siloBuilder.AddMemoryStreams("Default");
                    
                    // NOTE: In Orleans co-hosting, `siloBuilder.Services` IS the ASP.NET Core
                    // host's IServiceCollection — grains resolve services from the very same
                    // container the web host uses. Every host singleton registered above
                    // (GameSessionManager, WorldClock/SeasonManager/WeatherSystem/SpawnManager,
                    // IEventScheduler, PromptRegistry, MapGeneratorRegistry, MapValidator,
                    // PrefabLibrary, AgentToolRegistry, the HubWorld services, IWorldHost) plus
                    // the framework-provided IHubContext<> instances and Orleans's own
                    // IClusterClient/IGrainFactory are therefore already visible to grains with
                    // no bridging required.
                    //
                    // A previous "bridge" here re-registered each of those services with a
                    // factory that resolved the same service type. Because MS.DI applies
                    // last-registration-wins, each such descriptor resolved *itself*, producing
                    // unbounded self-recursion that hung the server at startup (the resolution of
                    // IClusterClient during host build never returned, so the startup banner
                    // never printed). The bridge is deliberately removed — do not reintroduce it.

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
            
            // Add auth middleware. UseAuthentication is conditional (no scheme = nothing to do),
            // but UseAuthorization runs unconditionally so [Authorize(Policy="GameClient")] on
            // GameHub resolves either way — the policy itself decides whether to allow anonymous.
            var appAzureAdSection = app.Configuration.GetSection("AzureAdB2C");
            if (!string.IsNullOrEmpty(appAzureAdSection["Domain"]))
            {
                app.UseAuthentication();
            }
            app.UseAuthorization();
            
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

        /// <summary>
        /// Decides storage mode ("memory" or "sqlite") from environment variables and returns
        /// the resolved SQLite connection string when applicable. Defaults to "memory" unless
        /// <c>ORLEANS_STORAGE=sqlite</c> is set, or <c>AETHERIUM_DATA_DIR</c> is set and
        /// <c>ORLEANS_STORAGE</c> is unset. Throws on unknown values.
        /// </summary>
        private static (string mode, string? connectionString) ResolveStorageConfiguration()
        {
            var dataDir = Environment.GetEnvironmentVariable("AETHERIUM_DATA_DIR");
            var defaultStorage = string.IsNullOrEmpty(dataDir) ? "memory" : "sqlite";
            var storageType = Environment.GetEnvironmentVariable("ORLEANS_STORAGE") ?? defaultStorage;

            if (storageType == "memory") return ("memory", null);
            if (storageType == "sqlite")
            {
                var resolvedDir = string.IsNullOrEmpty(dataDir)
                    ? Path.Combine(AppContext.BaseDirectory, "aetherium-data")
                    : dataDir;
                var dbPath = Path.Combine(resolvedDir, "aetherium.db");
                return ("sqlite", $"Data Source={dbPath};Cache=Shared");
            }
            throw new InvalidOperationException(
                $"Unknown ORLEANS_STORAGE value: {storageType}. Expected 'memory' or 'sqlite'.");
        }
    }
}


