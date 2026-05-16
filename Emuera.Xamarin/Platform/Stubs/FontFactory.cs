using MinorShift.Emuera.Runtime.Config;
using System.Collections.Generic;
using System.Drawing;
using SkiaSharp;
using MinorShift.Emuera.UI.Game;
using System;

namespace MinorShift.Emuera.UI;

internal static class FontFactory
{
	static readonly HashSet<string> rasterFontNames = new(StringComparer.OrdinalIgnoreCase)
	{
		"ＭＳ ゴシック", "MS Gothic", "MS UI Gothic",
		"MS PGothic",
		"ＭＳ 明朝", "MS Mincho", "MS PMincho",
	};

	static readonly Dictionary<(string fontname, float fontSize, FontStyle font_style, SkiaSharpFontEdging edging, SkiaSharpFontHinting hinting), SKFont> fontDic = [];
	static readonly Dictionary<(char, string), SKTypeface> fallbackTypefaceCache = [];
	static readonly Dictionary<(int, string), SKTypeface> fallbackTypefaceCodepointCache = [];
	static readonly Dictionary<(string fontname, int fontSize, FontStyle font_style), Font> gdiFontDic = [];

	public static bool IsRasterFont(string fontName)
	{
		if (string.IsNullOrEmpty(fontName))
			return false;
		return rasterFontNames.Contains(fontName);
	}

	public static Font GetGdiFont(string requestFontName, FontStyle style, float fontSize)
	{
		string fn = requestFontName;
		if (string.IsNullOrEmpty(requestFontName))
			fn = Config.FontName;

		int fontSizeInt = (int)fontSize;
		var key = (fn, fontSizeInt, style);

		if (gdiFontDic.TryGetValue(key, out var cachedFont))
			return cachedFont;

		Font font;
		try { font = new Font(fn, fontSizeInt, style, GraphicsUnit.Pixel); }
		catch { font = new Font(Config.FontName, fontSizeInt, style, GraphicsUnit.Pixel); }

		gdiFontDic[key] = font;
		return font;
	}

	public static SKFont GetFont(StringStyle stringStyle)
	{
		return GetFont(stringStyle.Fontname, stringStyle.FontStyle);
	}

	public static SKFont GetFont(string requestFontName, FontStyle style, float? fontSize = null, SkiaSharpFontEdging? edging = null, SkiaSharpFontHinting? hinting = null)
	{
		string fn = requestFontName;
		if (string.IsNullOrEmpty(requestFontName))
			fn = Config.FontName;
		fontSize ??= Config.FontSize;

		var actualEdging = edging ?? Config.FontEdging;
		var actualHinting = hinting ?? Config.FontHinting;

		var key = (fn, fontSize.Value, style, actualEdging, actualHinting);
		if (fontDic.TryGetValue(key, out var cachedFont))
			return cachedFont;

		try
		{
			var typeface = CreateTypefaceWithFallback(fn);
			var font = new SKFont(typeface, fontSize.Value)
			{
				Hinting = (SKFontHinting)actualHinting,
				Edging = (SKFontEdging)actualEdging
			};
			fontDic[key] = font;
			return font;
		}
		catch
		{
			return null;
		}
	}

	private static SKTypeface CreateTypefaceWithFallback(string fontName)
	{
		SKTypeface typeface = SKTypeface.FromFamilyName(fontName);
		if (typeface != null)
			return typeface;

		foreach (var customTypeface in GlobalStatic.CustomTypefaces)
		{
			if (customTypeface != null && customTypeface.FamilyName.Contains(fontName, StringComparison.OrdinalIgnoreCase))
				return customTypeface;
		}

		return SKTypeface.Default;
	}

	public static SKTypeface GetTypefaceWithFallback(string fontName)
	{
		return CreateTypefaceWithFallback(fontName);
	}

	public static SKFont GetFallbackFontForChar(char c, float fontSize, FontStyle style)
	{
		foreach (var customTypeface in GlobalStatic.CustomTypefaces)
		{
			if (customTypeface != null && customTypeface.ContainsGlyph(c))
			{
				return new SKFont(customTypeface, fontSize)
				{
					Hinting = (SKFontHinting)Config.FontHinting,
					Edging = (SKFontEdging)Config.FontEdging
				};
			}
		}

		var matchTypeface = SKFontManager.Default.MatchCharacter(c);
		if (matchTypeface != null)
		{
			return new SKFont(matchTypeface, fontSize)
			{
				Hinting = (SKFontHinting)Config.FontHinting,
				Edging = (SKFontEdging)Config.FontEdging
			};
		}

		return GetFont(Config.FontName, style, fontSize);
	}

