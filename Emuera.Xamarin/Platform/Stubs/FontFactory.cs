using MinorShift.Emuera.Runtime.Config;
using System.Collections.Generic;
using System.Drawing;
using SkiaSharp;
using MinorShift.Emuera.UI.Game;
using System;
using XEmuera.Drawing;
using XEmuera.Models;

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
	static readonly Dictionary<string, SKTypeface> fontAliases = new(StringComparer.OrdinalIgnoreCase);

	static FontFactory()
	{
		RegisterKnownAliases();
	}

	private static void RegisterKnownAliases()
	{
		var pairs = new (string alias, string target)[]
		{
			("ＭＳ ゴシック", "MS Gothic"),
			("ＭＳ Ｐゴシック", "MS PGothic"),
			("ＭＳ 明朝", "MS Mincho"),
			("ＭＳ Ｐ明朝", "MS PMincho"),
			("微软雅黑", "Microsoft YaHei"),
		};
		foreach (var (alias, target) in pairs)
		{
			var typeface = SKTypeface.FromFamilyName(target);
			if (typeface != null)
				fontAliases[alias] = typeface;
		}
	}

	public static void RegisterFontAlias(string name, SKTypeface typeface)
	{
		if (string.IsNullOrEmpty(name) || typeface == null)
			return;
		if (!fontAliases.ContainsKey(name))
			fontAliases[name] = typeface;
	}

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
		// 西文字体别名：Android 上不存在，映射到内嵌字体（与 FontModel.ResolveFontName 保持一致）
		if (string.Equals(fontName, "Times New Roman", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(fontName, "Arial", StringComparison.OrdinalIgnoreCase))
			fontName = "Microsoft YaHei";
		else if (string.Equals(fontName, "Courier New", StringComparison.OrdinalIgnoreCase))
			fontName = "MS Gothic";

		SKTypeface typeface = SKTypeface.FromFamilyName(fontName);
		if (typeface != null)
		{
			FontDiag.Log($"CreateTypefaceWithFallback: request=\"{fontName}\" → FromFamilyName family={typeface.FamilyName}");
			return typeface;
		}

		if (fontAliases.TryGetValue(fontName, out var aliasTypeface))
		{
			FontDiag.Log($"CreateTypefaceWithFallback: request=\"{fontName}\" → alias family={aliasTypeface.FamilyName}");
			return aliasTypeface;
		}

		foreach (var customTypeface in GlobalStatic.CustomTypefaces)
		{
			if (customTypeface != null && customTypeface.FamilyName.Contains(fontName, StringComparison.OrdinalIgnoreCase))
			{
				FontDiag.Log($"CreateTypefaceWithFallback: request=\"{fontName}\" → custom family={customTypeface.FamilyName}");
				return customTypeface;
			}
		}

		// Check FontModel.AllModels for embedded fonts (Android doesn't have system MS Gothic etc.)
		var modelFontModel = FontModel.GetFont(fontName);
		if (modelFontModel?.Typeface != null)
		{
			FontDiag.Log($"CreateTypefaceWithFallback: request=\"{fontName}\" → FontModel family={modelFontModel.Typeface.FamilyName}");
			return modelFontModel.Typeface;
		}

		FontDiag.Log($"CreateTypefaceWithFallback: request=\"{fontName}\" → Default");
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

		// 遍历 FontModel 内嵌字体（Android 上 FromFamilyName 找不到内嵌字体）
		foreach (var fontModel in FontModel.UserList)
		{
			if (fontModel?.Typeface != null && fontModel.Typeface.GetGlyph(c) != 0)
			{
				return new SKFont(fontModel.Typeface, fontSize)
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

		FontDiag.Log($"GetFallbackTypefaceForChar: char='{c}' (U+{((int)c):X4}) currentFont={currentFontName}");

		foreach (var customTypeface in GlobalStatic.CustomTypefaces)
		{
			if (customTypeface != null && customTypeface.ContainsGlyph(c))
			{
				FontDiag.Log($"  → custom font: {customTypeface.FamilyName}");
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
			safeFallbackFonts = ["MS Mincho", "MS PMincho", "SimSun", "NSimSun", "FangSong", "SimHei", "MingLiU", "PMingLiU", "Microsoft YaHei", "MS Gothic"];
		else
			safeFallbackFonts = ["SimHei", "MS Gothic", "MS PGothic", "MS UI Gothic", "Microsoft YaHei", "Meiryo", "Yu Gothic", "SimSun"];

		foreach (var fontName in safeFallbackFonts)
		{
			SKTypeface safeTypeface = null;
			bool shouldDispose = false;

			// 优先从 FontModel 获取内嵌字体（Android 上 FromFamilyName 找不到内嵌字体）
			// 直接遍历 AllModels，按 FamilyName 匹配，绕过 FontNameMapping 的别名映射
			bool foundInModel = false;
			foreach (var kv in FontModel.AllModels)
			{
				if (string.Equals(kv.Key, fontName, StringComparison.OrdinalIgnoreCase) ||
				    string.Equals(kv.Value.Typeface?.FamilyName, fontName, StringComparison.OrdinalIgnoreCase))
				{
					safeTypeface = kv.Value.Typeface;
					foundInModel = true;
					break;
				}
			}
			if (!foundInModel)
			{
				safeTypeface = SKTypeface.FromFamilyName(fontName);
				shouldDispose = true;
			}

			if (safeTypeface != null && safeTypeface.ContainsGlyph(c))
			{
				FontDiag.Log($"  → safeFallback: {fontName} (foundInModel={foundInModel}) ContainsGlyph=true");
				fallbackTypefaceCache[cacheKey] = safeTypeface;
				return safeTypeface;
			}
			if (safeTypeface != null)
			{
				FontDiag.Log($"  → safeFallback: {fontName} (foundInModel={foundInModel}) ContainsGlyph=false GetGlyph={safeTypeface.GetGlyph(c)}");
			}
			if (shouldDispose)
				safeTypeface?.Dispose();
		}

		var matchTypeface = SKFontManager.Default.MatchCharacter(c);
		if (matchTypeface != null)
		{
			FontDiag.Log($"  → MatchCharacter: family={matchTypeface.FamilyName} GetGlyph={matchTypeface.GetGlyph(c)}");
			fallbackTypefaceCache[cacheKey] = matchTypeface;
			return matchTypeface;
		}

		FontDiag.Log($"  → SKTypeface.Default (all fallbacks failed)");
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

		// 遍历 FontModel 内嵌字体（Android 上 FromFamilyName 找不到内嵌字体）
		foreach (var fontModel in FontModel.UserList)
		{
			if (fontModel?.Typeface != null && fontModel.Typeface.GetGlyph(codepoint) != 0)
			{
				fallbackTypefaceCodepointCache[cacheKey] = fontModel.Typeface;
				return fontModel.Typeface;
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
		// 遍历 FontModel 内嵌字体（Android 上 FromFamilyName 找不到内嵌字体）
		foreach (var fontModel in FontModel.UserList)
		{
			if (fontModel?.Typeface != null && fontModel.Typeface.GetGlyph((int)codepoint) != 0)
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
		fontAliases.Clear();
		RegisterKnownAliases();
	}
}
