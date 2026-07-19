using System;
using System.Collections.Generic;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.Lighting;
using Aetherium.Model;
using Aetherium.Systems;
using Aetherium.Topology;

namespace Aetherium.Server.Perception
{
    /// <summary>
    /// Sphere-native field of view + lighting (docs/design/h3-sphere-worldgen.md §7 P1). The square
    /// pipeline (<see cref="FovCalculator"/>, <see cref="LightCalculator"/>,
    /// <c>DirectionalFovCalculator</c>) already routes occlusion, distance, and the directional cone
    /// through <see cref="IGridTopology.Line"/> and <see cref="IGridTopology.Delta"/> — so the model is
    /// topology-general; its only square assumption is enumerating a rectangle. This runs the <b>same
    /// tested ray logic</b> over an H3 gridDisk instead, reusing <see cref="FovCalculator.GetCellOpacity"/>
    /// so mountains/forests/walls occlude on the sphere exactly as they do on a square grid.
    ///
    /// <para>Returns the set of cells the perceiver can actually see, each with its computed light level.
    /// A daylight open surface (the sample H3 world) reads at full ambient light and is limited only by
    /// occlusion and range; a torch/night world gets point-light pools and a darkness-shrunk view — the
    /// same behaviour as the planar path.</para>
    /// </summary>
    public sealed class H3VisionLighting
    {
        /// <summary>
        /// Visible cells (perceiver's own cell included) mapped to their light level in [0,1]. Occlusion
        /// is per-target raycasting via the topology's line; the directional cone (when
        /// <paramref name="fovDegrees"/> &lt; 360) is a post-filter on the bearing; darkness shrinks the
        /// effective range and culls unlit distant cells — mirroring <c>VisionSystem</c>.
        /// </summary>
        public Dictionary<GridCoord, double> ComputeVisible(
            World world, WorldLocation origin, int maxRange,
            int? headingDegrees, int? fovDegrees,
            LightingMode mode, double timeOfDay)
        {
            var topo = world.Topology;
            var originCell = GridCoord.From(origin);

            // ---- lighting model (ambient + point sources), matching LightingSystem/LightCalculator ----
            double ambient = 0.0;
            if (mode == LightingMode.Sunlight)
            {
                var sun = new SunlightCalculator();
                var (_, elevation) = sun.CalculateSunPosition(timeOfDay);
                var (_, _, _, intensity) = sun.GetSunlightColor(elevation);
                ambient = intensity; // an open surface reads at the sun's brightness
            }

            // Point sources: the player's torch (Torch mode), plus any LightSource entities inside the
            // viewport. Scanning the disk (O(cells)) rather than all entities keeps this off the
            // 288k-entity hot path; a source just outside the viewport is ignored (ambient dominates the
            // daylight case, and a torch you can't nearly see barely lights what you can).
            var sources = new List<(WorldLocation Loc, double Intensity, int Range)>();
            if (mode == LightingMode.Torch)
                sources.Add((origin, 0.9, 6)); // mirrors LightingSystem.ComputeLightingWithMode(Torch)

            foreach (var cell in topo.Range(originCell, maxRange))
            {
                if (!world.EntitiesByLocation.TryGetValue(cell.ToWorldLocation(), out var ents))
                    continue;
                foreach (var e in ents.Values)
                    if (e.Components.TryGetValue(typeof(LightSource), out var lsComp)
                        && lsComp is LightSource ls && ls.IsEnabled)
                        sources.Add((cell.ToWorldLocation(), ls.Intensity, ls.Range));
            }

            double LightAt(GridCoord cell)
            {
                double light = ambient;
                if (sources.Count > 0)
                {
                    var loc = cell.ToWorldLocation();
                    foreach (var s in sources)
                        light += PointLight(world, topo, s.Loc, s.Intensity, s.Range, loc);
                }
                return light > 1.0 ? 1.0 : light;
            }

            // Darkness shrinks the effective view range (VisionSystem): in near-black, you see only a
            // cell or two even if the nominal range is larger.
            double originLight = LightAt(originCell);
            int effectiveRange = maxRange;
            if (originLight < 0.1)
                effectiveRange = Math.Max(1, (int)(maxRange * originLight * 10.0));

            bool anyLight = originLight > 0.001 || sources.Count > 0 || ambient > 0.001;

            // ---- occlusion: raycast to every disk cell, marking cells along the ray up to the blocker ----
            var visible = new HashSet<GridCoord> { originCell };
            foreach (var target in topo.Range(originCell, effectiveRange))
            {
                if (target == originCell)
                    continue;
                var (dx, dy) = topo.Delta(originCell, target);
                if (Math.Sqrt(dx * dx + dy * dy) > effectiveRange)
                    continue;
                CastRay(world, topo, originCell, target, visible);
            }

            // ---- directional cone (post-filter on bearing, like DirectionalFovCalculator) ----
            bool directional = headingDegrees.HasValue && fovDegrees.HasValue && fovDegrees.Value < 360;
            double headX = 0, headY = 0, cosHalfFov = -1;
            if (directional)
            {
                double hr = headingDegrees!.Value * Math.PI / 180.0;
                headX = Math.Sin(hr);
                headY = -Math.Cos(hr); // north = -Y
                cosHalfFov = Math.Cos((fovDegrees!.Value / 2.0) * Math.PI / 180.0);
            }

            // ---- assemble: light each visible cell, apply the cone and the dark-cell rule ----
            var result = new Dictionary<GridCoord, double>();
            foreach (var cell in visible)
            {
                var (dx, dy) = topo.Delta(originCell, cell);
                double dist = Math.Sqrt(dx * dx + dy * dy);

                if (directional && dist > 1e-9)
                {
                    double nx = dx / dist, ny = dy / dist;
                    if (nx * headX + ny * headY < cosHalfFov)
                        continue; // outside the forward arc
                }

                double light = cell == originCell ? originLight : LightAt(cell);

                // A very dark cell in a lit world is only seen if it's right next to you.
                if (anyLight && light < 0.05 && dist > 2.0)
                    continue;

                result[cell] = light;
            }

            return result;
        }

