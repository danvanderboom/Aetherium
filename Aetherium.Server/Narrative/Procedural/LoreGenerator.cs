using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Aetherium.Entities;

using Aetherium.Model.Narrative;
namespace Aetherium.Server.Narrative.Procedural
{
    /// <summary>
    /// Generates consistent historical flavor text and lore fragments.
    /// Creates coherent world history with cross-referenced topics.
    /// </summary>
    public static class LoreGenerator
    {
        /// <summary>
        /// Generates a lore fragment with historical text based on topic and context.
        /// </summary>
        public static LoreFragment GenerateLoreFragment(
            string topic,
            string region,
            Dictionary<string, List<string>>? existingLore = null,
            Random? random = null)
        {
            var rng = random ?? new Random();
            var fragment = new LoreFragment();

            var (title, text, author, era) = GenerateLoreContent(topic, region, existingLore, rng);

            fragment.SetInscription(title, text, topic, author, era);

            return fragment;
        }

        /// <summary>
        /// Generates lore content (title, text, author, era) for a given topic.
        /// </summary>
        private static (string title, string text, string author, string era) GenerateLoreContent(
            string topic,
            string region,
            Dictionary<string, List<string>>? existingLore,
            Random random)
        {
            var era = GenerateEra(random);
            var author = GenerateAuthor(topic, random);

            string title;
            string text;

            switch (topic.ToLowerInvariant())
            {
                case "history":
                    (title, text) = GenerateHistoryText(region, era, existingLore, random);
                    break;

                case "legend":
                    (title, text) = GenerateLegendText(region, era, random);
                    break;

                case "journal":
                    (title, text) = GenerateJournalText(region, author, random);
                    break;

                case "prophecy":
                    (title, text) = GenerateProphecyText(region, era, random);
                    break;

                default:
                    (title, text) = GenerateGenericLore(region, topic, random);
                    break;
            }

            return (title, text, author, era);
        }

        /// <summary>
        /// Generates historical text about a region.
        /// </summary>
        private static (string title, string text) GenerateHistoryText(
            string region,
            string era,
            Dictionary<string, List<string>>? existingLore,
            Random random)
        {
            var title = $"History of {region}";
            var sb = new StringBuilder();

            sb.AppendLine($"During the {era}, {region} was a place of great significance.");
            
            // Reference existing lore if available to create continuity
            if (existingLore != null && existingLore.TryGetValue("legend", out var legends) && legends.Count > 0)
            {
                var legendRef = legends[random.Next(legends.Count)];
                sb.AppendLine($"As the ancient tales speak: '{legendRef.Substring(0, Math.Min(50, legendRef.Length))}...'");
            }

            sb.AppendLine($"The people of {region} built great structures and established traditions that lasted for generations.");
            sb.AppendLine($"Though time has weathered these lands, echoes of the {era} remain in the very stones.");

            return (title, sb.ToString());
        }

        /// <summary>
        /// Generates legendary/mythological text.
        /// </summary>
        private static (string title, string text) GenerateLegendText(string region, string era, Random random)
        {
            var legends = new[]
            {
                $"The Legend of the {region} Guardian",
                $"The Lost Treasure of {region}",
                $"The Ancient Curse of {region}"
            };

            var title = legends[random.Next(legends.Length)];
            var sb = new StringBuilder();

            sb.AppendLine($"Long ago, in the time of the {era}, a great legend was born in {region}.");
            sb.AppendLine($"It is said that a powerful entity once roamed these lands, protecting them from darkness.");
            sb.AppendLine($"Many have sought the truth behind this legend, but few have returned with answers.");
            sb.AppendLine($"The legend speaks of hidden power waiting for the worthy.");

            return (title, sb.ToString());
        }

        /// <summary>
        /// Generates journal/diary entry text.
        /// </summary>
        private static (string title, string text) GenerateJournalText(string region, string author, Random random)
        {
            var title = $"{author}'s Journal - Entry {random.Next(1, 100)}";
            var sb = new StringBuilder();

            var dates = new[] { "First day", "Third day", "Last week", "Two weeks ago" };
            var date = dates[random.Next(dates.Length)];

            sb.AppendLine($"{date} in {region}:");
            sb.AppendLine($"The journey has been challenging. {region} is not what I expected.");
            sb.AppendLine($"I have discovered ancient markings that suggest a deeper history than the locals admit.");
            sb.AppendLine($"More investigation is needed. I must press on.");
            sb.AppendLine($"- {author}");

            return (title, sb.ToString());
        }

        /// <summary>
        /// Generates prophetic/divinatory text.
        /// </summary>
        private static (string title, string text) GenerateProphecyText(string region, string era, Random random)
        {
            var title = $"The Prophecy of {region}";
            var sb = new StringBuilder();

            sb.AppendLine($"In the shadows of the {era}, a prophecy was written about {region}.");
            sb.AppendLine($"'When the stars align, a chosen one shall rise.");
            sb.AppendLine($"The ancient power will awaken, and the balance will shift.");
            sb.AppendLine($"Those who heed these words shall find the path, while others shall fall into darkness.'");
            sb.AppendLine($"The meaning remains unclear, but many believe the time draws near.");

            return (title, sb.ToString());
        }

        /// <summary>
        /// Generates generic lore text.
        /// </summary>
        private static (string title, string text) GenerateGenericLore(string region, string topic, Random random)
        {
            var title = $"Notes on {topic}";
            var sb = new StringBuilder();

            sb.AppendLine($"Information gathered about {topic} in the region of {region}.");
            sb.AppendLine($"Local sources speak of mysterious occurrences and ancient secrets.");
            sb.AppendLine($"Further research may reveal the truth behind these tales.");

            return (title, sb.ToString());
        }

        /// <summary>
        /// Generates a historical era name.
        /// </summary>
        private static string GenerateEra(Random random)
        {
            var eras = new[]
            {
                "First Age",
                "Golden Age",
                "Dark Age",
                "Age of Legends",
                "Ancient Times",
                "Forgotten Era",
                "Time of Heroes"
            };

            return eras[random.Next(eras.Length)];
        }

        /// <summary>
        /// Generates an author name based on topic.
        /// </summary>
        private static string GenerateAuthor(string topic, Random random)
        {
            var prefixes = new[] { "Ancient", "Famous", "Unknown", "Legendary", "Forgotten" };
            var suffixes = new[] { "Scholar", "Historian", "Explorer", "Sage", "Chronicler" };

            var prefix = prefixes[random.Next(prefixes.Length)];
            var suffix = topic switch
            {
                "journal" => "Traveler",
                "prophecy" => "Oracle",
                "legend" => "Bard",
                _ => suffixes[random.Next(suffixes.Length)]
            };

            return $"{prefix} {suffix}";
        }

        /// <summary>
        /// Generates multiple lore fragments for a region with consistent cross-references.
        /// </summary>
        public static List<LoreFragment> GenerateLoreSet(
            List<string> topics,
            string region,
            int count,
            Random? random = null)
        {
            var rng = random ?? new Random();
            var fragments = new List<LoreFragment>();
            var existingLore = new Dictionary<string, List<string>>();

            for (int i = 0; i < count; i++)
            {
                var topic = topics[i % topics.Count];
                var fragment = GenerateLoreFragment(topic, region, existingLore, rng);

                // Track generated lore for cross-referencing
                if (!existingLore.ContainsKey(topic))
                {
                    existingLore[topic] = new List<string>();
                }

                var inscription = fragment.Get<Components.Inscription>();
                if (inscription != null)
                {
                    existingLore[topic].Add(inscription.Text);
                }

                fragments.Add(fragment);
            }

            return fragments;
        }
    }
}

