using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using MinorShift.Emuera.Runtime.Utils;
using MinorShift.Emuera;
using MinorShift.Emuera.UI.Game;
using MinorShift.Emuera.Runtime.Config;
using XEmuera.Drawing;
using XEmuera.Models;
using XEmuera;
using SkiaSharp;
using TextDrawingMode = MinorShift.Emuera.Runtime.Config.TextDrawingMode;

namespace MinorShift.Emuera.GameView
{
	internal struct TextsWithFont
	{
		public string Text;
		public SKFont Font;
		public float Width;
	}

	internal sealed class ConsoleStyledString : AConsoleColoredPart
	{
		private ConsoleStyledString() { }
		public ConsoleStyledString(string str, StringStyle style)
		{
			this.Text = str;
			this.StringStyle = style;
			Font = Config.GetFont(style.Fontname, style.FontStyle);
			if (Font == null)
			{
				Error = true;
				return;
			}
			Color = style.Color;
			ButtonColor = style.ButtonColor;
			colorChanged = style.ColorChanged;
			if (!colorChanged && Color != Config.ForeColor)
				colorChanged = true;
			PointX = -1;
			Width = -1;

			BuildFallbacks();
		}
		public ConsoleStyledString(string str, StringStyle style, TextDrawingMode? renderMode, SkiaSharpFontEdging? fontEdging, SkiaSharpFontHinting? fontHinting, float? fontSize, FontVerticalAlign? verticalAlign)
		{
			this.Text = str;
			this.StringStyle = style;
			RenderMode = renderMode;
			FontEdging = fontEdging;
			FontHinting = fontHinting;
			FontSize = fontSize;
			VerticalAlign = verticalAlign;

			var actualFontSize = fontSize ?? Config.FontSize;
			var actualEdging = fontEdging ?? Config.FontEdging;
			var actualHinting = fontHinting ?? Config.FontHinting;

			var skFont = FontFactory.GetFont(style.Fontname, style.FontStyle, actualFontSize, actualEdging, actualHinting);
			if (skFont == null)
			{
				Error = true;
				return;
			}
			Font = new Font(skFont);
			Font.Style = style.FontStyle;

			Color = style.Color;
			ButtonColor = style.ButtonColor;
			colorChanged = style.ColorChanged;
			if (!colorChanged && Color != Config.ForeColor)
				colorChanged = true;
			PointX = -1;
			Width = -1;

			BuildFallbacks();
		}
		public Font Font{ get; private set;}
		public StringStyle StringStyle{ get; private set;}
		public float? FontSize { get; private set; }
		public TextDrawingMode? RenderMode { get; private set; }
		public SkiaSharpFontEdging? FontEdging { get; private set; }
		public SkiaSharpFontHinting? FontHinting { get; private set; }
		public FontVerticalAlign? VerticalAlign { get; private set; }
		List<TextsWithFont> _texts;
		public override bool CanDivide
		{
			get { return true; }
		}

		private void BuildFallbacks()
		{
			if (Font?.SKFont?.Typeface == null)
				return;

			var typeface = Font.SKFont.Typeface;

			bool hasSurrogate = false;
			for (int i = 0; i < Text.Length; i++)
			{
				if (char.IsSurrogate(Text[i])) { hasSurrogate = true; break; }
			}

			if (!hasSurrogate && typeface.ContainsGlyphs(Text))
			{
				_texts = null;
				return;
			}

			var textsWithFontList = new List<TextsWithFont>();
			var currentText = new StringBuilder();
			SKTypeface currentTypeface = null;

			var enumerator = System.Globalization.StringInfo.GetTextElementEnumerator(Text);
			while (enumerator.MoveNext())
			{
				string grapheme = enumerator.GetTextElement();
				int codepoint = char.ConvertToUtf32(grapheme, 0);

				SKTypeface glyphTypeface = typeface;

				if (!typeface.ContainsGlyphs(grapheme))
				{
					glyphTypeface = FontFactory.GetFallbackTypefaceForCodepoint(codepoint, typeface.FamilyName) ?? typeface;
				}

				if (currentTypeface == null)
					currentTypeface = glyphTypeface;

				if (glyphTypeface != currentTypeface)
				{
					if (currentText.Length > 0)
					{
						textsWithFontList.Add(CreateTextWithFont(currentTypeface, currentText.ToString()));
						currentText.Clear();
					}
					currentTypeface = glyphTypeface;
				}
				currentText.Append(grapheme);
			}

			if (currentText.Length > 0)
			{
				textsWithFontList.Add(CreateTextWithFont(currentTypeface, currentText.ToString()));
			}

			_texts = textsWithFontList;
		}

		private TextsWithFont CreateTextWithFont(SKTypeface typeface, string t)
		{
			var textsWithFont = new TextsWithFont
			{
				Text = t,
				Font = new SKFont(typeface, Font.Size)
			};
			using var paint = new SKPaint();
			paint.Typeface = typeface;
			paint.TextSize = Font.Size;
			textsWithFont.Width = paint.MeasureText(textsWithFont.Text);
			return textsWithFont;
		}

