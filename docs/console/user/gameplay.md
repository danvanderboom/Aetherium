# Gameplay Guide

How to play Console Game - exploring, interacting, and surviving in the dungeon.

## Getting Started

### First Steps
1. Launch the client and connect to the server
2. You'll spawn in a dungeon environment
3. The map shows your surroundings in ASCII art
4. Your character is typically at the center of the view
5. Use WASD or arrow keys to move around

### Understanding the Display

#### Map Symbols
- **`.`** - Open floor (walkable)
- **`#`** - Wall (blocks movement and vision)
- **`+`** - Door (can be opened/closed)
- **`@`** - Your character
- **Letters/Symbols** - Items, NPCs, monsters, or interactive objects

#### Visual Information
- **Lighting**: Areas are dimmed based on distance from light sources
- **Colors**: Different terrain and objects have distinct colors
- **Visibility**: You can only see what's lit and in your field of view

## Core Gameplay

### Exploration

#### Moving Through the World
- Walk through open areas (`.` tiles)
- Doors (`+`) can be opened with **O** and closed with **L**
- Some areas may be dark - use Torch mode (**1**) to illuminate
- Use stairs or ladders to change levels (**R** for up, **F** for down)

#### Orientation
- Your heading (North, South, East, West) affects:
  - Which direction "forward" moves you
  - Your field of view in directional vision mode
  - How you interact with objects in front of you
- Use the compass widget (if enabled) to track your heading

### Interacting with Objects

#### Picking Up Items
1. Move to a tile with an item
2. Press **G** to pick it up
3. Item is added to your inventory
4. Check inventory widget to see what you're carrying

#### Dropping Items
1. Press **P** to drop an item
2. Select which item to drop (if you have multiple)
3. Item is placed at your current location
4. Other players can pick it up

#### Doors
- **Opening**: Stand next to a door, face it, press **O**
- **Closing**: Stand next to an open door, press **L**
- Some doors may be locked or require keys

## Advanced Features

### Lighting & Vision Modes

#### Mode 1: Normal + Torch (Default)
**Best for:** General exploration, dungeons, night time

- Light source follows your character
- 6-tile radius of illumination
- Walls and obstacles cast shadows
- Standard color perception

**When to use:**
- Exploring dark dungeons
- Indoor environments
- Night time outdoor areas

#### Mode 2: Normal + Sunlight
**Best for:** Outdoor exploration, daytime

- Directional sunlight from the sky
- Sun position changes over time (day/night cycle)
- Sunrise/sunset have reddish tints
- Shadows cast by elevated terrain
- Night time (12 AM - 6 AM) is very dark

**When to use:**
- Outdoor areas during the day
- When you want to see long distances
- Experiencing the day/night cycle

**Time of Day Effects:**
- **6:00 AM** - Sunrise (red/orange tint)
- **12:00 PM** - Noon (bright white light, maximum visibility)
- **6:00 PM** - Sunset (red/orange tint)
- **12:00 AM** - Midnight (darkness)

#### Mode 3: Infrared + Torch
**Best for:** Tracking creatures, detecting life

- Heat-based vision
- Living creatures glow brightly
- Recent movement leaves heat trails
- Cold objects appear dark or black
- Trails fade over time (10-60 seconds)

**Heat Signatures:**
- **Bright white/yellow** - High heat (humans, active creatures)
- **Red/orange** - Moderate heat (warm objects, torches)
- **Dark red** - Low heat (fading trails)
- **Black** - No heat (cold walls, floors, old trails)

**When to use:**
- Tracking enemies or NPCs
- Seeing through darkness
- Detecting recent activity in an area
- Finding warm objects (torches, fires, energy cells)

#### Mode 4: Infrared + Sunlight
**Best for:** Outdoor heat tracking

- Combines infrared detection with ambient daylight
- See heat signatures in outdoor environments
- Sun provides base illumination
- Less useful at night

### Directional Vision

#### Standard Mode (360° Vision)
- See everything around you within range
- Easier for navigation
- Less realistic

#### Directional Mode (Cone Vision)
- Only see what's in front of you (~90° field of view)
- Must rotate to look around
- More challenging and immersive
- Simulates realistic human perception
- Toggle with **T** key

**Tips for Directional Mode:**
- Rotate frequently to check your surroundings
- Use fine rotation (Q/E) to scan areas
- Listen for audio cues behind you
- More strategic gameplay - ambushes are possible!

### Day/Night Cycle

When using Sunlight mode, time progresses:
- **Time Scale**: 60x real time (1 real minute = 1 game hour)
- **24-hour cycle**: Complete day/night cycle every 24 real minutes
- **Sun Movement**: Sun rises in east, sets in west
- **Lighting Changes**: Gradual shift from night to day

**Gameplay Impact:**
- Daytime: Maximum outdoor visibility
- Nighttime: Switch to Torch mode for light
- Dawn/Dusk: Atmospheric lighting, moderate visibility

## Strategy & Tips

### Efficient Exploration
1. **Start with Mode 1** (Torch) for general dungeon exploration
2. **Map mentally** - remember where you've been
3. **Check corners** - rotate to look before entering new areas
4. **Use landmarks** - unique terrain features help navigation

### Combat Preparation
1. **Switch to Infrared** before entering dangerous areas
2. **Track enemy movements** via heat trails
3. **Listen for audio cues** (footsteps, growls)
4. **Keep exits clear** - know your escape routes

### Resource Management
1. **Pick up useful items** early (torches, keys, potions)
2. **Drop unnecessary items** to free inventory space
3. **Note item locations** you can't carry yet
4. **Return for valuable items** after making space

### Environmental Awareness
1. **Check for doors** before assuming a wall is solid
2. **Try opening locked doors** - you might have the key
3. **Close doors behind you** to block pursuing enemies
4. **Use stairs tactically** - escape to different levels

### Advanced Tactics

#### Heat Trail Tracking
1. Switch to Infrared mode (**3** or **4**)
2. Look for red/orange trails on the ground
3. Brighter trails are more recent
4. Follow trails to find NPCs or track enemies
5. Your own trail shows where you've been

#### Stealth
1. Close doors behind you to hide your path
2. Wait for heat trails to fade before moving
3. Use cold areas (stone walls) to mask your presence
4. In infrared, you glow brightly - be aware!

#### Time-based Strategy
1. Use Sunlight mode to check time of day
2. Plan outdoor travel for daytime
3. Find shelter before sunset
4. Use darkness (night) for stealth approaches

## Common Issues

### "I can't see anything"
- Switch to Mode 1 (Torch) with the **1** key
- You might be in a dark area at night
- Check if directional vision is enabled - rotate around

### "Where am I going?"
- Check compass widget for your heading
- "Forward" is relative to where you're facing
- Use **Q/E** to adjust heading, then **W** to move

### "I can't open this door"
- Make sure you're facing the door
- Try moving one step closer
- Door might be locked - check inventory for keys

### "Heat trails are confusing"
- Switch back to Normal mode (**1** or **2**)
- Remember: bright = recent, dark = old
- Your own trail shows your previous path

### "I got lost"
- Use **J** to teleport to a random location (debug feature)
- Close and reopen client to respawn
- Ask server admin for help

## Next Steps

- **Experiment** with different vision modes in various environments
- **Practice** directional vision for a challenge
- **Explore** all levels of the dungeon
- **Interact** with NPCs and objects you find
- **Master** the controls and become efficient at navigation

Enjoy your adventure in the console dungeon!

