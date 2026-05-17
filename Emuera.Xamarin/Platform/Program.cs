using System;
using System.Collections.Generic;
using MinorShift.Emuera.Runtime.Utils;
using MinorShift.Emuera.Runtime.Script;
using MinorShift.Emuera.GameView;
using MinorShift.Emuera.Runtime.Script.Statements.Expression;
using MinorShift.Emuera.UI;
using System.IO;
using XEmuera;
using XEmuera.Resources;
using MinorShift.Emuera.Runtime.Utils.EvilMask;
using MinorShift._Library;
using WinmmTimer = MinorShift._Library.WinmmTimer;

namespace MinorShift.Emuera
{
	internal static class Program
	{
#if DESKTOP_APP
		[STAThread]
		internal static void Main(string[] args)
		{
			global::System.Windows.Forms.Application.EnableVisualStyles();
			global::System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
			global::System.Windows.Forms.Application.SetHighDpiMode(System.Windows.Forms.HighDpiMode.SystemAware);

			if (!Init())
				return;

			var form = new DesktopHostForm();
			form.Shown += (s, e) => form.InitEmuera();
			global::System.Windows.Forms.Application.Run(form);
		}
#endif
		/*
		コードの開始地点。
		ここでMainWindowを作り、
		MainWindowがProcessを作り、
		ProcessがGameBase・ConstantData・Variableを作る。
		
		
		*.ERBの読み込み、実行、その他の処理をProcessが、
		入出力をMainWindowが、
		定数の保存をConstantDataが、
		変数の管理をVariableが行う。
		 
		と言う予定だったが改変するうちに境界が曖昧になってしまった。
		 
		後にEmueraConsoleを追加し、それに入出力を担当させることに。
        
        1750 DebugConsole追加
         Debugを全て切り離すことはできないので一部EmueraConsoleにも担当させる
		
		TODO: 1819 MainWindow & Consoleの入力・表示組とProcess&Dataのデータ処理組だけでも分離したい

		*/
		/// <summary>
		/// アプリケーションのメイン エントリ ポイントです。
		/// </summary>
		//[STAThread]
		//static void Main(string[] args)
		internal static bool Init()
		{
			System.Diagnostics.Debug.WriteLine($"[Emuera] Program.Init() start");
			System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

			if (GlobalStatic.PathProvider == null)
				GlobalStatic.PathProvider = new DefaultPathProvider(Sys.ExeDir);

			System.Diagnostics.Debug.WriteLine($"[Emuera] PathProvider: ExeDir={ExeDir}, CsvDir={CsvDir}, ErbDir={ErbDir}");

#if DEBUG
			debugMode = true;
#endif

			#region EM_私家版_Emuera多言語化改造
			System.Diagnostics.Debug.WriteLine($"[Emuera] Loading language files...");
			Lang.LoadLanguageFiles();
			System.Diagnostics.Debug.WriteLine($"[Emuera] Language files loaded");
			#endregion


			if (!Directory.Exists(CsvDir))
			{
				System.Diagnostics.Debug.WriteLine($"[Emuera] CSV directory not found: {CsvDir}");
				PlatformInterop.ShowMessageWithTitle(Lang.UI.MainWindow.MsgBox.NoCsvFolder.Text, Lang.UI.MainWindow.MsgBox.FolderNotFound.Text);
				return false;
			}
			if (!Directory.Exists(ErbDir))
			{
				System.Diagnostics.Debug.WriteLine($"[Emuera] ERB directory not found: {ErbDir}");
				PlatformInterop.ShowMessageWithTitle(Lang.UI.MainWindow.MsgBox.NoErbFolder.Text, Lang.UI.MainWindow.MsgBox.FolderNotFound.Text);
				return false;
			}

			System.Diagnostics.Debug.WriteLine($"[Emuera] Loading config...");
			ConfigData.Instance.LoadConfig();
			Runtime.Config.JSON.JSONConfig.Load();
			System.Diagnostics.Debug.WriteLine($"[Emuera] Config loaded");

			#region EM_私家版_Emuera多言語化改造
			System.Diagnostics.Debug.WriteLine($"[Emuera] Setting language...");
			Lang.SetLanguage();
			System.Diagnostics.Debug.WriteLine($"[Emuera] Language set");
			#endregion

			System.Diagnostics.Debug.WriteLine($"[Emuera] Loading fonts...");
			LoadFontFiles();
			System.Diagnostics.Debug.WriteLine($"[Emuera] Fonts loaded");

			if (debugMode)
			{
				ConfigData.Instance.LoadDebugConfig();
				if (!Directory.Exists(DebugDir))
				{
					try { Directory.CreateDirectory(DebugDir); }
					catch { return false; }
				}
			}
			if ((!Config.AllowMultipleInstances) && (Sys.PrevInstance()))
			{
				PlatformInterop.ShowMessageWithTitle(Lang.UI.MainWindow.MsgBox.MultiInstanceInfo.Text, Lang.UI.MainWindow.MsgBox.InstaceExists.Text);
				return false;
			}

			StartTime = WinmmTimer.TickCount;
			System.Diagnostics.Debug.WriteLine($"[Emuera] Program.Init() complete");
			return true;
		}

