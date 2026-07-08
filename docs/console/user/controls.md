# Console Client Controls

Complete reference for keyboard controls in the Console Game client.

## Movement

### Basic Movement
- **W** or **↑** - Move forward (in the direction you're facing)
- **S** or **↓** - Move backward (opposite to where you're facing)
- **A** or **←** - Strafe left
- **D** or **→** - Strafe right

### Rotation
- **Q** - Rotate 15° counter-clockwise (fine adjustment)
- **E** - Rotate 15° clockwise (fine adjustment)
- **Z** - Rotate 90° counter-clockwise (sharp turn left)
- **C** - Rotate 90° clockwise (sharp turn right)

### Level Changes
- **R** or **Page Up** - Go up one level (stairs, ladders)
- **F** or **Page Down** - Go down one level

## Interactions

### Object Interaction
- **G** - Pick up / Get item at current location
- **P** - Drop item from inventory
- **O** - Open door or container
- **L** - Close door or container

## Vision & Lighting Modes

Switch between different vision and lighting combinations using number keys:

- **1** (or **Numpad 1**) - Normal Vision + Torch Lighting
  - Standard gameplay mode with a torch following you
  - See environment around your character in a limited radius

- **2** (or **Numpad 2**) - Normal Vision + Sunlight
  - See by natural daylight (if above ground)
  - Includes day/night cycle with sunrise/sunset color effects
  - Sun position affects shadows and lighting direction

- **3** (or **Numpad 3**) - Infrared Vision + Torch Lighting
  - Heat-based vision showing warm objects and creatures
  - See heat signatures and recent trails where entities passed
  - Living creatures glow brighter, cold objects appear dark

- **4** (or **Numpad 4**) - Infrared Vision + Sunlight
  - Combines infrared heat detection with ambient daylight

### Vision Features
- **T** - Toggle directional vision mode
  - Switches between omnidirectional and cone-based field of view
  - Directional mode simulates realistic human peripheral vision

### Navigation
- **M** - Toggle compass mode
  - Cycles the on-screen compass widget's display mode
  - Hold **Shift** with **M** to cycle the music track instead (see Audio Controls)

## User Interface

### Audio Controls
- **N** - Toggle audio on/off
  - Enables/disables sound effects and background music
  - Status message confirms current state
- **Shift+M** - Cycle to the next music track
  - Status message shows the current track

### Display Options
- **H** - Cycle through themes
  - Changes color scheme and visual style
  - Themes include: Default, Zen, Dark Mode, High Contrast, etc.

### System
- **Escape** - Quit game
  - Disconnects from server and exits client

## Debug / Special

- **J** - Jump to random location (teleport)
  - Useful for exploration and testing
  - Plays teleport sound effect

## Tips

### Movement Strategy
- Use arrow keys or WASD for smooth movement
- Combine Q/E for fine-tuning your heading
- Use Z/C for quick 90° turns in corridors

### Vision Modes
- **Torch mode** is best for dungeons and night exploration
- **Sunlight mode** works best outdoors during the day
- **Infrared mode** helps track enemies and see recent activity
- Switch modes based on environment and situation

### Audio Cues
- Footsteps play when moving
- Teleport effect confirms jump
- Interactive objects may have audio feedback

### Directional Vision
- When enabled, you can only see what's in front of you
- Rotate your view to look around
- More challenging but realistic experience

## Key Bindings Reference

| Key | Action |
|-----|--------|
| **W**, **↑** | Move Forward |
| **S**, **↓** | Move Backward |
| **A**, **←** | Strafe Left |
| **D**, **→** | Strafe Right |
| **Q** | Rotate 15° CCW |
| **E** | Rotate 15° CW |
| **Z** | Rotate 90° CCW |
| **C** | Rotate 90° CW |
| **R**, **PgUp** | Level Up |
| **F**, **PgDn** | Level Down |
| **G** | Get Item |
| **P** | Drop Item |
| **O** | Open |
| **L** | Close |
| **1** | Normal + Torch |
| **2** | Normal + Sunlight |
| **3** | Infrared + Torch |
| **4** | Infrared + Sunlight |
| **T** | Toggle Directional Vision |
| **M** | Toggle Compass Mode |
| **N** | Toggle Audio |
| **Shift+M** | Next Music Track |
| **H** | Cycle Theme |
| **J** | Jump (Teleport) |
| **Esc** | Quit |

---

*Note: Some features may require server-side support. Check server version compatibility.*

