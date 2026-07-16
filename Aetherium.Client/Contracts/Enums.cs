namespace Aetherium.Client.Contracts
{
    // Mirror enums (docs/design/unity-sample/unity-client-library.md): the wire carries enums
    // as INTEGERS, so member ORDER is the contract — each mirror must match Aetherium.Model's
    // declaration order exactly. The drift tests in Aetherium.Client.Tests assert value-level
    // equality against the server enums, so a reordered or inserted member breaks the build.

    /// <summary>Note South before East — matches the server's declaration order.</summary>
    public enum WorldDirection
    {
        North,
        South,
        East,
        West,
        Up,
        Down,
    }

    public enum RelativeDirection
    {
        Forward,
        Backward,
        Left,
        Right,
        Up,
        Down,
    }

    public enum VisualType
    {
        Character,
        Object,
        Attack,
    }

    public enum LightingMode
    {
        Torch,
        Sunlight,
        Ambient,
    }

    public enum VisionMode
    {
        Normal,
        Infrared,
    }
}