		public ConsoleStyledString DivideAt(int index, StringMeasure sm)
		{
			ConsoleStyledString ret = DivideAt(index);
			if (ret == null)
				return null;
			this.SetWidth(sm, XsubPixel);
			ret.SetWidth(sm, XsubPixel);
			return ret;
		}
		public ConsoleStyledString DivideAt(int index)
		{
			if ((index <= 0) || (index > Text.Length) || this.Error)
				return null;
			string str = Text.Substring(index, Text.Length - index);
			this.Text = Text.Substring(0, index);
			ConsoleStyledString ret = new ConsoleStyledString();
			ret.Font = this.Font;
			ret.Text = str;
			ret.Color = this.Color;
			ret.ButtonColor = this.ButtonColor;
			ret.colorChanged = this.colorChanged;
			ret.StringStyle = this.StringStyle;
			ret.XsubPixel = this.XsubPixel;
			ret.FontSize = this.FontSize;
			ret.RenderMode = this.RenderMode;
			ret.FontEdging = this.FontEdging;
			ret.FontHinting = this.FontHinting;
			ret.VerticalAlign = this.VerticalAlign;
			ret._texts = null;
			this._texts = null;
			return ret;
		}

		public override void SetWidth(StringMeasure sm, float subPixel)
		{
			if (this.Error)
			{
				Width = 0;
				return;
			}

			if (_texts == null)
			{
				Width = sm.GetDisplayLength(Text, Font);
			}
			else
			{
				var offsetX = 0.0f;
				foreach (var text in _texts) offsetX += text.Width;
				Width = (int)offsetX;
			}

			XsubPixel = subPixel;
			#region EmuEra-Rikaichan
			if (!rikaichaned && Config.RikaiEnabled)
			{
				rikaichaned = true;
				int len = Text.Length;
				Ends = new int[len];
				for (int i = 0; i < len; i++)
				{
					string temp = Text.Substring(0, i + 1);
					Ends[i] = sm.GetDisplayLength(temp, Font);
				}
			}
			#endregion
		}

		public override void DrawTo(SKCanvas graph, SKPoint point, bool isSelecting, bool isFocus, bool isBackLog, TextDrawingMode mode, bool isButton = false)
		{
			if (this.Error)
				return;
			Color color = this.Color;
			if(isSelecting)
				color = this.ButtonColor;
			else if (isBackLog && !colorChanged)
                color = Config.LogColor;

			var skColor = DisplayUtils.ToSKColor(color);
			float x = PointX;
			float baselineY = point.Y - Font.SKFont.Metrics.Ascent;
			// valign 偏移：top 不偏移，middle/bottom 按行高偏移
			if (VerticalAlign == FontVerticalAlign.Middle)
				baselineY += (Config.LineHeight - Font.SKFont.Metrics.Descent + Font.SKFont.Metrics.Ascent) / 2f;
			else if (VerticalAlign == FontVerticalAlign.Bottom)
				baselineY += Config.LineHeight - Font.SKFont.Metrics.Descent + Font.SKFont.Metrics.Ascent;

			using var paint = new SKPaint
			{
				Color = skColor,
				IsAntialias = Config.TextAntialias,
			};

			if (_texts == null)
			{
				paint.Typeface = Font.SKFont?.Typeface;
				paint.TextSize = Font.Size;
				paint.FakeBoldText = Font.Style.HasFlag(FontStyle.Bold);
				paint.TextSkewX = Font.Style.HasFlag(FontStyle.Italic) ? -0.4f : 0;
				graph.DrawText(Text, x, baselineY, Font.SKFont, paint);
			}
			else
			{
				foreach (var text in _texts)
				{
					paint.Typeface = text.Font.Typeface;
					paint.TextSize = Font.Size;
					paint.FakeBoldText = Font.Style.HasFlag(FontStyle.Bold);
					paint.TextSkewX = Font.Style.HasFlag(FontStyle.Italic) ? -0.4f : 0;
					graph.DrawText(text.Text, x, baselineY, text.Font, paint);
					x += text.Width;
				}
			}

			if (StringStyle.HasUnderline)
			{
				float underlineY = baselineY + (Font.SKFont.Metrics.UnderlinePosition ?? 0);
				paint.StrokeWidth = Font.SKFont.Metrics.UnderlineThickness ?? 1;
				paint.Typeface = Font.SKFont?.Typeface;
				graph.DrawLine(PointX, underlineY, PointX + Width, underlineY, paint);
			}
			if (StringStyle.HasStrikeout)
			{
				float strikeoutY = baselineY + (Font.SKFont.Metrics.StrikeoutPosition ?? 0);
				paint.StrokeWidth = Font.SKFont.Metrics.StrikeoutThickness ?? 1;
				paint.Typeface = Font.SKFont?.Typeface;
				graph.DrawLine(PointX, strikeoutY, PointX + Width, strikeoutY, paint);
			}
		}
	}
}
