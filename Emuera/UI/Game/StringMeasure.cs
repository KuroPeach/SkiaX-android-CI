using MinorShift.Emuera.Runtime.Config;
using System;
using System.Drawing;
using SkiaSharp;

namespace MinorShift.Emuera.UI.Game;

/// <summary>
/// テキスト長計測装置
/// 1819 必要になるたびにCreateGraphicsする方式をやめて 미리用意해둔 Graphicsを準備しておくことにする
/// </summary>
internal sealed class StringMeasure : IDisposable
{
	public static int GetDisplayLength(ReadOnlySpan<char> chars, SKFont f)
	{
		if (f == null)
			// P1 兜底：字体不可用（极端情况）时按字号估算，返回值必须 > 0——
			// 调用方 getStBar 的 while (width < DrawableWidth) 会因 0 宽度死循环。
			return chars.Length * Math.Max(1, (int)Config.FontSize);
		using var paint = new SKPaint();
		paint.Typeface = f.Typeface;
		paint.TextSize = f.Size;
		return (int)paint.MeasureText(chars.ToString());
	}


	bool disposed;
	public void Dispose()
	{
		if (disposed)
			return;
		disposed = true;
	}
}
