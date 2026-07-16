using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using NUnit.Framework;
using ServerModel = Aetherium.Model;
using ClientContracts = Aetherium.Client.Contracts;

namespace Aetherium.Client.Tests
{
    /// <summary>
    /// The protocol drift suite (docs/design/unity-sample/repo-structure.md): this project is
    /// the ONE place that references both sides of the wire. Two guards:
    /// (1) fully-populated server DTOs are serialized with the hub's JSON behavior and
    /// deserialized into the client mirrors, asserting field-level equality;
    /// (2) a reflection sweep asserts every public property on each server wire DTO has a
    /// same-named mirror counterpart, and every mirror enum matches the server enum's names
    /// AND integer values (enums ride the wire as ints — order is the contract).
    /// A new server field breaks this build, not a shipped game.
    /// </summary>
    [TestFixture]
    public class ProtocolDriftTests
    {
        // SignalR's default hub protocol is System.Text.Json with the server's property names
        // emitted verbatim (no naming policy, enums as integers). Both ends run the same
        // protocol, so round-tripping through these options mirrors the wire.
        private static readonly JsonSerializerOptions WireJson = new JsonSerializerOptions();

        private static TClient RoundTrip<TServer, TClient>(TServer server)
        {
            var json = JsonSerializer.Serialize(server, WireJson);
            return JsonSerializer.Deserialize<TClient>(json, WireJson)!;
        }

        // ---- (1) field-level round-trip of a fully-populated perception frame ----

        private static ServerModel.PerceptionDto FullServerFrame() => new ServerModel.PerceptionDto
        {
            PlayerLocation = new ServerModel.WorldLocationDto(0, 0, 0),
            PlayerHeading = ServerModel.WorldDirection.East,
            HeadingDegrees = 90,
            IsDirectionalVision = true,
            FieldOfViewDegrees = 140,
            Topology = "hex",
            SelfCellParity = 1,
            Visuals = new Dictionary<string, ServerModel.VisualDto>
            {
                ["1,-2,0"] = new ServerModel.VisualDto
                {
                    Location = new ServerModel.WorldLocationDto(1, -2, 0),
                    Terrain = new ServerModel.TileTypeDto("Cave", new Dictionary<string, string> { ["MapCharacter"] = "t" }),
                    Entities = { new ServerModel.TileTypeDto("Creature:waxgrub", new Dictionary<string, string>()) },
                    LightLevel = 0.6,
                    ThingsSeen = new Dictionary<ServerModel.VisualType, int> { [ServerModel.VisualType.Character] = 1 },
                },
            },
            VisibleBounds = new ServerModel.RectangleDto(-10, -10, 21, 21),
            UpdateTimestamp = Guid.NewGuid(),
            TileTypes = new Dictionary<string, ServerModel.TileTypeDto>
            {
                ["Player"] = new ServerModel.TileTypeDto("Player", new Dictionary<string, string> { ["MapCharacter"] = "@" }),
            },
            Inventory = new ServerModel.InventoryDto
            {
                Capacity = 10,
                Items = { new ServerModel.ItemDto { Id = "item-1", Label = "Medgel", Icon = "+", KeyId = null } },
            },
            VisibleItems =
            {
                new ServerModel.ItemDto { Id = "item-2", Label = "Arc Cutter", Icon = "/", KeyId = "red", Location = new ServerModel.WorldLocationDto(2, 1, 0) },
            },
            VisibleCharacters =
            {
                new ServerModel.CharacterDto
                {
                    Id = "npc-1",
                    Name = "Creature:custodian",
                    Tile = new ServerModel.TileTypeDto("Creature:custodian", new Dictionary<string, string> { ["ForegroundColor"] = "Cyan" }),
                    IsHostile = true,
                    Location = new ServerModel.WorldLocationDto(-1, 3, 0),
                },
            },
            Affordances =
            {
                new ServerModel.AffordanceDto
                {
                    Action = "use",
                    ActorId = "player-1",
                    TargetId = "door-1",
                    ItemId = "item-2",
                    RequiresKeyId = "red",
                    UsageOptions = { new ServerModel.AffordanceUsageDto { UsageId = "cut", Label = "Cut open", TargetId = "door-1" } },
                },
            },
            NavigationData = new ServerModel.NavigationDataDto { HasCompass = true, HeadingDegrees = 90, CardinalDirection = ServerModel.WorldDirection.East },
            CurrentLightingMode = ServerModel.LightingMode.Ambient,
            CurrentVisionMode = ServerModel.VisionMode.Infrared,
            GameTimeOfDay = 21.5,
            AmbientTint = (0.9, 0.5, 0.4),
            Weather = "Rainy",
            Season = "winter",
            Audio = new ServerModel.AudioPerceptionDto
            {
                Biome = "station",
                DangerLevel = 0.7f,
                ReverbPreset = "hall",
                Occlusion = 0.2f,
                AmbientEmitters = new Dictionary<string, ServerModel.AmbientEmitterDto>
                {
                    ["vent-1"] = new ServerModel.AmbientEmitterDto { X = 2, Y = 0, Z = 0, TrackName = "vent_hiss", Volume = 0.4f, Loop = true },
                },
                SuggestedMusicTrack = "tension_1",
                FootstepMaterial = "metal",
            },
            Interoception = new ServerModel.InteroceptionDto
            {
                Health = 12,
                MaxHealth = 40,
                Statuses = { new ServerModel.SelfStatusDto { Id = "burning", RemainingTicks = 3 } },
                Pools = { new ServerModel.ResourcePoolStateDto { Tag = "heat", Current = 10, Max = 50, IsInverse = true } },
                Cooldowns = { new ServerModel.AbilityReadinessDto { AbilityId = "breach", RemainingTicks = 2 } },
            },
        };

