using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using NUnit.Framework;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.Entities;
using Aetherium.Topology;
using Aetherium.WorldBuilders;
using H3;
using H3.Algorithms;
using H3.Extensions;

namespace Aetherium.Test.Perception
{
    /// <summary>
    /// Landmark perception: the persistent places and things the living planet is made of — transit
    /// <see cref="Station"/>s, a vehicle's <see cref="Boardable"/> exterior (a docked train/ship), and
    /// <see cref="Settlement"/>s — surface into <c>PerceptionDto.VisibleCharacters</c> as named,
    /// non-hostile contacts whenever the player can see their cell, so the world you can now build (rideable
    /// transit, settlements, the economy) is something a player can actually see and walk to. Ungated (no
    /// radio, unlike satellites) and FOV-limited (only what's visible). Covers both the square and the H3
    /// perception paths, which share the same surfacing helper.
    /// </summary>
    [TestFixture]
    public class LandmarkPerceptionTests
    {
        private static Aetherium.Model.PerceptionDto Perceive(World world, WorldLocation at)
            => new Aetherium.Server.PerceptionService().ComputePerception(
                world, at, Aetherium.WorldDirection.North, new Size(16, 16), null);

        // ---- square grid ----

        [Test]
        public void Planar_SurfacesStationVehicleAndSettlementAsNamedContacts()
        {
            var world = new World();
            var builder = new DungeonCrawlerWorldBuilder();
            world.AddTileTypes(builder.TileTypes);
            world.AddTerrainTypes(builder.CreateTerrainTypes(builder.TileTypes));
            for (int x = 0; x <= 8; x++)
                for (int y = 0; y <= 8; y++)
                    world.SetTerrain("Indoors", new WorldLocation(x, y, 0));

            var playerLoc = new WorldLocation(4, 4, 0);
            var player = new Character();
            player.Set(playerLoc);
            world.AddEntity(player);

            var station = new StationEntity();
            station.Set(new WorldLocation(4, 2, 0));
            station.Set(new Station { LineId = "rail", StopIndex = 0, Name = "Riverport" });
            world.AddEntity(station);

            var train = new VehicleExterior();
            train.Set(new WorldLocation(2, 4, 0));
            train.Set(new Boardable { VehicleInstanceId = "veh-1", DisplayName = "The Kestrel" });
            world.AddEntity(train);

            var town = new SettlementEntity();
            town.Set(new WorldLocation(6, 4, 0));
            town.Set(new Settlement { Name = "Oakhaven", Tier = SettlementTier.Town });
            world.AddEntity(town);

            var p = Perceive(world, playerLoc);
            var byId = p.VisibleCharacters.ToDictionary(c => c.Id, c => c);

            Assert.That(byId.ContainsKey(station.EntityId), Is.True, "the station is a visible contact");
            Assert.That(byId[station.EntityId].Name, Is.EqualTo("Riverport Station"));
            Assert.That(byId.ContainsKey(train.EntityId), Is.True, "the docked vehicle is a visible contact");
            Assert.That(byId[train.EntityId].Name, Is.EqualTo("The Kestrel"));
            Assert.That(byId.ContainsKey(town.EntityId), Is.True, "the settlement is a visible contact");
            Assert.That(byId[town.EntityId].Name, Is.EqualTo("Oakhaven"));

            Assert.That(new Entity[] { station, train, town }.All(e => !byId[e.EntityId].IsHostile), Is.True,
                "landmarks are never hostile");

            // Relative coordinates match the contract (player at 0,0): station (4,2) − player (4,4) = (0,-2).
            var st = byId[station.EntityId];
            Assert.That((st.Location!.X, st.Location.Y, st.Location.Z), Is.EqualTo((0, -2, 0)));
        }

