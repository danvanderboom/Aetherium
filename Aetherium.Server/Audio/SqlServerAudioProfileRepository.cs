using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Aetherium.Server.Audio
{
    /// <summary>
    /// SQL Server-based repository for biome audio profiles
    /// TODO: Implement SQL Server persistence
    /// </summary>
    public class SqlServerAudioProfileRepository : IAudioProfileRepository
    {
        private readonly string _connectionString;

        public SqlServerAudioProfileRepository(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        public Task<BiomeAudioProfile?> GetProfileAsync(string id)
        {
            // TODO: Implement SQL Server query
            throw new NotImplementedException("SQL Server audio profile repository not yet implemented. Use JsonAudioProfileRepository for now.");
        }

        public Task<IReadOnlyList<BiomeAudioProfile>> GetAllProfilesAsync()
        {
            // TODO: Implement SQL Server query
            throw new NotImplementedException("SQL Server audio profile repository not yet implemented. Use JsonAudioProfileRepository for now.");
        }

        public Task SaveProfileAsync(BiomeAudioProfile profile)
        {
            // TODO: Implement SQL Server insert/update
            throw new NotImplementedException("SQL Server audio profile repository not yet implemented. Use JsonAudioProfileRepository for now.");
        }

        public Task DeleteProfileAsync(string id)
        {
            // TODO: Implement SQL Server delete
            throw new NotImplementedException("SQL Server audio profile repository not yet implemented. Use JsonAudioProfileRepository for now.");
        }

        public Task InitializeAsync()
        {
            // TODO: Verify connection and create tables if needed
            return Task.CompletedTask;
        }
    }
}