        [Test]
        public void PerceptionFrame_RoundTrips_FieldForField()
        {
            var server = FullServerFrame();
            var mirror = RoundTrip<ServerModel.PerceptionDto, ClientContracts.PerceptionDto>(server);

            Assert.That(mirror.PlayerHeading, Is.EqualTo(ClientContracts.WorldDirection.East));
            Assert.That(mirror.HeadingDegrees, Is.EqualTo(90));
            Assert.That(mirror.IsDirectionalVision, Is.True);
            Assert.That(mirror.FieldOfViewDegrees, Is.EqualTo(140));
            Assert.That(mirror.Topology, Is.EqualTo("hex"));
            Assert.That(mirror.SelfCellParity, Is.EqualTo(1));
            Assert.That(mirror.UpdateTimestamp, Is.EqualTo(server.UpdateTimestamp));
            Assert.That(mirror.GameTimeOfDay, Is.EqualTo(21.5));
            Assert.That(mirror.Weather, Is.EqualTo("Rainy"));
            Assert.That(mirror.Season, Is.EqualTo("winter"));

            var visual = mirror.Visuals["1,-2,0"];
            Assert.That(visual.Terrain!.Name, Is.EqualTo("Cave"));
            Assert.That(visual.Terrain.Settings["MapCharacter"], Is.EqualTo("t"));
            Assert.That(visual.Entities.Single().Name, Is.EqualTo("Creature:waxgrub"));
            Assert.That(visual.LightLevel, Is.EqualTo(0.6));
            Assert.That(visual.ThingsSeen[ClientContracts.VisualType.Character], Is.EqualTo(1));

            Assert.That(mirror.VisibleBounds.X, Is.EqualTo(-10));
            Assert.That(mirror.VisibleBounds.Width, Is.EqualTo(21));

            Assert.That(mirror.Inventory!.Capacity, Is.EqualTo(10));
            Assert.That(mirror.Inventory.Items.Single().Label, Is.EqualTo("Medgel"));

            var item = mirror.VisibleItems.Single();
            Assert.That((item.Id, item.Label, item.Icon, item.KeyId), Is.EqualTo(("item-2", "Arc Cutter", "/", "red")));
            Assert.That((item.Location!.X, item.Location.Y, item.Location.Z), Is.EqualTo((2, 1, 0)));

            var character = mirror.VisibleCharacters.Single();
            Assert.That(character.Id, Is.EqualTo("npc-1"));
            Assert.That(character.Name, Is.EqualTo("Creature:custodian"));
            Assert.That(character.IsHostile, Is.True);
            Assert.That(character.Tile!.Settings["ForegroundColor"], Is.EqualTo("Cyan"));
            Assert.That((character.Location!.X, character.Location.Y), Is.EqualTo((-1, 3)));

            var affordance = mirror.Affordances.Single();
            Assert.That(affordance.Action, Is.EqualTo("use"));
            Assert.That(affordance.RequiresKeyId, Is.EqualTo("red"));
            Assert.That(affordance.UsageOptions.Single().UsageId, Is.EqualTo("cut"));

            Assert.That(mirror.NavigationData!.HasCompass, Is.True);
            Assert.That(mirror.NavigationData.CardinalDirection, Is.EqualTo(ClientContracts.WorldDirection.East));

            Assert.That(mirror.CurrentLightingMode, Is.EqualTo(ClientContracts.LightingMode.Ambient));
            Assert.That(mirror.CurrentVisionMode, Is.EqualTo(ClientContracts.VisionMode.Infrared));

            var audio = mirror.Audio!;
            Assert.That(audio.Biome, Is.EqualTo("station"));
            Assert.That(audio.DangerLevel, Is.EqualTo(0.7f));
            Assert.That(audio.ReverbPreset, Is.EqualTo("hall"));
            Assert.That(audio.AmbientEmitters["vent-1"].TrackName, Is.EqualTo("vent_hiss"));
            Assert.That(audio.SuggestedMusicTrack, Is.EqualTo("tension_1"));
            Assert.That(audio.FootstepMaterial, Is.EqualTo("metal"));

            var interoception = mirror.Interoception!;
            Assert.That((interoception.Health, interoception.MaxHealth), Is.EqualTo((12, 40)));
            Assert.That(interoception.Statuses.Single().Id, Is.EqualTo("burning"));
            Assert.That(interoception.Pools.Single().IsInverse, Is.True);
            Assert.That(interoception.Cooldowns.Single().AbilityId, Is.EqualTo("breach"));
        }

