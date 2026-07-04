using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Aetherium.Model.Pcg;

namespace WorldGenCLI.Services
{
    /// <summary>
    /// Manages PCG template persistence.
    /// </summary>
    public sealed class TemplateLibrary
    {
        private readonly string _templatesDirectory;

        public TemplateLibrary(string? templatesDirectory = null)
        {
            _templatesDirectory = templatesDirectory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Aetherium",
                "PCGTemplates");

            if (!Directory.Exists(_templatesDirectory))
            {
                Directory.CreateDirectory(_templatesDirectory);
            }
        }

        public List<string> ListTemplateNames()
        {
            if (!Directory.Exists(_templatesDirectory))
                return new List<string>();

            return Directory
                .GetFiles(_templatesDirectory, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(name => !string.IsNullOrEmpty(name))
                .Select(name => name!)
                .ToList();
        }

        public TemplateDto? LoadTemplate(string name)
        {
            var path = GetTemplatePath(name);
            if (!File.Exists(path))
                return null;

            try
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<TemplateDto>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to load template '{name}': {ex.Message}");
                return null;
            }
        }

        public bool SaveTemplate(TemplateDto template)
        {
            var path = GetTemplatePath(template.Name);
            try
            {
                var json = JsonSerializer.Serialize(template, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(path, json);
                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to save template '{template.Name}': {ex.Message}");
                return false;
            }
        }

        public bool DeleteTemplate(string name)
        {
            var path = GetTemplatePath(name);
            if (!File.Exists(path))
                return false;

            try
            {
                File.Delete(path);
                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to delete template '{name}': {ex.Message}");
                return false;
            }
        }

        private string GetTemplatePath(string name)
        {
            var safeName = string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
            return Path.Combine(_templatesDirectory, $"{safeName}.json");
        }
    }
}

