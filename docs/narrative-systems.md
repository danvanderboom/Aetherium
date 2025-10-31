# Emergent Narrative Systems

## Overview

The emergent narrative systems enable procedural storytelling through dynamic quest generation, environmental storytelling, NPC relationships, and consequence propagation. These systems create rich, interconnected narratives that respond to player actions.

## Key Features

### 1. Procedural Quest Generation

The `NarrativeGraphGenerator` creates multi-stage quest chains based on NPC goals:

```csharp
var npcGoals = narrative.NPCGoals;
var quests = NarrativeGraphGenerator.GenerateQuestChain(
    baseQuestId: "procedural-quest",
    npcGoals: npcGoals,
    narrativeDefinition: narrative,
    random: new Random(seed)
);
```

**Quest Types:**
- **Fetch**: Retrieve items for NPCs
- **Rescue**: Save NPCs or entities
- **Defend**: Protect locations or NPCs from threats
- **Generic**: Based on NPC goal types

**Prerequisite Chains:**
Quests automatically build dependency chains where simpler quests unlock more complex ones.

### 2. Environmental Storytelling

The `EnvironmentalStoryPass` places storytelling elements during world generation:

**Ruins Feature:**
- Places ancient ruins with historical inscriptions
- Each ruin contains coherent historical text based on region and era
- Inscriptions reference the world's history

**Abandoned Camps Feature:**
- Places abandoned camps with clues about events
- Camps contain inscription components with narrative text
- Provides environmental clues for exploration

**Lore Fragments:**
- Scatters books, inscriptions, and tablets throughout the world
- Supports topics: history, legend, journal, prophecy
- Cross-references between fragments for narrative continuity

**Usage in World Generation:**
```csharp
var constraints = new NarrativeGenerationConstraints
{
    NarrativeId = "my-narrative",
    LoreTopics = { "history", "legend", "journal" },
    StoryPOIs = {
        new NarrativePointOfInterest { Name = "ancient ruins", Importance = WorldPoiImportance.Preferred },
        new NarrativePointOfInterest { Name = "abandoned camp", Importance = WorldPoiImportance.Optional }
    }
};
```

### 3. NPC Relationship Networks

The `RelationshipMatrix` creates social graphs between NPCs:

```csharp
var relationshipMatrix = RelationshipMatrix.GenerateFromNPCGoals(
    npcGoals: narrative.NPCGoals,
    random: new Random(seed)
);

// Query relationships
var relationship = relationshipMatrix.GetRelationship("npc-guard", "npc-merchant");
var allies = relationshipMatrix.GetNPCsByRelationship("npc-guard", RelationshipCategory.Ally);
```

**Relationship Values:**
- `-1.0` to `-0.3`: Enemies
- `-0.3` to `0.3`: Neutral
- `0.3` to `1.0`: Allies

**Influence:**
- Relationships influence quest targets
- Dialogue options reflect relationships
- Quest outcomes may affect relationships

### 4. Consequence Propagation

The `NarrativeConsequenceEngine` generates follow-up quests from player actions:

```csharp
var engine = new NarrativeConsequenceEngine(clusterClient);
await engine.ProcessEventAsync(
    worldId: "my-world",
    narrativeId: "my-narrative",
    eventType: "quest_completed",
    eventData: new Dictionary<string, object> { ["questId"] = "quest-1" },
    narrativeStateScope: "shared"
);
```

**Supported Event Types:**
- `quest_completed`: Generates follow-up quests from grateful NPCs
- `npc_rescued`: Creates rescue chain quests
- `location_discovered`: Triggers investigation quests
- `item_collected`: Generates collection set quests
- `enemy_defeated`: Creates revenge quests

### 5. Hybrid Narrative State

Narrative state supports both shared and per-world persistence:

**Shared State (per-narrative):**
```csharp
var worldConfig = new WorldConfig
{
    NarrativeId = "shared-narrative",
    NarrativeStateScope = "shared" // Same state across all worlds
};
```

