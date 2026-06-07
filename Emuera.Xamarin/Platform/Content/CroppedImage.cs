using MinorShift.Emuera.Runtime.Script;
using MinorShift.Emuera.Runtime.Script.Parser;
using MinorShift.Emuera.Runtime.Utils;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using XEmuera;
using XEmuera.Drawing;
using trerror = MinorShift.Emuera.Runtime.Utils.EvilMask.Lang.Error;

namespace MinorShift.Emuera.Content
{
	abstract class AContentItem
	{
		protected AContentItem(string name) { Name = name; }
		public readonly string Name;
		//public bool Enabled { get; protected set; }
		public abstract bool IsCreated { get; }
	}

	internal abstract class ASprite : AContentItem, IDisposable
	{
		public ASprite(string name, Size size)
			: base(name)
		{
			if (size.Width < 0)
				size.Width = -size.Width;
			if (size.Height < 0)
				size.Height = -size.Height;
			DestBaseSize = size;
		}
		public abstract SKColor SpriteGetColor(int x, int y);
		/// <summary>
		/// 出力される標準のサイズ。正の値のみ。
		/// </summary>
		public readonly Size DestBaseSize;

		/// <summary>
		/// 出力時の位置調整。拡大縮小して出力する場合には同じ比率で調整する。
		/// </summary>
		public Point DestBasePosition;


		public abstract void GraphicsDraw(SKCanvas g, Point offset);
		public abstract void GraphicsDraw(SKCanvas g, Rectangle destRect);
		public abstract void GraphicsDraw(SKCanvas g, Rectangle destRect, SKColorFilter attr);
		public abstract void Dispose();
		public void Move(Point point){ DestBasePosition.Offset(point); }
	}


	internal abstract class ASpriteSingle : ASprite
	{
		public ASpriteSingle(string name, AbstractImage img, Rectangle rect)
			: base(name, rect.Size)
		{
			SrcRectangle = rect;
			BaseImage = img;
		}
		public ASpriteSingle(string name, AbstractImage img, Rectangle rect, Size destSize)
			: base(name, destSize)
		{
			SrcRectangle = rect;
			BaseImage = img;
		}
		public AbstractImage BaseImage;

		/// <summary>
		/// ソース画像上の位置を指定する四角形。Width, Heightは負の値をとり得る
		/// </summary>
		public readonly Rectangle SrcRectangle;
		private SKBitmap Bitmap
		{
			get
			{
				if (BaseImage != null && BaseImage.IsCreated)
				{
					// 延迟加载：确保 ConstImage 的 Bitmap 已从 SharedBitmapCache 获取
					if (BaseImage.Bitmap == null && BaseImage is ConstImage ci)
						ci.EnsureBitmapLoaded();
					return BaseImage.Bitmap;
				}
				return null;
			}
		}

		public override bool IsCreated
		{
			get { return (BaseImage != null && BaseImage.IsCreated); }
		}
		public override SKColor SpriteGetColor(int x, int y)
		{
			SKBitmap bmp = this.Bitmap;
			if (bmp == null)
				return SKColor.Empty;
			int bmpX = x + SrcRectangle.X;
			int bmpY = y + SrcRectangle.Y;
			if (bmpX < 0 || bmpX >= bmp.Width || bmpY < 0 || bmpY >= bmp.Height)
				return SKColor.Empty;

			return bmp.GetPixel(bmpX, bmpY);
		}
		public override void Dispose()
		{
			BaseImage = null;
		}


		public override void GraphicsDraw(SKCanvas g, Point offset)
		{
			offset.Offset(DestBasePosition);
			//g.DrawImage(Bitmap, new Rectangle(offset, DestBaseSize), SrcRectangle, GraphicsUnit.Pixel);

			DrawBitmapUtils.DrawBitmap(g, Bitmap, SrcRectangle, new Rectangle(offset, DestBaseSize));
		}
		public override void GraphicsDraw(SKCanvas g, Rectangle destRect)
		{
			if (!DestBasePosition.IsEmpty)
			{
				destRect.X = destRect.X + DestBasePosition.X * destRect.Width / DestBaseSize.Width;
				destRect.Y = destRect.Y + DestBasePosition.Y * destRect.Height / DestBaseSize.Height;
				destRect.Width = destRect.Width * SrcRectangle.Width / DestBaseSize.Width;
				destRect.Height = destRect.Height * SrcRectangle.Height / DestBaseSize.Height;
			}
			//g.DrawImage(Bitmap, destRect, SrcRectangle, GraphicsUnit.Pixel);

			DrawBitmapUtils.DrawBitmap(g, Bitmap, SrcRectangle, destRect);
		}

