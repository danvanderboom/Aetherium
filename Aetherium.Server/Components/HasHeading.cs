using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Aetherium.Core;

namespace Aetherium.Components
{
    public class HasHeading : Component
    {
        private int _heading;

        /// <summary>
        /// Heading in degrees (0-359). 0 = North, 90 = East, 180 = South, 270 = West.
        /// Automatically normalized to 0-359 range.
        /// </summary>
        public int Heading
        {
            get => _heading;
            set => _heading = NormalizeDegrees(value);
        }

        /// <summary>
        /// Field of view in degrees (0-360). Default is 120 degrees for human-like vision.
        /// 360 means omnidirectional vision.
        /// </summary>
        public int FieldOfViewDegrees { get; set; } = 120;

        /// <summary>
        /// Whether perception is limited to a forward cone (see <see cref="FieldOfViewDegrees"/>)
        /// centered on <see cref="Heading"/>. When false the character sees in all directions and
        /// the FOV is ignored. Per-character-type: a human sees a forward arc, a spider-like
        /// sensor might be omnidirectional. Drives whether the perception frame omits cells
        /// behind the character entirely (the frame is genuinely smaller — the server never
        /// computes or sends what falls outside the cone).
        /// </summary>
        public bool IsDirectional { get; set; }

        /// <summary>
        /// Optional per-character sight range in cells (null = the engine's default range).
        /// Note: in a dark world the torch/light radius still governs how far a character can
        /// actually see; this is an additional hard clip for character types that see further
        /// or shorter than the light allows (e.g. infrared hunters).
        /// </summary>
        public int? ViewRange { get; set; }

        /// <summary>
        /// Converts the current heading to the nearest cardinal WorldDirection.
        /// </summary>
        public WorldDirection ToWorldDirection()
        {
            // Round to nearest 90 degrees
            int normalized = Heading % 360;
            if (normalized < 0) normalized += 360;

            if (normalized < 45 || normalized >= 315)
                return WorldDirection.North;
            else if (normalized >= 45 && normalized < 135)
                return WorldDirection.East;
            else if (normalized >= 135 && normalized < 225)
                return WorldDirection.South;
            else
                return WorldDirection.West;
        }

        /// <summary>
        /// Normalizes degrees to the 0-359 range.
        /// </summary>
        private static int NormalizeDegrees(int degrees)
        {
            degrees = degrees % 360;
            if (degrees < 0)
                degrees += 360;
            return degrees;
        }
    }
}

