using System;
//using System.Drawing;
using System.Collections.Generic;
using XEmuera.Forms;
using MinorShift.Emuera.Runtime.Utils;
using MinorShift.Emuera.Runtime.Script;
using MinorShift.Emuera.GameView;
using MinorShift.Emuera.Runtime.Script.Statements.Expression;
using System.IO;
using XEmuera.Resources;
using MinorShift.Emuera.Runtime.Utils.EvilMask;
using MinorShift._Library;
using WinmmTimer = MinorShift._Library.WinmmTimer;

namespace MinorShift.Emuera
{
	internal static class Program
	{
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
			System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

			GlobalStatic.PathProvider = new DefaultPathProvider(Sys.ExeDir);

#if DEBUG
			debugMode = true;
#endif

			//解析モードの判定だけ先に行う
			//int argsStart = 0;
			#region EM_私家版_Emuera多言語化改造
			// List<string> otherArgs = new List<string>();
			// foreach (var arg in args)
            // {
			// 	//if ((args.Length > 0) && (args[0].Equals("-DEBUG", StringComparison.CurrentCultureIgnoreCase)))
			// 	if (arg.Equals("-DEBUG", StringComparison.CurrentCultureIgnoreCase))
			// 	{
			// 		// argsStart = 1;//デバッグモードかつ解析モード時に最初の1っこ(-DEBUG)を飛ばす
			// 		debugMode = true;
			// 	}
			// 	else if (arg.Equals("-GENLANG", StringComparison.CurrentCultureIgnoreCase))
			// 	{
			// 		Lang.GenerateDefaultLangFile();
			// 	}
			// 	else otherArgs.Add(arg);
			// }
			// //if (args.Length > argsStart)
			// if (otherArgs.Count > 0)
			#endregion

			#region EM_私家版_Emuera多言語化改造
			//Lang.LoadLanguageFile();
			#endregion
			#region EM_私家版_Icon指定機能
			// Icon icon = null;
			// {
			// 	var bmp = Utils.LoadImage(Utils.GetValidPath(Config.EmueraIcon));
			// 	if (bmp != null)
			// 	{
			// 		icon = Utils.MakeIconFromBmpFile(bmp);
			// 		bmp.Dispose();
			// 	}
			// }
			#endregion


			if (!Directory.Exists(CsvDir))
			{
				MessageBox.Show(Lang.UI.MainWindow.MsgBox.NoCsvFolder.Text, Lang.UI.MainWindow.MsgBox.FolderNotFound.Text);
				return false;
			}
			if (!Directory.Exists(ErbDir))
			{
				MessageBox.Show(Lang.UI.MainWindow.MsgBox.NoErbFolder.Text, Lang.UI.MainWindow.MsgBox.FolderNotFound.Text);
				return false;
			}

			ConfigData.Instance.LoadConfig();
			Runtime.Config.JSON.JSONConfig.Load();

			LoadFontFiles();

			if (debugMode)
			{
				ConfigData.Instance.LoadDebugConfig();
				if (!Directory.Exists(DebugDir))
				{
					try { Directory.CreateDirectory(DebugDir); }
					catch { return false; }
				}
			}
			//二重起動の禁止かつ二重起動
			if ((!Config.AllowMultipleInstances) && (Sys.PrevInstance()))
			{
				//MessageBox.Show("多重起動を許可する場合、emuera.configを書き換えて下さい", "既に起動しています");
				MessageBox.Show(Lang.UI.MainWindow.MsgBox.MultiInstanceInfo.Text, Lang.UI.MainWindow.MsgBox.InstaceExists.Text);
				return false;
			}
			
			// if (debugMode)
			// {
			// 	ConfigData.Instance.LoadDebugConfig();
			// 	if (!Directory.Exists(DebugDir))
			// 	{
			// 		try
			// 		{
			// 			Directory.CreateDirectory(DebugDir);
			// 		}
			// 		catch
			// 		{
			// 			MessageBox.Show(Lang.UI.MainWindow.MsgBox.FailedCreateDebugFolder.Text, Lang.UI.MainWindow.MsgBox.FolderNotFound.Text);
			// 			return;
			// 		}
			// 	}
			// }
			// if (AnalysisMode)
			// {
			// 	AnalysisFiles = new List<string>();
				#region EM_私家版_Emuera多言語化改造
			// 	// for (int i = argsStart; i < args.Length; i++)
			// 	foreach (var arg in otherArgs)
			// 	{
			// 		//if (!File.Exists(args[i]) && !Directory.Exists(args[i]))
			// 		if (!File.Exists(arg) && !Directory.Exists(arg))
			// 		{
			// 			MessageBox.Show(Lang.UI.MainWindow.MsgBox.ArgPathNotExists.Text);
			// 			return;
			// 		}
			// 		//if ((File.GetAttributes(args[i]) & FileAttributes.Directory) == FileAttributes.Directory)
			// 		if ((File.GetAttributes(arg) & FileAttributes.Directory) == FileAttributes.Directory)
			// 		{
			// 			//List<KeyValuePair<string, string>> fnames = Config.GetFiles(args[i] + "\\", "*.ERB");
			// 			List<KeyValuePair<string, string>> fnames = Config.GetFiles(arg + "\\", "*.ERB");
			// 			for (int j = 0; j < fnames.Count; j++)
			// 			{
			// 				AnalysisFiles.Add(fnames[j].Value);
			// 			}
			// 		}
			// 		else
			// 		{
			// 			//if (Path.GetExtension(args[i]).ToUpper() != ".ERB")
			// 			if (Path.GetExtension(arg).ToUpper() != ".ERB")
			// 			{
			// 				MessageBox.Show(Lang.UI.MainWindow.MsgBox.InvalidArg.Text);
			// 				return;
			// 			}
			// 			//AnalysisFiles.Add(args[i]);
			// 			AnalysisFiles.Add(arg);
			// 		}
			// 	}
				#endregion
			// }

