using Orleans;
using Orleans.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Aetherium.Server.MultiWorld
{
    /// <summary>
    /// Orleans grain caching content definitions.
    /// </summary>
    public class ContentCatalogGrain : Grain, IContentCatalogGrain
    {
        private readonly IPersistentState<ContentCatalogState> _state;

        public ContentCatalogGrain(
            [PersistentState("content", "worldStore")] IPersistentState<ContentCatalogState> state)
        {
            _state = state;
        }

        public override Task OnActivateAsync(CancellationToken cancellationToken)
        {
            if (_state.State == null)
            {
                _state.State = new ContentCatalogState
                {
                    Content = new Dictionary<string, ContentEntry>()
                };
            }

            return base.OnActivateAsync(cancellationToken);
        }

        public Task<T?> GetContentAsync<T>(string contentId) where T : class
        {
            var key = GetKey(typeof(T).Name, contentId);
            if (_state.State.Content.TryGetValue(key, out var entry))
            {
                try
                {
                    var content = JsonSerializer.Deserialize<T>(entry.JsonData);
                    return Task.FromResult<T?>(content);
                }
                catch
                {
                    return Task.FromResult<T?>(null);
                }
            }

            return Task.FromResult<T?>(null);
        }

        public async Task CacheContentAsync<T>(string contentId, T content) where T : class
        {
            var key = GetKey(typeof(T).Name, contentId);
            var jsonData = JsonSerializer.Serialize(content);
            
            _state.State.Content[key] = new ContentEntry
            {
                ContentId = contentId,
                ContentType = typeof(T).Name,
                JsonData = jsonData,
                CachedAt = DateTime.UtcNow
            };

            await _state.WriteStateAsync();
        }

        public Task<IReadOnlyList<string>> ListContentIdsAsync(string contentType)
        {
            var ids = _state.State.Content.Values
                .Where(e => e.ContentType == contentType)
                .Select(e => e.ContentId)
                .Distinct()
                .ToList();

            return Task.FromResult<IReadOnlyList<string>>(ids);
        }

        private static string GetKey(string contentType, string contentId)
        {
            return $"{contentType}:{contentId}";
        }
    }

    /// <summary>
    /// State for the content catalog grain.
    /// </summary>
    [GenerateSerializer]
    public class ContentCatalogState
    {
        [Id(0)] public Dictionary<string, ContentEntry> Content { get; set; } = new Dictionary<string, ContentEntry>();
    }

    /// <summary>
    /// Entry in the content catalog.
    /// </summary>
    [GenerateSerializer]
    public class ContentEntry
    {
        [Id(0)] public string ContentId { get; set; } = string.Empty;
        [Id(1)] public string ContentType { get; set; } = string.Empty;
        [Id(2)] public string JsonData { get; set; } = string.Empty;
        [Id(3)] public DateTime CachedAt { get; set; }
    }
}

