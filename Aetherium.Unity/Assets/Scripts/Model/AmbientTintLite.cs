#nullable enable
using System;
using UnityEngine;

namespace Aetherium.Unity.Model
{
    /// <summary>
    /// Per-frame ambient light tint (rgb, 0..1), mirroring the server's
    /// <c>PerceptionDto.AmbientTint</c>. Drives the atmosphere pass — a warm sunset,
    /// a cold night, etc. Defaults to neutral white (no tint).
    /// </summary>
    [Serializable]
    public class AmbientTintLite
    {
        public float R { get; set; } = 1f;
        public float G { get; set; } = 1f;
        public float B { get; set; } = 1f;

        public AmbientTintLite()
        {
        }

        public AmbientTintLite(float r, float g, float b)
        {
            R = r;
            G = g;
            B = b;
        }

        public Color ToColor() => new Color(R, G, B, 1f);
    }
}
