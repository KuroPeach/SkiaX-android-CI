using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;

namespace MinorShift.Emuera;

/// <summary>
/// 跨平台字体注册表：font/ 目录加载的私有字体（文件路径 + SKTypeface 族名）。
/// 桌面另持一份 GDI PrivateFontCollection 供 raster 字体路径使用；headless 只走 Skia。
/// </summary>
internal sealed class FontRegistry
{
	private readonly List<string> files = new();
	private readonly List<SKTypeface> typefaces = new();
	private readonly HashSet<string> familyNames = new(StringComparer.OrdinalIgnoreCase);

#if WINDOWS
	private readonly System.Drawing.Text.PrivateFontCollection gdi = new();
#endif

	public IReadOnlyList<string> Files => files;
	public IReadOnlyList<SKTypeface> Typefaces => typefaces;
	public IReadOnlyCollection<string> FamilyNames => familyNames;

	public void AddFontFile(string filepath)
	{
		if (string.IsNullOrEmpty(filepath))
			return;
		files.Add(filepath);
		try
		{
			var tf = SKTypeface.FromFile(filepath);
			if (tf != null)
			{
				typefaces.Add(tf);
				if (!string.IsNullOrEmpty(tf.FamilyName))
					familyNames.Add(tf.FamilyName);
			}
		}
		catch { }
#if WINDOWS
		try { gdi.AddFontFile(filepath); } catch { }
#endif
	}

	public bool Contains(string familyName)
		=> !string.IsNullOrEmpty(familyName) && familyNames.Contains(familyName);

#if WINDOWS
	/// <summary>按族名找私有字体（GDI raster 路径）；找不到返回 null。</summary>
	public System.Drawing.FontFamily GetGdiFamily(string familyName)
	{
		if (string.IsNullOrEmpty(familyName))
			return null;
		foreach (var ff in gdi.Families)
		{
			if (ff != null && string.Equals(ff.Name, familyName, StringComparison.OrdinalIgnoreCase))
				return ff;
		}
		return null;
	}
#endif
}