        [Test]
        public void AmbientTint_WireBehavior_IsPinned()
        {
            // The server's AmbientTint is a ValueTuple. System.Text.Json does not serialize
            // ValueTuple FIELDS by default, so today's wire carries an empty object and the
            // mirror keeps its neutral-white default. This test pins that behavior: if the
            // server ever starts emitting Item1/2/3 (e.g. IncludeFields or a converter), the
            // JSON changes shape and this assertion flips — update the mirror deliberately.
            var json = JsonSerializer.Serialize(FullServerFrame(), WireJson);
            using var doc = JsonDocument.Parse(json);
            var tint = doc.RootElement.GetProperty("AmbientTint");

            bool carriesItems = tint.TryGetProperty("Item1", out _);
            Assert.That(carriesItems, Is.False,
                "AmbientTint now carries Item1/2/3 on the wire — update ClientContracts.AmbientTintDto's drift expectations.");

            var mirror = JsonSerializer.Deserialize<ClientContracts.PerceptionDto>(json, WireJson)!;
            Assert.That(mirror.AmbientTint.Item1, Is.EqualTo(1.0), "mirror defaults to neutral white");
        }

        [Test]
        public void GameState_Vitals_ToolResult_RoundTrip()
        {
            var state = RoundTrip<ServerModel.GameStateDto, ClientContracts.GameStateDto>(new ServerModel.GameStateDto
            {
                PlayerId = "session-1",
                PlayerHeading = ServerModel.WorldDirection.South,
                TileTypes = new Dictionary<string, ServerModel.TileTypeDto> { ["Wall"] = new ServerModel.TileTypeDto("Wall", new Dictionary<string, string>()) },
            });
            Assert.That(state.PlayerId, Is.EqualTo("session-1"));
            Assert.That(state.PlayerHeading, Is.EqualTo(ClientContracts.WorldDirection.South));
            Assert.That(state.TileTypes.ContainsKey("Wall"), Is.True);

            var vitals = RoundTrip<ServerModel.PlayerVitalsDto, ClientContracts.PlayerVitalsDto>(new ServerModel.PlayerVitalsDto
            {
                Health = 0,
                MaxHealth = 100,
                IsDowned = true,
                DownedTicksRemaining = 8,
                IsInvulnerable = false,
            });
            Assert.That((vitals.Health, vitals.MaxHealth, vitals.IsDowned, vitals.DownedTicksRemaining),
                Is.EqualTo((0, 100, true, 8)));

            var tool = RoundTrip<ServerModel.ToolExecutionResultDto, ClientContracts.ToolExecutionResultDto>(
                new ServerModel.ToolExecutionResultDto
                {
                    Success = true,
                    Message = "Attacked",
                    Data = new Dictionary<string, object> { ["damage"] = 7, ["defeated"] = false },
                });
            Assert.That(tool.Success, Is.True);
            Assert.That(tool.Message, Is.EqualTo("Attacked"));
            Assert.That(tool.Data, Is.Not.Null);
        }