**Per-World State:**
```csharp
var worldConfig = new WorldConfig
{
    NarrativeId = "shared-narrative",
    NarrativeStateScope = "per-world", // Isolated state per world
    NarrativeSeed = 12345 // Deterministic seed
};
```

**State Grain Usage:**
```csharp
var stateGrain = grainFactory.GetGrain<INarrativeStateGrain>(
    scope == "per-world" ? $"{worldId}:{narrativeId}" : narrativeId
);

// Mark quest complete
await stateGrain.MarkQuestCompletedAsync("quest-1");

// Get available quests (respects prerequisites)
var availableQuests = await stateGrain.GetAvailableQuestsAsync();

// Update relationships
await stateGrain.UpdateRelationshipAsync("npc-1", "npc-2", 0.8f);
```

## Deterministic Generation

All narrative generation is deterministic when seeded:

```csharp
var seed = 12345;
var random = new Random(seed);

// Same seed produces identical output
var quests1 = NarrativeGraphGenerator.GenerateQuestChain(..., random: new Random(seed));
var quests2 = NarrativeGraphGenerator.GenerateQuestChain(..., random: new Random(seed));
// quests1 and quests2 are identical
```

**Usage in World Generation:**
```csharp
var context = new GeneratorContext(width, height, seed: worldConfig.NarrativeSeed);
var rng = context.GetRandom("narrative"); // Scoped deterministic RNG
```

## Integration Example

**Complete workflow:**

```csharp
// 1. Load narrative definition
var narrativeGrain = grainFactory.GetGrain<INarrativeGrain>("my-narrative");
var narrative = await narrativeGrain.GetNarrativeAsync();

// 2. Generate procedural quests from NPC goals
var proceduralQuests = NarrativeGraphGenerator.GenerateQuestChain(
    "base-quest",
    narrative.NPCGoals,
    narrative,
    new Random(seed)
);

// 3. Add generated quests to state
var stateGrain = grainFactory.GetGrain<INarrativeStateGrain>(
    worldConfig.NarrativeStateScope == "per-world" 
        ? $"{worldId}:{narrativeId}" 
        : narrativeId
);

foreach (var quest in proceduralQuests)
{
    await stateGrain.AddGeneratedQuestAsync(quest);
}

// 4. Generate relationships
var relationships = RelationshipMatrix.GenerateFromNPCGoals(
    narrative.NPCGoals,
    new Random(seed)
);

// 5. Set up world generation with story constraints
var constraints = new NarrativeGenerationConstraints
{
    NarrativeId = narrative.NarrativeId,
    LoreTopics = { "history", "legend" },
    StoryPOIs = { new NarrativePointOfInterest { Name = "ancient ruins" } }
};

// 6. Process player actions for consequences
var consequenceEngine = new NarrativeConsequenceEngine(grainFactory);
await consequenceEngine.ProcessEventAsync(worldId, narrativeId, "quest_completed", 
    new Dictionary<string, object> { ["questId"] = completedQuestId });
```

## Lore Fragment Topics

**History:**
- Historical accounts of regions and eras
- Cross-references with legends for continuity
- References to past events

**Legend:**
- Mythological tales and legends
- References to powerful entities
- Hidden power narratives

**Journal:**
- Traveler's diary entries
- Investigation notes
- Personal accounts

**Prophecy:**
- Divinatory texts
- Future predictions
- Balance-shifting narratives

## Best Practices

1. **Quest Chains**: Use prerequisite quests to create logical progression
2. **Lore Consistency**: Ensure lore fragments reference each other for continuity
3. **Relationship Balance**: Generate diverse relationships for interesting dynamics
4. **Event Frequency**: Don't generate consequences for every action; use probability
5. **Deterministic Seeds**: Use consistent seeds for reproducible narratives
6. **State Scope**: Use "shared" for persistent narratives, "per-world" for isolated stories

## See Also

- `Data/Narratives/` - Example narrative JSON files
- `Aetherium.Server/Narrative/` - Implementation code
- `Aetherium.Server/WorldGen/Features/Story/` - Story feature implementations

