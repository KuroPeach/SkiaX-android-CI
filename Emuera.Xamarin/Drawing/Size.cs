using System;

namespace XEmuera.Drawing
{
	public struct Size : IEquatable<Size>
	{
		public static readonly Size Empty = new Size();

		public Size(int width, int height)
		{
			Width = width;
			Height = height;
		}

		public Size(Point pt)
		{
			Width = pt.X;
			Height = pt.Y;
		}

		public int Width { get; set; }
		public int Height { get; set; }

		public bool IsEmpty => Width == 0 && Height == 0;

#if WINDOWS
		public static implicit operator System.Drawing.Size(Size s)
			=> new System.Drawing.Size(s.Width, s.Height);
		public static implicit operator Size(System.Drawing.Size s)
			=> new Size(s.Width, s.Height);
#endif

		public static bool operator ==(Size left, Size right) => left.Width == right.Width && left.Height == right.Height;
		public static bool operator !=(Size left, Size right) => !(left == right);

		public static Size operator +(Size sz1, Size sz2) => new Size(sz1.Width + sz2.Width, sz1.Height + sz2.Height);
		public static Size operator -(Size sz1, Size sz2) => new Size(sz1.Width - sz2.Width, sz1.Height - sz2.Height);

		public bool Equals(Size other) => Width == other.Width && Height == other.Height;
		public override bool Equals(object obj) => obj is Size s && Equals(s);
		public override int GetHashCode() => HashCode.Combine(Width, Height);

		public override string ToString() => $"{{Width={Width},Height={Height}}}";
	}
}
