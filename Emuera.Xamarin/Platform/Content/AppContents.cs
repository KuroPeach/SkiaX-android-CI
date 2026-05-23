using MinorShift.Emuera.Content;
using MinorShift.Emuera.Runtime.Script;
using MinorShift.Emuera.Runtime.Script.Parser;
using MinorShift.Emuera.Runtime.Utils;
using MinorShift.Emuera.Runtime.Utils.EvilMask;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using XEmuera;
using trerror = MinorShift.Emuera.Runtime.Utils.EvilMask.Lang.Error;
using TextDrawingMode = MinorShift.Emuera.Runtime.Config.TextDrawingMode;
using Microsoft.Data.Sqlite;

namespace MinorShift.Emuera.Content
{
	static class AppContents
	{
		static AppContents()
		{
			gList = new Dictionary<int, GraphicsImage>();
			metaDb = new SqliteConnection("Data Source=:memory:");
			metaDb.Open();
			using var cmd = metaDb.CreateCommand();
			cmd.CommandText = @"
				CREATE TABLE SpriteMeta (
					Name TEXT,
					FrameIndex INTEGER,
					FilePath TEXT,
					RectX INTEGER, RectY INTEGER, RectW INTEGER, RectH INTEGER,
					PosX INTEGER, PosY INTEGER,
					Delay INTEGER,
					DestW INTEGER, DestH INTEGER,
					IsAnime INTEGER,
					PRIMARY KEY (Name, FrameIndex)
				)";
			cmd.ExecuteNonQuery();
		}

		static readonly Dictionary<string, AContentFile> resourceDic = new Dictionary<string, AContentFile>();
		static readonly ConcurrentDictionary<string, ASprite> imageDictionary = new ConcurrentDictionary<string, ASprite>(Config.StrComper);
		static readonly Dictionary<int, GraphicsImage> gList;
		static readonly SqliteConnection metaDb;

		public static HashSet<ConstImage> tempLoadedConstImages = [];
		public static HashSet<GraphicsImage> tempLoadedGraphicsImages = [];

		static public GraphicsImage GetGraphics(int i)
		{
			if (gList.TryGetValue(i, out var g))
				return g;
			g = new GraphicsImage(i);
			gList[i] = g;
			return g;
		}

		static public ASprite GetSprite(string name)
		{
			if (name == null)
				return null;
			name = name.ToUpper(CultureInfo.InvariantCulture);
			if (imageDictionary.TryGetValue(name, out ASprite sprite))
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

		static private ASprite LoadSpriteFromMeta(string name)
		{
			using var cmd = metaDb.CreateCommand();
			cmd.CommandText = "SELECT * FROM SpriteMeta WHERE Name = @name ORDER BY FrameIndex ASC";
			cmd.Parameters.AddWithValue("@name", name);

			using var reader = cmd.ExecuteReader();
			List<MetaRow> rows = new List<MetaRow>();
			while (reader.Read())
			{
				rows.Add(new MetaRow
				{
					FilePath = reader.GetString(2),
					Rect = new Rectangle(reader.GetInt32(3), reader.GetInt32(4), reader.GetInt32(5), reader.GetInt32(6)),
					Pos = new Point(reader.GetInt32(7), reader.GetInt32(8)),
					Delay = reader.GetInt32(9),
					DestSize = new Size(reader.GetInt32(10), reader.GetInt32(11)),
					IsAnimeHeader = reader.GetInt32(12) == 1
				});
			}
			reader.Close();

			if (rows.Count == 0)
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
					if (!File.Exists(r.FilePath)) continue;

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
				if (!File.Exists(r.FilePath)) return null;

				// 检查是否为原生动画文件（GIF/WebP 等）
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
				imageDictionary[name] = newSprite;
			}

			return newSprite;
		}

		static public void CreateSpriteG(string imgName, GraphicsImage parent, Rectangle rect)
		{
			if (string.IsNullOrEmpty(imgName))
				throw new ArgumentOutOfRangeException();
			imgName = imgName.ToUpper();
			SpriteG newCImg = new SpriteG(imgName, parent, rect);
			imageDictionary[imgName] = newCImg;
		}

		static public void CreateSpriteG(string imgName, GraphicsImage parent, Rectangle rect, Point pos, Size destSize)
		{
			if (string.IsNullOrEmpty(imgName))
				return;
			imgName = imgName.ToUpper();
			SpriteG newSprite = new SpriteG(imgName, parent, rect, pos, destSize);
			imageDictionary[imgName] = newSprite;
		}

