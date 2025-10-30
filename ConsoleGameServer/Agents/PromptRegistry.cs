using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ConsoleGameServer.Agents
{
    /// <summary>
    /// Registry for loading and managing prompt templates from markdown files.
    /// Templates are stored in the Prompts/ directory and can be edited at runtime.
    /// </summary>
    public sealed class PromptRegistry
    {
        private readonly string _promptsDirectory;
        private readonly Dictionary<string, string> _templates = new Dictionary<string, string>();

        public PromptRegistry(string? promptsDirectory = null)
        {
            _promptsDirectory = promptsDirectory ?? Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Prompts");
        }

        /// <summary>
        /// Loads all prompt templates from the Prompts directory.
        /// </summary>
        public void LoadTemplates()
        {
            _templates.Clear();

            if (!Directory.Exists(_promptsDirectory))
            {
                Directory.CreateDirectory(_promptsDirectory);
                return;
            }

            var files = Directory.GetFiles(_promptsDirectory, "*.md");
            foreach (var file in files)
            {
                var name = Path.GetFileNameWithoutExtension(file);
                var content = File.ReadAllText(file);
                _templates[name] = content;
            }
        }

        /// <summary>
        /// Gets a prompt template by name. Returns null if not found.
        /// </summary>
        public string? GetTemplate(string name)
        {
            _templates.TryGetValue(name, out var template);
            return template;
        }

        /// <summary>
        /// Lists all available template names.
        /// </summary>
        public IEnumerable<string> ListTemplates() => _templates.Keys;

        /// <summary>
        /// Adds or updates a prompt template.
        /// </summary>
        public void SetTemplate(string name, string content)
        {
            _templates[name] = content;
            
            // Optionally save to file
            var filePath = Path.Combine(_promptsDirectory, $"{name}.md");
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllText(filePath, content);
        }

        /// <summary>
        /// Reloads templates from disk (useful for hot-reload scenarios).
        /// </summary>
        public void ReloadTemplates()
        {
            LoadTemplates();
        }
    }
}

