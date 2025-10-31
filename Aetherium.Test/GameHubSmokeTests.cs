using System;
using System.Threading.Tasks;
using NUnit.Framework;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;

namespace Aetherium.Test
{
    [TestFixture]
    public class GameHubSmokeTests
    {
        [Test]
        public async Task Connect_And_Disconnect_GameHub()
        {
            Environment.SetEnvironmentVariable("DISABLE_ORLEANS", "1");
            await using var factory = new WebApplicationFactory<Aetherium.Server.Program>();
            using var server = factory.Server;

            var baseUrl = factory.Server.BaseAddress.ToString().TrimEnd('/');
            var hubUrl = baseUrl + "/gamehub";

            var connection = new HubConnectionBuilder()
                .WithUrl(hubUrl, options =>
                {
                    options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                })
                .WithAutomaticReconnect()
                .Build();

            await connection.StartAsync();
            Assert.AreEqual(HubConnectionState.Connected, connection.State);

            await connection.StopAsync();
            Assert.AreEqual(HubConnectionState.Disconnected, connection.State);
        }
    }
}



