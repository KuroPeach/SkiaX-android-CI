using MinorShift.Emuera;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using XEmuera.Models;

namespace XEmuera.Drawing
{
	/// <summary>
	/// 字体诊断日志：累积在内存中，导出 emuera.log 时追加到末尾
	/// </summary>
	internal static class FontDiag
	{
		private static readonly StringBuilder _buffer = new();
		private static int _lineCount;
		private const int MaxLines = 2000;

		public static void Log(string message)
		{
			if (_lineCount >= MaxLines) return;
			_lineCount++;
			_buffer.AppendLine($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
		}

		/// <summary>
		/// 将诊断日志追加到 emuera.log 末尾，并清空缓冲
		/// </summary>
		public static void FlushTo(StreamWriter writer)
		{
			writer.WriteLine();
			writer.WriteLine($"========== FontDiag T5 ==========");

			// 直接在导出时探测字体信息，不依赖之前的 Log 调用
			try
			{
				writer.WriteLine($"[Probe] FontModel.Init = {FontModel.Init}");
				writer.WriteLine($"[Probe] FontModel.AllModels.Count = {FontModel.AllModels.Count}");
				foreach (var kv in FontModel.AllModels)
				{
					var fm = kv.Value;
					var tf = fm.Typeface;
					// 添加 Ascent 信息用于诊断基线偏移
					float ascent = 0;
					if (tf != null)
					{
						using var probePaint = new SKPaint { Typeface = tf, TextSize = 18 };
						ascent = probePaint.FontMetrics.Ascent;
					}
					writer.WriteLine($"[Probe] font={fm.Name} enabled={fm.Enabled} family={tf?.FamilyName} Ascent={ascent:F2} GetGlyph(劲)={tf?.GetGlyph('劲')} GetGlyph(组)={tf?.GetGlyph('组')} GetGlyph(姬)={tf?.GetGlyph('姬')}");
				}

				// 修复：如果 Microsoft YaHei 未启用，强制启用并重新加载
				if (FontModel.AllModels.TryGetValue("Microsoft YaHei", out var yaheiModel) && !yaheiModel.Enabled)
				{
					writer.WriteLine("[Fix] Force-enabling Microsoft YaHei and reloading DrawTextUtils");
					yaheiModel.Enabled = true;
					FontModel.Save(); // 这会调用 DrawTextUtils.Load()
				}
			}
			catch (Exception ex)
			{
				writer.WriteLine($"[Probe] ERROR: {ex}");
			}

			writer.WriteLine($"[Buffer] _lineCount={_lineCount} _buffer.Length={_buffer.Length}");
			if (_lineCount > 0)
			{
				writer.Write(_buffer.ToString());
			}
			writer.WriteLine($"========== FontDiag End ({_lineCount} lines) ==========");
			_buffer.Clear();
			_lineCount = 0;
		}
	}

	public static class DrawTextUtils
	{
		private static readonly ObservableCollection<FontCache> FontCaches = new ObservableCollection<FontCache>();

		private static readonly List<FontCache> EnabledFontCaches = new List<FontCache>();

		/// <summary>
		/// 系统字体回退缓存：字符 → FontModel（通过 MatchCharacter 匹配）
		/// 当所有 EnabledFontCaches 都不含某字符字形时，使用系统字体回退
		/// </summary>
		private static readonly Dictionary<char, FontModel> SystemFallbackCache = new Dictionary<char, FontModel>();

		private static SKPaint Paint;

		private static char[] CharList;

		private static int CharRemain;

		private static TextUnit[] TextUnits;

		private static FontCache UserCache;

		private static char Char;

		private static float Advance;

		public static void Load()
		{
			for (int i = FontCaches.Count - 1; i >= 0; i--)
			{
				if (!FontModel.UserList.Contains(FontCaches[i].FontModel))
					FontCaches.RemoveAt(i);
			}

			foreach (var fontModel in FontModel.UserList)
			{
				if (FontCaches.Any(item => item.FontModel == fontModel))
					continue;
				FontCaches.Add(new FontCache
				{
					FontModel = fontModel
				});
			}

			EnabledFontCaches.Clear();
			for (int i = 0; i < FontModel.UserList.Count; i++)
			{
				var fontModel = FontModel.UserList[i];
				var fontCache = FontCaches.FirstOrDefault(item => item.FontModel == fontModel);

				int index = FontCaches.IndexOf(fontCache);
				if (index != i)
					FontCaches.Move(index, i);

				if (fontModel.Enabled)
					EnabledFontCaches.Add(fontCache);
			}

			// [DIAG] 记录可用字体列表
			//FontDiag.Log($"Load: {EnabledFontCaches.Count} enabled fonts:");
			//foreach (var fc in EnabledFontCaches)
			//{
			//	var tf = fc.FontModel.Typeface;
			//	FontDiag.Log($"  {fc.FontModel.Name} family={tf?.FamilyName} GetGlyph(劲)={tf?.GetGlyph('劲')} GetGlyph(组)={tf?.GetGlyph('组')} GetGlyph(姬)={tf?.GetGlyph('姬')}");
			//}
		}

