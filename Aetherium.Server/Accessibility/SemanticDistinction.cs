using System.Collections.Generic;
using System.Linq;

namespace Aetherium.Server.Accessibility
{
    public enum AccessibilityChannel
    {
        Color,
        Shape,
        Label,
        Audio,
    }

    /// <summary>
    /// One thing a renderer must communicate to the player (e.g. "this tile is lava, not water"),
    /// and which channels it's encoded through (engine gap-analysis §4.13's colorblind contract:
    /// "every semantic distinction must be also encoded in shape/glyph/label/audio", not color alone).
    /// Optionally references a content-atlas <see cref="Aetherium.Model.ContentAtlas.AudioTag"/> id for its
    /// audio channel, reusing the vocabulary <c>add-content-atlas</c> already shipped instead of a
    /// second audio-tagging scheme.
    /// </summary>
    public class SemanticDistinction
    {
        public string Id { get; }
        public string Description { get; }
        public string? AudioTagId { get; private set; }

        private readonly HashSet<AccessibilityChannel> _channels = new();
        public IReadOnlyCollection<AccessibilityChannel> Channels => _channels;

        public SemanticDistinction(string id, string description)
        {
            Id = id;
            Description = description;
        }

        public void MarkEncodedBy(AccessibilityChannel channel) => _channels.Add(channel);

        public void SetAudioTag(string audioTagId)
        {
            AudioTagId = audioTagId;
            MarkEncodedBy(AccessibilityChannel.Audio);
        }

        public bool IsEncodedBy(AccessibilityChannel channel) => _channels.Contains(channel);
    }

    /// <summary>
    /// The colorblind-contract lint pass (engine gap-analysis §4.13): flags any
    /// <see cref="SemanticDistinction"/> that relies on <see cref="AccessibilityChannel.Color"/>
    /// with no other channel also present.
    /// </summary>
    public class ColorblindLintRule
    {
        public IReadOnlyList<string> FindViolations(IEnumerable<SemanticDistinction> distinctions)
            => distinctions
                .Where(d => d.IsEncodedBy(AccessibilityChannel.Color)
                         && !d.Channels.Any(c => c != AccessibilityChannel.Color))
                .Select(d => d.Id)
                .ToList();
    }
}
