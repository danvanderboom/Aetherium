using System;
using NUnit.Framework;
using Aetherium.Audio;
using Aetherium.Model;

namespace Aetherium.Test.Audio
{
    [TestFixture]
    public class AudioDirectorTests
    {
        private MockAudioSystem _mockAudio = null!;
        private AudioDirector _director = null!;

        [SetUp]
        public void SetUp()
        {
            _mockAudio = new MockAudioSystem();
            _director = new AudioDirector(_mockAudio);
        }

        [Test]
        public void OnPerception_DoesNothing_WhenAudioDisabled()
        {
            // Arrange
            _mockAudio.IsEnabled = false;
            var perception = CreatePerceptionWithAudio("forest", 0.0f);

            // Act
            _director.OnPerception(perception);

            // Assert
            Assert.That(_mockAudio.SetListenerCalls, Is.EqualTo(0));
        }

        [Test]
        public void OnPerception_SetsListener_WhenEnabled()
        {
            // Arrange
            var perception = CreatePerceptionWithAudio("forest", 0.0f);

            // Act
            _director.OnPerception(perception);

            // Assert
            Assert.That(_mockAudio.SetListenerCalls, Is.EqualTo(1));
            Assert.That(_mockAudio.LastListenerState, Is.Not.Null);
        }

        [Test]
        public void OnPerception_UpdatesBiome_WhenBiomeChanges()
        {
            // Arrange
            var perception1 = CreatePerceptionWithAudio("forest", 0.0f);
            var perception2 = CreatePerceptionWithAudio("dungeon", 0.0f);

            // Act
            _director.OnPerception(perception1);
            var stopCallsBefore = _mockAudio.StopAmbientLoopCalls;
            _director.OnPerception(perception2);

            // Assert
            Assert.That(_mockAudio.StopAmbientLoopCalls, Is.GreaterThan(stopCallsBefore));
        }

        [Test]
        public void OnPerception_UpdatesMusic_WhenDangerLevelCrossesThreshold()
        {
            // Arrange
            var safePerception = CreatePerceptionWithAudio("forest", 0.1f);
            var dangerPerception = CreatePerceptionWithAudio("forest", 0.4f);

            // Act
            _director.OnPerception(safePerception);
            var musicCallsBefore = _mockAudio.PlayBackgroundMusicCalls;
            _director.OnPerception(dangerPerception);

            // Assert
            Assert.That(_mockAudio.PlayBackgroundMusicCalls, Is.GreaterThan(musicCallsBefore));
        }

        [Test]
        public void OnPerception_DoesNotThrash_WhenDangerLevelOscillatesNearThreshold()
        {
            // Arrange
            var perception1 = CreatePerceptionWithAudio("forest", 0.35f); // Above danger threshold
            var perception2 = CreatePerceptionWithAudio("forest", 0.25f); // Below safe threshold but above danger
            var perception3 = CreatePerceptionWithAudio("forest", 0.15f); // Below safe threshold

            // Act
            _director.OnPerception(perception1);
            var musicCalls1 = _mockAudio.PlayBackgroundMusicCalls;
            _director.OnPerception(perception2);
            var musicCalls2 = _mockAudio.PlayBackgroundMusicCalls;
            _director.OnPerception(perception3);
            var musicCalls3 = _mockAudio.PlayBackgroundMusicCalls;

            // Assert - should only change when crossing thresholds
            Assert.That(musicCalls2, Is.EqualTo(musicCalls1)); // No change in middle zone
            Assert.That(musicCalls3, Is.GreaterThan(musicCalls2)); // Change when crossing safe threshold
        }

        [Test]
        public void PlayFootstep_UsesMaterial_FromCurrentPerception()
        {
            // Arrange
            var perception = CreatePerceptionWithAudio("forest", 0.0f);
            perception.Audio!.FootstepMaterial = "grass";
            _director.OnPerception(perception);

            // Act
            _director.PlayFootstep();

            // Assert
            Assert.That(_mockAudio.LastPositionalEffectName, Is.EqualTo("footstep-grass"));
        }

        [Test]
        public void PlayFootstep_UsesDefault_WhenMaterialIsStone()
        {
            // Arrange
            var perception = CreatePerceptionWithAudio("dungeon", 0.0f);
            perception.Audio!.FootstepMaterial = "stone";
            _director.OnPerception(perception);

            // Act
            _director.PlayFootstep();

            // Assert
            Assert.That(_mockAudio.LastPositionalEffectName, Is.EqualTo("footstep"));
        }

        [Test]
        public void PlayFootstep_DoesNothing_WhenAudioDisabled()
        {
            // Arrange
            _mockAudio.IsEnabled = false;
            var perception = CreatePerceptionWithAudio("forest", 0.0f);
            _director.OnPerception(perception);

            // Act
            _director.PlayFootstep();

            // Assert
            Assert.That(_mockAudio.PlayPositionalEffectCalls, Is.EqualTo(0));
        }

        private static PerceptionDto CreatePerceptionWithAudio(string biome, float dangerLevel)
        {
            return new PerceptionDto
            {
                Audio = new AudioPerceptionDto
                {
                    Biome = biome,
                    DangerLevel = dangerLevel,
                    ReverbPreset = "outdoor",
                    Occlusion = 0.0f,
                    FootstepMaterial = biome == "forest" ? "grass" : "stone",
                    SuggestedMusicTrack = dangerLevel > 0.3f ? "techno-synth-loop" : "mellow-guitar-loop"
                },
                HeadingDegrees = 0
            };
        }

        // Mock audio system for testing
        private class MockAudioSystem : IAudioSystem
        {
            public bool IsEnabled { get; set; } = true;
            public float MusicVolume => 0.5f;
            public float EffectsVolume => 0.7f;
            public string? CurrentTrack => null;

            public int SetListenerCalls { get; private set; }
            public int PlayPositionalEffectCalls { get; private set; }
            public int PlayBackgroundMusicCalls { get; private set; }
            public int StopAmbientLoopCalls { get; private set; }
            public AudioListenerState? LastListenerState { get; private set; }
            public string? LastPositionalEffectName { get; private set; }

            public void PlayBackgroundMusic(string trackName, bool loop = true)
            {
                PlayBackgroundMusicCalls++;
            }

            public void StopBackgroundMusic() { }

            public void PlaySoundEffect(string effectName) { }

            public void SetMusicVolume(float volume) { }

            public void SetEffectsVolume(float volume) { }

            public void NextMusicTrack() { }

            public void SetListener(AudioListenerState state)
            {
                SetListenerCalls++;
                LastListenerState = state;
            }

            public void PlayPositionalEffect(string effectName, AudioVector3 position, AudioPlaybackOptions? options = null)
            {
                PlayPositionalEffectCalls++;
                LastPositionalEffectName = effectName;
            }

            public void PlayAmbientLoop(string id, string trackName, AudioPlaybackOptions? options = null) { }

            public void StopAmbientLoop(string id)
            {
                StopAmbientLoopCalls++;
            }

            public void SetReverbPreset(string preset) { }

            public void SetOcclusion(float amount) { }

            public void Dispose() { }
        }
    }
}

