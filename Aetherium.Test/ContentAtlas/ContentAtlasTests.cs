using NUnit.Framework;
using Aetherium.Model.ContentAtlas;

namespace Aetherium.Test.ContentAtlas
{
    /// <summary>
    /// Unit coverage of the content-atlas schema (engine gap-analysis §4.10, Phase 1 — see
    /// openspec/changes/add-content-atlas). Not yet referenced by any live DTO.
    /// </summary>
    [TestFixture]
    public class ContentAtlasTests
    {
        [Test]
        public void MaterialTag_ExposesTypedFields_NotAStringBag()
        {
            var stone = new MaterialTag("stone", "Stone surface", hardness: 0.9, friction: 0.6, combustibility: 0.0);

            Assert.That(stone.Hardness, Is.EqualTo(0.9));
            Assert.That(stone.Friction, Is.EqualTo(0.6));
            Assert.That(stone.Combustibility, Is.EqualTo(0.0));
        }

        [Test]
        public void LightSourceTag_ExposesTypedFields()
        {
            var torch = new LightSourceTag("torch", "Handheld flame", "#FF8C1A", intensity: 0.7, flicker: true);

            Assert.That(torch.ColorHex, Is.EqualTo("#FF8C1A"));
            Assert.That(torch.Intensity, Is.EqualTo(0.7));
            Assert.That(torch.Flicker, Is.True);
        }

        [Test]
        public void AddTerrainTag_RejectsDuplicateId()
        {
            var atlas = new Model.ContentAtlas.ContentAtlas("1.0.0");
            Assert.That(atlas.AddTerrainTag(new TerrainTag("wall", "First")), Is.True);
            Assert.That(atlas.AddTerrainTag(new TerrainTag("wall", "Second")), Is.False, "Duplicate id must be rejected.");

            Assert.That(atlas.TerrainTags["wall"].Description, Is.EqualTo("First"), "Original tag must be unchanged.");
        }

        [Test]
        public void Contains_IsScopedPerCategory()
        {
            var atlas = new Model.ContentAtlas.ContentAtlas("1.0.0");
            atlas.AddTerrainTag(new TerrainTag("wall", "Solid wall"));

            Assert.That(atlas.Contains(ContentAtlasCategory.Terrain, "wall"), Is.True);
            Assert.That(atlas.Contains(ContentAtlasCategory.EntityKind, "wall"), Is.False, "Same id must not leak across categories.");
        }

        [TestCase("1.0.0", "1.0.0", true)]
        [TestCase("1.3.0", "1.0.0", true, TestName = "SameMajor_AdditiveMinorBump_IsCompatible")]
        [TestCase("1.0.5", "1.0.0", true, TestName = "SameMajor_PatchBump_IsCompatible")]
        [TestCase("2.0.0", "1.9.9", false, TestName = "DifferentMajor_IsNotCompatible")]
        public void SupportsClientVersion_ChecksMajorVersionOnly(string atlasVersion, string clientVersion, bool expected)
        {
            var atlas = new Model.ContentAtlas.ContentAtlas(atlasVersion);
            Assert.That(atlas.SupportsClientVersion(clientVersion), Is.EqualTo(expected));
        }

        [Test]
        public void SemVer_Parse_RoundTrips()
        {
            var v = SemVer.Parse("2.4.7");
            Assert.That(v.Major, Is.EqualTo(2));
            Assert.That(v.Minor, Is.EqualTo(4));
            Assert.That(v.Patch, Is.EqualTo(7));
            Assert.That(v.ToString(), Is.EqualTo("2.4.7"));
        }

        [Test]
        public void SemVer_Parse_RejectsMalformedInput()
        {
            Assert.Throws<System.FormatException>(() => SemVer.Parse("not-a-version"));
        }
    }
}
