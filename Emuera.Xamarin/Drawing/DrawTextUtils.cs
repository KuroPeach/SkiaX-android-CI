using MinorShift.Emuera;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using XEmuera.Models;
using XEmuera;

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
			writer.WriteLine($"========== FontDiag T6 ==========");

			try
			{
				writer.WriteLine($"[Probe] FontModel.Init = {FontModel.Init}");
				writer.WriteLine($"[Probe] FontModel.AllModels.Count = {FontModel.AllModels.Count}");
				foreach (var kv in FontModel.AllModels)
				{
					var fm = kv.Value;
					var tf = fm.Typeface;
					float ascent = 0;
					if (tf != null)
					{
						using var probePaint = new SKPaint { Typeface = tf, TextSize = 18 };
						ascent = probePaint.FontMetrics.Ascent;
					}
					//writer.WriteLine($"[Probe] font={fm.Name} enabled={fm.Enabled} family={tf?.FamilyName} Ascent={ascent:F2} GetGlyph(劲)={tf?.GetGlyph('劲')} GetGlyph(组)={tf?.GetGlyph('组')} GetGlyph(姬)={tf?.GetGlyph('姬')}");
					writer.WriteLine($"[Probe] font={fm.Name} enabled={fm.Enabled} family={tf?.FamilyName} Ascent={ascent:F2}");
				}

				// [DIAG-SPACE] 空格宽度诊断：对比 MeasureText vs GetGlyphWidths
				writer.WriteLine("[DIAG-SPACE] === Space character width diagnostics ===");
				try
				{
					var defaultFont = FontModel.Default;
					if (defaultFont?.Typeface != null)
					{
						using var probePaint = new SKPaint { Typeface = defaultFont.Typeface, TextSize = 18 };
						var spaceGlyphs = probePaint.GetGlyphs(" ");
						var spaceWidths = probePaint.GetGlyphWidths(" ");
						ushort typefaceGlyph = defaultFont.Typeface.GetGlyph(' ');
						float measureSingle = probePaint.MeasureText(" ");
						float measureTen = probePaint.MeasureText("          ");
						probePaint.TextSize = Config.FontSize;
						float measureSingleFull = probePaint.MeasureText(" ");
						float measureTenFull = probePaint.MeasureText("          ");
						var spaceWidthsFull = probePaint.GetGlyphWidths("          ");
						writer.WriteLine($"[DIAG-SPACE] Default font: {defaultFont.Name} family={defaultFont.Typeface.FamilyName}");
						writer.WriteLine($"[DIAG-SPACE]   GetGlyphs(' ') = [{string.Join(", ", spaceGlyphs)}]");
						writer.WriteLine($"[DIAG-SPACE]   GetGlyphWidths(' ') = [{string.Join(", ", spaceWidths)}]");
						writer.WriteLine($"[DIAG-SPACE]   Typeface.GetGlyph(0x20) = {typefaceGlyph}");
						writer.WriteLine($"[DIAG-SPACE]   MeasureText(' ') = {measureSingle:F4} (size=18)");
						writer.WriteLine($"[DIAG-SPACE]   MeasureText(' '*10) = {measureTen:F4} (size=18)");
						writer.WriteLine($"[DIAG-SPACE]   MeasureText(' ') = {measureSingleFull:F4} (size={Config.FontSize})");
						writer.WriteLine($"[DIAG-SPACE]   MeasureText(' '*10) = {measureTenFull:F4} (size={Config.FontSize})");
						writer.WriteLine($"[DIAG-SPACE]   GetGlyphWidths(' '*10) = [{string.Join(", ", spaceWidthsFull.Select(w => w.ToString("F2")))}]");
						writer.WriteLine($"[DIAG-SPACE]   Sum(GlyphWidths*10) = {spaceWidthsFull.Sum():F4} vs MeasureText*10 = {measureTenFull:F4} diff = {measureTenFull - spaceWidthsFull.Sum():F4}");
						writer.WriteLine($"[DIAG-SPACE]   MeasureText('A') = {probePaint.MeasureText("A"):F4}");
						writer.WriteLine($"[DIAG-SPACE]   MeasureText('漢') = {probePaint.MeasureText("漢"):F4}");
						writer.WriteLine($"[DIAG-SPACE]   GetGlyphWidths('A') = [{string.Join(", ", probePaint.GetGlyphWidths("A"))}]");
						writer.WriteLine($"[DIAG-SPACE]   GetGlyphWidths('漢') = [{string.Join(", ", probePaint.GetGlyphWidths("漢"))}]");
					}

					// 对所有已启用字体进行空格诊断
					foreach (var fm in FontModel.AllModels.Values.Where(f => f.Enabled))
					{
						if (fm.Typeface == null) continue;
						using var p = new SKPaint { Typeface = fm.Typeface, TextSize = Config.FontSize };
						var sg = p.GetGlyphs(" ");
						var sw = p.GetGlyphWidths(" ");
						float ms = p.MeasureText(" ");
						float m10 = p.MeasureText("          ");
						var sw10 = p.GetGlyphWidths("          ");
						writer.WriteLine($"[DIAG-SPACE] font={fm.Name} family={fm.Typeface.FamilyName} GetGlyphs(' ')=[{string.Join(",", sg)}] GlyphWidths(' ')=[{string.Join(",", sw)}] MeasureText(' ')={ms:F2} MeasureText(' '*10)={m10:F2} Sum(GlyphWidths*10)={sw10.Sum():F2} diff={m10 - sw10.Sum():F2}");
					}
				}
				catch (Exception ex)
				{
					writer.WriteLine($"[DIAG-SPACE] ERROR: {ex}");
				}

				// 修复：如果 Microsoft YaHei 未启用，强制启用并重新加载
				if (FontModel.AllModels.TryGetValue("Microsoft YaHei", out var yaheiModel) && !yaheiModel.Enabled)
				{
					writer.WriteLine("[Fix] Force-enabling Microsoft YaHei and reloading DrawTextUtils");
					yaheiModel.Enabled = true;
					FontModel.Save();
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
		public static void Load()
		{
		}

		public static void Reset()
		{
		}

		public static void DrawText(SKCanvas canvas, string text, Font font, RectangleF rect, Color color)
		{
			DrawText(canvas, text, font, rect.X, rect.Y, color);
		}

		public static void DrawText(SKCanvas canvas, string text, Font font, float x, float y, Color color)
		{
			if (string.IsNullOrEmpty(text))
				return;

			using var paint = new SKPaint
			{
				TextSize = font.Size > 0 ? font.Size : 18,
				Color = DisplayUtils.ToSKColor(color),
				IsAntialias = Config.TextAntialias,
				Typeface = font.FontModel?.Typeface ?? SKTypeface.Default,
				FakeBoldText = font.Style.HasFlag(FontStyle.Bold),
				TextSkewX = font.Style.HasFlag(FontStyle.Italic) ? -0.4f : 0,
			};

			float baselineY = y - paint.FontMetrics.Ascent;

			canvas.DrawText(text, x, baselineY, paint);

			if (font.Style.HasFlag(FontStyle.Strikeout))
			{
				float width = paint.MeasureText(text);
				paint.StrokeWidth = paint.FontMetrics.StrikeoutThickness ?? 1;
				canvas.DrawLine(x, baselineY + (paint.FontMetrics.StrikeoutPosition ?? 0), x + width, baselineY + (paint.FontMetrics.StrikeoutPosition ?? 0), paint);
			}
			if (font.Style.HasFlag(FontStyle.Underline))
			{
				float width = paint.MeasureText(text);
				paint.StrokeWidth = paint.FontMetrics.UnderlineThickness ?? 1;
				canvas.DrawLine(x, baselineY + (paint.FontMetrics.UnderlinePosition ?? 0), x + width, baselineY + (paint.FontMetrics.UnderlinePosition ?? 0), paint);
			}
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