using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aetherctl.Config
{
    /// <summary>
    /// Configuration for a single Aetherium server entry.
    /// </summary>
    public class ServerConfig
    {
        public string Name { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = string.Empty;
        public B2CConfig B2C { get; set; } = new B2CConfig();
    }

    /// <summary>
    /// Azure AD B2C configuration for server authentication.
    /// </summary>
    public class B2CConfig
    {
        public string Tenant { get; set; } = string.Empty;
        public string Policy { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string Scopes { get; set; } = string.Empty;
    }

    /// <summary>
    /// Root configuration containing all server entries.
    /// </summary>
    public class AetherctlConfig
    {
        public Dictionary<string, ServerConfig> Servers { get; set; } = new Dictionary<string, ServerConfig>();
        public string? CurrentServer { get; set; }
    }

    /// <summary>
    /// Manages Aetherctl configuration persistence.
    /// </summary>
    public static class ConfigManager
    {
        private static string GetConfigDirectory()
        {
            // Explicit override for tests and multi-profile operator setups.
            var overrideDir = Environment.GetEnvironmentVariable("AETHERCTL_CONFIG_DIR");
            if (!string.IsNullOrEmpty(overrideDir))
            {
                return overrideDir;
            }

            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(appData, "Aetherctl");
            }
            else
            {
                var xdgConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
                if (string.IsNullOrEmpty(xdgConfig))
                {
                    xdgConfig = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
                }
                return Path.Combine(xdgConfig, "aetherctl");
            }
        }

        private static string GetConfigPath()
        {
            return Path.Combine(GetConfigDirectory(), "config.json");
        }

        public static AetherctlConfig Load()
        {
            var configPath = GetConfigPath();
            if (!File.Exists(configPath))
            {
                return new AetherctlConfig();
            }

            try
            {
                var json = File.ReadAllText(configPath);
                return JsonSerializer.Deserialize<AetherctlConfig>(json) ?? new AetherctlConfig();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error loading config: {ex.Message}");
                return new AetherctlConfig();
            }
        }

        public static void Save(AetherctlConfig config)
        {
            var configDir = GetConfigDirectory();
            Directory.CreateDirectory(configDir);

            var configPath = GetConfigPath();
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            var json = JsonSerializer.Serialize(config, options);
            File.WriteAllText(configPath, json);
        }

        public static ServerConfig? GetServer(string name)
        {
            var config = Load();
            return config.Servers.TryGetValue(name, out var server) ? server : null;
        }

        public static ServerConfig? GetCurrentServer()
        {
            var config = Load();
            if (string.IsNullOrEmpty(config.CurrentServer))
                return null;
            return GetServer(config.CurrentServer);
        }

        public static void SetCurrentServer(string name)
        {
            var config = Load();
            config.CurrentServer = name;
            Save(config);
        }

        public static void AddServer(ServerConfig serverConfig)
        {
            var config = Load();
            config.Servers[serverConfig.Name] = serverConfig;
            Save(config);
        }

        public static void RemoveServer(string name)
        {
            var config = Load();
            config.Servers.Remove(name);
            if (config.CurrentServer == name)
            {
                config.CurrentServer = null;
            }
            Save(config);
        }

        public static List<string> ListServers()
        {
            var config = Load();
            return new List<string>(config.Servers.Keys);
        }
    }
}

