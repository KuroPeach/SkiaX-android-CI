using MinorShift.Emuera.Runtime.Config;
using MinorShift.Emuera.Runtime.Utils;
using MinorShift.Emuera.Runtime.Utils.EvilMask;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using trerror = MinorShift.Emuera.Runtime.Utils.EvilMask.Lang.Error;

namespace MinorShift.Emuera.UI.Game.Image;

static class AppContents
{
	static ConcurrentDictionary<int, GraphicsImage> gList = [];

	/// <summary>
	/// SpriteMeta 全表：名字（已 ToUpper(Invariant)）→ 按 FrameIndex 顺序排列的行。
	/// <para>比较器用 Ordinal：旧实现是内存 SQLite 的 BINARY 比较，插入/查询两侧都已 ToUpper，
	/// 实际语义就是「大写后精确匹配」；⛔ 不用 Config.StrComper（可被配置改成 IgnoreCase 而与旧行为分叉）。</para>
	/// <para>顺序：同一名字的行由 CSV 行序决定，且实测「跨文件同名 0 / (name,idx) 重号 0 /
	/// ANIME 头必在数据行之前」，故 List.Add 顺序与旧 <c>ORDER BY FrameIndex</c> 逐行等价。</para>
	/// </summary>
	private static readonly Dictionary<string, List<MetaRow>> metaRows = new(StringComparer.Ordinal);

	public static HashSet<ConstImage> tempLoadedConstImages = [];
	public static HashSet<GraphicsImage> tempLoadedGraphicsImages = [];

	static public GraphicsImage GetGraphics(int i)
	{
		if (gList.TryGetValue(i, out GraphicsImage value))
			return value;
		GraphicsImage g = new(i);
		gList[i] = g;
		return g;
	}

	static public ASprite GetSprite(string name)
	{
		if (string.IsNullOrEmpty(name))
			return null;
		name = name.ToUpper(CultureInfo.InvariantCulture);

		if (activeSprites.TryGetValue(name, out ASprite sprite))
		{
			if (sprite is SpriteAnimated sa)
				AnimSpriteCache.Touch(sa.FilePath);
			return sprite;
		}

		return LoadSpriteFromMeta(name);
	}

	private class MetaRow
	{
		public string FilePath;
		public Rectangle Rect;
		public Point Pos;
		public int Delay;
		public Size DestSize;
		public bool IsAnimeHeader;
	}

	private static ASprite LoadSpriteFromMeta(string name)
	{
		if (!metaRows.TryGetValue(name, out var rows) || rows.Count == 0)
			return null;

		ASprite newSprite = null;

		if (rows[0].IsAnimeHeader)
		{
			Size destSize = rows[0].DestSize;
			SpriteAnime anime = new SpriteAnime(name, destSize);

			Dictionary<string, ConstImage> sharedConstImages = new();

			for (int i = 1; i < rows.Count; i++)
			{
				var r = rows[i];
				if (!ResourceStore.Exists(r.FilePath)) continue;

				if (!sharedConstImages.TryGetValue(r.FilePath, out ConstImage img))
				{
					img = new ConstImage($"{name}_F{i}");
					img.CreateFrom(r.FilePath, false);
					sharedConstImages[r.FilePath] = img;
				}

				Rectangle fRect = r.Rect.Width == 0 ? new Rectangle(0, 0, img.Width, img.Height) : r.Rect;
				anime.AddFrame(img, fRect, r.Pos, r.Delay);
			}
			newSprite = anime;
		}
		else
		{
			var r = rows[0];
			if (!ResourceStore.Exists(r.FilePath)) return null;

			if (AnimatedImageHelper.GetAnimInfo(r.FilePath, out int w, out int h, out int fCount, out int[] delays))
			{
				Size destSize = (r.DestSize.Width > 0 && r.DestSize.Height > 0) ? r.DestSize : r.Rect.Size;
				if (destSize.IsEmpty) destSize = new Size(w, h);

				newSprite = new SpriteAnimated(name, r.FilePath, r.Rect, destSize, w, h, fCount, delays);
			}
			else
			{
				ConstImage img = new ConstImage(name + "_BASE");
				img.CreateFrom(r.FilePath, false);

				Rectangle fRect = r.Rect.Width == 0 ? new Rectangle(0, 0, img.Width, img.Height) : r.Rect;
				Size destSize = (r.DestSize.Width > 0 && r.DestSize.Height > 0) ? r.DestSize : fRect.Size;
				newSprite = new SpriteF(name, img, fRect, r.Pos, destSize);
			}
		}

		if (newSprite != null)
		{
			activeSprites[name] = newSprite;
		}

		return newSprite;
	}

