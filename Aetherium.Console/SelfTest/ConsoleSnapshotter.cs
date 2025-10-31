using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Aetherium.SelfTest
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

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadConsoleOutputAttribute(
            IntPtr hConsoleOutput,
            [Out] ushort[] lpAttribute,
            uint nLength,
            COORD dwReadCoord,
            out uint lpNumberOfAttrsRead);

        [StructLayout(LayoutKind.Sequential)]
        private struct SMALL_RECT
        {
            public short Left;
            public short Top;
            public short Right;
            public short Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CONSOLE_SCREEN_BUFFER_INFO
        {
            public COORD dwSize;
            public COORD dwCursorPosition;
            public ushort wAttributes;
            public SMALL_RECT srWindow;
            public COORD dwMaximumWindowSize;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetConsoleScreenBufferInfo(
            IntPtr hConsoleOutput,
            out CONSOLE_SCREEN_BUFFER_INFO lpConsoleScreenBufferInfo);

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

        public static ushort[,] CaptureAttrRect(int left, int top, int width, int height)
        {
            var hConsole = GetStdHandle(STD_OUTPUT_HANDLE);
            if (hConsole == IntPtr.Zero)
                return new ushort[0, 0];

            var attrs = new ushort[Math.Max(0, height), Math.Max(0, width)];
            for (int row = 0; row < height; row++)
            {
                var buffer = new ushort[width];
                ReadConsoleOutputAttribute(
                    hConsole,
                    buffer,
                    (uint)width,
                    new COORD((short)left, (short)(top + row)),
                    out _);
                for (int col = 0; col < width; col++)
                {
                    attrs[row, col] = buffer[col];
                }
            }
            return attrs;
        }

        public static ushort GetCurrentAttributes()
        {
            var hConsole = GetStdHandle(STD_OUTPUT_HANDLE);
            if (hConsole == IntPtr.Zero)
                return 0;

            if (GetConsoleScreenBufferInfo(hConsole, out var info))
                return info.wAttributes;
            return 0;
        }

        public static string[] GenerateAttrHeatmap(ushort[,] attrs, ushort defaultAttributes)
        {
            int height = attrs.GetLength(0);
            int width = attrs.GetLength(1);
            var lines = new string[Math.Max(0, height)];

            ushort defFg = (ushort)(defaultAttributes & 0x000F);
            ushort defBg = (ushort)((defaultAttributes & 0x00F0) >> 4);

            for (int row = 0; row < height; row++)
            {
                var line = new char[Math.Max(0, width)];
                for (int col = 0; col < width; col++)
                {
                    ushort attr = attrs[row, col];
                    ushort fg = (ushort)(attr & 0x000F);
                    ushort bg = (ushort)((attr & 0x00F0) >> 4);
                    bool fgDiff = fg != defFg;
                    bool bgDiff = bg != defBg;
                    line[col] = bgDiff ? 'B' : (fgDiff ? 'f' : '.');
                }
                lines[row] = new string(line);
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



