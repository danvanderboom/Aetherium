using System;
using System.Collections.Generic;
using System.Text;

namespace Aetherium.Core
{
    public class SoundEffects
    {
        public static void PlayTeleportSound()
        {
            Console.Beep(200, 100);
            Console.Beep(400, 100);
            Console.Beep(800, 100);
            Console.Beep(1600, 100);
        }

        public static void PlaySetTeleportHomeSound()
        {
            Console.Beep(1600, 100);
            Console.Beep(200, 100);
        }

        public static void PlayObstructionSound()
        {
            Console.Beep(200, 100);
        }

        public static void PlayDeathSound()
        {
            Console.Beep(200, 100);
            Console.Beep(100, 100);
            Console.Beep(50, 100);
        }

        public static void PlayDiggingSound()
        {
            Console.Beep(300, 25);
        }
    }
}

