using Aetherium.Dashboard.Hubs;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.Components.Web;
using Orleans;
using Orleans.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddSignalR();

// Add Orleans client for connecting to the cluster
builder.Host.UseOrleansClient(client =>
{
    client.UseLocalhostClustering();
});

// Add telemetry service - will get Orleans client from DI
builder.Services.AddSingleton<AgentTelemetryService>(sp =>
{
    var orleansClient = sp.GetRequiredService<IClusterClient>();
    return new AgentTelemetryService(orleansClient);
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

// Map SignalR hub
app.MapHub<AgentDashboardHub>("/agentDashboardHub");

app.MapRazorPages();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();

