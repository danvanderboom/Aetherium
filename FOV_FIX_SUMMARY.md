# FOV Coordinate Mismatch Fix - Summary

## Problem Identified

The FOV system had a critical coordinate system mismatch:

1. **FOV Computation** (line 92 in `ConsoleMapView.cs`):
   - Computes visibility in **unrotated world coordinates**
   - Stores results in `Vision.Visuals` with keys like `(10, 5, 0)` in world coords

2. **Visibility Check** (originally line 151):
   - Used **rotated coordinates** for dictionary lookup
   - When view rotates, the lookup key doesn't match stored keys
   - Result: Nothing appears visible after rotation!

## Fix Applied

**File:** `ConsoleGame/Views/ConsoleMapView.cs`

**Change:**
- Store unrotated location before rotation (line 131)
- Use unrotated location for FOV visibility check (line 158)

**Before:**
```csharp
var location = new WorldLocation((int)vLocation.X, (int)vLocation.Y, (int)vLocation.Z);
// ... rotation happens ...
// Visibility check uses rotated 'location'
if (Vision != null && !Vision.Visuals.ContainsKey(location))
```

**After:**
```csharp
// Store unrotated location BEFORE rotation
var unrotatedLocation = new WorldLocation((int)vLocation.X, (int)vLocation.Y, (int)vLocation.Z);
// ... rotation happens ...
var location = new WorldLocation((int)vLocation.X, (int)vLocation.Y, (int)vLocation.Z);
// Visibility check uses unrotated location
if (Vision != null && !Vision.Visuals.ContainsKey(unrotatedLocation))
```

## Why This Works

1. FOV is computed in world coordinates (doesn't change with heading)
2. Rotation is purely for display purposes
3. Visibility should be checked in the same coordinate system as FOV computation
4. Display can use rotated coordinates for rendering

## Test Maps Created

Created `FovDiagnosticWorldBuilder` with 8 test scenarios:
1. `simple_wall` - Basic wall blocking
2. `corner_occlusion` - L-corner blocking test
3. `partial_opacity` - Forest opacity accumulation
4. `door_test` - Door open/close transparency
5. `multiwall` - Multiple walls in sequence
6. `diagonal_wall` - Diagonal line blocking
7. `cross_hair` - Cardinal direction walls
8. `chamber` - Room with exit corridor

Expected visibility documented in `FOV_TEST_MAPS_EXPECTED.md`

## How to Test

1. Uncomment desired test map in `Program.cs`
2. Run the game
3. Move player (arrow keys) and rotate view (Q/E keys)
4. Verify that visibility works correctly after rotation
5. Compare actual visibility with expected visibility from documentation

## Remaining Considerations

The fix addresses the coordinate mismatch. Additional potential improvements:
- Algorithm efficiency (currently O(n²) rays, could use shadow casting)
- Opacity calculation method (linear vs multiplicative)
- Order of operations in ray casting (mark visible then check opacity)

But the main bug preventing visibility after rotation is now fixed.

