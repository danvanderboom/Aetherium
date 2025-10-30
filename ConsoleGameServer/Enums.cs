using System;
using System.Collections.Generic;
using System.Text;

namespace ConsoleGame
{
    public enum MazeLocationType
    {
        Room,
        Wall,
        Pillar
    }

    public enum Axis
    {
        X,
        Y,
        Z
    }

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

    public enum WorldEventType
    {
        EntityAdded,
        EntityMoved,
        EntityRemoved,
        ItemPickedUp,
        ItemDropped,
        ItemUsed,
        DoorOpened,
        DoorClosed,
        DoorLocked,
        DoorUnlocked
    }


    public enum VisualType
    {
        Character,
        Object,
        Attack
    }

    public enum SoundType
    {
        None,
        Movement,
        MovementObstruction,
        Attack,
        Death,
        TeleportRandomly,
        SetTeleportHome,
        Earthquake,
        Explosion
    }

    public enum FeelingType
    {
        None,
        Vibration,
        Pain,
        Heat,
        Cold,
        Hunger
    }

    public enum ActionType
    {
        Move,
        Attack,
        Eat,
        Teleport,
        Speak
    }
}