		public static void Reset()
		{
			foreach (var fontCache in FontCaches)
				fontCache.Clear();

			if (Paint != null)
				Paint.Dispose();
			Paint = new SKPaint
			{
				StrokeWidth = 2,
				IsAntialias = Config.TextAntialias,
				FilterQuality = Config.TextFilterQuality
			};
		}

		public static void DrawText(SKCanvas canvas, string text, Font font, RectangleF rect, Color color)
		{
			DrawText(canvas, text, font, rect.X, rect.Y, color);
		}

		public static void DrawText(SKCanvas canvas, string text, Font font, float x, float y, Color color)
		{
			if (string.IsNullOrEmpty(text))
				return;

			// 防御：Paint 未初始化或 FontCaches 为空时，直接用 Skia 默认字体绘制
			if (Paint == null || FontCaches.Count == 0)
			{
				using var fallbackPaint = new SKPaint
				{
					TextSize = font.Size > 0 ? font.Size : 18,
					Color = DisplayUtils.ToSKColor(color),
					IsAntialias = true,
					Typeface = font.FontModel?.Typeface ?? SKTypeface.Default,
				};
				float fbBaselineY = y - fallbackPaint.FontMetrics.Ascent;
				canvas.DrawText(text, x, fbBaselineY, fallbackPaint);
				return;
			}

			Paint.Typeface = font.FontModel.Typeface;
			Paint.TextSize = font.Size;
			Paint.FakeBoldText = font.Style.HasFlag(FontStyle.Bold);
			Paint.TextSkewX = font.Style.HasFlag(FontStyle.Italic) ? -0.4f : 0;
			Paint.Color = DisplayUtils.ToSKColor(color);

			CharList = text.ToCharArray();
			CharRemain = CharList.Length;
			TextUnits = new TextUnit[CharRemain];
			UserCache = Paint.Typeface == null
				? FontCaches[0] : FontCaches.FirstOrDefault(item => item.FontModel.Typeface == Paint.Typeface);

			if (UserCache == null)
				UserCache = FontCaches[0];

			//------------------------------------

			List<FontCache> fontCache = new List<FontCache>(EnabledFontCaches);
			fontCache.Remove(UserCache);
			fontCache.Insert(0, UserCache);

			int count = fontCache.Count;
			for (int i = 0; i < count * 2; i++)
			{
				if (i < count)
					fontCache[i].CheckGlyphs();
				else
					fontCache[i - count].PrepareGlyphs();

				if (CharRemain == 0)
					break;
			}

			// 系统字体回退：当所有 EnabledFontCaches 都不含某字符字形时，使用 SKFontManager.MatchCharacter 查找系统字体
			if (CharRemain > 0)
			{
				// [DIAG] 记录进入系统回退阶段的剩余字符和当前字体信息
				//var remainChars = new string(CharList.Where(c => c != char.MinValue).ToArray());
				//FontDiag.Log($"SystemFallback: text=\"{text}\" fontName={font.FontModel?.Name} familyName={font.FontModel?.Typeface?.FamilyName} CharRemain={CharRemain} chars=\"{remainChars}\"");

				for (int i = 0; i < CharList.Length; i++)
				{
					if (CharList[i] == char.MinValue)
						continue;

					char c = CharList[i];

					if (!SystemFallbackCache.TryGetValue(c, out FontModel fallbackModel))
					{
						var matchTypeface = SKFontManager.Default.MatchCharacter(c);
						if (matchTypeface != null)
						{
							// [DIAG] MatchCharacter 返回的字体信息
							//FontDiag.Log($"MatchCharacter: char='{c}' (U+{((int)c):X4}) → family={matchTypeface.FamilyName} GetGlyph={matchTypeface.GetGlyph(c)}");
							fallbackModel = new FontModel(matchTypeface);
							SystemFallbackCache[c] = fallbackModel;
						}
						else
						{
							// [DIAG] MatchCharacter 返回 null
							//FontDiag.Log($"MatchCharacter: char='{c}' (U+{((int)c):X4}) → null (no system font)");
							SystemFallbackCache[c] = null;
						}
					}

					if (fallbackModel == null)
						continue;

					Paint.Typeface = fallbackModel.Typeface;
					var widths = Paint.GetGlyphWidths(new[] { c });
					if (widths.Length > 0 && widths[0] > 0)
					{
						TextUnits[i] = new TextUnit
						{
							Text = c.ToString(),
							Advance = widths[0],
							FontModel = fallbackModel,
						};
						CharList[i] = char.MinValue;
						CharRemain--;
					}
				}
			}

			// 终极保底：如果经过所有内置字体和系统回退后仍有字符未匹配，
			// 强制使用 UserCache 画一个带宽度的占位符，防止排版错乱或吞字
			if (CharRemain > 0)
			{
				// [DIAG] 终极保底：记录用哪个字体画什么字符
				//var remainChars = new string(CharList.Where(c => c != char.MinValue).ToArray());
				//FontDiag.Log($"UltimateFallback: CharRemain={CharRemain} chars=\"{remainChars}\" font={UserCache.FontModel.Name} family={UserCache.FontModel.Typeface?.FamilyName}");

				for (int i = 0; i < CharList.Length; i++)
				{
					if (CharList[i] == char.MinValue)
						continue;

					TextUnits[i] = new TextUnit
					{
						Text = CharList[i].ToString(),
						Advance = font.Size,
						FontModel = UserCache.FontModel,
					};
					CharList[i] = char.MinValue;
					CharRemain--;
				}
			}

			//没有任何字可以绘制的大失败情况
			if (CharRemain == CharList.Length)
				return;

			//------------------------------------

			for (int i = TextUnits.Length - 2; i >= 0; i--)
			{
				if (TextUnits[i + 1] == null)
					continue;

				if (TextUnits[i] == null)
				{
					TextUnits[i] = TextUnits[i + 1];
					TextUnits[i + 1] = null;
					continue;
				}

				if (TextUnits[i].FontModel != TextUnits[i + 1].FontModel)
					continue;

				TextUnits[i].Text += TextUnits[i + 1].Text;
				TextUnits[i].Advance += TextUnits[i + 1].Advance;
				TextUnits[i + 1] = null;
			}

			//------------------------------------

			bool drawStrikeout = font.Style.HasFlag(FontStyle.Strikeout);
			bool drawUnderline = font.Style.HasFlag(FontStyle.Underline);
			float Strikeout = drawStrikeout ? (Paint.FontMetrics.StrikeoutPosition ?? 0) : 0;
			float Underline = drawUnderline ? (Paint.FontMetrics.UnderlinePosition ?? 0) : 0;

			// y 是行顶部坐标，SkiaSharp DrawText 要求基线坐标，需要减去 Ascent
			// 所有字符共享主字体的基线，避免不同字体 Ascent 差异导致基线偏移
			float baselineY = y - Paint.FontMetrics.Ascent;

			TextUnit unit;
			for (int i = 0; i < TextUnits.Length; i++)
			{
				if (TextUnits[i] == null)
					continue;

				unit = TextUnits[i];
				Paint.Typeface = unit.FontModel.Typeface;

				if (drawStrikeout)
					canvas.DrawLine(x, baselineY + Strikeout, x + unit.Advance, baselineY + Strikeout, Paint);
				if (drawUnderline)
					canvas.DrawLine(x, baselineY + Underline, x + unit.Advance, baselineY + Underline, Paint);

				canvas.DrawText(unit.Text, x, baselineY, Paint);

				x += unit.Advance;
			}
		}

