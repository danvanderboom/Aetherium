using System;

namespace Aetherium.Model.ContentAtlas
{
    /// <summary>Minimal <c>major.minor.patch</c> parser/comparer for <see cref="ContentAtlas"/> versioning.</summary>
    public readonly struct SemVer : IEquatable<SemVer>
    {
        public int Major { get; }
        public int Minor { get; }
        public int Patch { get; }

        public SemVer(int major, int minor, int patch)
        {
            Major = major;
            Minor = minor;
            Patch = patch;
        }

        public static SemVer Parse(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Version string must not be empty.", nameof(value));

            var parts = value.Split('.');
            if (parts.Length != 3
                || !int.TryParse(parts[0], out var major)
                || !int.TryParse(parts[1], out var minor)
                || !int.TryParse(parts[2], out var patch))
                throw new FormatException($"'{value}' is not a valid major.minor.patch version.");

            return new SemVer(major, minor, patch);
        }

        public bool Equals(SemVer other) => Major == other.Major && Minor == other.Minor && Patch == other.Patch;
        public override bool Equals(object? obj) => obj is SemVer other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Major, Minor, Patch);
        public override string ToString() => $"{Major}.{Minor}.{Patch}";
    }
}
