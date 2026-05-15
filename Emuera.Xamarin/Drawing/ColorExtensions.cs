using SkiaSharp;
using System.Drawing;

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
	}
}
