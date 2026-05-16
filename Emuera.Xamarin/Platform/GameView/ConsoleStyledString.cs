using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using MinorShift.Emuera.Runtime.Utils;
using MinorShift.Emuera;
using MinorShift.Emuera.UI.Game;
using MinorShift.Emuera.Runtime.Config;
using XEmuera.Drawing;
using SkiaSharp;
using TextDrawingMode = MinorShift.Emuera.Runtime.Config.TextDrawingMode;

namespace MinorShift.Emuera.GameView
{
	/// <summary>
	/// 装飾付文字列。stringとStringStyleからなる。
	/// </summary>
	internal sealed class ConsoleStyledString : AConsoleColoredPart
	{
		private ConsoleStyledString() { }
		public ConsoleStyledString(string str, StringStyle style)
		{
            //if ((StaticConfig.TextDrawingMode != TextDrawingMode.GRAPHICS) && (str.IndexOf('\t') >= 0))
            //    str = str.Replace("\t", "");
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
		}
		public ConsoleStyledString(string str, StringStyle style, TextDrawingMode? renderMode, SkiaSharpFontEdging? fontEdging, SkiaSharpFontHinting? fontHinting, float? fontSize)
			: this(str, style)
		{
			RenderMode = renderMode;
			FontEdging = fontEdging;
			FontHinting = fontHinting;
			FontSize = fontSize;
		}
		public Font Font{ get; private set;}
		public StringStyle StringStyle{ get; private set;}
		public float? FontSize { get; private set; }
		public TextDrawingMode? RenderMode { get; private set; }
		public SkiaSharpFontEdging? FontEdging { get; private set; }
		public SkiaSharpFontHinting? FontHinting { get; private set; }
		public override bool CanDivide
		{
			get { return true; }
		}
		//単一のボタンフラグ
		//public bool IsButton { get; set; }
		//indexの文字数の前方文字列とindex以降の後方文字列に分割
		public ConsoleStyledString DivideAt(int index, StringMeasure sm)
		{
			//if ((index <= 0)||(index > Text.Length)||this.Error)
			//	return null;
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
			return ret;
		}

		public override void SetWidth(StringMeasure sm, float subPixel)
		{
			if (this.Error)
			{
				Width = 0;
				return;
			}
			Width = sm.GetDisplayLength(Text, Font);
			XsubPixel = subPixel;
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

			//if (mode == TextDrawingMode.GRAPHICS)
			//	graph.DrawString(Text, Font, new SolidBrush(color), new Point(PointX, (int)point.Y));
			//else
			//	TextRenderer.DrawText(graph, Text, Font, new Point(PointX, (int)point.Y), color, TextFormatFlags.NoPrefix);

			DrawTextUtils.DrawText(graph, Text, Font, PointX, (int)point.Y, color);
		}

		//public override void GDIDrawTo(int pointY, bool isSelecting, bool isBackLog)
		//{
		//	if (this.Error)
		//		return;
		//	Color color = this.Color;
		//	if(isSelecting)
		//		color = this.ButtonColor;
		//	else if (isBackLog && !colorChanged)
		//		color = Config.LogColor;
		//	GDI.TabbedTextOutFull(Font,color,Text, PointX, pointY);
		//	//GDI.SetFont(Font);
		//	//GDI.SetTextColor(color);
		//	//GDI.TabbedTextOut(Text, PointX, pointY);
		//}

	}
}
