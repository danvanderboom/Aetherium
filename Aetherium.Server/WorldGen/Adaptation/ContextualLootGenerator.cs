using System;
using System.Collections.Generic;
using System.Linq;
using Aetherium.Server.Agents.Analysis;
using Aetherium.Model.Analysis;
using Aetherium.Server.Narrative;
using Aetherium.Model.Narrative;

namespace Aetherium.Server.WorldGen.Adaptation
{
    /// <summary>
    /// Generates contextual loot based on agent needs and behavior.
    /// </summary>
    public static class ContextualLootGenerator
    {
        /// <summary>
        /// Generates a loot table adjusted for agent needs.
        /// </summary>
        public static LootTable GenerateContextualLootTable(
            string tableId,
            List<ContentNeed> contentNeeds,
            InterestProfile? interestProfile = null,
            LootTable? baseTable = null)
        {
            var lootTable = new LootTable
            {
                TableId = tableId,
                Entries = new List<LootEntry>()
            };

            // Start with base table if provided
            if (baseTable != null)
            {
                lootTable.Entries.AddRange(baseTable.Entries);
            }

            // Add contextual loot entries based on needs
            foreach (var need in contentNeeds.OrderByDescending(n => n.Priority))
            {
                var contextualEntries = GenerateLootEntriesForNeed(need, interestProfile);
                lootTable.Entries.AddRange(contextualEntries);
            }

            // Remove duplicates (keep highest weight)
            lootTable.Entries = lootTable.Entries
                .GroupBy(e => e.ItemType)
                .Select(g => g.OrderByDescending(e => e.Weight).First())
                .ToList();

            return lootTable;
        }

        /// <summary>
        /// Adjusts an existing loot table based on agent needs.
        /// </summary>
        public static LootTable AdjustLootTable(
            LootTable existingTable,
            List<ContentNeed> contentNeeds,
            InterestProfile? interestProfile = null)
        {
            var adjustedTable = new LootTable
            {
                TableId = existingTable.TableId,
                Entries = new List<LootEntry>()
            };

            // Adjust weights based on needs
            foreach (var entry in existingTable.Entries)
            {
                var adjustedEntry = new LootEntry
                {
                    ItemType = entry.ItemType,
                    Weight = entry.Weight,
                    MinQuantity = entry.MinQuantity,
                    MaxQuantity = entry.MaxQuantity
                };

                // Increase weight if item addresses a need
                var matchingNeed = contentNeeds.FirstOrDefault(n => ItemAddressesNeed(entry.ItemType, n));
                if (matchingNeed != null)
                {
                    adjustedEntry.Weight = (int)(entry.Weight * (1.0 + matchingNeed.Priority));
                }

                adjustedTable.Entries.Add(adjustedEntry);
            }

            // Add contextual entries if needed
            var contextualEntries = GenerateContextualEntries(contentNeeds, interestProfile);
            foreach (var entry in contextualEntries)
            {
                var existingEntry = adjustedTable.Entries.FirstOrDefault(e => e.ItemType == entry.ItemType);
                if (existingEntry == null)
                {
                    adjustedTable.Entries.Add(entry);
                }
                else
                {
                    // Increase weight of existing entry
                    existingEntry.Weight = Math.Max(existingEntry.Weight, entry.Weight);
                }
            }

            return adjustedTable;
        }

        private static List<LootEntry> GenerateLootEntriesForNeed(ContentNeed need, InterestProfile? interestProfile)
        {
            var entries = new List<LootEntry>();

            switch (need.NeedType)
            {
                case "navigation_assistance":
                    entries.Add(new LootEntry
                    {
                        ItemType = "compass",
                        Weight = (int)(100 * need.Priority),
                        MinQuantity = 1,
                        MaxQuantity = 1
                    });
                    entries.Add(new LootEntry
                    {
                        ItemType = "map",
                        Weight = (int)(80 * need.Priority),
                        MinQuantity = 1,
                        MaxQuantity = 1
                    });
                    break;

                case "key_lock_assistance":
                    entries.Add(new LootEntry
                    {
                        ItemType = "key",
                        Weight = (int)(150 * need.Priority),
                        MinQuantity = 1,
                        MaxQuantity = 2
                    });
                    entries.Add(new LootEntry
                    {
                        ItemType = "lockpick",
                        Weight = (int)(100 * need.Priority),
                        MinQuantity = 1,
                        MaxQuantity = 1
                    });
                    break;

                case "combat_assistance":
                    entries.Add(new LootEntry
                    {
                        ItemType = "health_potion",
                        Weight = (int)(120 * need.Priority),
                        MinQuantity = 1,
                        MaxQuantity = 3
                    });
                    entries.Add(new LootEntry
                    {
                        ItemType = "weapon",
                        Weight = (int)(100 * need.Priority),
                        MinQuantity = 1,
                        MaxQuantity = 1
                    });
                    break;

                case "puzzle_assistance":
                    entries.Add(new LootEntry
                    {
                        ItemType = "tool",
                        Weight = (int)(100 * need.Priority),
                        MinQuantity = 1,
                        MaxQuantity = 1
                    });
                    break;
            }

            return entries;
        }

        private static List<LootEntry> GenerateContextualEntries(
            List<ContentNeed> contentNeeds,
            InterestProfile? interestProfile)
        {
            var entries = new List<LootEntry>();

            // Generate entries for each high-priority need
            foreach (var need in contentNeeds.Where(n => n.Priority >= 0.5))
            {
                var needEntries = GenerateLootEntriesForNeed(need, interestProfile);
                entries.AddRange(needEntries);
            }

            // Add items based on interest profile
            if (interestProfile != null && interestProfile.EngagingInteractions.Count > 0)
            {
                var preferredItem = interestProfile.EngagingInteractions.FirstOrDefault();
                if (preferredItem != null && !entries.Any(e => e.ItemType == preferredItem))
                {
                    entries.Add(new LootEntry
                    {
                        ItemType = preferredItem,
                        Weight = 80,
                        MinQuantity = 1,
                        MaxQuantity = 1
                    });
                }
            }

            return entries;
        }

        private static bool ItemAddressesNeed(string itemType, ContentNeed need)
        {
            var itemLower = itemType.ToLower();

            return need.NeedType switch
            {
                "navigation_assistance" => itemLower.Contains("compass") || itemLower.Contains("map"),
                "key_lock_assistance" => itemLower.Contains("key") || itemLower.Contains("lockpick"),
                "combat_assistance" => itemLower.Contains("health") || itemLower.Contains("potion") || itemLower.Contains("weapon"),
                "puzzle_assistance" => itemLower.Contains("tool"),
                _ => false
            };
        }
    }
}