		public override void GraphicsDraw(SKCanvas g, Rectangle destRect, SKColorFilter attr)
		{
			if (!DestBasePosition.IsEmpty)
			{
				destRect.X = destRect.X + DestBasePosition.X * destRect.Width / DestBaseSize.Width;
				destRect.Y = destRect.Y + DestBasePosition.Y * destRect.Height / DestBaseSize.Height;
				destRect.Width = destRect.Width * SrcRectangle.Width / DestBaseSize.Width;
				destRect.Height = destRect.Height * SrcRectangle.Height / DestBaseSize.Height;
			}
			////g.DrawImage(Bitmap, destRect, SrcRectangle, GraphicsUnit.Pixel, attr);←このパターンがない
			//g.DrawImage(Bitmap, destRect, SrcRectangle.X, SrcRectangle.Y, SrcRectangle.Width, SrcRectangle.Height, GraphicsUnit.Pixel, attr);

			DrawBitmapUtils.DrawBitmap(g, Bitmap, SrcRectangle, destRect, attr);
		}

	}

	/// <summary>
	/// ERB中で作るGを元にしたSprite。GDI非対応
	/// </summary>
	internal sealed class SpriteG : ASpriteSingle
	{
		public readonly int SourceGID;

		public SpriteG(string name, GraphicsImage gra, Rectangle rect)
			: base(name, gra, rect)
		{
			SourceGID = gra.ID;
		}
		public SpriteG(string name, GraphicsImage gra, Rectangle rect, Point pos, Size destSize)
			: base(name, gra, rect, destSize)
		{
			DestBasePosition = pos;
			SourceGID = gra.ID;
		}
	}

	/// <summary>
	/// ConstImage(csvから作るファイル占有型ベースイメージ)をもとにしたSprite
	/// </summary>
	internal sealed class SpriteF : ASpriteSingle
	{
		public SpriteF(string name, ConstImage image, Rectangle rect, Point pos)
			: base(name, image, rect)
		{
			this.DestBasePosition = pos;
		}
		public SpriteF(string name, ConstImage image, Rectangle rect, Point pos, Size destSize)
			: base(name, image, rect, destSize)
		{
			this.DestBasePosition = pos;
		}
	}

	/// <summary>
	/// AnimeするSprite。中身はほぼSprite
	/// </summary>
	internal sealed class SpriteAnime : ASprite
	{
		public SpriteAnime(string name, Size size)
			: base(name, size)
		{
			FrameList = new List<AnimeFrame>();
			totaltime = 0;
		}
		private sealed class AnimeFrame :IDisposable
		{
			public int index;
			public AbstractImage BaseImage;
			public Rectangle SrcRectangle;
			public Point Offset;
			public int DelayTimeMs;
			public void Normalize(Size parentSize)
			{
				Rectangle rect = Rectangle.Intersect(new Rectangle(Offset, SrcRectangle.Size), new Rectangle(new Point(), parentSize));
				if (rect.IsEmpty)
				{
					BaseImage = null;
					return;
				}
				Offset.X = rect.X;
				Offset.Y = rect.Y;
				SrcRectangle.Width = rect.Width;
				SrcRectangle.Height = rect.Height;
			}
			public void Dispose()
			{
				BaseImage = null;
			}
		}
		List<AnimeFrame> FrameList;
		public Int64 totaltime;

		internal bool AddFrame(AbstractImage parentImage, Rectangle rect, Point pos, int delay)
		{
			AnimeFrame frame = new AnimeFrame();
			frame.index = FrameList.Count;
			frame.BaseImage = parentImage;
			frame.SrcRectangle = rect;
			frame.Offset = pos;
			if(delay <= 0)
				delay = 1;
			frame.DelayTimeMs = delay;
			frame.Normalize(DestBaseSize);
			totaltime += delay;
			FrameList.Add(frame);
			return true;
		}
		
		/// <summary>
		/// アニメの経過時間を削除して最初からやり直す
		/// </summary>
		internal void ResetTime()
		{
			StartTime = -1;
			lastFrameTime = 0;
			lastFrame = -1;
		}

		private bool _paused = false;
		private Int64 _pausedAtTime = 0;

		internal void PauseAnimation()
		{
			if (_paused || StartTime < 0)
				return;
			_paused = true;
			_pausedAtTime = WinmmTimer.CurrentFrameTime;
		}

