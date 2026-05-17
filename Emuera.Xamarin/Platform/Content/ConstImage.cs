using MinorShift.Emuera.Runtime.Utils;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;

namespace MinorShift.Emuera.Content
{
	internal abstract class AbstractImage : AContentFile
	{
		public const int MAX_IMAGESIZE = 8192;
		public SKBitmap Bitmap;
		protected SKCanvas g;
	}

	internal sealed class ConstImage : AbstractImage
	{
		public ConstImage(string name)
		{ Name = name; }

		public readonly string Name;
		private string filepath;

		internal void CreateFrom(SKBitmap bmp, bool useGDI)
		{
			if (Bitmap != null)
				throw new Exception();
			try
			{
				Bitmap = bmp;
			}
			catch
			{
				return;
			}
			return;
		}

		internal void CreateFrom(string filepath, bool useGDI)
		{
			if (Bitmap != null)
				throw new Exception();
			this.filepath = filepath;
			try
			{
				using var codec = SKCodec.Create(filepath);
				if (codec != null)
				{
					var info = codec.Info;
					Bitmap = new SKBitmap(info.Width, info.Height);
				}
				else
				{
					Bitmap = SKBitmap.Decode(filepath);
				}
			}
			catch
			{
				return;
			}
		}

		public void EnsureBitmapLoaded()
		{
			if (Bitmap != null && filepath != null)
			{
				var decoded = SKBitmap.Decode(filepath);
				if (decoded != null)
				{
					Bitmap.Dispose();
					Bitmap = decoded;
				}
			}
		}

		public override void Dispose()
		{
			if (Bitmap == null)
				return;
			if (g != null)
			{
				g.Dispose();
				g = null;
			}
			if (Bitmap != null)
			{
				Bitmap.Dispose();
				Bitmap = null;
			}
		}

		~ConstImage()
		{
			Dispose();
		}

		public override bool IsCreated
		{
			get { return Bitmap != null; }
		}
	}
}
