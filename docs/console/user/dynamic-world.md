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

## Examples

### Example: Weather-Based Exploration

**Scenario**: You need to travel to a distant location during winter.

**Challenge**: Snow reduces visibility and makes travel slower.

**Solution**:
1. Check weather before departing (`appsettings.json` config shows weather patterns)
2. Wait for clear weather periods (snow transitions to clear periodically)
3. Plan rest stops during storms
4. Use indoor locations as weather shelters

**Result**: Safer travel by timing your journey with weather patterns.

### Example: Seasonal Creature Spawning

**Scenario**: You want to farm wolves for resources.

**Understanding**:
- Wolves spawn more frequently at night
- Spawn rates vary by season
- Weather affects spawn probability

**Strategy**:
1. Wait until nighttime (after 6 PM game time)
2. Choose winter for higher spawn rates
3. Avoid rainy weather (wolves avoid rain)
4. Check spawn locations regularly

**Result**: Optimized resource gathering by understanding spawn mechanics.

### Example: Path Emergence Strategy

**Scenario**: You frequently travel between two locations.

**Behavior**:
- Each traversal adds to the heatmap
- High-traffic paths become visible over time
- NPCs may follow established paths

**Tactic**:
1. Establish clear routes by consistent travel
2. Use paths for faster navigation (future feature)
3. Patrol off-path areas for hidden content
4. Let NPCs use your paths for predictable encounters

**Result**: Efficient navigation and strategic advantages.

### Example: Event Timing

**Scenario**: Merchant caravans are valuable for trading.

**Information**:
- Caravans spawn at scheduled times
- Events are location-based
- Some events are recurring

**Approach**:
1. Note merchant spawn times and locations
2. Plan routes to intersect with caravans
3. Save valuable items for merchant encounters
4. Watch for recurring events on intervals

**Result**: Maximized trading opportunities through planning.

### Example: Structure Discovery

**Scenario**: You want to find new buildings and structures.

**Mechanics**:
- NPCs build structures over time
- Buildings appear in suitable locations
- Structures take days to complete

**Method**:
1. Return to previously explored areas regularly
2. Check high-traffic locations (structures prefer populated areas)
3. Patrol during different seasons (building rates vary)
4. Explore during daytime (better visibility for discovery)

**Result**: Regular discovery of new content and structures.

## Troubleshooting

### Weather Not Changing

**Symptom**: Weather stays the same for long periods.

**Possible Causes**:
- Weather system disabled in configuration
- Low transition probability settings
- Region-specific issues

**Solutions**:
1. Check `EnableWeather` setting in `appsettings.json`
2. Verify `TickHz` is set correctly (world must tick for weather to update)
3. Wait longer - weather transitions are probabilistic
4. Check region is being ticked (verify server logs)

### Time Not Advancing

**Symptom**: Game time stays static.

**Possible Causes**:
- Server ticks disabled
- World tick service not running
- Configuration issues

**Solutions**:
1. Verify `TickHz > 0` in `appsettings.json`
2. Check server logs for tick service errors
3. Ensure `WorldTickService` is running
4. Restart server if needed

### Creatures Not Spawning

**Symptom**: No creatures appear despite correct conditions.

**Possible Causes**:
- Spawn system disabled
- Low spawn probability
- Weather/season mismatches
- Region not ticking

**Solutions**:
1. Check `EnableProceduralEvents` setting
2. Verify spawn probability settings
3. Wait longer - spawns are probabilistic
4. Check weather/season compatibility
5. Ensure region is active and ticking

### Paths Not Appearing

**Symptom**: Heatmap trails don't show up.

**Possible Causes**:
- Path emergence disabled
- Insufficient traversal count
- Visualization threshold too high

**Solutions**:
1. Verify `EnableAgentChanges` is enabled
2. Travel paths multiple times (paths need several traversals)
3. Check traversal heatmap thresholds
4. Use perception system to view heatmaps

### Structures Not Building

**Symptom**: No new buildings appear.

**Possible Causes**:
- Builder system disabled
- Low build probability
- No suitable locations
- World access unavailable

**Solutions**:
1. Check `EnableAgentChanges` setting
2. Wait longer - building takes in-game days
3. Explore different areas (structures prefer certain locations)
4. Check server logs for builder modifier errors

### Events Not Triggering

**Symptom**: Scheduled events don't occur.

**Possible Causes**:
- Event system disabled
- Event handlers not registered
- Timing mismatches
- Location issues

**Solutions**:
1. Verify `EnableProceduralEvents` is enabled
2. Check event scheduler logs
3. Verify event timing (use game time, not real time)
4. Check event location coordinates

### Seasonal Effects Not Working

**Symptom**: Season changes don't affect gameplay.

**Possible Causes**:
- Season system disabled
- Day calculation incorrect
- Modifiers not registered

**Solutions**:
1. Check `EnableSeasons` setting
2. Verify day calculation (120 days = 1 year cycle)
3. Check season manager is initialized
4. Verify weather modifiers are using season data

### Performance Issues

**Symptom**: Game lags or freezes during ticks.

**Possible Causes**:
- Too many active modifiers
- High tick frequency
- Large region sizes
- Resource constraints

**Solutions**:
1. Reduce `TickHz` (lower tick rate)
2. Disable unused systems (weather, seasons, etc.)
3. Reduce region size (if configurable)
4. Check server resource usage
5. Optimize modifier priority/execution

## Advanced Configuration

### Custom Modifiers

For advanced users who want to create custom temporal modifiers, see [Temporal Modifiers Guide](temporal-modifiers.md).

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
