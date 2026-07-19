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
            // Terrain is now referenced by id; auto-populate the TileTypes palette from the ids the
            // visuals reference so the store can resolve them — mirroring the server invariant that every
            // visual's TileTypeId is present in the frame's palette. Name == id keeps the existing
            // remembered.Terrain.Name assertions valid.
            foreach (var visual in frame.Visuals.Values)
                if (visual.TileTypeId is { } id && !frame.TileTypes.ContainsKey(id))
                    frame.TileTypes[id] = new TileTypeDto { Name = id };
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
                f.Visuals["0,0,0"] = new VisualDto { TileTypeId = "Cave" };
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
                f.Visuals["0,0,0"] = new VisualDto { TileTypeId = "Cave", LightLevel = 0.9 };
                f.Visuals["1,0,0"] = new VisualDto { TileTypeId = "Wall", LightLevel = 0.8 };
            }));
            Assert.That(revealed, Is.EquivalentTo(new[] { new GridPoint(0, 0, 0), new GridPoint(1, 0, 0) }));

            // Next frame only shows the origin cell — the wall slipped out of view but
            // stays remembered (explored-but-dark rendering).
            store.ApplyFrame(Frame(f =>
                f.Visuals["0,0,0"] = new VisualDto { TileTypeId = "Cave", LightLevel = 0.9 }));

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

            var first = Frame(f => f.Visuals["0,0,0"] = new VisualDto { TileTypeId = "Cave" });
            var second = Frame(f => f.Visuals["0,0,0"] = new VisualDto { TileTypeId = "Wall" });

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
                f.Visuals["0,0,0"] = new VisualDto { TileTypeId = "Floor" };
                f.Visuals["0,-1,0"] = new VisualDto { TileTypeId = "Wall" };
                f.Visuals["1,0,0"] = new VisualDto { TileTypeId = "Floor" };
            }));

            // Player steps east. Post-move frame (computed at the new location: the wall is
            // now rel -1,-1; old cell rel -1,0) arrives while the move is still in flight.
            var postMove = Frame(f =>
            {
                f.Visuals["0,0,0"] = new VisualDto { TileTypeId = "Floor" };
                f.Visuals["-1,0,0"] = new VisualDto { TileTypeId = "Floor" };
                f.Visuals["-1,-1,0"] = new VisualDto { TileTypeId = "Wall" };
                // Floor one cell north of the NEW position. Folded at the OLD anchor this
                // rel lands exactly on the remembered wall's cell — the corrupting write.
                f.Visuals["0,-1,0"] = new VisualDto { TileTypeId = "Floor" };
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

        [Test]
        public void Discontinuity_DuringHold_DropsTheHeldFrame()
        {
            // Respawn/portal while a move is in flight: the held frame was computed in the
            // pre-discontinuity epoch; applying it after the wipe would pollute fresh memory
            // with stale geometry folded at the reset anchor.
            var store = new PerceptionStore();

            using (store.HoldFrames())
            {
                store.ApplyFrame(Frame(f =>
                    f.Visuals["0,0,0"] = new VisualDto { TileTypeId = "PreDeathFloor" }));
                store.NoteDiscontinuity(ReanchorReason.Respawned);
            }

            Assert.That(store.Memory, Is.Empty, "the stale held frame must not leak into the new epoch");
            Assert.That(store.LatestFrame, Is.Null);
            Assert.That(store.Anchor, Is.EqualTo(GridPoint.Origin));
        }

        [Test]
        public void TerrainChange_InView_UpdatesRememberedIdentity()
        {
            // The render contract for doors and breached walls: when a cell's terrain
            // legitimately changes, memory must adopt the new identity (views re-materialize
            // from memory, so a stale name would leave a closed door on screen forever).
            var store = new PerceptionStore();

            store.ApplyFrame(Frame(f =>
                f.Visuals["1,0,0"] = new VisualDto { TileTypeId = "Door" }));
            store.ApplyFrame(Frame(f =>
                f.Visuals["1,0,0"] = new VisualDto { TileTypeId = "OpenDoor" }));

            var cell = store.Memory.Single(c => c.Position == new GridPoint(1, 0, 0));
            Assert.That(cell.Terrain!.Name, Is.EqualTo("OpenDoor"));
        }

        [Test]
        public void RotationOnlyFrames_DoNotChurnMemoryOrEntities()
        {
            // Relative offsets are world-axis-aligned, never heading-rotated: a frame that
            // differs only in HeadingDegrees must not move a single cell or entity. If this
            // churns, every turn would smear the map (the store-level face of the invariant
            // the in-proc rotate-then-step test pins live).
            var store = new PerceptionStore();
            PerceptionDto WithHeading(int heading) => Frame(f =>
            {
                f.HeadingDegrees = heading;
                f.Visuals["0,-1,0"] = new VisualDto { TileTypeId = "Wall" };
                f.Visuals["1,0,0"] = new VisualDto { TileTypeId = "Floor" };
                f.VisibleCharacters.Add(Npc("npc-1", 1, -1));
            });

            store.ApplyFrame(WithHeading(0));
            var wallBefore = store.Memory.Single(c => c.Terrain!.Name == "Wall").Position;
            var npcBefore = store.Entities.Single().Position;

            var moves = 0;
            store.EntityMoved += (_, _, _) => moves++;

            foreach (var heading in new[] { 90, 180, 270, 0 })
                store.ApplyFrame(WithHeading(heading));

            Assert.That(store.Memory.Single(c => c.Terrain!.Name == "Wall").Position, Is.EqualTo(wallBefore));
            Assert.That(store.Entities.Single().Position, Is.EqualTo(npcBefore));
            Assert.That(moves, Is.Zero, "rotation alone must never read as entity motion");
            Assert.That(store.Memory, Has.Count.EqualTo(2), "no phantom cells from turning in place");
        }

        [Test]
        public void ChangeLevel_MemoryIsPerDeck_NotOverwrittenAcrossZ()
        {
            // Multi-deck stations: the same X,Y on different decks are different cells. A
            // client rendering deck 1 must not inherit deck 0's geometry.
            var store = new PerceptionStore();

            store.ApplyFrame(Frame(f =>
                f.Visuals["1,0,0"] = new VisualDto { TileTypeId = "Wall" }));

            store.AdvanceAnchor(0, 0, 1); // took the stairs up
            store.ApplyFrame(Frame(f =>
                f.Visuals["1,0,0"] = new VisualDto { TileTypeId = "Vent" }));

            Assert.That(store.Memory.Single(c => c.Position == new GridPoint(1, 0, 0)).Terrain!.Name,
                Is.EqualTo("Wall"), "deck 0 geometry survives");
            Assert.That(store.Memory.Single(c => c.Position == new GridPoint(1, 0, 1)).Terrain!.Name,
                Is.EqualTo("Vent"), "deck 1 revealed at its own Z");
        }

        [Test]
        public void ReappearingEntity_IsFreshTracking_KillMarkDoesNotLinger()
        {
            // An entity that vanished and later re-enters perception is a NEW sighting: if
            // the old WasDefeated flag lingered, its next vanish would wrongly play death VFX.
            var store = new PerceptionStore();

            store.ApplyFrame(Frame(f => f.VisibleCharacters.Add(Npc("npc-1", 1, 0))));
            store.NoteAttackResult("npc-1", remainingHealth: 0, defeated: true);
            store.ApplyFrame(Frame(_ => { })); // vanishes (marked as a kill)

            var appearances = 0;
            store.EntityAppeared += _ => appearances++;
            store.ApplyFrame(Frame(f => f.VisibleCharacters.Add(Npc("npc-1", 2, 0)))); // same id returns

            Assert.That(appearances, Is.EqualTo(1), "re-entry raises EntityAppeared");
            Assert.That(store.Entities.Single().WasDefeated, Is.False, "fresh tracking, no stale kill mark");
        }

        [Test]
        public void SequencedFrames_StaleAfterMove_IsDropped()
        {
            // The tick-race variant of the wall-eater: a frame computed BEFORE our move can
            // be delivered AFTER the move's response (tick and tool flows share the server
            // connection). MoveSequence orders frames against our own movement: anything
            // older than the anchor's count must be dropped, not folded one cell off.
            var store = new PerceptionStore();
            var received = 0;
            store.FrameReceived += _ => received++;

            store.ApplyFrame(Frame(f =>
            {
                f.MoveSequence = 1;
                f.Visuals["0,-1,0"] = new VisualDto { TileTypeId = "Wall" };
            }));
            Assert.That(received, Is.EqualTo(1));

            store.AdvanceAnchor(1, 0, 0); // stepped east: anchor now corresponds to count 2

            // Stale pre-move frame arrives late: floor where (folded at the new anchor) the
            // wall lives. Must be dropped entirely.
            store.ApplyFrame(Frame(f =>
            {
                f.MoveSequence = 1;
                f.Visuals["-1,-1,0"] = new VisualDto { TileTypeId = "Floor" };
            }));
            Assert.That(received, Is.EqualTo(1), "stale frame must not raise FrameReceived");
            Assert.That(store.Memory.Single().Terrain!.Name, Is.EqualTo("Wall"), "memory untouched by the stale frame");

            // The genuine post-move frame folds normally.
            store.ApplyFrame(Frame(f =>
            {
                f.MoveSequence = 2;
                f.Visuals["0,0,0"] = new VisualDto { TileTypeId = "Floor" };
            }));
            Assert.That(received, Is.EqualTo(2));
            Assert.That(store.Memory.Single(c => c.Position == new GridPoint(1, 0, 0)).Terrain!.Name, Is.EqualTo("Floor"));
        }

        [Test]
        public void HeldFrames_PreferHigherSequence_NotLaterArrival()
        {
            // During a move's hold, a stale tick frame can be SENT after the post-move frame.
            // The stash must keep the highest MoveSequence, not the latest arrival.
            var store = new PerceptionStore();

            var postMove = Frame(f =>
            {
                f.MoveSequence = 2;
                f.Visuals["0,0,0"] = new VisualDto { TileTypeId = "Floor" };
            });
            var staleTick = Frame(f =>
            {
                f.MoveSequence = 1;
                f.Visuals["0,0,0"] = new VisualDto { TileTypeId = "Wall" };
            });

            using (store.HoldFrames())
            {
                store.ApplyFrame(postMove);   // push beat the response…
                store.ApplyFrame(staleTick);  // …then a stale tick lands even later
                store.AdvanceAnchor(1, 0, 0); // response resolves the move
            }

            Assert.That(store.LatestFrame, Is.SameAs(postMove),
                "the post-move frame survives the stash; the stale tick is discarded");
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
