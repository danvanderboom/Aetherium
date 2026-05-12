using System;
using System.IO;
using System.Threading.Tasks;
using Aetherium.Server.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Orleans;
using Orleans.Runtime;
using Orleans.Serialization;
using Orleans.Storage;

namespace Aetherium.Test.Persistence
{
    [TestFixture]
    public class SqliteGrainStorageTests
    {
        private string _tempDir = null!;
        private string _connectionString = null!;
        private Serializer _serializer = null!;
        private ILogger<SqliteGrainStorage> _logger = null!;

        [GenerateSerializer]
        public class SampleState
        {
            [Id(0)] public string Name { get; set; } = string.Empty;
            [Id(1)] public int Counter { get; set; }
            [Id(2)] public double Pi { get; set; }
        }

        private class FakeGrainState<T> : IGrainState<T>
        {
            public T State { get; set; } = default!;
            public string ETag { get; set; } = string.Empty;
            public bool RecordExists { get; set; }
        }

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "aetherium-sqlite-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            var dbPath = Path.Combine(_tempDir, "test.db");
            _connectionString = $"Data Source={dbPath};Cache=Shared";

            var services = new ServiceCollection();
            services.AddSerializer();
            var sp = services.BuildServiceProvider();
            _serializer = sp.GetRequiredService<Serializer>();
            _logger = NullLogger<SqliteGrainStorage>.Instance;
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
            }
            catch { /* best-effort cleanup */ }
        }

        [Test]
        public async Task Write_Then_Read_Roundtrips_State()
        {
            var store = new SqliteGrainStorage("worldStore", _connectionString, _serializer, _logger);
            var state = new FakeGrainState<SampleState>
            {
                State = new SampleState { Name = "alpha", Counter = 42, Pi = 3.14 },
            };
            var grainId = GrainId.Create("test-grain", "alpha");

            await store.WriteStateAsync("SampleState", grainId, state);

            var readState = new FakeGrainState<SampleState>();
            await store.ReadStateAsync("SampleState", grainId, readState);

            Assert.That(readState.RecordExists, Is.True);
            Assert.That(readState.State.Name, Is.EqualTo("alpha"));
            Assert.That(readState.State.Counter, Is.EqualTo(42));
            Assert.That(readState.State.Pi, Is.EqualTo(3.14).Within(0.0001));
            Assert.That(readState.ETag, Is.Not.Empty);
        }

        [Test]
        public async Task Read_Missing_Row_Returns_Default_State()
        {
            var store = new SqliteGrainStorage("worldStore", _connectionString, _serializer, _logger);
            var readState = new FakeGrainState<SampleState>();
            var grainId = GrainId.Create("test-grain", "ghost");

            await store.ReadStateAsync("SampleState", grainId, readState);

            Assert.That(readState.RecordExists, Is.False);
            Assert.That(readState.ETag, Is.Empty);
            Assert.That(readState.State, Is.Not.Null);
        }

        [Test]
        public async Task State_Persists_Across_Storage_Instances()
        {
            // Simulates server reboot: write via one instance, dispose, read via a fresh instance
            // against the same SQLite file.
            var grainId = GrainId.Create("test-grain", "durable");
            {
                var store = new SqliteGrainStorage("worldStore", _connectionString, _serializer, _logger);
                var state = new FakeGrainState<SampleState>
                {
                    State = new SampleState { Name = "before-restart", Counter = 100, Pi = 2.71 },
                };
                await store.WriteStateAsync("SampleState", grainId, state);
            }

            // No shared in-process state between the two store instances besides the on-disk file.
            {
                var store2 = new SqliteGrainStorage("worldStore", _connectionString, _serializer, _logger);
                var readState = new FakeGrainState<SampleState>();
                await store2.ReadStateAsync("SampleState", grainId, readState);

                Assert.That(readState.RecordExists, Is.True);
                Assert.That(readState.State.Name, Is.EqualTo("before-restart"));
                Assert.That(readState.State.Counter, Is.EqualTo(100));
            }
        }

        [Test]
        public async Task Clear_Removes_Row()
        {
            var store = new SqliteGrainStorage("worldStore", _connectionString, _serializer, _logger);
            var state = new FakeGrainState<SampleState>
            {
                State = new SampleState { Name = "to-be-deleted", Counter = 1 },
            };
            var grainId = GrainId.Create("test-grain", "del");
            await store.WriteStateAsync("SampleState", grainId, state);

            await store.ClearStateAsync("SampleState", grainId, state);

            Assert.That(state.RecordExists, Is.False);
            Assert.That(state.ETag, Is.Empty);

            var readBack = new FakeGrainState<SampleState>();
            await store.ReadStateAsync("SampleState", grainId, readBack);
            Assert.That(readBack.RecordExists, Is.False);
        }

        [Test]
        public async Task Update_Refreshes_State_And_ETag()
        {
            var store = new SqliteGrainStorage("worldStore", _connectionString, _serializer, _logger);
            var grainId = GrainId.Create("test-grain", "upd");
            var state = new FakeGrainState<SampleState>
            {
                State = new SampleState { Name = "v1", Counter = 1 },
            };
            await store.WriteStateAsync("SampleState", grainId, state);
            var etag1 = state.ETag;

            state.State.Name = "v2";
            state.State.Counter = 2;
            await store.WriteStateAsync("SampleState", grainId, state);
            var etag2 = state.ETag;

            Assert.That(etag2, Is.Not.EqualTo(etag1), "ETag should change on every write.");

            var readBack = new FakeGrainState<SampleState>();
            await store.ReadStateAsync("SampleState", grainId, readBack);
            Assert.That(readBack.State.Name, Is.EqualTo("v2"));
            Assert.That(readBack.State.Counter, Is.EqualTo(2));
            Assert.That(readBack.ETag, Is.EqualTo(etag2));
        }

        [Test]
        public async Task Storage_Names_Are_Isolated()
        {
            var grainId = GrainId.Create("test-grain", "shared");
            var storeA = new SqliteGrainStorage("worldStore", _connectionString, _serializer, _logger);
            var storeB = new SqliteGrainStorage("mapStore", _connectionString, _serializer, _logger);

            var stateA = new FakeGrainState<SampleState>
            {
                State = new SampleState { Name = "world-data" },
            };
            await storeA.WriteStateAsync("SampleState", grainId, stateA);

            var readFromB = new FakeGrainState<SampleState>();
            await storeB.ReadStateAsync("SampleState", grainId, readFromB);
            Assert.That(readFromB.RecordExists, Is.False, "Same grain id in a different storage should not collide.");
        }
    }
}
