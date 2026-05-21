using MinorShift.Emuera;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Text;
using XEmuera.Models;

namespace XEmuera.Drawing
{
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
							fallbackModel = new FontModel(matchTypeface);
							SystemFallbackCache[c] = fallbackModel;
						}
						else
						{
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
						// 不再让 Final 字体强行认领不支持的字符，留给后续 System Fallback 处理
						InvalidChar.Add(Char);
						continue;
					}

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