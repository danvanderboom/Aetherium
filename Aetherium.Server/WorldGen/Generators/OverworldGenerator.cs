using System;
using System.Collections.Generic;
using System.Linq;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.Entities;
using Aetherium.WorldBuilders;
using Aetherium.WorldGen.Algorithms.Noise;

namespace Aetherium.WorldGen.Generators
{
    /// <summary>
    /// A large open-world sandbox: continents of wilderness (plains, forest, desert, hills,
    /// mountains, water) shaped by two independent noise fields — elevation and moisture — so
    /// deserts form as coherent dry lowland regions rather than an elevation band. One or two
    /// rivers run downhill to the sea; three cities of distinct character (a grid capital, an
    /// organic town, a sparse outpost) sit on flat land, joined by a road network. Buildings
    /// are enterable single rooms with doors — one in the capital is locked, its key dropped on
    /// a nearby street — and some facades carry window walls (see-through, impassable).
    ///
    /// <para>No monsters: the sandbox is for exercising world generation and the door/key/window
    /// mechanics, not combat. Selected by <c>generatorType: overworld</c>.</para>
    ///
    /// <para>Uses the <see cref="OverworldWorldBuilder"/> palette (desert + window-wall terrain
    /// with explicit passability), kept separate from the diagnostic maze vocabulary.</para>
    /// </summary>
    public class OverworldGenerator : IMapGenerator
    {
        private readonly OverworldWorldBuilder _palette = new();

        // Biome thresholds on the (continent-shaped) elevation field. Fractal noise clusters
        // near 0.5, so a contrast stretch (see FillBiomes) is applied first to give the tail
        // biomes — hills, mountains, desert, forest — meaningful area; these thresholds then
        // sit on the stretched field.
        private const double SeaLevel = 0.30;
        private const double HillLevel = 0.60;
        private const double MountainLevel = 0.76;
        // Moisture splits the lowland band into desert / plains / forest.
        private const double DryLevel = 0.42;
        private const double WetLevel = 0.58;

        public World Generate(GeneratorContext context)
        {
            var world = new World();
            var tileTypes = _palette.TileTypes;
            world.AddTileTypes(tileTypes);
            world.AddTerrainTypes(_palette.CreateTerrainTypes(tileTypes));

            int w = context.Width, h = context.Height, z = context.ZLevel;
            var terrain = new string[w, h];
            var elevation = new double[w, h];
            var entities = new List<Entity>();

            FillBiomes(context, terrain, elevation);

            var rng = context.GetRandom("overworld-layout");

            CarveRivers(context, terrain, elevation, rng);

            var sites = PickCitySites(context, terrain, rng);

            // Ground first, then the road network, then buildings — so intercity roads laid on
            // clean townsite ground survive, and building placement can dodge the roads.
            foreach (var site in sites)
                LayCityGround(terrain, site, w, h);

            ConnectCities(terrain, sites, w, h);

            bool lockPlaced = false;
            foreach (var site in sites)
                BuildCity(context, terrain, entities, site, z, rng, ref lockPlaced);

            // The capital plaza is the spawn: force it walkable so a join never lands in a wall.
            var capital = sites[0];
            terrain[capital.X, capital.Y] = "Road";
            context.StartLocation = new WorldLocation(capital.X, capital.Y, z);

            // Commit terrain, then entities.
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    world.SetTerrain(terrain[x, y], new WorldLocation(x, y, z));

            foreach (var entity in entities)
                world.AddEntity(entity);

            // A sensible default light at the capital so an ambient-lit session isn't pitch black
            // (outdoor play uses sunlight; the carried lamp handles interiors).
            var light = new LightEntity();
            light.Set(new LightSource(1.0, 50));
            light.Set(context.StartLocation);
            world.AddEntity(light);

            return world;
        }

        // --- Biomes -----------------------------------------------------------------------

