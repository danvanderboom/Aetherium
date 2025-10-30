using System;

namespace ConsoleGameModel
{
    public enum WorldDirection
    {
        North,
        South,
        East,
        West,
        Up,
        Down
    }

    public enum RelativeDirection
    {
        Forward,
        Backward,
        Left,
        Right,
        Up,
        Down
    }

    public enum VisualType
    {
        Character,
        Object,
        Attack
    }

    public enum LightingMode
    {
        Torch,
        Sunlight,
        Ambient
    }

    public enum VisionMode
    {
        Normal,
        Infrared
    }
}


