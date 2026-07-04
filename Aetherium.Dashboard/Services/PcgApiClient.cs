using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Aetherium.Model.Pcg;

namespace Aetherium.Dashboard.Services
{
    /// <summary>
    /// Client for calling WorldGenCLI REST API.
    /// </summary>
    public class PcgApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public PcgApiClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _baseUrl = "http://localhost:5000/api"; // Default port
        }

        public PcgApiClient(HttpClient httpClient, string baseUrl)
        {
            _httpClient = httpClient;
            _baseUrl = baseUrl;
        }

        public async Task<List<GeneratorInfo>> GetGeneratorsAsync()
        {
            var response = await _httpClient.GetFromJsonAsync<List<GeneratorInfo>>($"{_baseUrl}/generators");
            return response ?? new List<GeneratorInfo>();
        }

        public async Task<ConstraintDescriptor> GetConstraintsSchemaAsync(string generatorId)
        {
            var response = await _httpClient.GetFromJsonAsync<ConstraintDescriptor>($"{_baseUrl}/generators/{generatorId}/constraints-schema");
            return response ?? new ConstraintDescriptor { GeneratorId = generatorId };
        }

        public async Task<List<string>> GetTemplateNamesAsync()
        {
            var response = await _httpClient.GetFromJsonAsync<List<string>>($"{_baseUrl}/templates");
            return response ?? new List<string>();
        }

        public async Task<TemplateDto> GetTemplateAsync(string name)
        {
            var response = await _httpClient.GetFromJsonAsync<TemplateDto>($"{_baseUrl}/templates/{name}");
            return response ?? throw new Exception($"Template '{name}' not found");
        }

        public async Task<bool> SaveTemplateAsync(TemplateDto template)
        {
            var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/templates", template);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> DeleteTemplateAsync(string name)
        {
            var response = await _httpClient.DeleteAsync($"{_baseUrl}/templates/{name}");
            return response.IsSuccessStatusCode;
        }

        public async Task<GenerateResponse> GenerateWorldAsync(GenerateRequest request)
        {
            var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/generate", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<GenerateResponse>() 
                ?? throw new Exception("Failed to deserialize generation response");
        }

        public async Task<AbTestResponse> GenerateAbTestAsync(AbTestRequest request)
        {
            var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/generate/abtest", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<AbTestResponse>() 
                ?? throw new Exception("Failed to deserialize A/B test response");
        }
    }
}

