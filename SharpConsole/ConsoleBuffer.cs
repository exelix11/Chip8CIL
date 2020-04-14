using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleGame
{
	class ConsoleBuffer
	{
		class ScreenChar
		{
			public char c;
			public ConsoleColor fg;

			public void Clear()
			{
				c = ' ';
				fg = ConsoleColor.White;
			}
		}

		struct Point { public int x, y; }

		Dictionary<Point, ScreenChar> FramePoints = new Dictionary<Point, ScreenChar>();
		Point[] OldPoints = new Point[0];

		bool ClearBeforeDraw = false;
		public int Width { get; private set; }
		public int Height { get; private set; }

		public ConsoleBuffer()
		{
			Console.OutputEncoding = Encoding.UTF8;
			Console.CursorVisible = false;
			checkSize();
		}

		public bool checkSize()
		{
			if (!(Console.WindowWidth == Width && Console.WindowHeight == Height ))
			{
				Resize(Console.WindowWidth, Console.WindowHeight);
				return false; //Buffer was not valid
			}
			return true;
		}

		void Resize(int width, int height)
		{
			Width = width;
			Height = height;
			UnbufferedClear();
		}

		public void UnbufferedClear() 
		{
			CursorX = 0;
			CursorY = 0;
			FramePoints.Clear();
			OldPoints = new Point[0];
			ClearBeforeDraw = false;
			Console.Clear();
		}

		public void Clear()
		{
			if (!checkSize()) return;
			CursorY = 0;
			CursorX = 0;
			ClearBeforeDraw = true;
		}

		public int CursorY = 0;
		public int CursorX = 0;

		public void WriteLine(string s, ConsoleColor foreground = ConsoleColor.White)
		{
			if (s == null) return;
			foreach (var c in s)
				Write(c, foreground);
			Write('\n');
		}

		public void Write(string s, ConsoleColor foreground = ConsoleColor.White)
		{
			foreach (var c in s)
				Write(c, foreground);
		}

		public void Write(char c, ConsoleColor foreground = ConsoleColor.White)
		{
			if (CursorX >= Width) { CursorX -= Width; CursorY++; }
			if (CursorY >= Height) return;

			if (c != ' ')
			{
				var pos = new Point() { x = CursorX, y = CursorY };
				var scrChar = new ScreenChar() { c = c, fg = foreground };

				if (FramePoints.ContainsKey(pos))
					FramePoints[pos] = scrChar;
				else
					FramePoints.Add(pos, scrChar);
			}

			if (c == '\n')
			{
				CursorY++;
				CursorX = 0;
			}
			else
				CursorX++;
		}

		public void Display()
		{
			if (!checkSize()) return;

			Console.BackgroundColor = ConsoleColor.Black;
			Console.ForegroundColor = ConsoleColor.White;

			if (ClearBeforeDraw)
			{
				foreach (var p in OldPoints.Where(x => !FramePoints.ContainsKey(x)))
				{
					Console.SetCursorPosition(p.x, p.y);
					Console.Write(' ');
				}
				ClearBeforeDraw = false;
			}

			foreach (var k in FramePoints)
			{
				Console.SetCursorPosition(k.Key.x, k.Key.y);
				Console.ForegroundColor = k.Value.fg;
				Console.Write(k.Value.c);
			}

			Console.ResetColor();
			Console.CursorVisible = false;

			CursorY = 0;
			CursorX = 0;

			OldPoints = FramePoints.Keys.ToArray();
			FramePoints.Clear();
		}
	}
}