		internal void ResumeAnimation()
		{
			if (!_paused)
				return;
			_paused = false;
			// Shift StartTime forward by the duration we were paused
			StartTime += (WinmmTimer.CurrentFrameTime - _pausedAtTime);
		}

		/// <summary>
		/// 開始時間調整用の値。ミリ秒でUInt32の範囲まで想定。
		/// </summary>
		Int64 StartTime = -1;
		uint lastFrameTime = 0;
		int lastFrame = -1;
		private AnimeFrame GetCurrentFrame()
		{
			if (totaltime <= 0)
				return null;
#if DEBUG
			if (FrameList.Count == 0)
				throw new ExeEE(trerror.EmptyFramelist.Text);
			if (lastFrame >= FrameList.Count)
				throw new ExeEE(trerror.OoRLasframe.Text);
#endif
			//一度もフレーム取得したことがない場合は現在時間を記録して最初のフレームを返す。
			if (StartTime < 0)
			{
				StartTime = WinmmTimer.CurrentFrameTime;
				lastFrame = 0;
				return FrameList[0];
			}
			//時間経過なしに複数回呼ばれた場合はさっき返したフレームをもう一度返す。
			if (WinmmTimer.CurrentFrameTime == lastFrameTime && lastFrame >= 0)
				return FrameList[lastFrame];
			//StartTimeからの経過時間をtotaltimeで剰余計算
			Int64 time = (WinmmTimer.CurrentFrameTime - StartTime) % totaltime;
			//winmmtimerは一周して0になることがあり得るのでその場合の対策。C#の剰余の結果の符号は左辺値の符号に等しい。
			if (time < 0)
				time += totaltime;
			foreach(AnimeFrame frame in FrameList)
			{
				time -=frame.DelayTimeMs;
				if (time <= 0)
				{
					lastFrame = frame.index;
					return frame;
				}
			}
			//ここまでこないはず
			throw new ExeEE(trerror.SpriteTimeOut.Text);
		}

		public override bool IsCreated
		{
			get { return true; }
		}

		public override void Dispose()
		{
			foreach (var frame in FrameList)
				frame.Dispose();
			FrameList.Clear();
			totaltime = 0;
			lastFrameTime = 0;
			StartTime = -1;
			lastFrame = -1;
		}


		public override SKColor SpriteGetColor(int x, int y)
		{
			throw new NotSupportedException();
			//Bitmap bmp = this.Bitmap;
			//if (bmp == null)
			//	return Color.Transparent;
			//int bmpX = x + SrcRectangle.X;
			//int bmpY = y + SrcRectangle.Y;
			//if (bmpX < 0 || bmpX >= bmp.Width || bmpY < 0 || bmpY >= bmp.Height)
			//	return Color.Transparent;

			//return bmp.GetPixel(bmpX, bmpY);
		}


		public override void GraphicsDraw(SKCanvas g, Point offset)
		{
			AnimeFrame frame = GetCurrentFrame();
			if (frame == null || frame.BaseImage == null || !frame.BaseImage.IsCreated)
				return;
			offset.Offset(DestBasePosition);
			offset.Offset(frame.Offset);
			Rectangle destRect = new Rectangle(offset, frame.SrcRectangle.Size);
			//g.DrawImage(frame.BaseImage.Bitmap, destRect, frame.SrcRectangle, GraphicsUnit.Pixel);

			var bmp = frame.BaseImage.Bitmap;
			if (bmp == null && frame.BaseImage is ConstImage ci)
				bmp = SharedBitmapCache.Get(ci.FilePath);
			if (bmp == null) return;
			DrawBitmapUtils.DrawBitmap(g, bmp, frame.SrcRectangle, destRect);
			return;
		}

		public override void GraphicsDraw(SKCanvas g, Rectangle destRect)
		{
			AnimeFrame frame = GetCurrentFrame();
			if (frame == null || frame.BaseImage == null || !frame.BaseImage.IsCreated)
				return;
			destRect.X = destRect.X + (DestBasePosition.X + frame.Offset.X) * destRect.Width / DestBaseSize.Width;
			destRect.Y = destRect.Y + (DestBasePosition.Y + frame.Offset.Y) * destRect.Height / DestBaseSize.Height;
			destRect.Width = frame.SrcRectangle.Width * destRect.Width / DestBaseSize.Width;
			destRect.Height = frame.SrcRectangle.Height * destRect.Height / DestBaseSize.Height;
			//g.DrawImage(frame.BaseImage.Bitmap, destRect, frame.SrcRectangle, GraphicsUnit.Pixel);

			var bmp = frame.BaseImage.Bitmap;
			if (bmp == null && frame.BaseImage is ConstImage ci)
				bmp = SharedBitmapCache.Get(ci.FilePath);
			if (bmp == null) return;
			DrawBitmapUtils.DrawBitmap(g, bmp, frame.SrcRectangle, destRect);
		}