        [Test]
        public void Planar_ALandmarkOutOfSightIsNotSurfaced()
        {
            var world = new World();
            var builder = new DungeonCrawlerWorldBuilder();
            world.AddTileTypes(builder.TileTypes);
            world.AddTerrainTypes(builder.CreateTerrainTypes(builder.TileTypes));
            for (int x = 0; x <= 8; x++)
                for (int y = 0; y <= 8; y++)
                    world.SetTerrain("Indoors", new WorldLocation(x, y, 0));

            var playerLoc = new WorldLocation(4, 4, 0);
            world.AddEntity(WithLoc(new Character(), playerLoc));

            // A station far off the lit room — no terrain there, so it never enters the vision frame.
            var farStation = new StationEntity();
            farStation.Set(new WorldLocation(40, 40, 0));
            farStation.Set(new Station { Name = "Faraway" });
            world.AddEntity(farStation);

            var p = Perceive(world, playerLoc);
            Assert.That(p.VisibleCharacters.Any(c => c.Id == farStation.EntityId), Is.False,
                "a landmark the player cannot see is not surfaced");
        }

        // ---- H3 sphere (the living planet) ----

        [Test]
        public void H3_SurfacesStationVehicleAndSettlementOnThePlanet()
        {
            var world = H3Flat(out var player, out var byDistance);

            var station = new StationEntity();
            station.Set(byDistance(2));
            station.Set(new Station { LineId = "rail", StopIndex = 3, Name = "Capitol" });
            world.AddEntity(station);

            var train = new VehicleExterior();
            train.Set(byDistance(3));
            train.Set(new Boardable { VehicleInstanceId = "train-7", DisplayName = "Capitol Line Express" });
            world.AddEntity(train);

            var city = new SettlementEntity();
            city.Set(byDistance(4));
            city.Set(new Settlement { Name = "Highspire", Tier = SettlementTier.City });
            world.AddEntity(city);

            var p = Perceive(world, player);
            var byId = p.VisibleCharacters.ToDictionary(c => c.Id, c => c);

            Assert.That(byId.ContainsKey(station.EntityId), Is.True, "the rail station is visible on the planet");
            Assert.That(byId[station.EntityId].Name, Is.EqualTo("Capitol Station"));
            Assert.That(byId.ContainsKey(train.EntityId), Is.True, "the parked train is a boardable contact");
            Assert.That(byId[train.EntityId].Name, Is.EqualTo("Capitol Line Express"));
            Assert.That(byId.ContainsKey(city.EntityId), Is.True, "the city is a visible contact");
            Assert.That(byId[city.EntityId].Name, Is.EqualTo("Highspire"));
            Assert.That(new Entity[] { station, train, city }.All(e => !byId[e.EntityId].IsHostile), Is.True);
        }

        // ---- helpers ----

        private static Entity WithLoc(Entity e, WorldLocation loc)
        {
            e.Set(new WorldLocation(loc.X, loc.Y, loc.Z));
            return e;
        }

        // A flat H3 patch around the player, plus a picker for a cell at a given ring distance (in view).
        private static World H3Flat(out WorldLocation player, out System.Func<int, WorldLocation> byDistance)
        {
            var world = new World { Topology = H3Topology.Instance };
            var palette = new OverworldWorldBuilder();
            var tt = palette.TileTypes;
            world.AddTileTypes(tt);
            world.AddTerrainTypes(palette.CreateTerrainTypes(tt));
            world.MinBand = -4;
            world.MaxBand = 8;

            var centerIdx = H3Index.GetRes0Cells().First().GetChildrenForResolution(3).First();
            var ringCells = new Dictionary<int, WorldLocation>();
            foreach (var d in centerIdx.GridDiskDistances(8))
            {
                var gc = H3Topology.FromH3((ulong)d.Index, 0);
                var loc = new WorldLocation(gc.X, gc.Y, 0);
                world.SetTerrain("Plains", loc);
                if (!ringCells.ContainsKey(d.Distance))
                    ringCells[d.Distance] = loc;
            }

            var center = H3Topology.FromH3((ulong)centerIdx, 0);
            player = new WorldLocation(center.X, center.Y, 0);
            byDistance = dist => ringCells[dist];
            return world;
        }
    }
}