        [Test]
        public void Lobby_WorldInfo_JoinResult_RoundTrip()
        {
            var worldInfo = RoundTrip<global::Aetherium.Server.MultiWorld.WorldInfo, ClientContracts.WorldInfo>(
                new global::Aetherium.Server.MultiWorld.WorldInfo
                {
                    WorldId = "w-1",
                    Name = "Aphelion Station",
                    Description = "A dark deck",
                    State = global::Aetherium.Server.MultiWorld.WorldState.Active,
                    PlayerCount = 2,
                    MaxPlayers = 20,
                    CreatedAt = new DateTime(2026, 7, 16, 12, 0, 0, DateTimeKind.Utc),
                    MapIds = { "w-1-map-1" },
                    GameDefinitionId = "aphelion",
                });
            Assert.That(worldInfo.WorldId, Is.EqualTo("w-1"));
            Assert.That(worldInfo.State, Is.EqualTo(ClientContracts.WorldState.Active));
            Assert.That(worldInfo.MapIds.Single(), Is.EqualTo("w-1-map-1"));
            Assert.That(worldInfo.GameDefinitionId, Is.EqualTo("aphelion"));

            var join = RoundTrip<global::Aetherium.Server.MultiWorld.JoinWorldResult, ClientContracts.JoinWorldResult>(
                new global::Aetherium.Server.MultiWorld.JoinWorldResult
                {
                    Success = true,
                    WorldId = "w-1",
                    MapId = "w-1-map-1",
                    SpawnX = 5,
                    SpawnY = 7,
                    SpawnZ = 0,
                });
            Assert.That(join.Success, Is.True);
            Assert.That((join.SpawnX, join.SpawnY, join.SpawnZ), Is.EqualTo((5, 7, 0)));
        }

        // ---- (2) reflection sweep: every server wire property has a mirror counterpart ----

