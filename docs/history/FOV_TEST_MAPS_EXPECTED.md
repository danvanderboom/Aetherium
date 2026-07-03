# FOV Test Maps - Expected Visibility

## Test Setup
Each test assumes the player character is positioned at a specific location in each map. The FOV should show what the player can actually see based on:
- Line of sight (no walls blocking)
- Cumulative opacity (forests can accumulate to block)
- Maximum range (maxRange parameter)

## Map 1: simple_wall
**Layout:**
```
Horizontal corridor: (0,5) to (19,5)
Wall at: (10,5)
```

### Test Case 1.1: Player at (5,5), facing North
**Expected Visible:**
- Origin (5,5) - ALWAYS visible
- All cells in horizontal corridor from (5,5) east to (9,5) - clear line of sight
- Wall cell (10,5) - the blocking wall itself should be visible
- NOT visible: (11,5) through (19,5) - blocked by wall at (10,5)
- All cells in horizontal corridor west from (5,5) to (0,5) - clear line of sight

**Visual:**
```
Player at *:
[0][1][2][3][4][*][6][7][8][9][#][ ][ ][ ][ ][ ][ ][ ][ ][ ]
                ^visible^  ^wall^hidden
```

### Test Case 1.2: Player at (15,5), facing North  
**Expected Visible:**
- Origin (15,5) - ALWAYS visible
- All cells in horizontal corridor from (15,5) west to (11,5) - clear line of sight
- Wall cell (10,5) - the blocking wall should be visible
- NOT visible: (9,5) through (0,5) - blocked by wall at (10,5)

**Visual:**
```
Player at *:
[ ][ ][ ][ ][ ][ ][ ][ ][ ][ ][#][ ][ ][ ][ ][*][ ][ ][ ][ ]
                      ^wall^hidden  ^visible^
```

---

## Map 2: corner_occlusion
**Layout:**
```
Horizontal corridor: (0,5) to (10,5)
Vertical corridor: (10,5) to (10,10)
Wall at corner: (10,5) - blocks the corner
```

### Test Case 2.1: Player at (5,5), facing North
**Expected Visible:**
- Origin (5,5) - ALWAYS visible
- Horizontal corridor east: (6,5) to (10,5) - including the wall at (10,5)
- Horizontal corridor west: (4,5) to (0,5)
- NOT visible: 
  - Any cells in vertical corridor (10,6) to (10,10) - blocked by corner at (10,5)
  - Cannot see "around the corner"

**Visual:**
```
Horizontal:
[0][1][2][3][4][*][6][7][8][9][#]
                ^visible^  ^wall

Vertical (blocked):
[#]
[ ]
[ ]
[ ]
[ ]
```

### Test Case 2.2: Player at (10,7), facing North (in vertical corridor)
**Expected Visible:**
- Origin (10,7) - ALWAYS visible
- Vertical corridor north: (10,6), (10,5) - can see the wall at corner
- Vertical corridor south: (10,8), (10,9), (10,10)
- NOT visible:
  - Horizontal corridor cells (0,5) to (9,5) - blocked by corner wall

---

## Map 3: partial_opacity
**Layout:**
```
Horizontal corridor: (0,15) to (19,15)
Forest tiles at: (8,15), (9,15), (10,15), (11,15)
Each forest has opacity 0.49
```

### Test Case 3.1: Player at (5,15), facing North
**Expected Visible:**
- Origin (5,15) - ALWAYS visible
- Clear corridor: (6,15), (7,15)
- First forest: (8,15) - opacity 0.49
- Second forest: (9,15) - cumulative opacity 0.98
- Third forest: (10,15) - cumulative opacity 1.47 (>= 1.0) - THIS is the blocking cell
- NOT visible: (11,15) onwards - blocked by cumulative opacity at (10,15)

**Visual:**
```
[*][ ][ ][t][t][t][BLOCKED][ ][ ][ ]
   ^clear^ ^forests accumulate^
   opacity: 0 → 0 → 0.49 → 0.98 → 1.47 (blocks)
```

### Test Case 3.2: Player at (12,15), facing North  
**Expected Visible:**
- Origin (12,15) - ALWAYS visible
- East corridor: (13,15) to (19,15)
- West corridor: Can see up to and including (10,15) but NOT (9,15) or earlier
  - From (12,15), going west:
    - (11,15): opacity 0.49
    - (10,15): cumulative 0.98 (still visible)
    - (9,15): cumulative 1.47 (blocks - THIS is blocking cell)
  - NOT visible: (8,15), (7,15), etc. - blocked

---

## Map 4: door_test
**Layout:**
```
Horizontal corridor: (0,5) to (19,5)
Door at: (10,5) - closed by default
```

### Test Case 4.1: Player at (5,5), Door CLOSED
**Expected Visible:**
- Origin (5,5) - ALWAYS visible
- Corridor east to (9,5)
- Door cell (10,5) - the closed door should be visible
- NOT visible: (11,5) to (19,5) - blocked by closed door

### Test Case 4.2: Player at (5,5), Door OPENED
**Expected Visible:**
- Origin (5,5) - ALWAYS visible
- Entire corridor: (6,5) to (19,5) - door is transparent when open
- Door cell (10,5) - still visible, but doesn't block

