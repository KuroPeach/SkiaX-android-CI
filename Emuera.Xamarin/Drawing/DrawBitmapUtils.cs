using MinorShift.Emuera;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace XEmuera.Drawing
{
	public static class DrawBitmapUtils
	{
		private static SKPaint Paint;

		public static void Reset()
		{
			if (Paint != null)
				Paint.Dispose();
			Paint = new SKPaint
			{
				IsAntialias = Config.ShapeAntialias,
				FilterQuality = Config.ShapeFilterQuality
			};
		}

		public static void DrawBitmap(SKCanvas canvas, SKBitmap bitmap, Rectangle src, Rectangle dest)
		{
			DrawBitmap(canvas, bitmap, src, dest, Paint);
		}

		public static void DrawBitmap(SKCanvas canvas, SKBitmap bitmap, Rectangle src, Rectangle dest, SKColorFilter colorFilter)
		{
			using (SKPaint paint = Paint.Clone())
			{
				paint.ColorFilter = colorFilter;
				DrawBitmap(canvas, bitmap, src, dest, paint);
			}
		}

		public static void DrawBitmap(SKCanvas canvas, SKBitmap bitmap, Rectangle src, Rectangle dest, SKPaint paint)
		{
			canvas.Save();
			canvas.Scale(Math.Sign(dest.Width), Math.Sign(dest.Height), dest.Left, dest.Top);

			dest.Width = Math.Abs(dest.Width);
			dest.Height = Math.Abs(dest.Height);

			SKRectI srcRect = SKRectI.Create(src.X, src.Y, src.Width, src.Height);
			SKRectI destRect = SKRectI.Create(dest.X, dest.Y, dest.Width, dest.Height);

			canvas.DrawBitmap(bitmap, srcRect, destRect, paint);
			canvas.Restore();
		}

		public static void DrawBitmap(SKCanvas canvas, SKBitmap bitmap, Rectangle dest)
		{
			canvas.Save();
			canvas.Scale(Math.Sign(dest.Width), Math.Sign(dest.Height), dest.X, dest.Y);

			dest.Width = Math.Abs(dest.Width);
			dest.Height = Math.Abs(dest.Height);

			SKRectI destRect = SKRectI.Create(dest.X, dest.Y, dest.Width, dest.Height);

			canvas.DrawBitmap(bitmap, destRect, Paint);
			canvas.Restore();
		}

		public static void DrawRect(SKCanvas canvas, SKPaint paint, Rectangle rect)
		{
			canvas.DrawRect(rect.X, rect.Y, rect.Width, rect.Height, paint);
		}
	}

	public class SolidBrush : Brush
	{
		public Color Color => DrawingColor;
		public SolidBrush(Color color) : base(color) { }
#if WINDOWS
		public SolidBrush(System.Drawing.SolidBrush sb) : base(sb.Color) { }
		public static implicit operator SolidBrush(System.Drawing.SolidBrush sb) => new SolidBrush(sb);
#endif
	}

	public class Brush : Pen
	{
		public Brush(Color color) : base(color)
		{
			IsStroke = false;
		}
#if WINDOWS
		public Brush(System.Drawing.Brush b) : base(((System.Drawing.SolidBrush)b).Color)
		{
			IsStroke = false;
		}
		public static implicit operator Brush(System.Drawing.Brush b) => new Brush(b);
#endif
	}

	public class Pen : SKPaint
	{
		public Color DrawingColor;
		public float Width { get => StrokeWidth; set => StrokeWidth = value; }
		public DashStyle DashStyle { get; set; } = DashStyle.Solid;
		public DashCap DashCap { get; set; } = DashCap.Flat;

		public Pen(Color color, long width = 1)
		{
			DrawingColor = color;
			IsStroke = true;
			Color = DisplayUtils.ToSKColor(color);
			StrokeWidth = width;
			IsAntialias = Config.ShapeAntialias;
			FilterQuality = Config.ShapeFilterQuality;
		}

#if WINDOWS
		public Pen(System.Drawing.Pen p)
		{
			DrawingColor = p.Color;
			IsStroke = true;
			Color = DisplayUtils.ToSKColor(p.Color);
			StrokeWidth = (long)p.Width;
			IsAntialias = Config.ShapeAntialias;
			FilterQuality = Config.ShapeFilterQuality;
		}

		public static implicit operator Pen(System.Drawing.Pen p) => new Pen(p);
#endif
	}

	public enum DashStyle
	{
		Solid = 0,
		Dash = 1,
		Dot = 2,
		DashDot = 3,
		DashDotDot = 4
	}

	public enum DashCap
	{
		Flat = 0,
		Round = 2,
		Triangle = 3
	}
}