        private static readonly (Type ServerType, Type ClientType)[] MirrorPairs =
        {
            (typeof(ServerModel.PerceptionDto), typeof(ClientContracts.PerceptionDto)),
            (typeof(ServerModel.VisualDto), typeof(ClientContracts.VisualDto)),
            (typeof(ServerModel.TileTypeDto), typeof(ClientContracts.TileTypeDto)),
            (typeof(ServerModel.CharacterDto), typeof(ClientContracts.CharacterDto)),
            (typeof(ServerModel.ItemDto), typeof(ClientContracts.ItemDto)),
            (typeof(ServerModel.InventoryDto), typeof(ClientContracts.InventoryDto)),
            (typeof(ServerModel.AffordanceDto), typeof(ClientContracts.AffordanceDto)),
            (typeof(ServerModel.AffordanceUsageDto), typeof(ClientContracts.AffordanceUsageDto)),
            (typeof(ServerModel.NavigationDataDto), typeof(ClientContracts.NavigationDataDto)),
            (typeof(ServerModel.AudioPerceptionDto), typeof(ClientContracts.AudioPerceptionDto)),
            (typeof(ServerModel.AmbientEmitterDto), typeof(ClientContracts.AmbientEmitterDto)),
            (typeof(ServerModel.RectangleDto), typeof(ClientContracts.RectangleDto)),
            (typeof(ServerModel.WorldLocationDto), typeof(ClientContracts.WorldLocationDto)),
            (typeof(ServerModel.InteroceptionDto), typeof(ClientContracts.InteroceptionDto)),
            (typeof(ServerModel.SelfStatusDto), typeof(ClientContracts.SelfStatusDto)),
            (typeof(ServerModel.ResourcePoolStateDto), typeof(ClientContracts.ResourcePoolStateDto)),
            (typeof(ServerModel.AbilityReadinessDto), typeof(ClientContracts.AbilityReadinessDto)),
            (typeof(ServerModel.GameStateDto), typeof(ClientContracts.GameStateDto)),
            (typeof(ServerModel.PlayerVitalsDto), typeof(ClientContracts.PlayerVitalsDto)),
            (typeof(ServerModel.ToolExecutionResultDto), typeof(ClientContracts.ToolExecutionResultDto)),
            (typeof(ServerModel.ToolInfoDto), typeof(ClientContracts.ToolInfoDto)),
            (typeof(ServerModel.ToolUsageOptionDto), typeof(ClientContracts.ToolUsageOptionDto)),
            (typeof(ServerModel.ToolParameterSchemaDto), typeof(ClientContracts.ToolParameterSchemaDto)),
            (typeof(ServerModel.ParameterDefinitionDto), typeof(ClientContracts.ParameterDefinitionDto)),
            (typeof(ServerModel.InteractionResultDto), typeof(ClientContracts.InteractionResultDto)),
            (typeof(ServerModel.UsageOptionDto), typeof(ClientContracts.UsageOptionDto)),
            (typeof(ServerModel.QuestSummaryDto), typeof(ClientContracts.QuestSummaryDto)),
            (typeof(ServerModel.QuestObjectiveDto), typeof(ClientContracts.QuestObjectiveDto)),
            (typeof(ServerModel.QuestLogDto), typeof(ClientContracts.QuestLogDto)),
            (typeof(ServerModel.EnterDungeonResultDto), typeof(ClientContracts.EnterDungeonResultDto)),
            (typeof(ServerModel.PartyInfoDto), typeof(ClientContracts.PartyInfoDto)),
            (typeof(ServerModel.PartyMemberDto), typeof(ClientContracts.PartyMemberDto)),
            (typeof(global::Aetherium.Server.MultiWorld.WorldInfo), typeof(ClientContracts.WorldInfo)),
            (typeof(global::Aetherium.Server.MultiWorld.JoinWorldResult), typeof(ClientContracts.JoinWorldResult)),
        };

        [Test]
        public void EveryServerWireProperty_HasAMirrorCounterpart()
        {
            var missing = new List<string>();
            foreach (var (serverType, clientType) in MirrorPairs)
            {
                var clientProperties = clientType
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Select(p => p.Name)
                    .ToHashSet(StringComparer.Ordinal);

                foreach (var property in serverType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    if (!clientProperties.Contains(property.Name))
                        missing.Add($"{serverType.Name}.{property.Name} has no mirror on {clientType.Name}");
            }
            Assert.That(missing, Is.Empty,
                "Server wire DTOs gained properties the client mirrors don't carry:\n" + string.Join("\n", missing));
        }

        private static readonly (Type ServerEnum, Type ClientEnum)[] EnumPairs =
        {
            (typeof(ServerModel.WorldDirection), typeof(ClientContracts.WorldDirection)),
            (typeof(ServerModel.RelativeDirection), typeof(ClientContracts.RelativeDirection)),
            (typeof(ServerModel.VisualType), typeof(ClientContracts.VisualType)),
            (typeof(ServerModel.LightingMode), typeof(ClientContracts.LightingMode)),
            (typeof(ServerModel.VisionMode), typeof(ClientContracts.VisionMode)),
            (typeof(global::Aetherium.Server.MultiWorld.WorldState), typeof(ClientContracts.WorldState)),
        };

        [Test]
        public void EveryMirrorEnum_MatchesServerNamesAndIntegerValues()
        {
            // Enums cross the wire as integers, so VALUE (declaration order) is the contract.
            foreach (var (serverEnum, clientEnum) in EnumPairs)
            {
                foreach (var name in Enum.GetNames(serverEnum))
                {
                    Assert.That(Enum.IsDefined(clientEnum, name), Is.True,
                        $"{clientEnum.Name} is missing member '{name}' (present on server {serverEnum.Name}).");
                    var serverValue = Convert.ToInt32(Enum.Parse(serverEnum, name));
                    var clientValue = Convert.ToInt32(Enum.Parse(clientEnum, name));
                    Assert.That(clientValue, Is.EqualTo(serverValue),
                        $"{clientEnum.Name}.{name} = {clientValue}, but server {serverEnum.Name}.{name} = {serverValue} — integer wire values diverged.");
                }
            }
        }
    }
}
