using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using SkiaSharp;

namespace MinorShift.Emuera.UI.Game.Image;

/// <summary>
/// 全局共享静态位图缓存池
/// 解决 Sprite Sheet (大图拼接) 模式下，同一文件被多次解码导致的内存爆炸问题
/// </summary>
public static class SharedBitmapCache
{
	private static readonly Dictionary<string, Entry> cache = new();
	// 存节点而不是字符串，LRU 命中/移动为 O(1)
	private static readonly Dictionary<string, LinkedListNode<string>> nodes = new();
	private static readonly LinkedList<string> lru = new();
	// 按解码后实际像素字节数硬限流（eraTW 大图单张可达十几 MB，按张数限制会失控）
	private const long MaxCacheBytes = 256L * 1024 * 1024;
	private static long cacheBytes;

	private readonly struct Entry
	{
		public readonly SKBitmap Bitmap;
		public readonly long Bytes;
		public Entry(SKBitmap bitmap, long bytes)
		{
			Bitmap = bitmap;
			Bytes = bytes;
		}
	}
	private static readonly object cacheLock = new object();

	public static SKBitmap Get(string filepath)
	{
		if (string.IsNullOrEmpty(filepath)) return null;
		lock (cacheLock)
		{
			if (cache.TryGetValue(filepath, out var entry))
			{
				touch(filepath);
				return entry.Bitmap;
			}
		}

		// 解码放在锁外：大图 decode 可达数十 ms，持着全局 cacheLock 会卡住整条绘制链
		// （预算从「200 张」改成字节后淘汰更频繁，命中率下降 ⇒ 这条路径比原来更常走）。
		// 并发下同一路径可能重复解码，由 add 内的 existing 检查去重。
		SKBitmap decoded;
		try
		{
			decoded = SKBitmap.Decode(filepath);
			if (decoded == null) return null;
		}
		catch
		{
			return null;
		}

		lock (cacheLock)
		{
			add(filepath, decoded);
			// 并发下可能已被别的线程抢先插入（add 已把本张 supersede 掉并 Dispose）⇒ 必须回缓存里的那张
			return cache.TryGetValue(filepath, out var stored) ? stored.Bitmap : decoded;
		}
	}

	private static long byteSize(SKBitmap bmp) => (long)bmp.RowBytes * bmp.Height;

	private static void touch(string filepath)
	{
		if (!nodes.TryGetValue(filepath, out var node))
		{
			// cache/nodes 失步时自愈，避免把 KeyNotFoundException 抛到调用方
			nodes[filepath] = lru.AddLast(filepath);
			return;
		}
		lru.Remove(node);
		lru.AddLast(node);
	}

	private static void add(string filepath, SKBitmap bmp)
	{
		if (cache.TryGetValue(filepath, out var existing))
		{
			if (ReferenceEquals(existing.Bitmap, bmp))
			{
				touch(filepath);
				return;
			}
			cacheBytes -= existing.Bytes;
			existing.Bitmap.Dispose();
			cache.Remove(filepath);
			if (nodes.TryGetValue(filepath, out var existNode))
			{
				lru.Remove(existNode);
				nodes.Remove(filepath);
			}
		}

		var bytes = byteSize(bmp);
		if (bytes > MaxCacheBytes)
		{
			// 单张解码后即超预算：无论怎么淘汰都装不下。清空整个 LRU 后让它独占，
			// 否则每次取用都要重解码这张图（更慢，且峰值内存一样）。正常素材不会走到这里
			// （eraTW 实测约 14 MB/张）。
			foreach (var entry in cache.Values)
				entry.Bitmap.Dispose();
			cache.Clear();
			nodes.Clear();
			lru.Clear();
			cacheBytes = 0;
		}
		else
		{
			// 硬预算：超限即淘汰最久未用的条目。不保留条目下限，否则大图场景会突破数百 MB。
			// 当前帧已绘制的图片在 LRU 尾部，淘汰不会波及它们。
			while (lru.Count > 0 && cacheBytes + bytes > MaxCacheBytes)
			{
				var oldest = lru.First.Value;
				lru.RemoveFirst();
				nodes.Remove(oldest);
				if (cache.Remove(oldest, out var old))
				{
					cacheBytes -= old.Bytes;
					old.Bitmap.Dispose();
				}
			}
		}

		cache[filepath] = new Entry(bmp, bytes);
		nodes[filepath] = lru.AddLast(filepath);
		cacheBytes += bytes;
	}

	public static bool GetInfo(string filepath, out int width, out int height)
	{
		width = height = 0;
		if (string.IsNullOrEmpty(filepath)) return false;

		lock (cacheLock)
		{
			if (cache.TryGetValue(filepath, out var entry))
			{
				width = entry.Bitmap.Width;
				height = entry.Bitmap.Height;
				return true;
			}
		}

		try
		{
			using var codec = SKCodec.Create(filepath);
			if (codec != null)
			{
				width = codec.Info.Width;
				height = codec.Info.Height;
				return true;
			}
		}
		catch { }
		return false;
	}

	public static void Set(string filepath, SKBitmap bmp)
	{
		if (string.IsNullOrEmpty(filepath) || bmp == null) return;
		lock (cacheLock)
		{
			add(filepath, bmp);
		}
	}

	public static int Count
	{
		get { lock (cacheLock) { return cache.Count; } }
	}

	public static long Bytes
	{
		get { lock (cacheLock) { return cacheBytes; } }
	}

	public static void Clear()
	{
		lock (cacheLock)
		{
			foreach (var entry in cache.Values) entry.Bitmap.Dispose();
			cache.Clear();
			nodes.Clear();
			lru.Clear();
			cacheBytes = 0;
		}
	}
}

internal sealed class ConstImage : AbstractImage
{
	public ConstImage(string name)
	{
		Name = name;
	}

	public readonly string Name;
	private string filepath;

	public int Width;
	public int Height;

	public string FilePath => filepath;

	internal void CreateFrom(string filepath, bool useGDI)
	{
		if (!string.IsNullOrEmpty(this.filepath)) throw new Exception();
		this.filepath = filepath;

		if (SharedBitmapCache.GetInfo(filepath, out int w, out int h))
		{
			Width = w;
			Height = h;
		}
	}

	internal void CreateFrom(SKBitmap bmp, string filepath, bool useGDI)
	{
		if (!string.IsNullOrEmpty(this.filepath)) throw new Exception();
		this.filepath = filepath;
		this.Width = bmp.Width;
		this.Height = bmp.Height;

		SharedBitmapCache.Set(filepath, bmp);
	}

	public override void Dispose()
	{
		if (canvas != null)
		{
			canvas.Dispose();
			canvas = null;
		}
	}

	public override bool IsCreated => !string.IsNullOrEmpty(filepath);

	public override SKBitmap SKBitmap
	{
		get
		{
			return SharedBitmapCache.Get(filepath);
		}
		set { }
	}
}