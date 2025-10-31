using System.Collections.Generic;
using System.Threading.Tasks;

namespace Aetherium.Server.Audio
{
    /// <summary>
    /// Repository interface for biome audio profiles
    /// </summary>
    public interface IAudioProfileRepository
    {
        /// <summary>
        /// Get a profile by ID
        /// </summary>
        Task<BiomeAudioProfile?> GetProfileAsync(string id);

        /// <summary>
        /// Get all available profiles
        /// </summary>
        Task<IReadOnlyList<BiomeAudioProfile>> GetAllProfilesAsync();

        /// <summary>
        /// Save or update a profile
        /// </summary>
        Task SaveProfileAsync(BiomeAudioProfile profile);

        /// <summary>
        /// Delete a profile by ID
        /// </summary>
        Task DeleteProfileAsync(string id);

        /// <summary>
        /// Initialize the repository (load from source)
        /// </summary>
        Task InitializeAsync();
    }
}

