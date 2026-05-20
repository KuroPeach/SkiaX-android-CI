using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;
using XEmuera.Drawing;
using System.Collections.ObjectModel;
using System.ComponentModel;
using XEmuera.Resources;

namespace XEmuera.Models
{
	public class FontModel : INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler PropertyChanged;

		private static bool Init;

		public const string PrefKeyEnabledFont = nameof(PrefKeyEnabledFont);

		private const string separator = "_|_";

		private const string DefaultFontName = "MS Gothic";
		private const string FinalFontName = "Microsoft YaHei";
		private const string SimHeiFontName = "SimHei";

		public static readonly List<FontModel> UserList = new List<FontModel>();

		public static FontGroup EnabledList;

		public static FontGroup DisabledList;

		private static readonly Dictionary<string, FontModel> AllModels = new Dictionary<string, FontModel>();

		public static FontModel Default { get; private set; }

		public static FontModel Final { get; private set; }

		private static readonly Mapping<string, string> FontNameMapping = new Mapping<string, string>();

		public string Name { get; private set; }

		public string OtherName { get; private set; }

		public SKTypeface Typeface { get; private set; }

		public bool Enabled
		{
			get { return _enabled; }
			set
			{
				if (_enabled == value && Init)
					return;
				_enabled = value;
				Sort();
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Enabled)));
			}
		}
		bool _enabled;

		public FontModel(SKTypeface typeface)
		{
			Typeface = typeface;
			Name = typeface.FamilyName;
		}

		private void Sort()
		{
			EnabledList.Remove(this);
			DisabledList.Remove(this);

			if (_enabled)
				EnabledList.Add(this);
			else
				DisabledList.Insert(0, this);
		}

		public static void Load()
		{
			Init = false;

			InitFontMapping();

			AddFontFromResource(DefaultFontName);
			AddFontFromResource(FinalFontName);
			AddFontFromResource(SimHeiFontName);

			Default = AllModels[DefaultFontName];

			LoadFontsFolder();

			// 为缺失的中文字体注册回退别名（映射到内嵌的 Microsoft YaHei）
			RegisterMissingChineseFontFallbacks();

			EnabledList = new FontGroup
			{
				Name = StringsText.FontReplaceByOrder,
			};
			DisabledList = new FontGroup(AllModels.Values)
			{
				Name = StringsText.FontOnlyCallInGame,
			};

			string enabledFont = GameUtils.GetPreferences(PrefKeyEnabledFont, null);
			if (string.IsNullOrEmpty(enabledFont))
			{
				Default.Enabled = true;
				AllModels[FinalFontName].Enabled = true;
				AllModels[SimHeiFontName].Enabled = true;
			}
			else
			{
				foreach (var item in enabledFont.Split(new[] { separator }, StringSplitOptions.RemoveEmptyEntries))
				{
					if (AllModels.TryGetValue(item, out var fontModel))
						fontModel.Enabled = true;
				}
			}

			Save();

			Init = true;
		}

		public static void Save()
		{
			UserList.Clear();
			foreach (var item in EnabledList)
				UserList.Add(item);
			foreach (var item in DisabledList)
				UserList.Add(item);

			if (EnabledList.Count > 0)
				Final = EnabledList.Last();
			else
				Final = Default;

			GameUtils.SetPreferences(PrefKeyEnabledFont, string.Join(separator, EnabledList.Select(item => item.Name)));

			DrawTextUtils.Load();
		}

		private static void LoadFontsFolder()
		{
			var basePath = GameFolderModel.Instance.Path;
			var fontDirs = new List<string>();

			// 扫描 font/ 和 fonts/ 目录（与 Program.LoadFontFiles 保持一致）
			var dir1 = basePath + Path.DirectorySeparatorChar + "font" + Path.DirectorySeparatorChar;
			if (Directory.Exists(dir1))
				fontDirs.Add(dir1);
			var dir2 = basePath + Path.DirectorySeparatorChar + "fonts" + Path.DirectorySeparatorChar;
			if (Directory.Exists(dir2))
				fontDirs.Add(dir2);

			if (fontDirs.Count == 0)
				return;

			foreach (var fontDir in fontDirs)
			{
				foreach (string ext in new[] { "*.ttf", "*.otf", "*.ttc" })
				{
					foreach (var fontFile in FileUtils.GetFiles(fontDir, ext, SearchOption.AllDirectories))
					{
						string fileName = Path.GetFileNameWithoutExtension(fontFile);
						if (AllModels.ContainsKey(fileName))
							continue;

						var typeface = SKTypeface.FromFile(fontFile);
						if (typeface == null)
							continue;

						if (AllModels.ContainsKey(typeface.FamilyName))
						{
							// 同族字体已存在，注册别名后释放
							RegisterFontAlias(fileName, typeface.FamilyName);
							typeface.Dispose();
						}
						else
						{
							AddFontModel(typeface, fileName);
							// 为常见中文字体注册中文别名
							RegisterChineseFontAliases(fileName, typeface.FamilyName);
						}
					}
				}
			}
		}

		/// <summary>
		/// 为常见中文字体文件名注册中文别名映射。
		/// 例如 simhei.ttf → "黑体"→"SimHei", simsun.ttf → "宋体"→"SimSun"
		/// </summary>
		private static void RegisterChineseFontAliases(string fileName, string familyName)
		{
			var lowerFileName = fileName.ToLowerInvariant();
			var aliasMap = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
			{
				{ "simhei", new[] { "黑体" } },
				{ "simsun", new[] { "宋体" } },
				{ "simfang", new[] { "仿宋" } },
				{ "simkai", new[] { "楷体" } },
				{ "msyh", new[] { "微软雅黑" } },
			};

			foreach (var kvp in aliasMap)
			{
				if (lowerFileName.StartsWith(kvp.Key))
				{
					foreach (var alias in kvp.Value)
					{
						if (!FontNameMapping.GetByKey(alias, out _))
							FontNameMapping.Add(alias, familyName);
					}
				}
			}
		}

		/// <summary>
		/// 为缺失的中文字体注册回退别名。
		/// 当外部字体文件（如 simhei.ttf）不存在时，将中文别名（如"黑体"）映射到内嵌的 Microsoft YaHei。
		/// </summary>
		private static void RegisterMissingChineseFontFallbacks()
		{
			// 中文字体别名 → 对应的外部字体文件前缀
			var chineseFontFallbacks = new (string alias, string filePrefix)[]
			{
				("黑体", "simhei"),
				("宋体", "simsun"),
				("仿宋", "simfang"),
				("楷体", "simkai"),
			};

			// 检查外部字体是否已加载（通过检查是否有对应的 FontModel）
			foreach (var (alias, filePrefix) in chineseFontFallbacks)
			{
				// 如果别名已有映射，说明外部字体已加载，跳过
				if (FontNameMapping.GetByKey(alias, out _))
					continue;

				// 别名未映射，说明外部字体不存在，回退到 Microsoft YaHei
				if (AllModels.TryGetValue("Microsoft YaHei", out var yaheiModel))
				{
					FontNameMapping.Add(alias, yaheiModel.Name);
				}
			}
		}

		private static void InitFontMapping()
		{
			FontNameMapping.Clear();

			FontNameMapping.Add("ＭＳ ゴシック", "MS Gothic");
			FontNameMapping.Add("ＭＳ Ｐゴシック", "MS PGothic");
			FontNameMapping.Add("ＭＳ 明朝", "MS Mincho");
			FontNameMapping.Add("ＭＳ Ｐ明朝", "MS PMincho");
			FontNameMapping.Add("微软雅黑", "Microsoft YaHei");
			FontNameMapping.Add("黑体", "SimHei");
			// 中文字体别名在 LoadFontsFolder → RegisterChineseFontAliases 中动态注册
			// （因为需要知道外部字体的实际族名）
		}

		private static void AddFontFromResource(string fileName)
		{
			if (AllModels.ContainsKey(fileName))
				return;

			string path = $"XEmuera.Resources.Fonts.{fileName}.ttf";
			SKTypeface sKTypeface = null;

			try
			{
				var stream = GameUtils.GetManifestResourceStream(path);
				if (stream != null)
					sKTypeface = SKTypeface.FromStream(stream);
			}
			catch { }

			// 嵌入资源字体加载失败时，使用系统默认字体作为回退
			if (sKTypeface == null)
				sKTypeface = SKTypeface.Default;

			AddFontModel(sKTypeface, fileName);
		}

		private static void AddFontModel(SKTypeface sKTypeface, string fileName)
		{
			var fontModel = new FontModel(sKTypeface);
			if (AllModels.ContainsKey(fontModel.Name))
			{
				// 同族字体已存在，跳过（避免重复键异常）
				return;
			}
			AllModels.Add(fontModel.Name, fontModel);

			FontNameMapping.GetByValue(fontModel.Name, out var otherName, fileName);
			fontModel.OtherName = otherName;

			// 注册文件名作为别名（如 "msgothic" → "MS Gothic"）
			if (!string.Equals(fileName, fontModel.Name, StringComparison.OrdinalIgnoreCase))
			{
				FontNameMapping.Add(fileName, fontModel.Name);
			}
		}

		/// <summary>
		/// 注册字体别名（文件名→族名），用于同族字体重复加载时
		/// </summary>
		private static void RegisterFontAlias(string alias, string familyName)
		{
			if (!string.Equals(alias, familyName, StringComparison.OrdinalIgnoreCase))
			{
				if (!FontNameMapping.GetByKey(alias, out _))
					FontNameMapping.Add(alias, familyName);
			}
		}

		public static bool HasFont(string fontName)
		{
			if (FontNameMapping.GetByKey(fontName, out string newFontName))
				fontName = newFontName;
			return AllModels.ContainsKey(fontName);
		}

		public static FontModel GetFont(string fontName)
		{
			if (FontNameMapping.GetByKey(fontName, out string newFontName))
				fontName = newFontName;
			if (AllModels.TryGetValue(fontName, out FontModel model))
				return model;
			return Default;
		}
	}

	public class FontGroup : ObservableCollection<FontModel>
	{
		public string Name { get; set; }

		public FontGroup() : base() { }

		public FontGroup(IEnumerable<FontModel> models) : base(models) { }
	}
}
