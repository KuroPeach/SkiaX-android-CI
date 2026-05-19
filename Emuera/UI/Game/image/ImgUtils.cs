using MinorShift.Emuera.Runtime.Utils;
using SkiaSharp;
using System.IO;

namespace MinorShift.Emuera.UI.Game.Image;

static class ImgUtils
{
	public static SKBitmap LoadImageSK(string filepath)
	{
		if (!File.Exists(filepath))
			return null;

		try
		{
			var skbmp = SKBitmap.Decode(filepath);
			return skbmp;
		}
		catch
		{
			return null;
		}
	}

#if WINDOWS
	public static System.Drawing.Bitmap LoadImage(string filepath)
	{
		if (!File.Exists(filepath))
			return null;

		try
		{
			using var skbmp = SKBitmap.Decode(filepath);
			if (skbmp != null)
			{
				return SkiaSharp.Views.Desktop.Extensions.ToBitmap(skbmp);
			}
			return new System.Drawing.Bitmap(filepath);
		}
		catch
		{
			return null;
		}
	}
#endif
}
