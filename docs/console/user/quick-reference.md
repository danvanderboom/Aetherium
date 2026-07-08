# Quick Reference

Fast lookup for Console Game controls and features.

## Quick Controls

```
Movement          | Vision Modes        | Interaction
------------------|---------------------|------------------
W/↑   Forward     | 1  Normal + Torch   | G  Get Item
S/↓   Backward    | 2  Normal + Sun     | P  Drop Item
A/←   Left        | 3  Infrared + Torch | O  Open
D/→   Right       | 4  Infrared + Sun   | L  Close
Q     Rotate CCW  | T  Toggle Direction |
E     Rotate CW   | M  Compass Mode     | UI
Z     Turn Left   | Debug/Special       | ----------------
C     Turn Right  | ----------------    | N  Toggle Audio
R/PgUp   Up Level | J  Teleport        | ⇧M Next Music Track
F/PgDn   Down Lvl |                     | H  Cycle Theme
                  |                     | Esc Quit
```

## Vision Mode Quick Guide

| Mode | Best For | Key Feature |
|------|----------|-------------|
| **1** Normal + Torch | Dungeons, caves | Light follows you |
| **2** Normal + Sunlight | Outdoors, day | Day/night cycle |
| **3** Infrared + Torch | Tracking, stealth | See heat/trails |
| **4** Infrared + Sunlight | Outdoor tracking | Heat + daylight |

## Heat Signatures (Infrared Mode)

| Color | Meaning | Examples |
|-------|---------|----------|
| White/Yellow | Very hot | Active creatures, humans |
| Red/Orange | Hot | Torches, fires, recent trails |
| Dark Red | Warm | Fading trails, embers |
| Black | Cold | Walls, floors, old trails |

## Map Symbols

| Symbol | Object | Can Walk? |
|--------|--------|-----------|
| `.` | Floor | ✓ |
| `#` | Wall | ✗ |
| `+` | Door | ✓ (if open) |
| `@` | You | — |
| Letters | Items/NPCs | ✓ (usually) |

## Time of Day (Sunlight Mode)

| Time | Sun Position | Lighting |
|------|--------------|----------|
| 6:00 AM | East (rising) | Red/orange tint |
| 12:00 PM | Overhead | Maximum brightness |
| 6:00 PM | West (setting) | Red/orange tint |
| 12:00 AM | Below horizon | Darkness |

**Time Scale:** 60× real time (1 real minute = 1 game hour)

## Common Sequences

### Exploring a New Area
```
1. Press 1 (Torch mode)
2. Move forward (W)
3. Rotate to look around (Q/E)
4. Press G to pick up items
5. Press O to open doors
```

### Tracking an Enemy
```
1. Press 3 (Infrared mode)
2. Look for bright signatures
3. Follow red/orange trails
4. Rotate to scan area (Q/E)
5. Switch to Normal (1) when close
```

### Opening a Door
```
1. Face the door (rotate with Q/E)
2. Move next to it (W/A/S/D)
3. Press O to open
4. Press L to close behind you
```

### Using Items with Multiple Options
```
1. Use an item on a target (via affordance)
2. If multiple usage options appear, select one:
   - Type the number (1, 2, 3, etc.)
   - Or use the option name (e.g., "force-open")
3. Item executes with selected usage mode

Examples:
- Crowbar on door: "Force Open" or "Unlock Door"
- Lockpick on locked door: "Lockpick"
- Potion: "Consume"
- Key on matching door: Auto-unlocks (single option)
```

### Changing Levels
```
1. Find stairs/ladder
2. Face them (rotate if needed)
3. Press R (up) or F (down)
4. Reorient on new level
```

## Pro Tips

💡 **Navigation**
- Use Z/C for quick 90° turns in corridors
- Fine-tune with Q/E (15° increments)
- Close doors (L) behind you for safety

💡 **Vision**
- Mode 1 for most situations
- Mode 3 to find living creatures
- Mode 2 outdoors during day
- Toggle directional (T) for challenge

💡 **Exploration**
- Check heat trails to see where NPCs went
- Sunrise/sunset provides atmospheric lighting
- Dark areas need Torch mode
- Listen for audio cues

💡 **Efficiency**
- Pick up (G) everything useful
- Drop (P) items you don't need
- Teleport (J) if lost
- Cycle themes (H) for better visibility

## Troubleshooting

| Problem | Solution |
|---------|----------|
| Can't see | Press **1** (Torch mode) |
| Lost direction | Check compass, or press **Z** to turn around |
| Door won't open | Face it, move closer, try **O** again |
| Too dark | Switch to Torch mode (**1**) or wait for sunrise (**2**) |
| Can't find NPC | Use Infrared (**3**) to see heat trails |

## Keyboard Layout Hint

```
Rotation        Movement      
Q   E           W/↑      
              A/← ↓/S →/D
Z   C         

Number Modes
1 2 3 4 (Vision)

Interaction
G O P L
(Get Open Drop cLose)
```

---

**See also:**
- [Full Controls Guide](controls.md) - Complete key reference
- [Gameplay Guide](gameplay.md) - Detailed mechanics and strategy