		public static void RebootMain()
		{
			Content.AppContents.UnloadContents();
			if (Reboot)
			{
				ParserMediator.ClearWarningList();
				ParserMediator.Initialize(null);
				GlobalStatic.Reset();
				ConfigData.Instance.ReLoadConfig();
			}
			WinmmTimer.Reset();
		}

		private static void LoadFontFiles()
		{
			var fontDirs = new List<string>();
			var dir1 = ExeDir + "font" + Path.DirectorySeparatorChar;
			if (Directory.Exists(dir1))
				fontDirs.Add(dir1);
			else if (Directory.Exists(ExeDir + "FONT" + Path.DirectorySeparatorChar))
				fontDirs.Add(ExeDir + "FONT" + Path.DirectorySeparatorChar);
			var dir2 = ExeDir + "fonts" + Path.DirectorySeparatorChar;
			if (Directory.Exists(dir2))
				fontDirs.Add(dir2);

			var loadedFamilyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (var fontDir in fontDirs)
			{
				foreach (string ext in new[] { "*.ttf", "*.otf", "*.ttc" })
				{
					foreach (string fontFile in FileUtils.GetFiles(fontDir, ext, SearchOption.AllDirectories))
					{
						GlobalStatic.Pfc.AddFontFile(fontFile);
						try
						{
							var typeface = SkiaSharp.SKTypeface.FromFile(fontFile);
							if (typeface != null)
							{
								if (!loadedFamilyNames.Contains(typeface.FamilyName))
								{
									loadedFamilyNames.Add(typeface.FamilyName);
									GlobalStatic.CustomTypefaces.Add(typeface);
									FontFactory.RegisterFontAlias(typeface.FamilyName, typeface);
									var fileName = Path.GetFileNameWithoutExtension(fontFile);
									if (!string.Equals(fileName, typeface.FamilyName, StringComparison.OrdinalIgnoreCase))
										FontFactory.RegisterFontAlias(fileName, typeface);
								}
							}
						}
						catch { }
					}
				}
			}
		}

		/// <summary>
		/// 実行ファイルのディレクトリ。最後に\を付けたstring
		/// </summary>
		public static string ExeDir => GlobalStatic.PathProvider?.ExeDir ?? "";
		public static string WorkingDir => GlobalStatic.PathProvider?.ExeDir ?? "";
		public static string CsvDir => GlobalStatic.PathProvider?.CsvDir ?? "";
		public static string ErbDir => GlobalStatic.PathProvider?.ErbDir ?? "";
		public static string DebugDir => GlobalStatic.PathProvider?.DebugDir ?? "";
		public static string DatDir => GlobalStatic.PathProvider?.DatDir ?? "";
		public static string ContentDir => GlobalStatic.PathProvider?.ContentDir ?? "";
		public static string ExeName => GlobalStatic.PathProvider?.ExeName ?? "emuera";
		#region EE_PLAYSOUND系
		public static string MusicDir => GlobalStatic.PathProvider?.MusicDir ?? "";
		public static bool FileExists(ref string filepath) => FileUtils.Exists(ref filepath);
		//public static IPlayer[] sound = new IPlayer[10];
		//public static IPlayer bgm = null;
		#endregion

		public static bool Reboot = false;
		//public static int RebootClientX = 0;
		public static int RebootClientY = 0;
		//public static FormWindowState RebootWinState = FormWindowState.Normal;
		//public static Point RebootLocation;

		public static bool AnalysisMode = false;
		public static List<string> AnalysisFiles = null;
		public static bool rebootFlag = false;

		public static bool debugMode = false;
		public static bool DebugMode { get { return debugMode; } }


		public static uint StartTime { get; private set; }

	}
}