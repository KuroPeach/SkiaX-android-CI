using SkiaSharp;

namespace SkiaSharp.Views.Desktop
{
	public static class SkColorExtensions
	{
		public static int ToArgb(this SKColor color)
		{
			return (color.Alpha << 24) | (color.Red << 16) | (color.Green << 8) | color.Blue;
		}

		public static Color ToDrawingColor(this Color color)
		{
			return color;
		}

		public static Xamarin.Forms.Color ToFormsColor(this Color c)
		{
			return Xamarin.Forms.Color.FromRgba(c.R, c.G, c.B, c.A);
		}
	}
}