		static public bool CreateSpriteFromFileDynamic(string name, string filepath)
		{
			if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(filepath))
				return false;
			name = name.ToUpper(CultureInfo.InvariantCulture);
			if (imageDictionary.ContainsKey(name))
				return true;
			if (!FileUtils.Exists(ref filepath))
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
				imageDictionary[name] = newSprite;
				return true;
			}
			return false;
		}

		static public bool GetSprite_OnlyCheckExists(string name)
		{
			if (string.IsNullOrEmpty(name))
				return false;
			name = name.ToUpper(CultureInfo.InvariantCulture);
			if (imageDictionary.ContainsKey(name))
				return true;
			using var cmd = metaDb.CreateCommand();
			cmd.CommandText = "SELECT Name FROM SpriteMeta WHERE Name = @name LIMIT 1";
			cmd.Parameters.AddWithValue("@name", name);
			using var reader = cmd.ExecuteReader();
			return reader.Read();
		}

		static public void SpriteDispose(string name)
		{
			if (name == null)
				return;
			name = name.ToUpper(CultureInfo.InvariantCulture);
			if (imageDictionary.TryRemove(name, out var aSprite))
				aSprite.Dispose();
		}

		static public long SpriteDisposeAll(bool delCsvImage)
		{
			int sprites = imageDictionary.Count;
			foreach (var s in imageDictionary.Values)
				s.Dispose();
			imageDictionary.Clear();

			SharedBitmapCache.Clear();

			return sprites;
		}

		internal static void CreateSpriteAnime(string imgName, int w, int h)
		{
			if (string.IsNullOrEmpty(imgName))
				throw new ArgumentOutOfRangeException();
			imgName = imgName.ToUpper();
			SpriteAnime newCImg = new SpriteAnime(imgName, new Size(w, h));
			imageDictionary[imgName] = newCImg;
		}

		static public Exception LoadContents(bool useAsync = false)
		{
			System.Diagnostics.Debug.WriteLine($"[Emuera] AppContents.LoadContents: ContentDir={Program.ContentDir}");
			if (!Directory.Exists(Program.ContentDir))
			{
				System.Diagnostics.Debug.WriteLine($"[Emuera] AppContents.LoadContents: ContentDir does not exist, skipping");
				return null;
			}
			try
			{
				string[] csvFiles = FileUtils.GetFiles(Program.ContentDir, "*.csv", SearchOption.AllDirectories);
				System.Diagnostics.Debug.WriteLine($"[Emuera] AppContents.LoadContents: found {csvFiles.Length} csv files, writing metadata to SQLite");

				using var trans = metaDb.BeginTransaction();
				using var insertCmd = metaDb.CreateCommand();
				insertCmd.Transaction = trans;
				insertCmd.CommandText = @"
					INSERT OR REPLACE INTO SpriteMeta (Name, FrameIndex, FilePath, RectX, RectY, RectW, RectH, PosX, PosY, Delay, DestW, DestH, IsAnime)
					VALUES (@Name, @FrameIndex, @FilePath, @RectX, @RectY, @RectW, @RectH, @PosX, @PosY, @Delay, @DestW, @DestH, @IsAnime)";

				var pName = insertCmd.Parameters.Add("@Name", SqliteType.Text);
				var pFrameIndex = insertCmd.Parameters.Add("@FrameIndex", SqliteType.Integer);
				var pFilePath = insertCmd.Parameters.Add("@FilePath", SqliteType.Text);
				var pRectX = insertCmd.Parameters.Add("@RectX", SqliteType.Integer);
				var pRectY = insertCmd.Parameters.Add("@RectY", SqliteType.Integer);
				var pRectW = insertCmd.Parameters.Add("@RectW", SqliteType.Integer);
				var pRectH = insertCmd.Parameters.Add("@RectH", SqliteType.Integer);
				var pPosX = insertCmd.Parameters.Add("@PosX", SqliteType.Integer);
				var pPosY = insertCmd.Parameters.Add("@PosY", SqliteType.Integer);
				var pDelay = insertCmd.Parameters.Add("@Delay", SqliteType.Integer);
				var pDestW = insertCmd.Parameters.Add("@DestW", SqliteType.Integer);
				var pDestH = insertCmd.Parameters.Add("@DestH", SqliteType.Integer);
				var pIsAnime = insertCmd.Parameters.Add("@IsAnime", SqliteType.Integer);

				int spriteCount = 0;

				foreach (var filepath in csvFiles)
				{
					SpriteAnime currentAnime = null;
					string directory = Path.GetDirectoryName(filepath) + Path.DirectorySeparatorChar;
					string filename = Path.GetFileName(filepath);
					string[] lines = File.ReadAllLines(filepath, EncodingHandler.DetectEncoding(filepath));
					int lineNo = 0;
					int frameIndex = 0;

					foreach (var line in lines)
					{
						lineNo++;
						if (line.Length == 0)
							continue;
						string str = line.Trim();
						if (str.Length == 0 || str.StartsWith(";"))
							continue;
						string[] tokens = str.Split(',');
						ScriptPosition sp = new ScriptPosition(filename, lineNo);

						string name = tokens[0].Trim().ToUpper();
						string arg2 = tokens.Length > 1 ? tokens[1].Replace('\\', Path.DirectorySeparatorChar) : "";
						if (name.Length == 0 || arg2.Length == 0)
							continue;

						if (arg2.Equals("ANIME", StringComparison.OrdinalIgnoreCase))
						{
							if (tokens.Length < 4)
								continue;
							int[] sizeValue = new int[2];
							bool sccs = true;
							for (int i = 0; i < 2; i++)
								sccs &= int.TryParse(tokens[i + 2], out sizeValue[i]);
							if (!sccs || sizeValue[0] <= 0 || sizeValue[1] <= 0)
								continue;

							pName.Value = name;
							pFrameIndex.Value = 0;
							pFilePath.Value = "";
							pRectX.Value = 0;
							pRectY.Value = 0;
							pRectW.Value = sizeValue[0];
							pRectH.Value = sizeValue[1];
							pPosX.Value = 0;
							pPosY.Value = 0;
							pDelay.Value = 0;
							pDestW.Value = sizeValue[0];
							pDestH.Value = sizeValue[1];
							pIsAnime.Value = 1;
							insertCmd.ExecuteNonQuery();
							spriteCount++;
							currentAnime = new SpriteAnime(name, new Size(sizeValue[0], sizeValue[1]));
							frameIndex = 1;
							continue;
						}

						if (arg2.IndexOf('.') < 0)
							continue;

						string parentName = directory + arg2;
						if (!FileUtils.Exists(ref parentName))
						{
							ParserMediator.Warn(string.Format(trerror.NotExistImageFile.Text, arg2), sp, 1);
							continue;
						}

						Rectangle rect = new Rectangle();
						Point pos = new Point();
						int delay = 1000;
						int destW = 0;
						int destH = 0;
						bool isAnimeFrame = currentAnime != null && currentAnime.Name == name;

						if (tokens.Length >= 6)
						{
							int[] rectValue = new int[4];
							bool sccs = true;
							for (int i = 0; i < 4; i++)
								sccs &= int.TryParse(tokens[i + 2], out rectValue[i]);
							if (sccs)
								rect = new Rectangle(rectValue[0], rectValue[1], rectValue[2], rectValue[3]);
							if (tokens.Length >= 8)
							{
								sccs = true;
								for (int i = 0; i < 2; i++)
									sccs &= int.TryParse(tokens[i + 6], out rectValue[i]);
								if (sccs)
									pos = new Point(rectValue[0], rectValue[1]);
								if (tokens.Length >= 9)
									int.TryParse(tokens[8], out delay);
							}
							if (tokens.Length >= 11)
							{
								int.TryParse(tokens[9], out destW);
								int.TryParse(tokens[10], out destH);
							}
						}

						pName.Value = name;
						pFrameIndex.Value = isAnimeFrame ? frameIndex++ : 1;
						pFilePath.Value = parentName;
						pRectX.Value = rect.X;
						pRectY.Value = rect.Y;
						pRectW.Value = rect.Width;
						pRectH.Value = rect.Height;
						pPosX.Value = pos.X;
						pPosY.Value = pos.Y;
						pDelay.Value = delay;
						pDestW.Value = destW;
						pDestH.Value = destH;
						pIsAnime.Value = 0;
						insertCmd.ExecuteNonQuery();
						spriteCount++;

						if (!isAnimeFrame)
							currentAnime = null;
					}
				}

				trans.Commit();
				System.Diagnostics.Debug.WriteLine($"[Emuera] AppContents.LoadContents: {spriteCount} sprite metadata entries written to SQLite (no images decoded)");
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[Emuera] AppContents.LoadContents EXCEPTION: {ex.Message}");
				return ex;
			}
			return null;
		}

		static public void UnloadContents()
		{
			foreach (var img in resourceDic.Values)
				img.Dispose();
			resourceDic.Clear();
			foreach (var s in imageDictionary.Values)
				s.Dispose();
			imageDictionary.Clear();
			foreach (var graph in gList.Values)
				graph.GDispose();
			gList.Clear();

			SharedBitmapCache.Clear();
		}

		static public void UnloadGraphicList()
		{
			foreach (var graph in gList.Values)
				graph.GDispose();
			gList.Clear();

			List<string> keysToRemove = [];
			foreach (var kvp in imageDictionary)
			{
				if (kvp.Value is SpriteG)
					keysToRemove.Add(kvp.Key);
			}
			foreach (var key in keysToRemove)
			{
				if (imageDictionary.TryRemove(key, out ASprite sprite))
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
					img.GDispose();
				tempLoadedGraphicsImages.Clear();
			}
		}
	}
}