			//MainWindow win = null;
			//while (true)
			//{
			StartTime = WinmmTimer.TickCount;
			// 	using (win = new MainWindow())
			// 	{
					#region EM_私家版_Emuera多言語化改造
			// 		win.TranslateUI();
					#endregion
					#region EM_私家版_Icon指定機能
			// 		if (icon != null)
			// 			win.SetupIcon(icon);
					#endregion
			// 		Application.Run(win);
			// 		Content.AppContents.UnloadContents();
			// 		if (!Reboot)
			// 			break;
			// 		RebootWinState = win.WindowState;
			// 		if (win.WindowState == FormWindowState.Normal)
			// 		{
			// 			RebootClientY = win.ClientSize.Height;
			// 			RebootLocation = win.Location;
			// 		}
			// 		else
			// 		{
			// 			RebootClientY = 0;
			// 			RebootLocation = new Point();
			// 		}
			// 	}
			// 	//条件次第ではParserMediatorが空でない状態で再起動になる場合がある
			// 	ParserMediator.ClearWarningList();
			// 	ParserMediator.Initialize(null);
			// 	GlobalStatic.Reset();
			// 	//GC.Collect();
			// 	Reboot = false;
			// 	ConfigData.Instance.ReLoadConfig();
			// }

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
			var fontDir = ExeDir + "font" + Path.DirectorySeparatorChar;
			if (!Directory.Exists(fontDir))
				fontDir = ExeDir + "FONT" + Path.DirectorySeparatorChar;
			if (!Directory.Exists(fontDir))
				return;

			foreach (string fontFile in Directory.GetFiles(fontDir, "*.ttf", SearchOption.AllDirectories))
			{
				GlobalStatic.Pfc.AddFontFile(fontFile);
				try
				{
					var typeface = SkiaSharp.SKTypeface.FromFile(fontFile);
					if (typeface != null)
						GlobalStatic.CustomTypefaces.Add(typeface);
				}
				catch { }
			}

			foreach (string fontFile in Directory.GetFiles(fontDir, "*.otf", SearchOption.AllDirectories))
			{
				GlobalStatic.Pfc.AddFontFile(fontFile);
				try
				{
					var typeface = SkiaSharp.SKTypeface.FromFile(fontFile);
					if (typeface != null)
						GlobalStatic.CustomTypefaces.Add(typeface);
				}
				catch { }
			}

			foreach (string fontFile in Directory.GetFiles(fontDir, "*.ttc", SearchOption.AllDirectories))
			{
				GlobalStatic.Pfc.AddFontFile(fontFile);
				try
				{
					var typeface = SkiaSharp.SKTypeface.FromFile(fontFile);
					if (typeface != null)
						GlobalStatic.CustomTypefaces.Add(typeface);
				}
				catch { }
			}
		}

		/// <summary>
		/// 実行ファイルのディレクトリ。最後に\を付けたstring
		/// </summary>
		public static string ExeDir => GlobalStatic.PathProvider?.ExeDir ?? "";
		public static string WorkingDir = "";
		public static string CsvDir => GlobalStatic.PathProvider?.CsvDir ?? "";
		public static string ErbDir => GlobalStatic.PathProvider?.ErbDir ?? "";
		public static string DebugDir => GlobalStatic.PathProvider?.DebugDir ?? "";
		public static string DatDir => GlobalStatic.PathProvider?.DatDir ?? "";
		public static string ContentDir => GlobalStatic.PathProvider?.ContentDir ?? "";
		public static string ExeName => GlobalStatic.PathProvider?.ExeName ?? "emuera";
		#region EE_PLAYSOUND系
		public static string MusicDir => GlobalStatic.PathProvider?.MusicDir ?? "";
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