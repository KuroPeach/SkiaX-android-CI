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
					IsAnime INTEGER,
					PRIMARY KEY (Name, FrameIndex)
				)";
			cmd.ExecuteNonQuery();
		}

		static readonly Dictionary<string, AContentFile> resourceDic = new Dictionary<string, AContentFile>();
		static readonly ConcurrentDictionary<string, ASprite> imageDictionary = new ConcurrentDictionary<string, ASprite>(Config.StrComper);
		static readonly Dictionary<int, GraphicsImage> gList;
		static readonly SqliteConnection metaDb;

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
				return sprite;
			return LoadSpriteFromMeta(name);
		}

		static private ASprite LoadSpriteFromMeta(string name)
		{
			using var cmd = metaDb.CreateCommand();
			cmd.CommandText = "SELECT * FROM SpriteMeta WHERE Name = @name ORDER BY FrameIndex ASC";
			cmd.Parameters.AddWithValue("@name", name);
			using var reader = cmd.ExecuteReader();

			var rows = new List<(string FilePath, Rectangle Rect, Point Pos, int Delay, bool IsAnimeHeader)>();
			bool isAnimeHeader = false;
			while (reader.Read())
			{
				int isAnimeVal = reader.GetInt32(reader.GetOrdinal("IsAnime"));
				if (reader.GetInt32(reader.GetOrdinal("FrameIndex")) == 0 && isAnimeVal == 1)
				{
					isAnimeHeader = true;
					continue;
				}
				string fp = reader.GetString(reader.GetOrdinal("FilePath"));
				int rx = reader.GetInt32(reader.GetOrdinal("RectX"));
				int ry = reader.GetInt32(reader.GetOrdinal("RectY"));
				int rw = reader.GetInt32(reader.GetOrdinal("RectW"));
				int rh = reader.GetInt32(reader.GetOrdinal("RectH"));
				int px = reader.GetInt32(reader.GetOrdinal("PosX"));
				int py = reader.GetInt32(reader.GetOrdinal("PosY"));
				int delay = reader.GetInt32(reader.GetOrdinal("Delay"));
				rows.Add((fp, new Rectangle(rx, ry, rw, rh), new Point(px, py), delay, false));
			}

			if (rows.Count == 0)
				return null;

			ASprite newSprite = null;

			if (isAnimeHeader)
			{
				SpriteAnime anime = null;
				foreach (var r in rows)
				{
					if (!resourceDic.TryGetValue(r.FilePath, out var aContentFile))
					{
						ConstImage img = new ConstImage(r.FilePath);
						img.CreateFrom(r.FilePath, false);
						if (!img.IsCreated)
							return null;
						resourceDic[r.FilePath] = img;
						aContentFile = img;
					}
					if (!(aContentFile is ConstImage parentImage) || !parentImage.IsCreated)
						return null;
					Rectangle fRect = r.Rect;
					if (fRect.Width <= 0 || fRect.Height <= 0)
						fRect = new Rectangle(0, 0, parentImage.Bitmap.Width, parentImage.Bitmap.Height);
					if (anime == null)
					{
						anime = new SpriteAnime(name, new Size(fRect.Width, fRect.Height));
					}
					anime.AddFrame(parentImage, fRect, r.Pos, r.Delay);
				}
				newSprite = anime;
			}
			else
			{
				var r = rows[0];
				if (!resourceDic.TryGetValue(r.FilePath, out var aContentFile))
				{
					ConstImage img = new ConstImage(r.FilePath);
					img.CreateFrom(r.FilePath, false);
					if (!img.IsCreated)
						return null;
					resourceDic[r.FilePath] = img;
					aContentFile = img;
				}
				if (!(aContentFile is ConstImage parentImage) || !parentImage.IsCreated)
					return null;
				Rectangle rect = r.Rect;
				if (rect.Width <= 0 || rect.Height <= 0)
					rect = new Rectangle(0, 0, parentImage.Bitmap.Width, parentImage.Bitmap.Height);
				newSprite = new SpriteF(name, parentImage, rect, r.Pos);
			}

			if (newSprite != null)
				imageDictionary[name] = newSprite;
			return newSprite;
		}

		static public void SpriteDispose(string name)
		{
			if (name == null)
				return;
			name = name.ToUpper(CultureInfo.InvariantCulture);
			if (imageDictionary.TryRemove(name, out var aSprite))
				aSprite.Dispose();
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
			try
			{
				var bmp = SKBitmap.Decode(filepath);
				if (bmp == null)
					return false;
				ConstImage img = new ConstImage(name + "_DYN");
				img.CreateFrom(bmp, false);
				ASprite newSprite = new SpriteF(name, img, new Rectangle(0, 0, bmp.Width, bmp.Height), Point.Empty);
				imageDictionary[name] = newSprite;
				return true;
			}
			catch
			{
				return false;
			}
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

		static public long SpriteDisposeAll(bool delCsvImage)
		{
			int sprites = imageDictionary.Count;
			foreach (var s in imageDictionary.Values)
				s.Dispose();
			imageDictionary.Clear();
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

		static public bool LoadContents(bool useAsync = false)
		{
			System.Diagnostics.Debug.WriteLine($"[Emuera] AppContents.LoadContents: ContentDir={Program.ContentDir}");
			if (!Directory.Exists(Program.ContentDir))
			{
				System.Diagnostics.Debug.WriteLine($"[Emuera] AppContents.LoadContents: ContentDir does not exist, skipping");
				return true;
			}
			try
			{
				string[] csvFiles = FileUtils.GetFiles(Program.ContentDir, "*.csv", SearchOption.AllDirectories);
				System.Diagnostics.Debug.WriteLine($"[Emuera] AppContents.LoadContents: found {csvFiles.Length} csv files, writing metadata to SQLite");

				using var trans = metaDb.BeginTransaction();
				using var insertCmd = metaDb.CreateCommand();
				insertCmd.Transaction = trans;
				insertCmd.CommandText = @"
					INSERT OR REPLACE INTO SpriteMeta (Name, FrameIndex, FilePath, RectX, RectY, RectW, RectH, PosX, PosY, Delay, IsAnime)
					VALUES (@Name, @FrameIndex, @FilePath, @RectX, @RectY, @RectW, @RectH, @PosX, @PosY, @Delay, @IsAnime)";

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
				var pIsAnime = insertCmd.Parameters.Add("@IsAnime", SqliteType.Integer);

				int spriteCount = 0;

				foreach (var filepath in csvFiles)
				{
					SpriteAnime currentAnime = null;
					string directory = Path.GetDirectoryName(filepath) + Path.DirectorySeparatorChar;
					string filename = Path.GetFileName(filepath);
					string[] lines = File.ReadAllLines(filepath, Config.Encode);
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
						pIsAnime.Value = isAnimeFrame ? 0 : 0;
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
				return false;
			}
			return true;
		}

		static public void UnloadContents()
		{
			foreach (var img in resourceDic.Values)
				img.Dispose();
			resourceDic.Clear();
			imageDictionary.Clear();
			foreach (var graph in gList.Values)
				graph.GDispose();
			gList.Clear();
		}

		static public void UnloadGraphicList()
		{
			foreach (var graph in gList.Values)
				graph.GDispose();
			gList.Clear();
		}

		static public void UnloadTempLoadedConstImageNames()
		{
		}

		static public void UnloadTempLoadedGraphicsImageNames()
		{
		}
	}
}
