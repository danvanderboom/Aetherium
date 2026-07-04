namespace Aetherium.Model
{
    /// <summary>
    /// A character — a monster/NPC or another player — visible within the
    /// perceiving player's field of view. Carries a <see cref="TileTypeDto"/> so
    /// the client can render it through the same glyph/color pipeline as terrain
    /// and the player marker. Emitted per visible cell in
    /// <see cref="PerceptionDto.VisibleCharacters"/>, parallel to
    /// <see cref="PerceptionDto.VisibleItems"/>; the perceiving player's own
    /// character is never included (they are always the center marker).
    /// </summary>
    public class CharacterDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = "Character";

        /// <summary>
        /// Glyph/color source, taken from the entity's Tile component. May be null
        /// for a bare character with no tile — the client falls back to a default
        /// glyph in that case.
        /// </summary>
        public TileTypeDto? Tile { get; set; }

        /// <summary>True for monsters/NPCs; false for player characters.</summary>
        public bool IsHostile { get; set; }

        /// <summary>
        /// Location relative to the perceiving player (who is always 0,0,0), matching
        /// the relative-coordinate contract used by <see cref="PerceptionDto.Visuals"/>.
        /// </summary>
        public WorldLocationDto? Location { get; set; }
    }
}
