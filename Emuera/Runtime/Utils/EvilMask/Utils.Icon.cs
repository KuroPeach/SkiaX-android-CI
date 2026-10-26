using SkiaSharp;
using SkiaSharp.Views.Desktop;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace MinorShift.Emuera.Runtime.Utils.EvilMask;

/// <summary>桌面专用：从文件读 Bitmap / 生成窗口 Icon（依赖 GDI+，headless 工程排除本文件）。</summary>
internal sealed partial class Utils
{
	// filepathの安全性(ゲームフォルダ以外のフォルダか)を確認しない
	public static Bitmap LoadImage(string filepath)
	{
		Bitmap bmp = null;
		if (!File.Exists(filepath)) return null;
		try
		{
			using var skbmp = SKBitmap.Decode(filepath);
			if (skbmp != null)
			{
				return skbmp.ToBitmap();
			}
			return new Bitmap(filepath);
		}
		catch { }
		return bmp;
	}

	// ビットマップファイルからアイコンファイルをつくる
	public static Icon MakeIconFromBmpFile(Bitmap bmp)
	{
		Image img = bmp;

		Bitmap bitmap = new(256, 256, PixelFormat.Format32bppArgb);
		Graphics g = Graphics.FromImage(bitmap);
		g.DrawImage(img, new Rectangle(0, 0, 256, 256));
		g.Dispose();

		Icon icon = Icon.FromHandle(bitmap.GetHicon());

		img.Dispose();
		bitmap.Dispose();
		return icon;
	}
}
