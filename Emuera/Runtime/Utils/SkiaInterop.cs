using SkiaSharp;
using System.Drawing;

namespace MinorShift.Emuera;

/// <summary>
/// System.Drawing.Primitives ⇄ SkiaSharp 的跨平台转换。
/// 替代 SkiaSharp.Views.Desktop 的 ToSKColor/ToDrawingColor/ToSKRect/ToSKPoint/ToSKSize 扩展
/// （后者的 .NET 8 实现随 System.Drawing.Common 仅 Windows 可用）。
/// </summary>
internal static class SkiaInterop
{
	public static SKColor ToSKColor(this Color c) => new(c.A, c.R, c.G, c.B);

	public static Color ToDrawingColor(this SKColor c)
		=> Color.FromArgb(c.Alpha, c.Red, c.Green, c.Blue);

	public static SKPoint ToSKPoint(this Point p) => new(p.X, p.Y);

	public static SKPoint ToSKPoint(this PointF p) => new(p.X, p.Y);

	public static SKSize ToSKSize(this Size s) => new(s.Width, s.Height);

	public static SKSize ToSKSize(this SizeF s) => new(s.Width, s.Height);

	public static SKRect ToSKRect(this Rectangle r) => new(r.Left, r.Top, r.Right, r.Bottom);

	public static SKRect ToSKRect(this RectangleF r) => new(r.Left, r.Top, r.Right, r.Bottom);
}
