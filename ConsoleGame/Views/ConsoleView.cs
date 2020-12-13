using System;
using System.Drawing;

namespace ConsoleGame.Views
{
    public abstract class ConsoleView
    {
        public Point ScreenPosition { get; set; }

        public Size Size { get; set; }

        public bool HasFrame { get; set; }

        public ConsoleColor FrameBackgroundColor { get; set; } = ConsoleColor.Gray;

        public ConsoleColor FrameForegroundColor { get; set; } = ConsoleColor.Black;

        public ConsoleColor BackgroundColor { get; set; } = ConsoleColor.Black;

        public ConsoleView() { }

        public ConsoleView(Point location, Size size, bool hasFrame = true)
        {
            ScreenPosition = location;
            Size = size;
            HasFrame = hasFrame;
        }

        class BoxFrameInfo
        {
            public static char Corner = ' ';
            public static char Horizontal = ' ';
            public static char Vertical = ' ';
        }

        public void Clear(bool clearFrame = false, ConsoleColor? backgroundColor = null)
        {
            Console.BackgroundColor = backgroundColor.HasValue ? backgroundColor.Value : this.BackgroundColor;

            var contentSize = HasFrame ? Size.FromDelta(-1, -1) : Size;
            var clearWidth = clearFrame ? Size.Width : contentSize.Width;
            var clearHeight = clearFrame ? Size.Height : contentSize.Height;

            var start = !HasFrame || (HasFrame && clearFrame) ? ScreenPosition
                : ScreenPosition.FromDelta(+1, +1);

            var hline = new string(' ', clearWidth);

            for (int y = 0; y < clearHeight; y++)
            {
                Console.SetCursorPosition(ScreenPosition.X, ScreenPosition.Y + y);
                Console.Write(hline);
            }
        }

        public virtual void DrawFrame()
        {
            Console.SetCursorPosition(ScreenPosition.X, ScreenPosition.Y);

            Console.BackgroundColor = FrameBackgroundColor;
            Console.ForegroundColor = FrameForegroundColor;

            var hline =
                BoxFrameInfo.Corner
                + new string(BoxFrameInfo.Horizontal, Size.Width - 2)
                + BoxFrameInfo.Corner;

            Console.Write(hline);

            for (int y = 0; y < Size.Height - 2; y++)
            {
                Console.CursorTop = ScreenPosition.Y + y + 1;

                Console.CursorLeft = ScreenPosition.X;
                Console.Write(BoxFrameInfo.Vertical);

                Console.CursorLeft = ScreenPosition.X + Size.Width - 1;
                Console.Write(BoxFrameInfo.Vertical);
            }

            Console.CursorTop += 1;
            Console.CursorLeft = ScreenPosition.X;

            Console.Write(hline);
        }

        public void DrawContents()
        {
            if (HasFrame)
                DrawContents(ScreenPosition.FromDelta(+1, +1), Size.FromDelta(-2, -2));
            else
                DrawContents(ScreenPosition, Size);
        }

        protected abstract void DrawContents(Point screenPosition, Size size);

        public static string CenterText(string text, int length)
        {
            var start = (length / 2) - (text.Length / 2);
            return text.PadLeft(text.Length + start).PadRight(length);
        }
    }
}
