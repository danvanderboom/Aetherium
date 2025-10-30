# FOV Bug Summary - Step 1 Analysis

## The Main Bug: Coordinate System Mismatch

### The Problem

In `ConsoleMapView.cs`:

1. **Line 92**: FOV is computed in **unrotated world coordinates**:
   ```csharp
   Vision = visionSystem.ComputeVision(World, WorldLocation, bounds, maxRange);
   ```
   This stores locations in `Vision.Visuals` using world coordinates like `(10, 5, 0)`.

2. **Lines 123-148**: The view loops through screen coordinates and **rotates** them:
   ```csharp
   var vLocation = new Vector3(WorldLocation.X + x - xoffset, WorldLocation.Y + y - yoffset, WorldLocation.Z);
   // ... rotation happens here ...
   var location = new WorldLocation((int)vLocation.X, (int)vLocation.Y, (int)vLocation.Z);
   ```

3. **Line 151**: Visibility check uses the **rotated location**:
   ```csharp
   if (Vision != null && !Vision.Visuals.ContainsKey(location))
   ```
   
   But `Vision.Visuals` contains **unrotated** world coordinates!

### Result

When the player rotates their view:
- FOV is computed for world coordinates (0,0) = player facing north
- But the visibility check uses rotated coordinates
- Example: If player at (10,10) faces East, a cell at (11,10) in world coords
  - Gets rotated to (-1, 10) or similar
  - Vision.Visuals lookup for rotated coords fails
  - Cell appears invisible even though it should be visible!

### Expected Behavior

FOV should be computed in rotated coordinates, OR the visibility check should use unrotated coordinates. The rotation should only affect what's DISPLAYED, not what's VISIBLE.

## Secondary Issues

1. **Order of operations in ray casting**: Cells marked visible before opacity check (might be okay but fragile)
2. **Inefficient algorithm**: O(n²) rays instead of shadow casting
3. **Linear opacity accumulation**: Might need verification if multiplicative would be better

## Solution Approach

The fix should ensure FOV computation and visibility checks use the same coordinate system. Options:
- Option A: Rotate the bounds before computing FOV, then check visibility in rotated coords
- Option B: Keep FOV in world coords, unrotate the location before visibility check
- Option C: Rotate the VisionFrame after computation to match current heading

