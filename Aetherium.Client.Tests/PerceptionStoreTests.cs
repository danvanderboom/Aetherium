using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Aetherium.Client;
using Aetherium.Client.Contracts;

namespace Aetherium.Client.Tests
{
    /// <summary>
    /// The PerceptionStore's three jobs (docs/design/unity-sample/unity-client-library.md):
    /// anchoring (stable client-space from player-relative frames), frame diffing (entity
    /// lifecycle events), and memory (last-seen terrain). All tests drive the store with
    /// synthetic frames — no server, no SignalR.
    /// </summary>
    [TestFixture]
    public class PerceptionStoreTests
    {
        private static PerceptionDto Frame(
            Action<PerceptionDto>? configure = null)
        {
            var frame = new PerceptionDto { UpdateTimestamp = Guid.NewGuid() };
            configure?.Invoke(frame);
            return frame;
        }

        private static CharacterDto Npc(string id, int relX, int relY, string name = "Creature:custodian") => new CharacterDto
        {
            Id = id,
            Name = name,
            IsHostile = true,
            Location = new WorldLocationDto(relX, relY, 0),
        };

        private static ItemDto Loot(string id, int relX, int relY) => new ItemDto
        {
            Id = id,
            Label = "Scrap",
            Icon = "*",
            Location = new WorldLocationDto(relX, relY, 0),
        };

        // ---- anchoring ----

        [Test]
        public void Anchor_StartsAtOrigin_AndAdvancesWithMovement()
        {
            var store = new PerceptionStore();
            Assert.That(store.Anchor, Is.EqualTo(GridPoint.Origin));

            store.AdvanceAnchor(1, 0, 0);   // stepped east
            store.AdvanceAnchor(0, -2, 0);  // two steps north
            Assert.That(store.Anchor, Is.EqualTo(new GridPoint(1, -2, 0)));
        }

        [Test]
        public void Entities_GetStableClientSpacePositions_AcrossPlayerMovement()
        {
            var store = new PerceptionStore();

            // The custodian stands 2 east of the player.
            store.ApplyFrame(Frame(f => f.VisibleCharacters.Add(Npc("npc-1", 2, 0))));
            Assert.That(store.Entities.Single().Position, Is.EqualTo(new GridPoint(2, 0, 0)));

            // The player steps east; the custodian holds still, so its relative offset
            // shrinks to 1 — but its client-space position must NOT move.
            store.AdvanceAnchor(1, 0, 0);
            var moved = new List<string>();
            store.EntityMoved += (entity, _, _) => moved.Add(entity.Id);
            store.ApplyFrame(Frame(f => f.VisibleCharacters.Add(Npc("npc-1", 1, 0))));

            Assert.That(store.Entities.Single().Position, Is.EqualTo(new GridPoint(2, 0, 0)),
                "a stationary entity keeps its client-space cell while the player moves");
            Assert.That(moved, Is.Empty, "no EntityMoved for an entity that only changed relative offset");
        }

        [Test]
        public void Discontinuity_ResetsAnchorEntitiesAndMemory_AndRaisesReanchored()
        {
            var store = new PerceptionStore();
            store.AdvanceAnchor(3, 3, 0);
            store.ApplyFrame(Frame(f =>
            {
                f.Visuals["0,0,0"] = new VisualDto { Terrain = new TileTypeDto { Name = "Cave" } };
                f.VisibleCharacters.Add(Npc("npc-1", 1, 0));
            }));

            ReanchorReason? reason = null;
            store.Reanchored += r => reason = r;
            store.NoteDiscontinuity(ReanchorReason.Portal);

            Assert.That(reason, Is.EqualTo(ReanchorReason.Portal));
            Assert.That(store.Anchor, Is.EqualTo(GridPoint.Origin));
            Assert.That(store.Entities, Is.Empty, "tracking is wiped — the far side is a different place");
            Assert.That(store.Memory, Is.Empty, "remembered geometry can no longer be trusted");
            Assert.That(store.LatestFrame, Is.Null);
        }

        // ---- frame diffing → lifecycle events ----

