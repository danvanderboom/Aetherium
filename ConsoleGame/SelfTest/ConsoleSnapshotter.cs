using System;
using System.Runtime.InteropServices;
using System.Text;

namespace ConsoleGame.SelfTest
{
    internal static class ConsoleSnapshotter
    {
        private const int STD_OUTPUT_HANDLE = -11;

        [StructLayout(LayoutKind.Sequential)]
        private struct COORD
        {
            public short X;
            public short Y;
            public COORD(short x, short y) { X = x; Y = y; }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool ReadConsoleOutputCharacterW(
            IntPtr hConsoleOutput,
            [Out] StringBuilder lpCharacter,
            uint nLength,
            COORD dwReadCoord,
            out uint lpNumberOfCharsRead);

        public static string[] CaptureRect(int left, int top, int width, int height)
        {
            var hConsole = GetStdHandle(STD_OUTPUT_HANDLE);
            if (hConsole == IntPtr.Zero)
                return Array.Empty<string>();

            var lines = new string[Math.Max(0, height)];
            for (int row = 0; row < height; row++)
            {
                var sb = new StringBuilder(width);
                sb.Append(' ', width);
                ReadConsoleOutputCharacterW(
                    hConsole,
                    sb,
                    (uint)width,
                    new COORD((short)left, (short)(top + row)),
                    out _);
                lines[row] = sb.ToString();
            }
            return lines;
        }

        public static string NormalizeLines(string[] lines)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < lines.Length; i++)
            {
                sb.AppendLine(lines[i]);
            }
            return sb.ToString();
        }
    }
}


