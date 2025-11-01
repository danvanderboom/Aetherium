using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Aetherium.Model;
using Microsoft.Extensions.Configuration;

namespace Aetherium.Dashboard.Services
{
    /// <summary>
    /// Client for calling Management REST API.
    /// </summary>
    public class ManagementApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly string? _apiKey;

        public ManagementApiClient(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            var baseUrlFromConfig = configuration["ManagementApi:BaseUrl"];
            _baseUrl = !string.IsNullOrEmpty(baseUrlFromConfig) 
                ? baseUrlFromConfig 
                : "api/management";
            _apiKey = configuration["ManagementApi:ApiKey"];
            
            if (!string.IsNullOrEmpty(_apiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("X-Dashboard-ApiKey", _apiKey);
            }
        }

        public async Task<List<WorldInfoDto>> GetWorldsAsync()
        {
            var response = await _httpClient.GetFromJsonAsync<List<WorldInfoDto>>($"{_baseUrl}/worlds");
            return response ?? new List<WorldInfoDto>();
        }

        public async Task<WorldInfoDto?> GetWorldAsync(string worldId)
        {
            try
            {
                var response = await _httpClient.GetFromJsonAsync<WorldInfoDto>($"{_baseUrl}/worlds/{worldId}");
                return response;
            }
            catch (HttpRequestException)
            {
                return null;
            }
        }

        public async Task<WorldInfoDto> CreateWorldAsync(CreateWorldRequestDto request)
        {
            var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/worlds", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<WorldInfoDto>() 
                ?? throw new Exception("Failed to deserialize world response");
        }

        public async Task<bool> ShutdownWorldAsync(string worldId)
        {
            var response = await _httpClient.DeleteAsync($"{_baseUrl}/worlds/{worldId}");
            return response.IsSuccessStatusCode;
        }

        public async Task<List<SessionInfoDto>> GetSessionsAsync()
        {
            var response = await _httpClient.GetFromJsonAsync<List<SessionInfoDto>>($"{_baseUrl}/sessions");
            return response ?? new List<SessionInfoDto>();
        }

        public async Task<SessionInfoDto?> GetSessionAsync(string sessionId)
        {
            try
            {
                var response = await _httpClient.GetFromJsonAsync<SessionInfoDto>($"{_baseUrl}/sessions/{sessionId}");
                return response;
            }
            catch (HttpRequestException)
            {
                return null;
            }
        }

        public async Task<bool> StopSessionAsync(string sessionId)
        {
            var response = await _httpClient.DeleteAsync($"{_baseUrl}/sessions/{sessionId}");
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> AttachAgentAsync(string sessionId, AttachAgentRequestDto request)
        {
            var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/sessions/{sessionId}/attach", request);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> DetachAgentAsync(string sessionId, DetachAgentRequestDto request)
        {
            var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/sessions/{sessionId}/detach", request);
            return response.IsSuccessStatusCode;
        }

        public async Task<List<AgentInfoDto>> GetAgentsAsync()
        {
            var response = await _httpClient.GetFromJsonAsync<List<AgentInfoDto>>($"{_baseUrl}/agents");
            return response ?? new List<AgentInfoDto>();
        }

        public async Task<ManagementStats?> GetStatsAsync()
        {
            try
            {
                var response = await _httpClient.GetFromJsonAsync<ManagementStats>($"{_baseUrl}/stats");
                return response;
            }
            catch (HttpRequestException)
            {
                return null;
            }
        }

        public bool HasApiKey => !string.IsNullOrEmpty(_apiKey);
    }

    /// <summary>
    /// Statistics from management API.
    /// </summary>
    public class ManagementStats
    {
        public int ActiveAgents { get; set; }
        public int ActiveSessions { get; set; }
        public int ActiveWorlds { get; set; }
        public int TotalWorlds { get; set; }
    }
}