	static public void CreateSpriteG(string imgName, GraphicsImage parent, Rectangle rect, Point pos, Size destSize)
	{
		if (string.IsNullOrEmpty(imgName))
			return;
		imgName = imgName.ToUpper(CultureInfo.InvariantCulture);

		SpriteG newSprite = new SpriteG(imgName, parent, rect, pos, destSize);
		// 同名重建（eratw 的 GDISPOSE→SPRITECREATE 链）会把旧 sprite 顶掉，
		// 旧快照必须一并作废，否则 FE 永远贴旧图。首次创建没有旧快照，Invalidate 自然返回 0。
		bool replaced = activeSprites.ContainsKey(imgName);
		activeSprites[imgName] = newSprite;
		if (replaced)
			NotifySpriteDisposed(imgName);
	}

	static public void CreateSpriteAnime(string imgName, int w, int h)
	{
		if (string.IsNullOrEmpty(imgName))
			return;
		imgName = imgName.ToUpper(CultureInfo.InvariantCulture);
		SpriteAnime anime = new SpriteAnime(imgName, new Size(w, h));
		bool replaced = activeSprites.ContainsKey(imgName);
		activeSprites[imgName] = anime;
		if (replaced)
			NotifySpriteDisposed(imgName);
	}

	public static bool CreateSpriteFromFileDynamic(string name, string filepath)
	{
		if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(filepath))
			return false;
		name = name.ToUpper(CultureInfo.InvariantCulture);

		if (activeSprites.TryGetValue(name, out _))
			return true;

		if (!ResourceStore.Exists(filepath))
			return false;

		ASprite newSprite = null;
		if (AnimatedImageHelper.GetAnimInfo(filepath, out int w, out int h, out int fCount, out int[] delays))
		{
			newSprite = new SpriteAnimated(name, filepath, Rectangle.Empty, new Size(w, h), w, h, fCount, delays);
		}
		else
		{
			ConstImage img = new ConstImage(name + "_DYN");
			img.CreateFrom(filepath, false);
			newSprite = new SpriteF(name, img, new Rectangle(0, 0, img.Width, img.Height), Point.Empty, new Size(img.Width, img.Height));
		}