		private class FontCache
		{
			public FontModel FontModel;

			private readonly Dictionary<char, float> ValidChar = new Dictionary<char, float>();

			private readonly HashSet<char> InvalidChar = new HashSet<char>();

			public void CheckGlyphs()
			{
				if (ValidChar.Count == 0)
					return;

				for (int i = 0; i < CharList.Length; i++)
				{
					Char = CharList[i];
					if (Char == char.MinValue || InvalidChar.Contains(Char))
						continue;
					if (!ValidChar.TryGetValue(Char, out Advance))
						continue;

					AddTextUnits(i);
				}
			}

			public void PrepareGlyphs()
			{
				Paint.Typeface = FontModel.Typeface;
				var glyphs = Paint.GetGlyphs(CharList);
				var glyphWidths = Paint.GetGlyphWidths(CharList);
				int glyphCount = Math.Min(CharList.Length, Math.Min(glyphs.Length, glyphWidths.Length));

				for (int i = 0; i < glyphCount; i++)
				{
					Char = CharList[i];
					if (Char == char.MinValue)
						continue;

					if (glyphs[i] == 0)
					{
						InvalidChar.Add(Char);
						// [DIAG] 只记录非 ASCII 字符的缺失（ASCII 缺失太常见，噪音太大）
						//if (Char > 0x7F)
						//	FontDiag.Log($"PrepareGlyphs: font={FontModel.Name} char='{Char}' (U+{((int)Char):X4}) → glyphs[i]=0 (missing)");
						continue;
					}

					// Android 上 SKPaint.GetGlyphs 可能因字体替换机制对 .notdef 字形返回非零 glyph ID，
					// 导致 MS Gothic 错误认领其不支持的简体汉字（如贝、厅）。
					// 使用 Typeface.GetGlyph 直接查询 cmap 表做二次验证。
					if (Char > 0x7F && FontModel.Typeface.GetGlyph(Char) == 0)
					{
						InvalidChar.Add(Char);
						// [DIAG] SKPaint.GetGlyphs 误报：glyphs[i]!=0 但 Typeface.GetGlyph==0
						//FontDiag.Log($"PrepareGlyphs: font={FontModel.Name} char='{Char}' (U+{((int)Char):X4}) → glyphs[i]={glyphs[i]} but GetGlyph=0 (false positive!)");
						continue;
					}

					// [DIAG] 记录简体汉字被字体认领的情况（排查 GetGlyph 返回非零但渲染方框的问题）
					// 简体汉字范围 U+4E00-U+9FFF，MS Gothic 不太可能完整覆盖
					//if (Char >= 0x4E00 && Char <= 0x9FFF)
					//{
					//	ushort glyphId = FontModel.Typeface.GetGlyph(Char);
					//	float width = glyphWidths.Length > i ? glyphWidths[i] : -1;
					//	FontDiag.Log($"PrepareGlyphs: font={FontModel.Name} char='{Char}' (U+{((int)Char):X4}) → glyphId={glyphId} width={width:F1} accepted");
					//}

					Advance = glyphWidths[i];
					ValidChar[Char] = Advance;

					AddTextUnits(i);
				}
			}

