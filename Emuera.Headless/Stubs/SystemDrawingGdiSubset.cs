using System;

// N1-c：headless 工程不引用 System.Drawing.Common（仅 Windows）。
// 这里只放共享源码仍需要的 GDI 子集「数据持有者」：
//   - Pen/Brush/SolidBrush 同时被 Skia 绘制路径读取（Color/Width/DashStyle）；
//   - FontStyle 是 StringStyle/FontFactory 的公共枚举；
// GDI 实际绘制路径均在 #if WINDOWS 内，非 Windows 不会执行。
// ⚠️ 本文件只在 Emuera.Headless.csproj 中编译（桌面工程用真正的 System.Drawing.Common）。

namespace System.Drawing
{
	[Flags]
	public enum FontStyle
	{
		Regular = 0,
		Bold = 1,
		Italic = 2,
		Underline = 4,
		Strikeout = 8,
	}

	public enum GraphicsUnit
	{
		World = 0,
		Display = 1,
		Pixel = 2,
		Point = 3,
		Inch = 4,
		Document = 5,
		Millimeter = 6,
	}

	public abstract class Brush : IDisposable
	{
		public abstract void Dispose();
	}

	public sealed class SolidBrush : Brush
	{
		public SolidBrush(Color color) { Color = color; }
		public Color Color { get; set; }
		public override void Dispose() { }
	}

	public sealed class Pen : IDisposable
	{
		public Pen(Color color) : this(color, 1f) { }
		public Pen(Color color, float width)
		{
			Color = color;
			Width = width;
			DashStyle = Drawing2D.DashStyle.Solid;
			DashCap = Drawing2D.DashCap.Flat;
		}
		public Color Color { get; set; }
		public float Width { get; set; }
		public Drawing2D.DashStyle DashStyle { get; set; }
		public Drawing2D.DashCap DashCap { get; set; }
		public void Dispose() { }
	}
}

namespace System.Drawing.Drawing2D
{
	public enum DashStyle
	{
		Solid = 0,
		Dash = 1,
		Dot = 2,
		DashDot = 3,
		DashDotDot = 4,
		Custom = 5,
	}

	public enum DashCap
	{
		Flat = 0,
		Round = 2,
		Triangle = 3,
	}
}
