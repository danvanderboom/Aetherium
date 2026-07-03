# FOV Implementation Issues Analysis

## Step 1: Why FOV Might Not Be Working Right

Based on code analysis of `FovCalculator.cs`, here are the potential issues:

### Critical Bug #1: Order of Operations in Ray Casting

**Location:** `FovCalculator.cs` lines 48-51

```csharp
visible[step.Y - bounds.Y, step.X - bounds.X] = true;  // Line 48: Mark as visible FIRST
cumulativeOpacity += GetCellOpacity(world, step);     // Line 51: Then accumulate opacity
if (cumulativeOpacity > 1.0 - 1e-9)                   // Line 54: Then check if blocked
    break;
```

**Problem:** Cells are marked as visible BEFORE checking if cumulative opacity should block them. While this works for the blocking cell itself (you should see the wall), it creates ambiguity about whether cells are truly visible or just accidentally marked.

**Expected Behavior:** 
- Check opacity BEFORE marking
- Mark visible only if not blocked
- The blocking cell itself should be visible (you see the wall)

**Current Behavior:**
- Mark visible first
- Then accumulate opacity
- Then break if blocked

This might work, but it's fragile and could lead to incorrect visibility in edge cases.

### Potential Issue #2: Independent Ray Casting

**Location:** `FovCalculator.cs` lines 22-58

The algorithm casts a ray to EVERY cell in bounds independently:
```csharp
for (int by = bounds.Top; by < bounds.Bottom; by++)
    for (int bx = bounds.Left; bx < bounds.Right; bx++)
        // Cast ray to (bx, by)
```

**Potential Problems:**
1. **Inefficiency**: O(n²) rays for n cells in bounds
2. **Consistency**: Multiple rays may intersect the same intermediate cell, making visibility dependent on which ray processed it first
3. **Corner Peeking**: This algorithm might allow seeing around corners if rays are cast in a certain order

**Traditional FOV algorithms** use:
- Shadow casting (cast rays outward, mark visible cells)
- Diamond-based algorithms
- Recursive shadowcast

### Potential Issue #3: Opacity Accumulation Logic

**Location:** `FovCalculator.cs` line 51

Opacity is ADDED linearly: `cumulativeOpacity += GetCellOpacity(world, step)`

**Question:** Is linear addition correct for opacity? 
- In visual systems, opacity usually multiplies: `opacity = 1 - (1-opacity1) * (1-opacity2)`
- Linear addition means: 0.5 + 0.5 = 1.0 blocks (works for forests)
- Multiplicative means: 1 - (1-0.5)*(1-0.5) = 0.75 (two forests = 0.75 opacity, not 1.0)

**Current implementation uses linear addition**, which seems intentional for the "Forest blocks after 3 tiles" test case.

### Potential Issue #4: Bounds Handling with Rotation

**Location:** `ConsoleMapView.cs` lines 81-95

The FOV is computed with world bounds, then the view is rotated. The rotation happens AFTER FOV calculation:
```csharp
Vision = visionSystem.ComputeVision(World, WorldLocation, bounds, maxRange);  // Line 92
// ... later ...
if (Heading != WorldDirection.North) { /* rotate vLocation */ }  // Line 136
```

**Potential Problem:** FOV is computed in world coordinates, but visibility check happens AFTER rotation. If the rotation changes which cells are visible, this could cause issues.

**Check:** Does `Vision.Visuals.ContainsKey(location)` check the rotated or unrotated location?

Looking at line 153: `if (Vision != null && !Vision.Visuals.ContainsKey(location))`
- `location` is the rotated world location (line 150)
- `Vision.Visuals` contains unrotated world locations (computed before rotation)

**THIS IS LIKELY THE MAIN BUG!** The FOV is computed in world coordinates, but the visibility check uses rotated coordinates, so the keys don't match!

### Potential Issue #5: Empty Cells Outside Bounds

**Location:** `FovCalculator.cs` line 46

```csharp
if (bounds.Contains(stepPoint))
{
    visible[step.Y - bounds.Y, step.X - bounds.X] = true;
    // ...
}
```

Cells outside bounds are skipped, but rays might pass through them. If a blocking cell is outside bounds, the ray continues and might incorrectly mark cells inside bounds as visible.

## Test Maps Created

I've created `FovDiagnosticWorldBuilder` with 8 test scenarios:

1. **simple_wall**: Horizontal corridor with wall at (10,5) - tests basic blocking
2. **corner_occlusion**: L-shaped corridor - tests corner blocking
3. **partial_opacity**: Forest tiles - tests opacity accumulation  
4. **door_test**: Corridor with door - tests door transparency
5. **multiwall**: Multiple walls in sequence - tests repeated blocking
6. **diagonal_wall**: Diagonal wall - tests diagonal line blocking
7. **cross_hair**: Cross pattern - tests cardinal direction blocking
8. **chamber**: Room with exit - tests room interior visibility

## Next Steps

1. Run each test map and document what SHOULD be visible
2. Compare with what IS actually visible
3. Identify which bug(s) are causing the issues
4. Fix the rotation coordinate mismatch issue (Issue #4 seems most likely)

