using MinorShift.Emuera.UI.Game;
using SkiaSharp;
using System.Drawing;

namespace MinorShift.Emuera.GameView;

/// <summary>N1-c 无窗工程：Rikaichan（可选日语词典浮窗）的 no-op 替身。桌面工程用真实现。</summary>
internal sealed class Rikaichan
{
	public bool enabled = false;
	public ConsoleStyledString css;
	public Point point;
	public int curLineY = -1;
	public string laststr;
	public int strpos = -1;
	public int laststrpos = -1;
	public string output;
	public bool hidden = true;

	public void OnPaint(SKCanvas graph, StringMeasure stringMeasure, int screenWidth) { }
}
