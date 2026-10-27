using MinorShift.Emuera.Runtime.Config;
using System.Collections.Generic;
using System.Drawing;
#if WINDOWS
using System.Drawing.Text;
#endif
using SkiaSharp;
using MinorShift.Emuera.UI.Game;
using System;
using System.Diagnostics;

namespace MinorShift.Emuera.UI;

internal static class FontFactory
{
	static readonly HashSet<string> rasterFontNames = new(StringComparer.OrdinalIgnoreCase)
	/*
	{
		"ＭＳ ゴシック", "MS Gothic", "MS UI Gothic",
		"MS PGothic",
		"SimHei",
		"ＭＳ 明朝", "MS Mincho", "MS PMincho",
		"SimSun", "NSimSun", "FangSong", "MingLiU", "PMingLiU"
	};
	*/
	{
		"ＭＳ ゴシック", "MS Gothic", "MS UI Gothic",
		"MS PGothic",
		"ＭＳ 明朝", "MS Mincho", "MS PMincho",
	};

	static readonly Dictionary<(string fontname, float fontSize, FontStyle font_style, SkiaSharpFontEdging edging, SkiaSharpFontHinting hinting), SKFont> fontDic = [];
	static readonly Dictionary<(char, string), SKTypeface> fallbackTypefaceCache = [];
	static readonly Dictionary<(int, string), SKTypeface> fallbackTypefaceCodepointCache = [];
#if WINDOWS
	static readonly Dictionary<(string fontname, int fontSize, FontStyle font_style), Font> gdiFontDic = [];
#endif

	/// <summary>
	/// 供 emoji/符号使用的「单色」回退字体（按序）。
	/// 目的：避免回退到 Segoe UI Emoji 等彩色字体，让 👁 / ☀ 等以黑白字符呈现。
	/// 说明：SKFontManager.MatchCharacter 对 U+1F441 会返回彩色字体 'Segoe UI Emoji'，
	/// 而 'Segoe UI Symbol' 同时含该字形且为单色（实测 distinctColors=1）。
	/// </summary>
	static readonly string[] monochromeSymbolFonts = {
		"Segoe UI Symbol",
	};

	/// <summary>
	/// 在单色符号字体中查找含指定码位字形的字体；找不到返回 null。
	/// </summary>
	private static SKTypeface GetMonochromeSymbolTypeface(int codepoint)
	{
		foreach (var name in monochromeSymbolFonts)
		{
			try
			{
				var tf = SKTypeface.FromFamilyName(name);
				if (tf != null && tf.FamilyName.Equals(name, StringComparison.OrdinalIgnoreCase) && tf.GetGlyph(codepoint) != 0)
					return tf;
				tf?.Dispose();
			}
			catch { }
		}
		return null;
	}

	public static bool IsRasterFont(string fontName)
	{
		if (string.IsNullOrEmpty(fontName))
			return false;
		return rasterFontNames.Contains(fontName);
	}

	/// <summary>字体可用性（跨平台）：私有字体 → 系统字体（Windows 用 GDI 枚举，其他用 Skia 精确族名）。</summary>
	public static bool IsFontAvailable(string fontName)
	{
		if (string.IsNullOrEmpty(fontName))
			return false;
		if (GlobalStatic.Pfc.Contains(fontName))
			return true;
#if WINDOWS
		try
		{
			using var ifc = new InstalledFontCollection();
			foreach (var ff in ifc.Families)
			{
				if (ff != null && string.Equals(ff.Name, fontName, StringComparison.OrdinalIgnoreCase))
					return true;
			}
		}
		catch { }
#endif
		try
		{
			var tf = SKTypeface.FromFamilyName(fontName);
			return tf != null && string.Equals(tf.FamilyName, fontName, StringComparison.OrdinalIgnoreCase);
		}
		catch
		{
			return false;
		}
	}

	/// <summary>
	/// 诊断用：返回 FontFactory 各缓存字典的条目数
	/// </summary>
	internal static (int fontCount, int fallbackCount, int fallbackCpCount, int gdiCount) GetDiagnosticStats()
	{
		lock (fontDic)
#if WINDOWS
			return (fontDic.Count, fallbackTypefaceCache.Count, fallbackTypefaceCodepointCache.Count, gdiFontDic.Count);
#else
			return (fontDic.Count, fallbackTypefaceCache.Count, fallbackTypefaceCodepointCache.Count, 0);
#endif
	}

#if WINDOWS
	/// <summary>
	/// 在 font/ 目录加载的私有字体集合（GlobalStatic.Pfc）中按族名查找 FontFamily。
	/// 关键：new Font(string) 无法解析未安装到系统的私有字体（会静默回退到默认字体），
	/// 必须用 FontFamily 才能命中 game-icons 之类的私有字体。
	/// </summary>
	private static FontFamily FindPrivateFontFamily(string name)
	{
		return GlobalStatic.Pfc.GetGdiFamily(name);
	}

