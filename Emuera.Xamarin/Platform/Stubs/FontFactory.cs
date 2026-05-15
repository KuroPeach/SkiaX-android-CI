using SkiaSharp;
using XEmuera.Drawing;
using MinorShift.Emuera.Runtime.Config;

namespace MinorShift.Emuera.UI;

internal static class FontFactory
{
	public static SKFont GetFont(string requestFontName, FontStyle style, float? fontSize = null, SkiaSharpFontEdging? edging = null, SkiaSharpFontHinting? hinting = null)
	{
		var typeface = SKTypeface.FromFamilyName(requestFontName) ?? SKTypeface.Default;
		var font = new SKFont(typeface, fontSize ?? 16);
		return font;
	}

	public static Font GetGdiFont(string requestFontName, FontStyle style, float fontSize)
	{
		return new Font(requestFontName, (int)fontSize, style, GraphicsUnit.Pixel);
	}

	public static bool IsRasterFont(string fontName) => false;

	public static void ClearFont() { }
}
