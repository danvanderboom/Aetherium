# Narrative Assets

Narratives define the quest structure, loot tables, monster density, and NPC goals for game worlds. They provide context and objectives for players.

## Directory Structure

```
Data/Narratives/
├── tutorial-village.json       # Example: Tutorial narrative
├── dungeon-exploration.json    # Example: Dungeon narrative
└── (your custom narratives)
```

## Narrative JSON Format

### Top-Level Fields

- **NarrativeId** (string): Unique identifier
- **Name** (string): Human-readable name
- **Description** (string): Brief description
- **Quests** (array): List of quest definitions
- **LootTables** (array): Loot table definitions
- **MonsterDensityRules** (array): Monster spawn rules per area
- **NPCGoals** (array): NPC goal definitions

## Quest Definition

```json
{
  "QuestId": "quest-example",
  "Name": "Quest Name",
  "Description": "Quest description",
  "Objectives": [
    {
      "ObjectiveId": "obj-1",
      "Type": "ObjectiveType",
      "Description": "Objective description",
      "Required": true
    }
  ],
  "Rewards": {
    "Experience": 100,
    "Gold": 50,
    "Items": ["item-id-1", "item-id-2"]
  },
  "PrerequisiteQuests": ["quest-id-that-must-be-completed-first"]
}
```

### Objective Types

- **TalkToNPC**: Speak with an NPC
  - **TargetNPC**: NPC ID to talk to
- **VisitLocation**: Go to a specific location
  - **TargetLocation**: Location ID
- **CollectItems**: Gather items
  - **TargetItem**: Item ID
  - **RequiredCount**: Number needed
- **DefeatEnemies**: Kill enemies
  - **TargetEnemyType**: Enemy type
  - **RequiredCount**: Number to defeat
- **UseItem**: Use an item at a location
  - **TargetItem**: Item to use
  - **TargetLocation**: Where to use it

## Loot Table Definition

```json
{
  "LootTableId": "loot-common-chest",
  "Name": "Common Chest Loot",
  "Entries": [
    {
      "ItemId": "item-gold-coins",
      "Weight": 100,
      "MinQuantity": 5,
      "MaxQuantity": 20
    },
    {
      "ItemId": "item-health-potion",
      "Weight": 50,
      "MinQuantity": 1,
      "MaxQuantity": 3
    }
  ]
}
```

### Loot Table Fields

- **LootTableId** (string): Unique identifier
- **Name** (string): Human-readable name
- **Entries** (array): List of possible drops
  - **ItemId**: Item to drop
  - **Weight**: Relative probability (higher = more common)
  - **MinQuantity**: Minimum number to drop
  - **MaxQuantity**: Maximum number to drop

## Monster Density Rules

```json
{
  "AreaId": "forest",
  "MonsterTypes": ["enemy-wolf", "enemy-bear"],
  "DensityPercentage": 15.0,
  "MinLevel": 2,
  "MaxLevel": 5
}
```

### Fields

- **AreaId** (string): Identifier for the area/region
- **MonsterTypes** (array): List of monster types that can spawn
- **DensityPercentage** (float): Percentage of tiles that should have monsters (0-100)
- **MinLevel**: Minimum monster level
- **MaxLevel**: Maximum monster level

## NPC Goal Definition

```json
{
  "NPCGoalId": "goal-elder-teach",
  "NPCId": "npc-elder",
  "GoalType": "Teach",
  "Description": "Teach new adventurers",
  "RelatedQuestIds": ["quest-welcome"],
  "RequiredAssets": ["dialogue-elder-welcome", "dialogue-elder-advice"]
}
```

### Goal Types

- **Teach**: NPC provides information/training
- **Collect**: NPC needs items gathered
- **Protect**: NPC wants area protected
- **Trade**: NPC is a merchant
- **Quest**: NPC gives out quests

### Fields

- **NPCGoalId** (string): Unique identifier
- **NPCId** (string): NPC this goal belongs to
- **GoalType** (string): Type of goal
- **Description** (string): What the NPC wants
- **RelatedQuestIds** (array): Quests tied to this goal
- **RequiredAssets** (array): Assets that need to exist (dialogue, items, enemies)

## Complete Example

See `tutorial-village.json` for a complete working example with:
- 3 interconnected quests
- 2 loot tables
- 3 monster density rules
- 3 NPC goals

## Usage

### CLI Commands

```powershell
# Load a narrative
dotnet run --project AgentCLI narrative load Data/Narratives/tutorial-village.json

# Show narrative details
dotnet run --project AgentCLI narrative show tutorial-village

# Create a world with a narrative
dotnet run --project AgentCLI world create MyWorld "Test World" --narrative tutorial-village

# Delete a narrative
dotnet run --project AgentCLI narrative delete tutorial-village
```

### In Code

```csharp
// Get narrative grain
var narrative = grainFactory.GetGrain<INarrativeGrain>("tutorial-village");

// Load narrative from file
var json = await File.ReadAllTextAsync("Data/Narratives/tutorial-village.json");
var definition = JsonSerializer.Deserialize<NarrativeDefinition>(json);
await narrative.SetNarrativeAsync(definition);

// Query narrative data
var quests = await narrative.GetQuestsAsync();
var lootTables = await narrative.GetLootTablesAsync();
var monsterRules = await narrative.GetMonsterDensityRulesAsync();
var npcGoals = await narrative.GetNPCGoalsAsync();

// Use in world generation
var worldConfig = new WorldConfig
{
    NarrativeId = "tutorial-village",
    // ... other config
};
```

## Best Practices

1. **Quest Chains**: Use PrerequisiteQuests to create logical progression
2. **Balanced Rewards**: Scale experience/gold/items appropriately with difficulty
3. **Clear Objectives**: Make quest objectives specific and measurable
4. **Loot Weights**: Higher weights = more common drops (typically 10-100)
5. **Monster Density**: Start low (5-10%) for outdoor areas, higher (20-30%) for dungeons
6. **NPC Goals**: Link NPCs to quests to create cohesive stories
7. **Required Assets**: List assets that need to exist for the narrative to work

## Narrative-Aware Generation

When a world is created with a narrative:
- Generators receive the narrative context
- NPC spawn points are created for quest NPCs
- Monster density follows the rules
- Loot tables are applied to chests/enemies
- Quest locations are marked in the world

## Testing Your Narrative

1. Load it via CLI: `narrative load path/to/file.json`
2. Create a test world: `world create TestWorld "Test" --narrative your-narrative-id`
3. Join the world and verify quests appear
4. Check that NPCs and monsters spawn correctly
5. Test loot drops from enemies and chests

