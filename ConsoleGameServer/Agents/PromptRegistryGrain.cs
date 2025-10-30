using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Orleans;

namespace ConsoleGameServer.Agents
{
    /// <summary>
    /// Grain implementation that wraps PromptRegistry with Orleans access.
    /// </summary>
    public sealed class PromptRegistryGrain : Grain, IPromptRegistryGrain
    {
        private readonly PromptRegistry _registry;

        public PromptRegistryGrain(PromptRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public Task AddOrUpdateAsync(string name, string content)
        {
            _registry.SetTemplate(name, content);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<string>> ListAsync()
        {
            var names = new List<string>(_registry.ListTemplates());
            return Task.FromResult((IReadOnlyList<string>)names);
        }

        public Task<string> GetAsync(string name)
        {
            var content = _registry.GetTemplate(name);
            if (content == null)
                throw new ArgumentException($"Prompt template '{name}' not found.");
            return Task.FromResult(content);
        }
    }
}


