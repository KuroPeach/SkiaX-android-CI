using MinorShift.Emuera.Runtime.Config;
using MinorShift.Emuera.GameView;
using SkiaSharp;
using System.Text;

namespace MinorShift.Emuera.UI.Game;

abstract class AConsoleDisplayNode
{
	public bool Error { get; protected set; }
	public string Text { get; protected set; }
	public string AltText { get; protected set; }
	public virtual int PointX { get; set; }
	public float XsubPixel { get; set; }
	public float WidthF { get; set; }
	public int Width { get; set; }
	public virtual int Top { get { return 0; } }
	public virtual int Bottom { get { return Config.FontSize; } }
	public abstract bool CanDivide { get; }
	public SKSize? Size;
	public SKPoint? Point;

	public abstract void DrawTo(SKCanvas graph, SKPoint point, bool isSelecting, bool isFocus, bool isBackLog, TextDrawingMode mode, bool isButton = false);
	public abstract void SetWidth(StringMeasure sm, float subPixel);
	public override string ToString()
	{
		if (Text == null) return "";
		return Text;
	}

	public object Parent { get; set; }
	public int Depth { get; set; }
	public virtual StringBuilder BuildString(StringBuilder sb)
	{
		if (Text != null) sb.Append(Text);
		return sb;
	}
}
