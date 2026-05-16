using SkiaSharp;
using System;

namespace XEmuera.Drawing
{
	public struct Color : IEquatable<Color>
	{
		private readonly uint _argb;

		private Color(uint argb) { _argb = argb; }

		public byte A => (byte)(_argb >> 24);
		public byte R => (byte)(_argb >> 16);
		public byte G => (byte)(_argb >> 8);
		public byte B => (byte)_argb;

		public bool IsEmpty => _argb == 0;

		public static Color FromArgb(int argb)
		{
			return new Color((uint)argb);
		}

		public static Color FromArgb(int red, int green, int blue)
		{
			return FromArgb(255, red, green, blue);
		}

		public static Color FromArgb(int alpha, int red, int green, int blue)
		{
			return new Color((uint)((alpha << 24) | (red << 16) | (green << 8) | blue));
		}

		public static Color FromArgb(int alpha, Color baseColor)
		{
			return FromArgb(alpha, baseColor.R, baseColor.G, baseColor.B);
		}

		public static Color FromName(string name)
		{
			if (string.IsNullOrEmpty(name))
				return Empty;
			switch (name.ToLowerInvariant())
			{
				case "transparent": return Transparent;
				case "black": return Black;
				case "white": return White;
				case "red": return Red;
				case "green": return Green;
				case "blue": return Blue;
				case "gray": case "grey": return Gray;
				case "darkblue": return DarkBlue;
				case "darkgreen": return DarkGreen;
				case "darkgray": case "darkgrey": return DarkGray;
				case "darkred": return DarkRed;
				case "yellow": return Yellow;
				case "cyan": case "aqua": return Cyan;
				case "magenta": case "fuchsia": return Magenta;
				case "lime": return Lime;
				case "maroon": return Maroon;
				case "olive": return Olive;
				case "purple": return Purple;
				case "silver": return Silver;
				case "teal": return Teal;
				case "navy": return Navy;
				case "orange": return Orange;
				case "pink": return Pink;
				case "brown": return Brown;
				case "gold": return Gold;
				case "lightblue": return LightBlue;
				case "lightgray": case "lightgrey": return LightGray;
				case "lightgreen": return LightGreen;
				case "lightyellow": return LightYellow;
				case "lightpink": return LightPink;
				case "lightsalmon": return LightSalmon;
				case "lightcyan": return LightCyan;
				case "coral": return Coral;
				case "salmon": return Salmon;
				case "tomato": return Tomato;
				case "violet": return Violet;
				case "indigo": return Indigo;
				case "hotpink": return HotPink;
				case "crimson": return Crimson;
				case "chocolate": return Chocolate;
				case "tan": return Tan;
				case "wheat": return Wheat;
				case "khaki": return Khaki;
				case "lavender": return Lavender;
				case "plum": return Plum;
				case "orchid": return Orchid;
				case "sienna": return Sienna;
				case "peru": return Peru;
				case "firebrick": return Firebrick;
				case "darkorange": return DarkOrange;
				case "darkviolet": return DarkViolet;
				case "deeppink": return DeepPink;
				case "goldenrod": return Goldenrod;
				case "greenyellow": return GreenYellow;
				case "limegreen": return LimeGreen;
				case "mediumblue": return MediumBlue;
				case "mediumorchid": return MediumOrchid;
				case "mediumpurple": return MediumPurple;
				case "mediumseagreen": return MediumSeaGreen;
				case "mediumslateblue": return MediumSlateBlue;
				case "mediumspringgreen": return MediumSpringGreen;
				case "mediumturquoise": return MediumTurquoise;
				case "mediumvioletred": return MediumVioletRed;
				case "midnightblue": return MidnightBlue;
				case "orangered": return OrangeRed;
				case "royalblue": return RoyalBlue;
				case "seagreen": return SeaGreen;
				case "slateblue": return SlateBlue;
				case "slategray": case "slategrey": return SlateGray;
				case "springgreen": return SpringGreen;
				case "steelblue": return SteelBlue;
				case "yellowgreen": return YellowGreen;
				default: return Empty;
			}
		}

		public static Color FromUint(uint value)
		{
			return new Color(value);
		}

		public int ToArgb()
		{
			return (int)_argb;
		}

		public SKColor ToSKColor()
		{
			return new SKColor(R, G, B, A);
		}

		public static readonly Color Empty = new Color(0);

