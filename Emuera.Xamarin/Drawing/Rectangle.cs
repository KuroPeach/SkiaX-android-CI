using System;

namespace XEmuera.Drawing
{
	public struct Rectangle : IEquatable<Rectangle>
	{
		public static readonly Rectangle Empty = new Rectangle();

		public Rectangle(int x, int y, int width, int height)
		{
			X = x;
			Y = y;
			Width = width;
			Height = height;
		}

		public Rectangle(Point location, Size size)
		{
			X = location.X;
			Y = location.Y;
			Width = size.Width;
			Height = size.Height;
		}

		public int X { get; set; }
		public int Y { get; set; }
		public int Width { get; set; }
		public int Height { get; set; }

		public int Left => X;
		public int Top => Y;
		public int Right => X + Width;
		public int Bottom => Y + Height;

		public int LeftSet { set => X = value; }
		public int TopSet { set => Y = value; }

		public Point Location
		{
			get => new Point(X, Y);
			set { X = value.X; Y = value.Y; }
		}

		public Size Size
		{
			get => new Size(Width, Height);
			set { Width = value.Width; Height = value.Height; }
		}

		public bool IsEmpty => X == 0 && Y == 0 && Width == 0 && Height == 0;

		public bool Contains(Point point)
		{
			return point.X >= X && point.X < X + Width &&
				   point.Y >= Y && point.Y < Y + Height;
		}

		public bool Contains(int x, int y)
		{
			return x >= X && x < X + Width &&
				   y >= Y && y < Y + Height;
		}

		public bool Contains(Rectangle rect)
		{
			return X <= rect.X && rect.Right <= Right &&
				   Y <= rect.Y && rect.Bottom <= Bottom;
		}

		public bool IntersectsWith(Rectangle rect)
		{
			return rect.X < Right && X < rect.Right &&
				   rect.Y < Bottom && Y < rect.Bottom;
		}

		public static Rectangle Intersect(Rectangle a, Rectangle b)
		{
			int x = Math.Max(a.X, b.X);
			int y = Math.Max(a.Y, b.Y);
			int right = Math.Min(a.Right, b.Right);
			int bottom = Math.Min(a.Bottom, b.Bottom);
			if (x < right && y < bottom)
				return new Rectangle(x, y, right - x, bottom - y);
			return Empty;
		}

		public static Rectangle Union(Rectangle a, Rectangle b)
		{
			int x = Math.Min(a.X, b.X);
			int y = Math.Min(a.Y, b.Y);
			int right = Math.Max(a.Right, b.Right);
			int bottom = Math.Max(a.Bottom, b.Bottom);
			return new Rectangle(x, y, right - x, bottom - y);
		}

		public void Offset(int x, int y)
		{
			X += x;
			Y += y;
		}

		public void Offset(Point pos)
		{
			Offset(pos.X, pos.Y);
		}

		public Rectangle Inflate(int width, int height)
		{
			return new Rectangle(X - width, Y - height, Width + 2 * width, Height + 2 * height);
		}

#if WINDOWS
		public static implicit operator System.Drawing.Rectangle(Rectangle r)
			=> new System.Drawing.Rectangle(r.X, r.Y, r.Width, r.Height);
		public static implicit operator Rectangle(System.Drawing.Rectangle r)
			=> new Rectangle(r.X, r.Y, r.Width, r.Height);
#endif

		public static bool operator ==(Rectangle left, Rectangle right)
			=> left.X == right.X && left.Y == right.Y && left.Width == right.Width && left.Height == right.Height;
		public static bool operator !=(Rectangle left, Rectangle right) => !(left == right);

		public bool Equals(Rectangle other)
			=> X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;
		public override bool Equals(object obj) => obj is Rectangle r && Equals(r);
		public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);

		public override string ToString() => $"{{X={X},Y={Y},Width={Width},Height={Height}}}";
	}
}
