using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Aetherium.Server.Agents
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
        /// Renders a template by substituting <c>{{variable}}</c> placeholders with the supplied
        /// values (placeholder names are matched case-insensitively and may include surrounding
        /// whitespace, e.g. <c>{{ goal }}</c>). Returns null if the template is not found.
        /// Placeholders with no matching variable are left intact so a missing value is visible
        /// rather than silently blanked.
        /// </summary>
        public string? Render(string name, IReadOnlyDictionary<string, string> variables)
        {
            var template = GetTemplate(name);
            if (template is null) return null;
            return Substitute(template, variables);
        }

        /// <summary>
        /// Substitutes <c>{{variable}}</c> placeholders in <paramref name="template"/> using
        /// <paramref name="variables"/>. Exposed for callers that already hold a template string.
        /// </summary>
        public static string Substitute(string template, IReadOnlyDictionary<string, string> variables)
        {
            if (string.IsNullOrEmpty(template) || variables is null || variables.Count == 0)
                return template;

            return Regex.Replace(template, @"\{\{\s*(\w+)\s*\}\}", match =>
            {
                var key = match.Groups[1].Value;
                foreach (var kvp in variables)
                {
                    if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
                        return kvp.Value ?? string.Empty;
                }
                return match.Value; // leave unknown placeholders untouched
            });
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