			private void AddTextUnits(int i)
			{
				TextUnits[i] = new TextUnit
				{
					Text = Char.ToString(),
					Advance = Advance,
					FontModel = FontModel,
				};
				CharList[i] = char.MinValue;
				CharRemain--;
			}

			public void Clear()
			{
				ValidChar.Clear();
			}
		}

		private class TextUnit
		{
			public string Text;
			public float Advance;
			public FontModel FontModel;
		}

	}

	[Flags]
	public enum FontStyle
	{
		Regular = 0,
		Bold = 1 << 0,
		Italic = 1 << 1,
		Strikeout = 1 << 2,
		Underline = 1 << 3
	}

	public enum GraphicsUnit
	{
		World,
		Display,
		Pixel,
		Point,
		Inch,
		Document,
		Millimeter
	}

	public class Font
	{
		public FontModel FontModel { get; }
		public float Size { get; }
		public FontStyle Style { get; set; }
		public SKFont SKFont { get; }
		public string Name => FontModel?.Typeface?.FamilyName ?? "";

		public Font(string fontName, float size, FontStyle style, GraphicsUnit unit)
		{
			FontModel = FontModel.GetFont(fontName);
			Size = size;
			Style = style;
			SKFont = new SKFont(FontModel?.Typeface ?? SKTypeface.Default, size);
		}

		public Font(string fontName, float size)
		{
			FontModel = FontModel.GetFont(fontName);
			Size = size;
			Style = FontStyle.Regular;
			SKFont = new SKFont(FontModel?.Typeface ?? SKTypeface.Default, size);
		}

		public Font(SKFont skFont)
		{
			SKFont = skFont;
			FontModel = FontModel.GetFont(skFont?.Typeface?.FamilyName ?? "");
			Size = skFont?.Size ?? 0;
			Style = FontStyle.Regular;
		}

		public static implicit operator SKFont(Font f) => f?.SKFont;
		public static implicit operator Font(SKFont f) => f == null ? null : new Font(f);
#if WINDOWS
		public static implicit operator System.Drawing.Font(Font f) =>
			f == null ? null : new System.Drawing.Font(f.Name, f.Size, (System.Drawing.FontStyle)f.Style, System.Drawing.GraphicsUnit.Pixel);
#endif
#if WINDOWS
		public static implicit operator Font(System.Drawing.Font f) =>
			f == null ? null : new Font(f.Name, f.Size, (FontStyle)(int)f.Style, GraphicsUnit.Pixel);
#endif

		public void SetStyle(FontStyle style)
        {
			Style = style;
        }

		public Int64 StyleToNum()
        {
			return (Int64)Style;
        }
	}
}