**Visual:**
```
CLOSED: [*][ ][ ][ ][ ][ ][ ][ ][ ][D][ ][ ][ ][ ]
            ^visible^  ^door blocks^

OPENED: [*][ ][ ][ ][ ][ ][ ][ ][ ][D][ ][ ][ ][ ]
            ^all visible - door transparent^
```

---

## Map 5: multiwall
**Layout:**
```
Horizontal corridor: (0,10) to (19,10)
Walls at: (5,10), (10,10), (15,10)
```

### Test Case 5.1: Player at (0,10), facing North
**Expected Visible:**
- Origin (0,10) - ALWAYS visible
- Clear corridor to first wall: (1,10) to (5,10)
- First wall (5,10) - visible (the wall itself)
- NOT visible: (6,10) onwards - blocked by wall at (5,10)

**Note:** Even though there are walls at (10,10) and (15,10), they should not be visible because the first wall at (5,10) blocks them.

**Visual:**
```
[*][ ][ ][ ][ ][#][ ][ ][ ][ ][#][ ][ ][ ][ ][#][ ]
   ^visible^  ^wall^blocked (others hidden)
```

---

## Map 6: diagonal_wall
**Layout:**
```
Open space: 15x15 grid (0,0) to (14,14)
Diagonal wall at: (5,5), (6,6), (7,7)
```

### Test Case 6.1: Player at (2,2), facing North
**Expected Visible:**
- Origin (2,2) - ALWAYS visible
- Cells in 360° view until hitting diagonal wall
- Wall cells (5,5), (6,6), (7,7) - the walls themselves should be visible
- Cells directly beyond the diagonal wall in that direction should be blocked
- Cells in other directions (not intersecting the diagonal wall) should be visible within range

**Visual (simplified - showing diagonal):**
```
[ ][ ][*]
[ ][ ][ ]   ↑ can see in these directions
[ ][ ][ ]   but diagonal wall blocks
[ ][ ][ ]   line of sight in that
[ ][#][ ]   specific direction
[ ][ ][#]
[ ][ ][ ][#]
```

---

## Map 7: cross_hair
**Layout:**
```
Open space: 20x20 grid
Walls at cardinal directions from center (10,10):
- North: (10,5)
- South: (10,15)
- West: (5,10)
- East: (15,10)
```

### Test Case 7.1: Player at (10,10), facing North
**Expected Visible:**
- Origin (10,10) - ALWAYS visible
- North: Cells to (10,6), then wall at (10,5) visible, NOT visible beyond
- South: Cells to (10,14), then wall at (10,15) visible, NOT visible beyond
- West: Cells to (6,10), then wall at (5,10) visible, NOT visible beyond
- East: Cells to (14,10), then wall at (15,10) visible, NOT visible beyond
- Diagonals: Should be visible in diagonal directions up to range limits

**Visual:**
```
      [#]     ← north wall (blocks north beyond)
       |
[#]---[*]---[#]  ← player at center
       |
      [#]     ← south wall (blocks south beyond)
```

---

## Map 8: chamber
**Layout:**
```
Room: (5,5) to (14,14) - 10x10 room
Walls around room perimeter
Exit corridor: (15,10) to (24,10) - horizontal corridor to the east
```

### Test Case 8.1: Player at (10,10), facing North (center of room)
**Expected Visible:**
- Origin (10,10) - ALWAYS visible
- Entire room interior: all cells within room bounds
- Wall cells: Perimeter walls should be visible (you can see the walls)
- NOT visible:
  - Cells outside room walls (0,0) to (4,14), etc. - blocked by perimeter walls
  - Exit corridor (15,10) onwards - blocked by wall at (15,10)
  
**Note:** This tests room interior visibility - you should see the whole room but not outside.

### Test Case 8.2: Player at (14,10), facing East (near exit)
**Expected Visible:**
- Origin (14,10) - ALWAYS visible
- Room interior: Cells within line of sight from (14,10)
- Exit corridor: (15,10), (16,10), etc. - can see through the exit
- Wall at (15,10): The wall forming the exit should be visible (if it exists)
- NOT visible:
  - Areas blocked by walls between player and target
  - Areas outside maxRange if range is limited

---

## General Visibility Rules

### Always Visible
- **Origin cell**: The cell where the player is standing is ALWAYS visible, regardless of walls or opacity.

### Blocking Rules
1. **Fully opaque walls (opacity = 1.0)**: Block all line of sight beyond them
   - The wall cell itself MAY be visible
   - Cells beyond the wall are NOT visible

2. **Cumulative opacity**: Partial opacity cells (e.g., Forest with 0.49) accumulate
   - Visibility continues until cumulative opacity >= 1.0
   - The cell where opacity reaches >= 1.0 is the blocking cell
   - That blocking cell MAY be visible
   - Cells beyond are NOT visible

3. **Open doors**: When `OpensAndCloses.IsOpen = true`, treat as opacity 0
   - Allows vision through the door
   - Closed doors block like walls

4. **Water**: Transparent (doesn't block vision)
   - Can see through water tiles

### Range Limiting
- Cells beyond `maxRange` distance (Euclidean) are NOT visible
- Range check happens BEFORE opacity check

### Corner Occlusion
- Cannot see around corners
- If a wall forms a corner between origin and target, target is blocked
- Line of sight must be unobstructed along the ray path