		public override void GraphicsDraw(SKCanvas g, Rectangle destRect, SKColorFilter attr)
		{
			AnimeFrame frame = GetCurrentFrame();
			if (frame == null || frame.BaseImage == null || !frame.BaseImage.IsCreated)
				return;
			destRect.X = destRect.X + (DestBasePosition.X + frame.Offset.X) * destRect.Width / DestBaseSize.Width;
			destRect.Y = destRect.Y + (DestBasePosition.Y + frame.Offset.Y) * destRect.Height / DestBaseSize.Height;
			destRect.Width = frame.SrcRectangle.Width * destRect.Width / DestBaseSize.Width;
			destRect.Height = frame.SrcRectangle.Height * destRect.Height / DestBaseSize.Height;
			////g.DrawImage(frame.BaseImage.Bitmap, destRect, SrcRectangle, GraphicsUnit.Pixel, attr);←このパターンがない
			//g.DrawImage(frame.BaseImage.Bitmap, destRect, frame.SrcRectangle.X, frame.SrcRectangle.Y, frame.SrcRectangle.Width, frame.SrcRectangle.Height, GraphicsUnit.Pixel, attr);

			var bmp = frame.BaseImage.Bitmap;
			if (bmp == null && frame.BaseImage is ConstImage ci)
				bmp = SharedBitmapCache.Get(ci.FilePath);
			if (bmp == null) return;
			DrawBitmapUtils.DrawBitmap(g, bmp, frame.SrcRectangle, destRect, attr);
		}

	}

	/// <summary>
	/// 原生动画 Sprite，基于 IFileBacked 接口支持 Evict/Reload
	/// 使用 AnimatedImageHelper 解码多帧图片，AnimSpriteCache 管理 LRU 淘汰
	/// </summary>
	internal sealed class SpriteAnimated : ASprite, IFileBacked
	{
		private readonly string filepath;
		private readonly int frameCount;
		private readonly int totalDuration;
		private readonly int[] frameTimestamps;

		private SKBitmap[] frames;
		private bool isEvicted;
		private long startTime;
		private readonly Rectangle srcRect;

		public SpriteAnimated(string name, string filepath, Rectangle srcRect, Size destSize, int width, int height, int count, int[] delays)
			: base(name, destSize)
		{
			this.filepath = filepath;
			this.srcRect = srcRect.IsEmpty ? new Rectangle(0, 0, width, height) : srcRect;
			this.frameCount = count;

			this.frameTimestamps = new int[count];
			int cumulative = 0;
			for (int i = 0; i < count; i++)
			{
				cumulative += delays[i];
				this.frameTimestamps[i] = cumulative;
			}
			this.totalDuration = cumulative;

			this.frames = Array.Empty<SKBitmap>();
			this.isEvicted = true;
			this.startTime = Stopwatch.GetTimestamp();

			if (frameCount > 1)
			{
				AnimSpriteCache.RegisterSprite(this);
				AnimSpriteCache.Add(filepath, this);
			}
		}

		private void EnsureLoaded()
		{
			if (frameCount <= 0) return;
			if (!isEvicted && frames.Length > 0) return;

			var decoded = AnimatedImageHelper.Decode(filepath);
			if (decoded != null)
			{
				frames = new SKBitmap[decoded.Count];
				for (int i = 0; i < decoded.Count; i++)
					frames[i] = decoded[i].Bitmap;
			}
			isEvicted = false;
		}

		public string FilePath => filepath;
		public bool IsEvicted => isEvicted;
		public int FrameCount => frameCount;
		public override bool IsCreated => frameCount > 0;

		public SKBitmap GetFrame(int index)
		{
			EnsureLoaded();
			if (frameCount > 1) AnimSpriteCache.Touch(filepath);
			if (index < 0 || index >= frameCount || frames == null || frames.Length == 0) return null;
			return frames[index];
		}

