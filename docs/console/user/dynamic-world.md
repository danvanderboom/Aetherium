# Dynamic World Systems

The world in Console Game is alive and evolves over time. This guide explains how time, weather, seasons, and dynamic features affect gameplay.

## Time and Day/Night Cycle

### Game Time vs Real Time

- **Real Time Conversion**: By default, one in-game day equals 24 real minutes (configurable)
- **Day/Night Cycle**: The game advances through day and night automatically
- **Time Scale**: The server ticks at 1 Hz (once per second), advancing game time proportionally

### Time Effects

#### Daytime (6:00 AM - 6:00 PM)
- Better visibility in outdoor areas
- Some creatures are more active (e.g., bandits, bears)
- Sunlight provides natural illumination
- Ideal for exploration and travel

#### Nighttime (6:00 PM - 6:00 AM)
- Reduced visibility (requires torches or artificial light)
- Nocturnal creatures become more active (e.g., wolves, zombies)
- More dangerous encounters
- Use light sources carefully to avoid detection

### Clock Display

The game displays:
- **Current Time of Day**: Shown in hours (0-24 format)
- **Day Number**: Which game day it is (starts at day 0)
- **Season**: Current season of the year

## Weather System

### Weather Types

The world has different weather conditions that affect gameplay:

#### Clear
- Normal visibility
- No special effects
- Most common weather type

#### Cloudy
- Slightly reduced visibility
- May transition to rain

#### Rainy
- Reduced visibility (slightly)
- Some creatures avoid rain (bandits, wolves)
- Water sounds in audio perception

#### Snowy
- Occurs primarily in winter
- Reduced visibility
- Affects creature behavior
- Creates unique audio ambiance

#### Foggy
- Significantly reduced visibility
- Makes navigation challenging
- Dangerous creatures may be harder to spot

#### Stormy
- Severe weather conditions
- Very low visibility
- Most creatures avoid storms
- High danger but potential rewards

### Weather Effects

- **Visibility**: Affects how far you can see
- **Spawn Rates**: Different creatures have preferences for weather
- **Audio**: Weather changes audio perception (rain sounds, wind, etc.)
- **Movement**: Severe weather may affect movement (future feature)

### Weather Transitions

Weather changes:
- **Hourly**: Weather can transition each hour
- **Season-Based**: Some weather types are more likely in certain seasons
- **Probabilistic**: Transitions use weighted probabilities based on current conditions

## Seasons

### Seasonal Cycle

The game follows a 4-season cycle:
- **Spring**: Days 0-29 (30 days)
- **Summer**: Days 30-59 (30 days)
- **Fall**: Days 60-89 (30 days)
- **Winter**: Days 90-119 (30 days)
- **Cycle**: Repeats every 120 days (4 seasons × 30 days)

### Seasonal Effects

#### Spring
- More rain
- Moderate temperatures
- Balanced creature spawns
- Good for exploration

#### Summer
- Clearer weather
- Less rain
- Some creatures more active during day
- Best visibility conditions

#### Fall
- Increased rain
- Transition weather patterns
- Varied creature behavior
- Good harvesting season (future feature)

#### Winter
- Snow and cold weather
- Reduced daylight
- Some creatures hibernate or migrate (future feature)
- Snow affects visibility and movement

### Seasonal Gameplay Impact

- **Creature Spawns**: Different creatures are more/less common in each season
- **Weather Patterns**: Seasons influence which weather types occur
- **Resource Availability**: Seasonal resources may vary (future feature)
- **Day Length**: Seasons could affect day/night length (future feature)

## Dynamic World Features

### Path Emergence

#### How Paths Form

- **Player Movement**: Every time you walk through an area, the system records your path
- **Heatmap Tracking**: The game tracks how often each tile is traversed
- **Visual Indicators**: Frequently-used paths may become visible over time
- **Efficiency**: Following established paths may be faster (future feature)

#### Path Effects

- **Visibility**: Well-traveled paths become easier to see
- **Navigation**: Paths help you find your way back
- **Exploration**: Less-traveled areas may contain hidden secrets
- **Markers**: High-traffic areas may spawn resources or events

### Agent-Built Structures

#### Structure Building

NPCs and agents can build structures in the world:
- **Buildings**: Houses, shops, outposts
- **Infrastructure**: Bridges, roads, fortifications
- **Decorative**: Statues, monuments, gardens

#### Building Mechanics