		if (newSprite != null)
		{
			activeSprites[name] = newSprite;
			return true;
		}
		return false;
	}

	static public bool GetSprite_OnlyCheckExists(string name)
	{
		if (string.IsNullOrEmpty(name))
			return false;
		name = name.ToUpper(CultureInfo.InvariantCulture);
		if (activeSprites.ContainsKey(name))
			return true;
		return metaRows.ContainsKey(name);
	}

	static public void SpriteDispose(string name)
	{
		if (string.IsNullOrEmpty(name))
			return;
		name = name.ToUpper(CultureInfo.InvariantCulture);
		if (activeSprites.TryRemove(name, out ASprite sprite))
		{
			sprite.Dispose();
		}
		// 不管物理上是否真的删掉了，ERB 声明「该名作废」就必须让 EWP 快照失效
		//（同名重建＝DISPOSE→CREATE，此处漏发会让 FE 永远贴旧图）。
		NotifySpriteDisposed(name);
	}

	static public long SpriteDisposeAll(bool delCsvImage)
	{
		int sprites = activeSprites.Count;
		foreach (var s in activeSprites.Values)
			s.Dispose();
		activeSprites.Clear();

		SharedBitmapCache.Clear();

		// 全量作废（含 delCsvImage=false 的「只清运行期」语义）。
		NotifySpriteDisposedAll();

		return sprites;
	}

	/// <summary>
	/// 精灵被 SPRITEDISPOSE 的通知（脚本线程/UI 线程都可能触发）。
	/// 订阅方必须自行合并、只置脏/入队，⛔ 在回调里绘制或阻塞（可能被非 UI 路径调到）。
	/// </summary>
	public static event Action<string> SpriteDisposed;
	/// <summary>SPRITEDISPOSEALL 的通知（语义同上）。</summary>
	public static event Action SpriteDisposedAll;

	private static void NotifySpriteDisposed(string name)
	{
		try { SpriteDisposed?.Invoke(name); } catch { }
	}

	private static void NotifySpriteDisposedAll()
	{
		try { SpriteDisposedAll?.Invoke(); } catch { }
	}

	static public Exception LoadContents(bool reload)
	{
		// 包存在但目录不存在时，CSV 全部来自包；两者都无才是真空（P1 工单 §4.4）
		if (!Directory.Exists(Program.ContentDir) && !ResourceStore.Enabled)
			return null;
		try
		{
			if (reload)
			{
				metaRows.Clear();

				SpriteDisposeAll(true);
			}

			// 磁盘 ∪ 包内，磁盘优先（去重键＝整条绝对路径的 OrdinalIgnoreCase）
			var diskCsv = Directory.Exists(Program.ContentDir)
				? Directory.EnumerateFiles(Program.ContentDir, "*.csv", SearchOption.AllDirectories)
				: Enumerable.Empty<string>();
			var csvFiles = diskCsv
				.Concat(ResourceStore.EnumerateRelative(".csv").Select(VirtualPath))
				.Distinct(StringComparer.OrdinalIgnoreCase);

			foreach (var path in csvFiles)
			{
				// W3/Linux：目录前缀用平台分隔符（"\\" 在 Linux 下是文件名的一部分）。
				string directory = Path.GetDirectoryName(path) + Path.DirectorySeparatorChar;
				string[] lines = ReadCsvLines(path);

				foreach (var line in lines)
				{
					string str = line.Trim();
					if (str.Length == 0 || str.StartsWith(';'))
						continue;
					string[] tokens = str.Split(',');
					if (tokens.Length < 2)
						continue;

					string name = tokens[0].Trim().ToUpper(CultureInfo.InvariantCulture);
					string arg2 = tokens[1].Trim();

					if (arg2.Equals("ANIME", StringComparison.OrdinalIgnoreCase))
					{
						if (tokens.Length >= 4)
						{
							int.TryParse(tokens[2], out int width);
							int.TryParse(tokens[3], out int height);
							if (width > 0 && height > 0)
							{
								AddMetaRow(name, new MetaRow
								{
									FilePath = "",
									Rect = Rectangle.Empty,
									Pos = Point.Empty,
									Delay = 0,
									DestSize = new Size(width, height),
									IsAnimeHeader = true
								});
							}
						}
						continue;
					}

					var row = new MetaRow
					{
						FilePath = directory + arg2,
						Rect = Rectangle.Empty,
						Pos = Point.Empty,
						Delay = 0,
						DestSize = Size.Empty,
						IsAnimeHeader = false
					};

					if (tokens.Length >= 6)
					{
						int.TryParse(tokens[2], out int rx);
						int.TryParse(tokens[3], out int ry);
						int.TryParse(tokens[4], out int rw);
						int.TryParse(tokens[5], out int rh);
						row.Rect = new Rectangle(rx, ry, rw, rh);
					}
					if (tokens.Length >= 8)
					{
						int.TryParse(tokens[6], out int px);
						int.TryParse(tokens[7], out int py);
						row.Pos = new Point(px, py);
					}
					if (tokens.Length >= 9)
					{
						int.TryParse(tokens[8], out int delay);
						row.Delay = delay;
					}
					if (tokens.Length >= 11)
					{
						int.TryParse(tokens[9], out int destW);
						int.TryParse(tokens[10], out int destH);
						row.DestSize = new Size(destW, destH);
					}

					AddMetaRow(name, row);
				}
			}
		}
		catch (Exception e)
		{
			return e;
		}
		return null;
	}

	/// <summary>把一行追加到该名字的行表（首次出现即建表）。行序＝CSV 行序＝旧的 FrameIndex 序。</summary>
	static void AddMetaRow(string name, MetaRow row)
	{
		if (!metaRows.TryGetValue(name, out var list))
		{
			list = new List<MetaRow>(4);
			metaRows[name] = list;
		}
		list.Add(row);
	}

	/// <summary>
	/// 包内 posix 相对路径 → ContentDir 下的绝对路径（磁盘上可以不存在）。
	/// 必须这么合成：:365 的 <c>directory + arg2</c> 与 ResourceStore 的前缀匹配都依赖「绝对路径形态」，
	/// 直接用相对路径会让 GetDirectoryName 得空串 ⇒ 该 CSV 的所有图片引用静默丢失。
	/// </summary>
	static string VirtualPath(string rel)
	{
		return Path.Combine(Program.ContentDir, rel.Replace('/', Path.DirectorySeparatorChar));
	}

	/// <summary>
	/// 读一个 CSV：磁盘命中走原逻辑（含编码探测），否则从包内整条读、用同一套编码探测解析。
	/// ⛔ 不能直接 Encoding.UTF8.GetString——CP932 的 CSV 会变乱码。
	/// </summary>
	static string[] ReadCsvLines(string path)
	{
		if (File.Exists(path))
			return File.ReadAllLines(path, EncodingHandler.DetectEncoding(path));

		var bytes = ResourceStore.ReadAllBytes(path);
		if (bytes == null)
			return Array.Empty<string>();

		// DetectEncoding(Stream) 各分支都会恢复 Position，可安全复用；StreamReader 再按 BOM 走一遍
		using var stream = new MemoryStream(bytes, writable: false);
		using var reader = new StreamReader(stream, EncodingHandler.DetectEncoding(stream), detectEncodingFromByteOrderMarks: true);
		var lines = new List<string>();
		while (reader.ReadLine() is { } line)
			lines.Add(line);
		return lines.ToArray();
	}

	static public void UnloadContents()
	{
		SpriteDisposeAll(true);
		foreach (var graph in gList.Values)
			graph.GDispose();
		gList.Clear();
	}

	static public void UnloadGraphicList()
	{
		foreach (var graph in gList.Values)
			graph.GDispose();
		gList.Clear();

		List<string> keysToRemove = [];
		foreach (var kvp in activeSprites)
		{
			if (kvp.Value is SpriteG)
				keysToRemove.Add(kvp.Key);
			else if (kvp.Value is SpriteAnime anime && anime.HasGraphicsImageFrame())
				keysToRemove.Add(kvp.Key);
		}
		foreach (var key in keysToRemove)
		{
			if (activeSprites.TryRemove(key, out ASprite sprite))
				sprite.Dispose();
		}
	}

	static public void UnloadTempLoadedConstImageNames()
	{
		lock (tempLoadedConstImages)
		{
			foreach (ConstImage img in tempLoadedConstImages)
				img.Dispose();
			tempLoadedConstImages.Clear();
		}
	}

	static public void UnloadTempLoadedGraphicsImageNames()
	{
		lock (tempLoadedGraphicsImages)
		{
			foreach (GraphicsImage img in tempLoadedGraphicsImages)
				if (img.useImgList)
					img.UnLoad();
			tempLoadedGraphicsImages.Clear();
		}
	}

	private static ConcurrentDictionary<string, ASprite> activeSprites = new(Config.StrComper);

	public static (int activeSprites, int graphicsImages, int tempConstImages, int tempGraphicsImages, int metaRowCount) GetDiagnosticStats()
	{
		int tc, tg;
		lock (tempLoadedConstImages)
			tc = tempLoadedConstImages.Count;
		lock (tempLoadedGraphicsImages)
			tg = tempLoadedGraphicsImages.Count;
		// 口径＝行数（对齐旧的 SELECT COUNT(*)），⛔ 不是名字数（那会从 ~39.7k 变成 ~38.9k 让人误判丢数据）
		int metaRowCount = 0;
		foreach (var rows in metaRows.Values)
			metaRowCount += rows.Count;
		return (activeSprites.Count, gList.Count, tc, tg, metaRowCount);
	}
}