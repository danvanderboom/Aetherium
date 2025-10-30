using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ConsoleGameServer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services
            builder.Services.AddSignalR();
            builder.Services.AddSingleton<GameSessionManager>();

            // Configure URLs
            builder.WebHost.UseUrls("http://localhost:5000");

            var app = builder.Build();

            // Configure middleware
            app.MapHub<GameHub>("/gamehub");

            Console.WriteLine("Console Game Server starting on http://localhost:5000");
            Console.WriteLine("Waiting for client connections...");

            app.Run();
        }
    }
}