	public static SKTypeface GetFallbackTypefaceForChar(char c)
	{
		return GetFallbackTypefaceForChar(c, Config.FontName);
	}

	public static SKTypeface GetFallbackTypefaceForChar(char c, string currentFontName)
	{
		var cacheKey = (c, currentFontName);
		if (fallbackTypefaceCache.TryGetValue(cacheKey, out var cachedTypeface))
			return cachedTypeface;

		foreach (var customTypeface in GlobalStatic.CustomTypefaces)
		{
			if (customTypeface != null && customTypeface.ContainsGlyph(c))
			{
				fallbackTypefaceCache[cacheKey] = customTypeface;
				return customTypeface;
			}
		}

		bool isSerif = !string.IsNullOrEmpty(currentFontName) && (
			currentFontName.Contains("Mincho", StringComparison.OrdinalIgnoreCase) ||
			currentFontName.Contains("明朝", StringComparison.OrdinalIgnoreCase) ||
			currentFontName.Contains("宋体", StringComparison.OrdinalIgnoreCase) ||
			currentFontName.Contains("Sun", StringComparison.OrdinalIgnoreCase) ||
			currentFontName.Contains("Serif", StringComparison.OrdinalIgnoreCase));

		string[] safeFallbackFonts;
		if (isSerif)
			safeFallbackFonts = ["MS Mincho", "MS PMincho", "SimSun", "NSimSun", "FangSong", "MingLiU", "PMingLiU", "MS Gothic", "SimHei"];
		else
			safeFallbackFonts = ["MS Gothic", "MS PGothic", "MS UI Gothic", "SimHei", "Microsoft YaHei", "Meiryo", "Yu Gothic", "SimSun"];

		foreach (var fontName in safeFallbackFonts)
		{
			var safeTypeface = SKTypeface.FromFamilyName(fontName);
			if (safeTypeface != null && safeTypeface.FamilyName.Equals(fontName, StringComparison.OrdinalIgnoreCase) && safeTypeface.ContainsGlyph(c))
			{
				fallbackTypefaceCache[cacheKey] = safeTypeface;
				return safeTypeface;
			}
			safeTypeface?.Dispose();
		}

		var matchTypeface = SKFontManager.Default.MatchCharacter(c);
		if (matchTypeface != null)
		{
			fallbackTypefaceCache[cacheKey] = matchTypeface;
			return matchTypeface;
		}

		return SKTypeface.Default;
	}

	public static SKTypeface GetFallbackTypefaceForCodepoint(int codepoint, string currentFontName)
	{
		if (codepoint <= 0xFFFF)
			return GetFallbackTypefaceForChar((char)codepoint, currentFontName);

		var cacheKey = (codepoint, currentFontName);
		if (fallbackTypefaceCodepointCache.TryGetValue(cacheKey, out var cachedTypeface))
			return cachedTypeface;

		foreach (var customTypeface in GlobalStatic.CustomTypefaces)
		{
			if (customTypeface != null && customTypeface.GetGlyph(codepoint) != 0)
			{
				fallbackTypefaceCodepointCache[cacheKey] = customTypeface;
				return customTypeface;
			}
		}

		var matchTypeface = SKFontManager.Default.MatchCharacter(codepoint);
		if (matchTypeface != null)
		{
			fallbackTypefaceCodepointCache[cacheKey] = matchTypeface;
			return matchTypeface;
		}

		return SKTypeface.Default;
	}

	public static bool TryGetGlyphFromFallback(string fontName, uint codepoint)
	{
		SKTypeface typeface = CreateTypefaceWithFallback(fontName);
		if (typeface == null)
			typeface = SKTypeface.Default;
		if (typeface.GetGlyph((int)codepoint) != 0)
			return true;
		foreach (var customTypeface in GlobalStatic.CustomTypefaces)
		{
			if (customTypeface != null && customTypeface.GetGlyph((int)codepoint) != 0)
				return true;
		}
		return false;
	}

	public static void ClearFont()
	{
		fontDic.Clear();
		foreach (var typeface in fallbackTypefaceCache.Values)
			typeface?.Dispose();
		fallbackTypefaceCache.Clear();
		foreach (var typeface in fallbackTypefaceCodepointCache.Values)
			typeface?.Dispose();
		fallbackTypefaceCodepointCache.Clear();
		gdiFontDic.Clear();
	}
}