- **Time-Based**: Structures take time to build (in-game days)
- **Location-Based**: Buildings appear in suitable locations
- **Blueprints**: Structures use prefab blueprints
- **Persistence**: Built structures persist across sessions

#### Interaction with Structures

- **Exploration**: Discover new buildings as they appear
- **Trading**: Some structures may house merchants (future feature)
- **Shelter**: Structures can provide protection from weather
- **Resources**: Structures may contain loot or resources

### Procedural Events

#### Event Types

The world generates time-based and location-based events:

##### Merchant Caravans
- **Scheduling**: Appear at scheduled times and locations
- **Traveling**: Caravans move through the world
- **Trading**: Interact with merchants (future feature)
- **Rarity**: Not common, plan your encounters

##### Monster Invasions
- **Spawning**: Groups of enemies appear at locations
- **Timing**: Invade at scheduled times
- **Intensity**: Vary in size and difficulty
- **Rewards**: Defeating invasions may yield rewards

#### Event Mechanics

- **Time-Triggered**: Events occur at specific game times
- **Location-Based**: Events happen at particular coordinates
- **Recurring**: Some events repeat on intervals
- **World State**: Events can be influenced by world conditions

## Configuration

### Server Settings

The dynamic world systems can be configured in `appsettings.json`:

```json
{
  "Simulation": {
    "TickHz": 1.0,
    "DayLengthMinutes": 24,
    "RegionSize": 64,
    "EnableWeather": true,
    "EnableSeasons": true,
    "EnableAgentChanges": true,
    "EnableProceduralEvents": true
  }
}
```

### Settings Explained

- **TickHz**: Server ticks per second (how often the world updates)
- **DayLengthMinutes**: Real minutes for one in-game day
- **RegionSize**: Size of regions (64×64 tiles default)
- **EnableWeather**: Toggle weather system
- **EnableSeasons**: Toggle seasonal cycle
- **EnableAgentChanges**: Enable path emergence and structure building
- **EnableProceduralEvents**: Enable scheduled events

## Gameplay Tips

### Time Management

- **Plan Travel**: Consider time of day before long journeys
- **Rest**: Nighttime is dangerous - find shelter
- **Daylight**: Use daytime hours for exploration and combat
- **Night Activity**: Some quests or events may require nighttime

### Weather Awareness

- **Check Weather**: Weather affects visibility and creature spawns
- **Wait for Clear**: Some activities are easier in clear weather
- **Storm Safety**: Avoid traveling during storms
- **Seasonal Prep**: Prepare for seasonal weather changes

### Path Usage

- **Follow Paths**: Well-traveled paths are safer and faster
- **Explore Off-Path**: Hidden areas may contain secrets
- **Mark Locations**: Your movements create visible paths over time
- **Trail Blazing**: First to explore an area may find unique rewards

### Structure Discovery

- **Regular Patrols**: Check areas you've visited - new structures may appear
- **Structure Types**: Different structures serve different purposes
- **Building Time**: Structures take time to complete
- **Resource Nodes**: Buildings may indicate resource-rich areas

### Event Preparation

- **Timing**: Some events occur at specific times - be ready
- **Location**: Events happen at specific locations - explore regularly
- **Merchant Encounters**: Save valuable items for merchant caravans
- **Invasion Defense**: Prepare for monster invasions in known locations

## Technical Details

### Regional System

The world is divided into 64×64 tile regions:
- Each region maintains its own state (weather, creatures, structures)
- Regions tick independently for performance
- Regional state persists across sessions
- Efficient for large worlds

### Temporal Modifiers

Modifiers apply changes over time:
- **Spawn Modifier**: Controls creature spawning based on time/weather
- **Builder Modifier**: Handles structure building by NPCs
- **Custom Modifiers**: Extensible system for new time-based effects

### Persistence

World state is saved:
- **Snapshots**: Full state snapshots of regions
- **Deltas**: Incremental changes tracked for efficiency
- **Compaction**: Deltas periodically merged into snapshots
- **Automatic**: World saves periodically (every 5 minutes by default)

## Future Enhancements

Planned features for the dynamic world:
- **Resource Seasons**: Seasonal resource availability
- **Day Length Variation**: Longer days in summer, shorter in winter
- **Climate Zones**: Different weather patterns in different areas
- **Ecosystem Dynamics**: Creature populations change over time
- **Player Construction**: Players can build structures
- **Event Variants**: More diverse event types
- **Weather Hazards**: Weather-based damage or effects
- **Seasonal Quests**: Season-specific content