        [Test]
        public void FirstSighting_RaisesEntityAppeared_WithCreatureTypeParsed()
        {
            var store = new PerceptionStore();
            TrackedEntity? appeared = null;
            store.EntityAppeared += e => appeared = e;

            store.ApplyFrame(Frame(f => f.VisibleCharacters.Add(Npc("npc-1", 2, -1))));

            Assert.That(appeared, Is.Not.Null);
            Assert.That(appeared!.Id, Is.EqualTo("npc-1"));
            Assert.That(appeared.CreatureTypeId, Is.EqualTo("custodian"), "theme binding keys off the content id");
            Assert.That(appeared.IsItem, Is.False);
            Assert.That(appeared.Position, Is.EqualTo(new GridPoint(2, -1, 0)));
        }

        [Test]
        public void EntityMotion_RaisesEntityMoved_WithClientSpaceFromTo()
        {
            var store = new PerceptionStore();
            store.ApplyFrame(Frame(f => f.VisibleCharacters.Add(Npc("npc-1", 2, 0))));

            (GridPoint From, GridPoint To)? movement = null;
            store.EntityMoved += (_, from, to) => movement = (from, to);
            store.ApplyFrame(Frame(f => f.VisibleCharacters.Add(Npc("npc-1", 2, 1))));

            Assert.That(movement, Is.Not.Null);
            Assert.That(movement!.Value.From, Is.EqualTo(new GridPoint(2, 0, 0)));
            Assert.That(movement.Value.To, Is.EqualTo(new GridPoint(2, 1, 0)));
        }

        [Test]
        public void LeavingPerception_RaisesEntityVanished_WithKillMarkingFromAttackResults()
        {
            var store = new PerceptionStore();
            store.ApplyFrame(Frame(f => f.VisibleCharacters.Add(Npc("npc-1", 1, 0))));

            // Our own attack reported the kill — the store folds it in, so the vanish
            // reads as a death, not an unexplained disappearance.
            store.NoteAttackResult("npc-1", remainingHealth: 0, defeated: true);

            TrackedEntity? vanished = null;
            store.EntityVanished += e => vanished = e;
            store.ApplyFrame(Frame()); // next frame: gone

            Assert.That(vanished, Is.Not.Null);
            Assert.That(vanished!.WasDefeated, Is.True);
            Assert.That(vanished.LastKnownHealth, Is.EqualTo(0));
            Assert.That(store.Entities, Is.Empty);
        }

        [Test]
        public void Items_AreTracked_LikeCharacters()
        {
            var store = new PerceptionStore();
            store.ApplyFrame(Frame(f => f.VisibleItems.Add(Loot("item-1", 0, 2))));

            var tracked = store.Entities.Single();
            Assert.That(tracked.IsItem, Is.True);
            Assert.That(tracked.Item!.Label, Is.EqualTo("Scrap"));
            Assert.That(tracked.Position, Is.EqualTo(new GridPoint(0, 2, 0)));
        }

        // ---- memory ----

        [Test]
        public void Memory_RetainsExploredCells_AndFlagsOutOfViewOnes()
        {
            var store = new PerceptionStore();
            var revealed = new List<GridPoint>();
            store.CellRevealed += cell => revealed.Add(cell.Position);

            store.ApplyFrame(Frame(f =>
            {
                f.Visuals["0,0,0"] = new VisualDto { Terrain = new TileTypeDto { Name = "Cave" }, LightLevel = 0.9 };
                f.Visuals["1,0,0"] = new VisualDto { Terrain = new TileTypeDto { Name = "Wall" }, LightLevel = 0.8 };
            }));
            Assert.That(revealed, Is.EquivalentTo(new[] { new GridPoint(0, 0, 0), new GridPoint(1, 0, 0) }));

            // Next frame only shows the origin cell — the wall slipped out of view but
            // stays remembered (explored-but-dark rendering).
            store.ApplyFrame(Frame(f =>
                f.Visuals["0,0,0"] = new VisualDto { Terrain = new TileTypeDto { Name = "Cave" }, LightLevel = 0.9 }));

            var wall = store.Memory.Single(c => c.Position == new GridPoint(1, 0, 0));
            Assert.That(wall.Terrain!.Name, Is.EqualTo("Wall"));
            Assert.That(wall.InView, Is.False);
            var origin = store.Memory.Single(c => c.Position == GridPoint.Origin);
            Assert.That(origin.InView, Is.True);
        }

        // ---- frame holds (the move/push anchor race) ----

