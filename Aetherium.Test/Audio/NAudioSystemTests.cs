extern alias Console;
using System;
using System.IO;
using NUnit.Framework;
using NAudioSystem = Console::Aetherium.Audio.NAudioSystem;
using AudioConfig = Console::Aetherium.Audio.AudioConfig;

namespace Aetherium.Test.Audio
{
    /// <summary>
    /// Unit coverage of the NAudio-backed audio system's degradation behavior (P3-9). Previously
    /// audio had no tests beyond <see cref="AudioDirectorTests"/> against a mock; these pin the
    /// graceful-fallback contract: never throw, never spam the TUI, resolve assets from bin/.
    /// </summary>
    [TestFixture]
    public class NAudioSystemTests
    {
        [Test]
        public void IsOutputAvailable_NeverThrows()
        {
            // It gates the NullAudioSystem fallback, so it must be safe on any host.
            Assert.DoesNotThrow(() => NAudioSystem.IsOutputAvailable());
        }

        [Test]
        public void AssetRoot_RelativePath_ResolvesUnderBaseDirectory()
        {
            // The config default ("Assets/Audio") is cwd-relative; it must resolve under the app
            // base directory so lookups succeed when the client runs from bin/.
            using var sys = new NAudioSystem(new AudioConfig { AssetPath = "Assets/Audio" });

            Assert.That(Path.IsPathRooted(sys.AssetRoot), Is.True);
            Assert.That(sys.AssetRoot,
                Does.StartWith(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)));
        }

        [Test]
        public void AssetRoot_AbsolutePath_IsPreserved()
        {
            var abs = Path.Combine(Path.GetTempPath(), "aetherium-audio-abs");
            using var sys = new NAudioSystem(new AudioConfig { AssetPath = abs });

            Assert.That(sys.AssetRoot, Is.EqualTo(abs));
        }

        [Test]
        public void MissingTrack_IsSilent_DoesNotThrow_AndKeepsAudioEnabled()
        {
            // A missing file is a soft failure (like a not-found effect): silent, no self-mute.
            using var sys = new NAudioSystem(new AudioConfig
            {
                AssetPath = Path.Combine(Path.GetTempPath(), "aetherium-no-such-audio-dir")
            });

            Assert.DoesNotThrow(() => sys.PlaySoundEffect("nope"));
            Assert.DoesNotThrow(() => sys.PlayBackgroundMusic("nope"));
            Assert.That(sys.LastError, Is.Null, "A missing asset is not a device error.");
            Assert.That(sys.IsEnabled, Is.True, "A missing asset must not disable audio.");
        }

        [Test]
        public void SetReverbPreset_RecordsPreset_EvenThoughDspIsUnsupported()
        {
            // Reverb DSP is intentionally unsupported; the preset is still recorded for introspection.
            using var sys = new NAudioSystem(new AudioConfig());
            sys.SetReverbPreset("cave");
            Assert.That(sys.CurrentReverbPreset, Is.EqualTo("cave"));
        }
    }
}
