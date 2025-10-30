using System.Collections.Generic;
using System.Threading.Tasks;
using Orleans;

namespace ConsoleGameServer.Agents
{
    /// <summary>
    /// Grain interface for managing prompt templates on the server.
    /// </summary>
    public interface IPromptRegistryGrain : IGrainWithStringKey
    {
        Task AddOrUpdateAsync(string name, string content);
        Task<IReadOnlyList<string>> ListAsync();
        Task<string> GetAsync(string name);
    }
}