        [Test]
        public void HoldFrames_DefersApplication_LatestWins_AppliedOnRelease()
        {
            var store = new PerceptionStore();
            var received = new List<PerceptionDto>();
            store.FrameReceived += received.Add;

            var first = Frame(f => f.Visuals["0,0,0"] = new VisualDto { Terrain = new TileTypeDto { Name = "Cave" } });
            var second = Frame(f => f.Visuals["0,0,0"] = new VisualDto { Terrain = new TileTypeDto { Name = "Wall" } });

            using (store.HoldFrames())
            {
                store.ApplyFrame(first);
                store.ApplyFrame(second);
                Assert.That(store.LatestFrame, Is.Null, "held frames must not apply inside the scope");
                Assert.That(received, Is.Empty);
            }

            // Only the latest held frame applies (frames are full snapshots).
            Assert.That(received, Has.Count.EqualTo(1));
            Assert.That(store.LatestFrame, Is.SameAs(second));
            Assert.That(store.Memory.Single().Terrain!.Name, Is.EqualTo("Wall"));
        }

        [Test]
        public void HoldFrames_MoveRace_WallBehindYouSurvivesTheMove()
        {
            // The live bug: the hub pushes the post-move frame BEFORE the move response
            // arrives. Unheld, that frame folds against the old anchor — the floor now one
            // step behind the player overwrites the remembered wall you just walked past.
            var store = new PerceptionStore();

            // Standing at origin: wall visible one cell north (rel 0,-1).
            store.ApplyFrame(Frame(f =>
            {
                f.Visuals["0,0,0"] = new VisualDto { Terrain = new TileTypeDto { Name = "Floor" } };
                f.Visuals["0,-1,0"] = new VisualDto { Terrain = new TileTypeDto { Name = "Wall" } };
                f.Visuals["1,0,0"] = new VisualDto { Terrain = new TileTypeDto { Name = "Floor" } };
            }));

            // Player steps east. Post-move frame (computed at the new location: the wall is
            // now rel -1,-1; old cell rel -1,0) arrives while the move is still in flight.
            var postMove = Frame(f =>
            {
                f.Visuals["0,0,0"] = new VisualDto { Terrain = new TileTypeDto { Name = "Floor" } };
                f.Visuals["-1,0,0"] = new VisualDto { Terrain = new TileTypeDto { Name = "Floor" } };
                f.Visuals["-1,-1,0"] = new VisualDto { Terrain = new TileTypeDto { Name = "Wall" } };
                // Floor one cell north of the NEW position. Folded at the OLD anchor this
                // rel lands exactly on the remembered wall's cell — the corrupting write.
                f.Visuals["0,-1,0"] = new VisualDto { Terrain = new TileTypeDto { Name = "Floor" } };
            });

            using (store.HoldFrames())
            {
                store.ApplyFrame(postMove);      // push beats the response…
                store.AdvanceAnchor(1, 0, 0);    // …then the response advances the anchor
            }

            // The wall's client-space cell (0,-1) must still be a wall. Without the hold it
            // would have been overwritten by the post-move frame's floor at old-anchor rel.
            var wall = store.Memory.Single(c => c.Position == new GridPoint(0, -1, 0));
            Assert.That(wall.Terrain!.Name, Is.EqualTo("Wall"));
            Assert.That(store.Memory.Single(c => c.Position == new GridPoint(1, 0, 0)).Terrain!.Name,
                Is.EqualTo("Floor"), "the player's new cell folded at the settled anchor");
        }

        // ---- parsing helpers ----

        [Test]
        public void RelativeKeys_ParseIncludingNegatives_AndRejectGarbage()
        {
            Assert.That(PerceptionStore.TryParseRelativeKey("3,-4,1", out var rel), Is.True);
            Assert.That(rel, Is.EqualTo((3, -4, 1)));
            Assert.That(PerceptionStore.TryParseRelativeKey("1,2", out _), Is.False);
            Assert.That(PerceptionStore.TryParseRelativeKey("a,b,c", out _), Is.False);
            Assert.That(PerceptionStore.TryParseRelativeKey("", out _), Is.False);
        }

        [Test]
        public void CreatureTypeId_ParsesFromNameOrTile_NullForPlayers()
        {
            Assert.That(PerceptionStore.ParseCreatureTypeId(new CharacterDto { Name = "Creature:mite" }), Is.EqualTo("mite"));
            Assert.That(PerceptionStore.ParseCreatureTypeId(new CharacterDto
            {
                Name = "Something",
                Tile = new TileTypeDto { Name = "Creature:overseer" },
            }), Is.EqualTo("overseer"));
            Assert.That(PerceptionStore.ParseCreatureTypeId(new CharacterDto { Name = "player-7" }), Is.Null);
        }
    }
}