		public int GetCurrentFrameIndex()
		{
			if (totalDuration <= 0 || frameCount <= 0) return 0;
			if (startTime == 0) startTime = Stopwatch.GetTimestamp();

			long elapsed = (long)Stopwatch.GetElapsedTime(startTime).TotalMilliseconds % totalDuration;
			for (int i = 0; i < frameTimestamps.Length; i++)
			{
				if (elapsed < frameTimestamps[i]) return i;
			}
			return frameCount - 1;
		}

		public void Evict()
		{
			if (frames == null) return;
			foreach (var frame in frames) frame?.Dispose();
			frames = Array.Empty<SKBitmap>();
			isEvicted = true;
		}

		public void Reload()
		{
			if (!isEvicted) return;
			startTime = Stopwatch.GetTimestamp();
			EnsureLoaded();
			if (frameCount > 1) AnimSpriteCache.Touch(filepath);
		}

		public override void Dispose()
		{
			Evict();
			if (frameCount > 1) AnimSpriteCache.Evict(filepath);
		}

		public override void GraphicsDraw(SKCanvas g, Rectangle destRect)
		{
			if (isEvicted || frames.Length == 0) EnsureLoaded();
			var frame = GetFrame(GetCurrentFrameIndex());
			if (frame != null)
			{
				DrawBitmapUtils.DrawBitmap(g, frame, srcRect, destRect);
			}
		}

		public override void GraphicsDraw(SKCanvas g, Point offset) { throw new NotSupportedException(); }
		public override void GraphicsDraw(SKCanvas g, Rectangle destRect, SKColorFilter attr)
		{
			if (isEvicted || frames.Length == 0) EnsureLoaded();
			var frame = GetFrame(GetCurrentFrameIndex());
			if (frame != null)
			{
				DrawBitmapUtils.DrawBitmap(g, frame, srcRect, destRect, attr);
			}
		}
		public override SKColor SpriteGetColor(int x, int y) { throw new NotSupportedException(); }
	}

	/// <summary>
	/// 动画 Sprite 的 LRU 缓存管理器
	/// 最多同时保持 MaxAnimations 个动画的帧数据在内存中
	/// 超出时自动 Evict 最久未使用的动画
	/// </summary>
	internal sealed class AnimSpriteCache
	{
		private static readonly LinkedList<string> lruOrder = new();
		private static readonly Dictionary<string, LinkedListNode<string>> lruNodes = new();
		private static readonly Dictionary<string, WeakReference<IFileBacked>> spriteRefs = new();
		private static readonly object lockObj = new();
		private const int MaxAnimations = 6;

		public static void Add(string filepath, IFileBacked sprite)
		{
			lock (lockObj)
			{
				if (lruNodes.TryGetValue(filepath, out var node))
				{
					lruOrder.Remove(node);
					lruNodes[filepath] = lruOrder.AddLast(filepath);
					spriteRefs[filepath] = new WeakReference<IFileBacked>(sprite);
					return;
				}
				while (lruOrder.Count >= MaxAnimations)
				{
					var oldest = lruOrder.First;
					if (oldest != null)
					{
						lruOrder.RemoveFirst();
						var oldFilepath = oldest.Value;
						lruNodes.Remove(oldFilepath);
						if (spriteRefs.TryGetValue(oldFilepath, out var weakRef) && weakRef.TryGetTarget(out var oldSprite))
							oldSprite.Evict();
						spriteRefs.Remove(oldFilepath);
					}
				}
				lruNodes[filepath] = lruOrder.AddLast(filepath);
				spriteRefs[filepath] = new WeakReference<IFileBacked>(sprite);
			}
		}

		public static void RegisterSprite(IFileBacked sprite)
		{
			if (sprite == null) return;
			lock (lockObj)
			{
				var filepath = sprite.FilePath;
				if (lruNodes.ContainsKey(filepath))
					spriteRefs[filepath] = new WeakReference<IFileBacked>(sprite);
			}
		}

		public static void Touch(string filepath)
		{
			lock (lockObj)
			{
				if (!lruNodes.TryGetValue(filepath, out var node)) return;
				lruOrder.Remove(node);
				lruNodes[filepath] = lruOrder.AddLast(filepath);
			}
		}

		public static void Evict(string filepath)
		{
			lock (lockObj)
			{
				if (!lruNodes.TryGetValue(filepath, out var node)) return;
				lruOrder.Remove(node);
				lruNodes.Remove(filepath);
				if (spriteRefs.TryGetValue(filepath, out var weakRef) && weakRef.TryGetTarget(out var sprite))
					sprite.Evict();
				spriteRefs.Remove(filepath);
			}
		}
	}
}