        // Mirrors FovCalculator.CastRay: walk the topology line, marking each cell visible until the
        // running opacity reaches 1.0 (the blocker itself is seen; nothing beyond it). The origin cell
        // is skipped. Reuses FovCalculator.GetCellOpacity so the opacity model is identical to the
        // square path (terrain ObstructsView + entity ObstructsView, open doors excluded).
        private static void CastRay(World world, IGridTopology topo, GridCoord originCell, GridCoord target, HashSet<GridCoord> visible)
        {
            double cumulativeOpacity = 0.0;
            foreach (var cell in topo.Line(originCell, target))
            {
                if (cell == originCell)
                    continue;
                visible.Add(cell);
                cumulativeOpacity += FovCalculator.GetCellOpacity(world, cell.ToWorldLocation());
                if (cumulativeOpacity > FovCalculator.OpaqueThreshold)
                    return;
            }
        }

        // Mirrors LightCalculator.CalculateLightAlongRay: linear distance falloff, occlusion accumulated
        // along the topology line, and the target's own opacity does NOT block light arriving at it (a
        // wall face is lit even though it shadows cells beyond).
        private static double PointLight(World world, IGridTopology topo, WorldLocation source, double intensity, int range, WorldLocation target)
        {
            if (intensity <= 0.0 || range <= 0)
                return 0.0;
            if (target == source)
                return intensity;

            var sourceCell = GridCoord.From(source);
            var (dx, dy) = topo.Delta(sourceCell, GridCoord.From(target));
            double distance = Math.Sqrt(dx * dx + dy * dy);
            if (distance > range)
                return 0.0;

            double distanceAttenuation = intensity * (1.0 - distance / range);
            if (distanceAttenuation <= 0.0)
                return 0.0;

            double cumulativeOpacity = 0.0;
            bool reachedTarget = false;
            foreach (var cell in topo.Line(sourceCell, GridCoord.From(target)))
            {
                if (cell == sourceCell)
                    continue;
                var step = cell.ToWorldLocation();
                if (step == target)
                {
                    reachedTarget = true;
                    break; // the target's own opacity must not block light arriving at it
                }
                cumulativeOpacity += FovCalculator.GetCellOpacity(world, step);
                if (cumulativeOpacity >= FovCalculator.OpaqueThreshold)
                    return 0.0;
            }

            return reachedTarget ? Math.Max(0.0, distanceAttenuation * (1.0 - cumulativeOpacity)) : 0.0;
        }
    }
}
