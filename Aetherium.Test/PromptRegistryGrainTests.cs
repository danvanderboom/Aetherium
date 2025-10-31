using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using Microsoft.Extensions.DependencyInjection;
using Orleans.TestingHost;
using Orleans.Hosting;
using Orleans.Hosting;
using Aetherium.Server.Agents;

namespace Aetherium.Test
{
    [TestFixture, Ignore("Enable when Orleans codegen packages are available in CI environment")]
    public class PromptRegistryGrainTests
    {
        private TestCluster _cluster = null!;

        private sealed class SiloConfigurator : ISiloConfigurator
        {
            public void Configure(ISiloBuilder siloBuilder)
            {
                var temp = Path.Combine(Path.GetTempPath(), "prompts-" + Guid.NewGuid().ToString("N"));
                var registry = new PromptRegistry(temp);
                registry.LoadTemplates();
                siloBuilder.ConfigureServices(services =>
                {
                    services.AddSingleton(registry);
                });
            }
        }

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            var builder = new TestClusterBuilder(1);
            builder.AddSiloBuilderConfigurator<SiloConfigurator>();
            _cluster = builder.Build();
            _cluster.Deploy();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _cluster.StopAllSilos();
        }

        [Test]
        public async Task Add_And_List_Prompts()
        {
            var grain = _cluster.GrainFactory.GetGrain<IPromptRegistryGrain>("registry");
            var name = "explorer-test";
            var content = "# Explorer Test\nHello";
            await grain.AddOrUpdateAsync(name, content);

            var names = await grain.ListAsync();
            CollectionAssert.Contains(names, name);

            var fetched = await grain.GetAsync(name);
            StringAssert.StartsWith("# Explorer", fetched);
        }
    }
}



