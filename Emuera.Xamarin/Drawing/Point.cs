using System;

namespace XEmuera.Drawing
{
	public struct Point : IEquatable<Point>
	{
		public static readonly Point Empty = new Point();

		public Point(int x, int y)
		{
			X = x;
			Y = y;
		}

		public Point(Size size)
		{
			X = size.Width;
			Y = size.Height;
		}

		public int X { get; set; }
		public int Y { get; set; }

		public bool IsEmpty => X == 0 && Y == 0;

		public void Offset(int dx, int dy)
		{
			X += dx;
			Y += dy;
		}

		public void Offset(Point p)
		{
			Offset(p.X, p.Y);
		}

#if WINDOWS
		public static implicit operator System.Drawing.Point(Point p)
			=> new System.Drawing.Point(p.X, p.Y);
		public static implicit operator Point(System.Drawing.Point p)
			=> new Point(p.X, p.Y);
#endif

		public static bool operator ==(Point left, Point right) => left.X == right.X && left.Y == right.Y;
		public static bool operator !=(Point left, Point right) => !(left == right);

		public static Point operator +(Point pt, Size sz) => new Point(pt.X + sz.Width, pt.Y + sz.Height);
		public static Point operator -(Point pt, Size sz) => new Point(pt.X - sz.Width, pt.Y - sz.Height);

		public bool Equals(Point other) => X == other.X && Y == other.Y;
		public override bool Equals(object obj) => obj is Point p && Equals(p);
		public override int GetHashCode() => HashCode.Combine(X, Y);

		public override string ToString() => $"{{X={X},Y={Y}}}";
	}
}
