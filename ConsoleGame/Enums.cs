using System;
using System.Collections.Generic;
using System.Text;

namespace ConsoleGame
{
    public enum Axis
    {
        X,
        Y,
        Z
    }

    public enum Direction
    {
        North,
        South,
        East,
        West,
        Up,
        Down
    }

    public enum WorldEventType
    {
        EntityAdded,
        EntityMoved,
        EntityRemoved
    }

    //public enum TileType
    //{
    //    None,
    //    Indoors,
    //    Soil,
    //    TreeRoots,
    //    Rock,
    //    Wall,
    //    Mountain,
    //    Road,
    //    Plains,
    //    Forest,
    //    Water,
    //    Cave,
    //    Player,
    //    Monster,
    //    DeadMonster,
    //    Upstairs,
    //    Downstairs
    //}

    //public enum TerrainType
    //{
    //    None,
    //    Indoors,
    //    Wall,
    //    Mountain,
    //    Road,
    //    Plains,
    //    Forest,
    //    Water,
    //    Cave,
    //    Upstairs,
    //    Downstairs
    //}

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