        private static void FillBiomes(GeneratorContext context, string[,] terrain, double[,] elevation)
        {
            int w = context.Width, h = context.Height;
            var elevNoise = new PerlinNoise(context.EffectiveSeed);
            var moistNoise = new PerlinNoise(context.GetRandom("overworld-moisture").Next());

            const double elevScale = 0.010;   // large features → continent-sized biomes
            const double moistScale = 0.016;
            const int octaves = 4;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    double e = Stretch(elevNoise.FractalNoiseNormalized(x * elevScale, y * elevScale, octaves, 0.5, 2.0), 1.5);
                    // Continent falloff: push the map edges toward water so the landmass reads as
                    // a coastline rather than terrain clipped by the border — gentle, so land
                    // dominates and only the outer margin drowns.
                    double nx = (double)x / w - 0.5, ny = (double)y / h - 0.5;
                    double edge = Math.Sqrt(nx * nx + ny * ny) * 2.0; // ~0 center, ~1 at edge mid
                    e -= SmoothStep(0.80, 1.20, edge) * 0.35;
                    e = Math.Clamp(e, 0.0, 1.0);
                    elevation[x, y] = e;

                    double m = Stretch(moistNoise.FractalNoiseNormalized(x * moistScale, y * moistScale, octaves, 0.5, 2.0), 1.7);
                    terrain[x, y] = Classify(e, m);
                }
            }
        }

        // Fractal noise piles up near 0.5 (central-limit of the octaves); this spreads values
        // toward the extremes so the tail biomes get real area instead of a sliver.
        private static double Stretch(double v, double k) => Math.Clamp(0.5 + (v - 0.5) * k, 0.0, 1.0);

        private static string Classify(double e, double m)
        {
            if (e < SeaLevel) return "Water";
            if (e >= MountainLevel) return "Mountain";
            if (e >= HillLevel) return "Hills";
            // Lowland band: moisture decides.
            if (m < DryLevel) return "Desert";
            if (m < WetLevel) return "Plains";
            return "Forest";
        }

        // --- Rivers -----------------------------------------------------------------------

        private static void CarveRivers(GeneratorContext context, string[,] terrain, double[,] elevation, Random rng)
        {
            int w = context.Width, h = context.Height;
            int rivers = 1 + rng.Next(2); // 1-2

            for (int r = 0; r < rivers; r++)
            {
                // Start at a high, dry point and follow the steepest descent to the sea.
                int sx = 0, sy = 0; double best = -1;
                for (int attempt = 0; attempt < 40; attempt++)
                {
                    int cx = rng.Next(w / 6, w - w / 6);
                    int cy = rng.Next(h / 6, h - h / 6);
                    if (elevation[cx, cy] > best) { best = elevation[cx, cy]; sx = cx; sy = cy; }
                }
                if (best < HillLevel) continue; // no decent headwater found this pass

                int x = sx, y = sy;
                for (int step = 0; step < w + h; step++)
                {
                    PaintWater(terrain, x, y, w, h);
                    if (terrain[x, y] == "Water" && elevation[x, y] < SeaLevel) break;

                    // Descend to the lowest 8-neighbor, with a little jitter so it meanders.
                    int lx = x, ly = y; double low = elevation[x, y];
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            int ax = x + dx, ay = y + dy;
                            if (ax < 0 || ay < 0 || ax >= w || ay >= h) continue;
                            double ev = elevation[ax, ay] + (rng.NextDouble() - 0.5) * 0.02;
                            if (ev < low) { low = ev; lx = ax; ly = ay; }
                        }
                    if (lx == x && ly == y) break; // local minimum (lake); stop
                    x = lx; y = ly;
                    if (elevation[x, y] < SeaLevel) { PaintWater(terrain, x, y, w, h); break; }
                }
            }
        }

        // A 2-wide river reads better than a hairline and won't be lost under road bridges.
        private static void PaintWater(string[,] terrain, int x, int y, int w, int h)
        {
            terrain[x, y] = "Water";
            if (x + 1 < w) terrain[x + 1, y] = "Water";
        }

        // --- City site selection ----------------------------------------------------------

        private enum CityStyle { GridCapital, ScatterTown }

        private struct CitySite
        {
            public int X, Y, Radius, BuildingCount;
            public CityStyle Style;
            public bool Plaza;
        }

        private static List<CitySite> PickCitySites(GeneratorContext context, string[,] terrain, Random rng)
        {
            int w = context.Width, h = context.Height;
            var templates = new[]
            {
                new CitySite { Radius = 26, Style = CityStyle.GridCapital, BuildingCount = 0, Plaza = false },
                new CitySite { Radius = 18, Style = CityStyle.ScatterTown, BuildingCount = 12, Plaza = true },
                new CitySite { Radius = 12, Style = CityStyle.ScatterTown, BuildingCount = 5, Plaza = true },
            };

            double minSep = Math.Min(w, h) * 0.42;
            var sites = new List<CitySite>();

            foreach (var template in templates)
            {
                var site = template;
                if (TryPlaceSite(terrain, w, h, sites, minSep, ref site, rng, strict: true) ||
                    TryPlaceSite(terrain, w, h, sites, minSep * 0.6, ref site, rng, strict: false))
                {
                    sites.Add(site);
                }
                else
                {
                    // Last resort: map thirds, nudged to land — a site is better than a gap.
                    site.X = Math.Clamp(w / 4 + sites.Count * w / 4, site.Radius + 6, w - site.Radius - 6);
                    site.Y = Math.Clamp(h / 2, site.Radius + 6, h - site.Radius - 6);
                    sites.Add(site);
                }
            }
            return sites;
        }

        private static bool TryPlaceSite(string[,] terrain, int w, int h, List<CitySite> placed,
            double minSep, ref CitySite site, Random rng, bool strict)
        {
            int margin = site.Radius + 6;
            for (int attempt = 0; attempt < 1500; attempt++)
            {
                int x = rng.Next(margin, w - margin);
                int y = rng.Next(margin, h - margin);

                if (strict && !IsBuildableLowland(terrain[x, y])) continue;
                if (!strict && terrain[x, y] == "Water") continue;

                // Reject sites whose core straddles sea or mountain — no coastal/cliff towns.
                if (strict && !CoreIsDryLand(terrain, x, y, site.Radius / 2, w, h)) continue;

                bool tooClose = placed.Any(p =>
                {
                    double dx = p.X - x, dy = p.Y - y;
                    return Math.Sqrt(dx * dx + dy * dy) < minSep;
                });
                if (tooClose) continue;

                site.X = x; site.Y = y;
                return true;
            }
            return false;
        }

        private static bool IsBuildableLowland(string t) => t is "Plains" or "Desert" or "Hills";

        private static bool CoreIsDryLand(string[,] terrain, int cx, int cy, int r, int w, int h)
        {
            foreach (var (dx, dy) in new[] { (0, 0), (r, 0), (-r, 0), (0, r), (0, -r) })
            {
                int x = cx + dx, y = cy + dy;
                if (x < 0 || y < 0 || x >= w || y >= h) return false;
                if (terrain[x, y] is "Water" or "Mountain") return false;
            }
            return true;
        }

        // --- City construction ------------------------------------------------------------

        private static void LayCityGround(string[,] terrain, CitySite site, int w, int h)
        {
            for (int y = site.Y - site.Radius; y <= site.Y + site.Radius; y++)
                for (int x = site.X - site.Radius; x <= site.X + site.Radius; x++)
                {
                    if (x < 1 || y < 1 || x >= w - 1 || y >= h - 1) continue;
                    // Clean, flat townsite ground — but keep rivers running through it.
                    if (terrain[x, y] != "Water") terrain[x, y] = "Plains";
                }
        }

        private static void BuildCity(GeneratorContext context, string[,] terrain, List<Entity> entities,
            CitySite site, int z, Random rng, ref bool lockPlaced)
        {
            if (site.Style == CityStyle.GridCapital)
                BuildGridCity(terrain, entities, site, z, rng, ref lockPlaced);
            else
                BuildScatterCity(terrain, entities, site, z, rng, ref lockPlaced);
        }

        private static void BuildGridCity(string[,] terrain, List<Entity> entities, CitySite site,
            int z, Random rng, ref bool lockPlaced)
        {
            int w = terrain.GetLength(0), h = terrain.GetLength(1);
            int x0 = site.X - site.Radius, y0 = site.Y - site.Radius;
            int x1 = site.X + site.Radius, y1 = site.Y + site.Radius;
            const int block = 10, street = 2;

            // Street grid.
            for (int y = y0; y <= y1; y += block + street)
                for (int x = x0; x <= x1; x++)
                    for (int s = 0; s < street; s++)
                        SetRoad(terrain, x, y + s, w, h);
            for (int x = x0; x <= x1; x += block + street)
                for (int y = y0; y <= y1; y++)
                    for (int s = 0; s < street; s++)
                        SetRoad(terrain, x + s, y, w, h);

            // One building per block, facing the block's own nearest street (its top-left corner).
            for (int by = y0 + street; by < y1 - 4; by += block + street)
                for (int bx = x0 + street; bx < x1 - 4; bx += block + street)
                {
                    int bw = rng.Next(5, 8), bh = rng.Next(5, 8);
                    int px = bx + 1, py = by + 1;
                    bool locked = !lockPlaced;
                    if (PlaceBuilding(terrain, entities, px, py, bw, bh, z, rng,
                        faceX: bx - street, faceY: by - street,
                        window: rng.NextDouble() < 0.5, locked: locked, w: w, h: h))
                    {
                        if (locked) lockPlaced = true;
                    }
                }
        }

        private static void BuildScatterCity(string[,] terrain, List<Entity> entities, CitySite site,
            int z, Random rng, ref bool lockPlaced)
        {
            int w = terrain.GetLength(0), h = terrain.GetLength(1);

            if (site.Plaza)
                for (int dy = -3; dy <= 3; dy++)
                    for (int dx = -3; dx <= 3; dx++)
                        if (dx * dx + dy * dy <= 9) SetRoad(terrain, site.X + dx, site.Y + dy, w, h);

            int placed = 0, attempts = site.BuildingCount * 8;
            while (placed < site.BuildingCount && attempts-- > 0)
            {
                int r = site.Radius - 4;
                int px = site.X + rng.Next(-r, r), py = site.Y + rng.Next(-r, r);
                int bw = rng.Next(4, 7), bh = rng.Next(4, 7);
                bool locked = !lockPlaced && placed == 0 && site.Style == CityStyle.GridCapital; // capital only
                if (PlaceBuilding(terrain, entities, px, py, bw, bh, z, rng,
                    faceX: site.X, faceY: site.Y,
                    window: rng.NextDouble() < 0.45, locked: locked, w: w, h: h))
                {
                    if (locked) lockPlaced = true;
                    ConnectDoorToCenter(terrain, px, py, bw, bh, site.X, site.Y, w, h);
                    placed++;
                }
            }
        }

        /// <summary>Stamp a single-room building: wall ring + interior, a doorway facing
        /// (faceX,faceY), optionally a window on another wall and a lock whose key is dropped on
        /// open ground nearby. Returns false (placing nothing) if the footprint isn't clear.</summary>
        private static bool PlaceBuilding(string[,] terrain, List<Entity> entities, int x0, int y0,
            int bw, int bh, int z, Random rng, int faceX, int faceY, bool window, bool locked, int w, int h)
        {
            if (x0 < 1 || y0 < 1 || x0 + bw >= w - 1 || y0 + bh >= h - 1) return false;

            // Keep clear of roads, rivers and other structures.
            for (int y = y0; y < y0 + bh; y++)
                for (int x = x0; x < x0 + bw; x++)
                    if (terrain[x, y] is "Road" or "Water" or "Wall" or "Indoors" or "WindowWall")
                        return false;

            for (int y = y0; y < y0 + bh; y++)
                for (int x = x0; x < x0 + bw; x++)
                {
                    bool border = x == x0 || x == x0 + bw - 1 || y == y0 || y == y0 + bh - 1;
                    terrain[x, y] = border ? "Wall" : "Indoors";
                }

            // Door on the side facing the target point; the doorway cell becomes floor.
            int cx = x0 + bw / 2, cy = y0 + bh / 2;
            int ddx = faceX - cx, ddy = faceY - cy;
            int doorX, doorY;
            if (Math.Abs(ddx) >= Math.Abs(ddy))
            {
                doorX = ddx >= 0 ? x0 + bw - 1 : x0;
                doorY = cy;
            }
            else
            {
                doorX = cx;
                doorY = ddy >= 0 ? y0 + bh - 1 : y0;
            }
            terrain[doorX, doorY] = "Indoors";

            var door = new Door();
            door.Set(new WorldLocation(doorX, doorY, z));
            string keyShape = "brass";
            if (locked)
            {
                var oc = door.AllComponents.OfType<OpensAndCloses>().FirstOrDefault();
                if (oc != null) { oc.IsLocked = true; oc.KeyShape = keyShape; }
            }
            entities.Add(door);

            if (window)
            {
                // A window on a wall other than the door's: pick a border midpoint that's still Wall.
                var candidates = new (int x, int y)[]
                {
                    (x0 + bw / 2, y0), (x0 + bw / 2, y0 + bh - 1),
                    (x0, y0 + bh / 2), (x0 + bw - 1, y0 + bh / 2),
                };
                foreach (var (wx, wy) in candidates.OrderBy(_ => rng.Next()))
                {
                    if ((wx == doorX && wy == doorY) || terrain[wx, wy] != "Wall") continue;
                    terrain[wx, wy] = "WindowWall";
                    break;
                }
            }

            if (locked)
            {
                var spot = FindOpenGroundNear(terrain, doorX, doorY, 6, w, h);
                if (spot is { } cell)
                {
                    var key = new KeyItem(keyShape);
                    key.Set(new WorldLocation(cell.x, cell.y, z));
                    entities.Add(key);
                }
            }
            return true;
        }

        private static (int x, int y)? FindOpenGroundNear(string[,] terrain, int ox, int oy, int maxR, int w, int h)
        {
            for (int r = 2; r <= maxR; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        int x = ox + dx, y = oy + dy;
                        if (x < 0 || y < 0 || x >= w || y >= h) continue;
                        if (terrain[x, y] is "Road" or "Plains" or "Desert" or "Hills")
                            return (x, y);
                    }
            return null;
        }

        private static void ConnectDoorToCenter(string[,] terrain, int x0, int y0, int bw, int bh,
            int tx, int ty, int w, int h)
        {
            // A short path stub from the building toward the plaza so scattered houses read as a town.
            int x = x0 + bw / 2, y = y0 + bh / 2;
            for (int step = 0; step < 6; step++)
            {
                if (Math.Abs(tx - x) >= Math.Abs(ty - y)) x += Math.Sign(tx - x);
                else y += Math.Sign(ty - y);
                if (x < 0 || y < 0 || x >= w || y >= h) break;
                if (terrain[x, y] == "Plains") terrain[x, y] = "Road";
            }
        }

        // --- Roads ------------------------------------------------------------------------

        private static void ConnectCities(string[,] terrain, List<CitySite> sites, int w, int h)
        {
            // Connect every pair so the three cities form a road triangle.
            for (int i = 0; i < sites.Count; i++)
                for (int j = i + 1; j < sites.Count; j++)
                    CarveRoad(terrain, sites[i].X, sites[i].Y, sites[j].X, sites[j].Y, w, h);
        }

        // L-shaped 2-wide road; bridges water rather than routing around it (slice-simple).
        private static void CarveRoad(string[,] terrain, int x0, int y0, int x1, int y1, int w, int h)
        {
            int x = x0, y = y0;
            while (x != x1) { SetRoad(terrain, x, y0, w, h); SetRoad(terrain, x, y0 + 1, w, h); x += Math.Sign(x1 - x); }
            while (y != y1) { SetRoad(terrain, x1, y, w, h); SetRoad(terrain, x1 + 1, y, w, h); y += Math.Sign(y1 - y); }
        }

        private static void SetRoad(string[,] terrain, int x, int y, int w, int h)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            terrain[x, y] = "Road";
        }

        private static double SmoothStep(double edge0, double edge1, double x)
        {
            double t = Math.Clamp((x - edge0) / (edge1 - edge0), 0.0, 1.0);
            return t * t * (3.0 - 2.0 * t);
        }
    }
}
