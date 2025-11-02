using Orleans;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Aetherium.Server.MultiWorld
{
    /// <summary>
    /// Orleans grain caching content definitions (world templates, dungeons, events, etc.).
    /// </summary>
    public interface IContentCatalogGrain : IGrainWithGuidKey
    {
        /// <summary>
        /// Gets a content definition by ID and type.
        /// </summary>
        Task<T?> GetContentAsync<T>(string contentId) where T : class;

        /// <summary>
        /// Caches content definitions.
        /// </summary>
        Task CacheContentAsync<T>(string contentId, T content) where T : class;

        /// <summary>
        /// Lists all content IDs of a given type.
        /// </summary>
        Task<IReadOnlyList<string>> ListContentIdsAsync(string contentType);
    }
}