	public static Font GetGdiFont(string requestFontName, FontStyle style, float fontSize)
	{
		string fn = requestFontName;
		if (string.IsNullOrEmpty(requestFontName))
			fn = Config.FontName;

		int fontSizeInt = (int)fontSize;
		var key = (fn, fontSizeInt, style);

		if (gdiFontDic.TryGetValue(key, out var cachedFont))
		{
			return cachedFont;
		}

		Font font = null;

		// 1) 优先用私有字体（font/ 目录）：new Font(字体名) 找不到未安装的私有字体
		try
		{
			var privateFamily = FindPrivateFontFamily(fn);
			if (privateFamily != null)
				font = new Font(privateFamily, fontSizeInt, style, GraphicsUnit.Pixel);
		}
		catch { }

		// 2) 系统已安装字体
		if (font == null)
		{
			try
			{
				font = new Font(fn, fontSizeInt, style, GraphicsUnit.Pixel);
			}
			catch { }
		}

		// 3) 兜底到配置字体
		if (font == null)
		{
			try
			{
				font = new Font(Config.FontName, fontSizeInt, style, GraphicsUnit.Pixel);
			}
			catch
			{
				font = new Font(FontFamily.GenericSansSerif, fontSizeInt, style, GraphicsUnit.Pixel);
			}
		}

		gdiFontDic[key] = font;
		return font;
	}
#endif

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
		{
			return cachedFont;
		}
		
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
		catch (Exception e)
		{
			// P1（2026-10-27）：原实现 catch{return null} 把字体创建失败静默成下游 NRE
			// （Config.DefaultFont → StringMeasure.GetDisplayLength）。这里记录一次并直构兜底。
			LogFontFailureOnce(e, fn, fontSize.Value);
			return CreateEmergencyFont(fontSize.Value);
		}
	}

	private static int fontFailureLogged;

	/// <summary>P1：字体创建失败只记录一次，避免逐帧刷屏；headless/EWP 下 stderr 会被 EngineProcess 落盘。</summary>
	private static void LogFontFailureOnce(Exception e, string fontName, float fontSize)
	{
		if (System.Threading.Interlocked.Exchange(ref fontFailureLogged, 1) != 0)
			return;
		try
		{
			string msg = $"[font] create failed name=\"{fontName}\" size={fontSize}: {e.GetType().Name}: {e.Message}";
			Debug.WriteLine(msg);
			Console.Error.WriteLine(msg);
		}
		catch
		{
			// 日志失败不影响主流程
		}
	}

	private static SKFont emergencyFont;

	/// <summary>
	/// P1 兜底字体：字体创建失败时的最后一道防线。
	/// 返回 null 只在 Skia 完全不可用时发生（调用方 StringMeasure 已做空守卫）。
	/// </summary>
	public static SKFont CreateEmergencyFont(float fontSize = -1)
	{
		if (fontSize <= 0)
			fontSize = Config.FontSize;
		bool defaultSize = Math.Abs(fontSize - Config.FontSize) < 0.01f;
		if (defaultSize)
		{
			var cached = emergencyFont;
			if (cached != null)
				return cached;
		}
		try
		{
			var typeface = SKTypeface.Default ?? SKTypeface.CreateDefault();
			var font = new SKFont(typeface, fontSize);
			if (defaultSize)
				emergencyFont = font;
			return font;
		}
		catch (Exception e)
		{
			LogFontFailureOnce(e, "<emergency>", fontSize);
			return null;
		}
	}

	/// <summary>
	/// 在 font/ 目录加载的私有字体（GlobalStatic.CustomTypefaces）中按族名查找。
	/// 精确族名优先，其次宽松包含匹配（兼容 "game-icons bold" 之类请求）。
	/// </summary>
	private static SKTypeface MatchCustomTypeface(string fontName)
	{
		var list = GlobalStatic.Pfc.Typefaces;
		if (list == null || string.IsNullOrEmpty(fontName))
			return null;

		foreach (var ct in list)
		{
			if (ct != null && string.Equals(ct.FamilyName, fontName, StringComparison.OrdinalIgnoreCase))
				return ct;
		}
		foreach (var ct in list)
		{
			if (ct != null && ct.FamilyName.Contains(fontName, StringComparison.OrdinalIgnoreCase))
				return ct;
		}
		return null;
	}

	private static SKTypeface CreateTypefaceWithFallback(string fontName)
	{
		if (string.IsNullOrEmpty(fontName))
			return SKTypeface.Default;

		// 1) 优先匹配游戏自带私有字体（font/ 目录）。
		//    关键：SKTypeface.FromFamilyName 对"未安装的字体名"不会返回 null，
		//    而是返回默认字体（如 'Segoe UI'）。若先调用它，后面的自定义字体分支
		//    永远不可达（死代码），导致 <font face='game-icons'> / GSETFONT "game-icons"
		//    这类私有字体全部失效。故必须先查自定义字体。
		var custom = MatchCustomTypeface(fontName);
		if (custom != null)
			return custom;

		// 2) 系统字体（含 黑体->SimHei 之类的本地化别名，保持原有行为）
		var typeface = SKTypeface.FromFamilyName(fontName);
		if (typeface != null)
			return typeface;

		// 3) 最后回退到默认字体
		return SKTypeface.Default;
	}

	public static SKTypeface GetTypefaceWithFallback(string fontName)
	{
		return CreateTypefaceWithFallback(fontName);
	}

	public static SKFont GetFallbackFontForChar(char c, float fontSize, FontStyle style)
	{
		// 复用 Typeface 查找逻辑：与 GetFallbackTypefaceForChar 共享同一回退层级与缓存，
		// 避免此处单独查找单色符号字体时每次新建 SKTypeface 且不入缓存
		var fallbackTypeface = GetFallbackTypefaceForChar(c, Config.FontName);
		if (fallbackTypeface != null && fallbackTypeface != SKTypeface.Default)
		{
			return new SKFont(fallbackTypeface, fontSize)
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
		{
			return cachedTypeface;
		}

		foreach (var customTypeface in GlobalStatic.Pfc.Typefaces)
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
		{
			safeFallbackFonts = new string[] {
				"MS Mincho", "MS PMincho",
				"SimSun", "NSimSun", "FangSong",
				"MingLiU", "PMingLiU",
				"MS Gothic", "SimHei"
			};
		}
		else
		{
			safeFallbackFonts = new string[] {
				"MS Gothic", "MS PGothic", "MS UI Gothic",
				"SimHei", "Microsoft YaHei",
				"Meiryo", "Yu Gothic",
				"SimSun"
			};
		}

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

		// 优先单色符号字体（避免 MatchCharacter 返回彩色 emoji 字体）
		var symbolTypeface = GetMonochromeSymbolTypeface(c);
		if (symbolTypeface != null)
		{
			fallbackTypefaceCache[cacheKey] = symbolTypeface;
			return symbolTypeface;
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

		foreach (var customTypeface in GlobalStatic.Pfc.Typefaces)
		{
			if (customTypeface != null && customTypeface.GetGlyph(codepoint) != 0)
			{
				fallbackTypefaceCodepointCache[cacheKey] = customTypeface;
				return customTypeface;
			}
		}

		// 优先单色符号字体（避免 MatchCharacter 返回彩色 emoji 字体）
		var symbolTypeface = GetMonochromeSymbolTypeface(codepoint);
		if (symbolTypeface != null)
		{
			fallbackTypefaceCodepointCache[cacheKey] = symbolTypeface;
			return symbolTypeface;
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
		foreach (var customTypeface in GlobalStatic.Pfc.Typefaces)
		{
			if (customTypeface != null && customTypeface.GetGlyph((int)codepoint) != 0)
				return true;
		}
		return false;
	}

	public static void ClearFont()
	{
		// 重要：下列字典里保存的都是“共享”对象，绝不可在此手动 Dispose：
		//  - fontDic / gdiFontDic：同一托管包装实例会被 Config.DefaultFont 以及所有
		//    GraphicsImage / 控制台共同复用；Dispose 会让其余持有者访问已释放的原生对象。
		//  - fallbackTypefaceCache / fallbackTypefaceCodepointCache：其值可能同时被
		//    GlobalStatic.CustomTypefaces（游戏自有字体）与 SKTypeface.Default（进程级静态）
		//    引用，Dispose 会连带销毁这些共享对象。
		// 因此这里只清空引用，原生资源交由 GC / SkiaSharp 终结器统一 release。
		fontDic.Clear();
		fallbackTypefaceCache.Clear();
		fallbackTypefaceCodepointCache.Clear();
#if WINDOWS
		gdiFontDic.Clear();
#endif

		Debug.WriteLine("[FontFactory] 释放字体缓存（仅清除引用，交由 GC 回收）");
	}
}