		public static readonly Color Transparent = new Color(0x00000000);
		public static readonly Color Black = new Color(0xFF000000);
		public static readonly Color White = new Color(0xFFFFFFFF);
		public static readonly Color Red = new Color(0xFFFF0000);
		public static readonly Color Green = new Color(0xFF008000);
		public static readonly Color Blue = new Color(0xFF0000FF);
		public static readonly Color Gray = new Color(0xFF808080);
		public static readonly Color Grey = Gray;
		public static readonly Color DarkBlue = new Color(0xFF00008B);
		public static readonly Color DarkGreen = new Color(0xFF006400);
		public static readonly Color DarkGray = new Color(0xFFA9A9A9);
		public static readonly Color DarkGrey = DarkGray;
		public static readonly Color DarkRed = new Color(0xFF8B0000);
		public static readonly Color Yellow = new Color(0xFFFFFF00);
		public static readonly Color Cyan = new Color(0xFF00FFFF);
		public static readonly Color Aqua = Cyan;
		public static readonly Color Magenta = new Color(0xFFFF00FF);
		public static readonly Color Fuchsia = Magenta;
		public static readonly Color Lime = new Color(0xFF00FF00);
		public static readonly Color Maroon = new Color(0xFF800000);
		public static readonly Color Olive = new Color(0xFF808000);
		public static readonly Color Purple = new Color(0xFF800080);
		public static readonly Color Silver = new Color(0xFFC0C0C0);
		public static readonly Color Teal = new Color(0xFF008080);
		public static readonly Color Navy = new Color(0xFF000080);
		public static readonly Color Orange = new Color(0xFFFFA500);
		public static readonly Color Pink = new Color(0xFFFFC0CB);
		public static readonly Color Brown = new Color(0xFFA52A2A);
		public static readonly Color Gold = new Color(0xFFFFD700);
		public static readonly Color LightBlue = new Color(0xFFADD8E6);
		public static readonly Color LightGray = new Color(0xFFD3D3D3);
		public static readonly Color LightGrey = LightGray;
		public static readonly Color LightGreen = new Color(0xFF90EE90);
		public static readonly Color LightYellow = new Color(0xFFFFFFE0);
		public static readonly Color LightPink = new Color(0xFFFFB6C1);
		public static readonly Color LightSalmon = new Color(0xFFFFA07A);
		public static readonly Color LightCyan = new Color(0xFFE0FFFF);
		public static readonly Color Coral = new Color(0xFFFF7F50);
		public static readonly Color Salmon = new Color(0xFFFA8072);
		public static readonly Color Tomato = new Color(0xFFFF6347);
		public static readonly Color Violet = new Color(0xFFEE82EE);
		public static readonly Color Indigo = new Color(0xFF4B0082);
		public static readonly Color HotPink = new Color(0xFFFF69B4);
		public static readonly Color Crimson = new Color(0xFFDC143C);
		public static readonly Color Chocolate = new Color(0xFFD2691E);
		public static readonly Color Tan = new Color(0xFFD2B48C);
		public static readonly Color Wheat = new Color(0xFFF5DEB3);
		public static readonly Color Khaki = new Color(0xFFF0E68C);
		public static readonly Color Lavender = new Color(0xFFE6E6FA);
		public static readonly Color Plum = new Color(0xFFDDA0DD);
		public static readonly Color Orchid = new Color(0xFFDA70D6);
		public static readonly Color Sienna = new Color(0xFFA0522D);
		public static readonly Color Peru = new Color(0xFFCD853F);
		public static readonly Color Firebrick = new Color(0xFFB22222);
		public static readonly Color DarkOrange = new Color(0xFFFF8C00);
		public static readonly Color DarkViolet = new Color(0xFF9400D3);
		public static readonly Color DeepPink = new Color(0xFFFF1493);
		public static readonly Color Goldenrod = new Color(0xFFDAA520);
		public static readonly Color GreenYellow = new Color(0xFFADFF2F);
		public static readonly Color LimeGreen = new Color(0xFF32CD32);
		public static readonly Color MediumBlue = new Color(0xFF0000CD);
		public static readonly Color MediumOrchid = new Color(0xFFBA55D3);
		public static readonly Color MediumPurple = new Color(0xFF9370DB);
		public static readonly Color MediumSeaGreen = new Color(0xFF3CB371);
		public static readonly Color MediumSlateBlue = new Color(0xFF7B68EE);
		public static readonly Color MediumSpringGreen = new Color(0xFF00FA9A);
		public static readonly Color MediumTurquoise = new Color(0xFF48D1CC);
		public static readonly Color MediumVioletRed = new Color(0xFFC71585);
		public static readonly Color MidnightBlue = new Color(0xFF191970);
		public static readonly Color OrangeRed = new Color(0xFFFF4500);
		public static readonly Color RoyalBlue = new Color(0xFF4169E1);
		public static readonly Color SeaGreen = new Color(0xFF2E8B57);
		public static readonly Color SlateBlue = new Color(0xFF6A5ACD);
		public static readonly Color SlateGray = new Color(0xFF708090);
		public static readonly Color SlateGrey = SlateGray;
		public static readonly Color SpringGreen = new Color(0xFF00FF7F);
		public static readonly Color SteelBlue = new Color(0xFF4682B4);
		public static readonly Color YellowGreen = new Color(0xFF9ACD32);

		public static implicit operator SKColor(Color c) => new SKColor(c.R, c.G, c.B, c.A);
		public static implicit operator Color(SKColor c) => new Color((uint)((c.Alpha << 24) | (c.Red << 16) | (c.Green << 8) | c.Blue));

#if WINDOWS
		public static implicit operator System.Drawing.Color(Color c) => System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B);
		public static implicit operator Color(System.Drawing.Color c) => new Color((uint)c.ToArgb());
#endif

		public static bool operator ==(Color left, Color right) => left._argb == right._argb;
		public static bool operator !=(Color left, Color right) => left._argb != right._argb;

		public bool Equals(Color other) => _argb == other._argb;
		public override bool Equals(object obj) => obj is Color c && _argb == c._argb;
		public override int GetHashCode() => _argb.GetHashCode();

		public override string ToString()
		{
			return $"Color [A={A}, R={R}, G={G}, B={B}]";
		}
	}
}
