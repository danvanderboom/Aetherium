using Aetherium.Dashboard;
using Aetherium.Dashboard.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Configuration;
using Orleans;
using Orleans.Configuration;
using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddSignalR();

// Add reverse proxy for forwarding SignalR hub to server
builder.Services.AddReverseProxy()
    .LoadFromMemory(
        routes: new[]
        {
            new RouteConfig
            {
                RouteId = "agentHub",
                ClusterId = "server",
                Match = new RouteMatch
                {
                    Path = "/agentDashboardHub/{**catchall}"
                }
            }
        },
        clusters: new[]
        {
            new ClusterConfig
            {
                ClusterId = "server",
                Destinations = new Dictionary<string, DestinationConfig>
                {
                    ["destination1"] = new()
                    {
                        Address = "http://localhost:5000"
                    }
                }
            }
        });

// Add Orleans client for connecting to the cluster
builder.Host.UseOrleansClient(client =>
{
    client.UseLocalhostClustering();
    // Configure cluster options to match server configuration
    client.Configure<ClusterOptions>(opts =>
    {
        opts.ClusterId = "dev";
        opts.ServiceId = "Aetherium";
    });
});

// Add hosted service to handle Orleans client connection with retry/backoff
builder.Services.AddHostedService<OrleansClientConnectionService>();

// Add telemetry service - will get Orleans client from DI
builder.Services.AddSingleton<AgentTelemetryService>(sp =>
{
    var orleansClient = sp.GetRequiredService<IClusterClient>();
    return new AgentTelemetryService(orleansClient);
});

// Add PCG API client
builder.Services.AddHttpClient<PcgApiClient>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5000/api/");
    client.Timeout = TimeSpan.FromMinutes(5); // Long timeout for generation
});

// Add Management API client
builder.Services.AddHttpClient<ManagementApiClient>((sp, client) =>
{
    client.BaseAddress = new Uri("http://localhost:5000/");
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddTypedClient((client, sp) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    return new ManagementApiClient(client, config);
});

// Add CORS for SignalR
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseCors();

app.MapRazorPages();
app.MapBlazorHub();
app.MapReverseProxy(); // Map reverse proxy for SignalR hub forwarding
app.MapFallbackToPage("/_Host");

app.Run();

