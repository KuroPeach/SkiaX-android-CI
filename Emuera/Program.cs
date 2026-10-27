using MinorShift.Emuera.GameProc.Function;
using MinorShift.Emuera.Runtime.Config;
using MinorShift.Emuera.Runtime.Config.JSON;
using MinorShift.Emuera.Runtime.Utils;
using MinorShift.Emuera.Runtime.Utils.EvilMask;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime;
using System.Windows.Forms;

namespace MinorShift.Emuera;
#nullable enable
static partial class Program
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
	/// 启动期致命错误（CSV/ERB 缺失等）：自动化模式绝不弹模态框；GUI 模式显示带路径的错误后退出。
	/// 统一写 stderr（EngineProcess 会把 stderr 落到 engine_stderr.txt），并让进程以 2 退出。
	/// </summary>
	private static void ReportStartupError(string code, string title, string text)
	{
		try { Console.Error.WriteLine("[startup] " + code); } catch { }
		if (EwpMode || EwpHeadlessMode || CliDebugMode)
		{
			// EWP/CLI/headless：不弹窗，直接失败退出；调用方已写 stderr。
			Environment.Exit(2);
			return;
		}
		// 普通 GUI：模型化错误窗（非系统模态 MessageBox），显示路径；关闭后以 2 退出。
		try
		{
			var form = new Form
			{
				Text = title,
				Width = 760,
				Height = 300,
				StartPosition = FormStartPosition.CenterScreen,
				MinimizeBox = false,
				MaximizeBox = false,
			};
			var box = new TextBox
			{
				Multiline = true,
				ReadOnly = true,
				ScrollBars = ScrollBars.Vertical,
				Dock = DockStyle.Fill,
				Text = text,
			};
			var button = new Button { Text = "退出", Dock = DockStyle.Bottom, Height = 40 };
			button.Click += (_, __) => form.Close();
			form.Controls.Add(box);
			form.Controls.Add(button);
			form.Show();
			Application.Run(form);
		}
		catch
		{
			// 极端情况下回退原生 MessageBox；仍然退出
			MessageBox.Show(text, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
		Environment.Exit(2);
	}

	/// <summary>
	/// アプリケーションのメイン エントリ ポイントです。
	/// </summary>
	[STAThread]
	static void Main(string[] args)
	{
		// memo: Shift-JISを扱うためのおまじない
		System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
		try { Console.OutputEncoding = new System.Text.UTF8Encoding(false); } catch { }
		var rootCommand = new RootCommand("Emuera");

		#region eee_カレントディレクトリー
		WorkingDir = AssemblyData.WorkingDir;

		CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
		CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;

		var exeDirOption = new Option<string>(
			name: "--ExeDir",
			description: "与えられたフォルダのEraを起動します"
		);
		rootCommand.AddOption(exeDirOption);

		var debugModeOption = new Option<bool>(
			name: "-Debug",
			description: "デバッグモード"
		);
		debugModeOption.AddAlias("-debug");
		debugModeOption.AddAlias("-DEBUG");
		rootCommand.AddOption(debugModeOption);

		var genLangOption = new Option<bool>(
			name: "-GenLang",
			description: "言語ファイルテンプレ生成"
		);
		rootCommand.AddOption(genLangOption);
		var cliDebugOption = new Option<bool>(
			name: "--CliDebug",
			description: "CLI 调试模式：stdin/stdout JSON Lines 协议，供 agent 驱动调试（隐含 -Debug）"
		);
		cliDebugOption.AddAlias("-CliDebug");
		rootCommand.AddOption(cliDebugOption);
		var ewpOption = new Option<bool>(
			name: "--ewp",
			description: "eraWire EWP/0 宿主：stdio/TCP 事件流（hello/legacy.line/input.request/frame.commit/pixel.frame），B3"
		);
		ewpOption.AddAlias("-ewp");
		rootCommand.AddOption(ewpOption);
		var ewpPortOption = new Option<int>(
			name: "--ewp-port",
			description: "EWP TCP 模式端口（与 --ewp 连用；缺省 0＝stdio）"
		);
		ewpPortOption.AddAlias("-ewp-port");
		rootCommand.AddOption(ewpPortOption);
		var ewpJsonOption = new Option<bool>(
			name: "--ewp-json",
			description: "EWP 用 JSON-Lines 编码（调试/抓包形态；A2 起默认＝MessagePack，spec §2 正本）"
		);
		ewpJsonOption.AddAlias("-ewp-json");
		rootCommand.AddOption(ewpJsonOption);
		var ewpMsgpackOption = new Option<bool>(
			name: "--ewp-msgpack",
			description: "EWP 用 MessagePack 编码（A2 起默认即此，显式同义；4 字节长度前缀帧）"
		);
		ewpMsgpackOption.AddAlias("-ewp-msgpack");
		rootCommand.AddOption(ewpMsgpackOption);
		var ewpHeadlessOption = new Option<bool>(
			name: "--ewp-headless",
			description: "无窗 EWP 宿主：不创建 MainWindow / 不进 Application.Run（N1-b）"
		);
		ewpHeadlessOption.AddAlias("-ewp-headless");
		rootCommand.AddOption(ewpHeadlessOption);

		var filesArg = new Argument<string[]>(
					"解析するファイル"
				)
		{ Arity = ArgumentArity.ZeroOrMore };
		rootCommand.AddArgument(filesArg);

		var result = rootCommand.Parse(args);

		//実行ディレクトリが引数で与えられた場合
		var exeDir = result.GetValueForOption(exeDirOption);
		if (exeDir != null)
		{
			SetDirPaths(exeDir);
		}

		#endregion
		//エラー出力用
		//1815 .exeが東方板のNGワードに引っかかるそうなので除去
		ExeName = Path.GetFileNameWithoutExtension(AssemblyData.ExeName);

		var debugMode = result.GetValueForOption(debugModeOption);
		DebugMode = debugMode;
		var cliDebugMode = result.GetValueForOption(cliDebugOption);
		CliDebugMode = cliDebugMode;
		if (cliDebugMode)
		{
			// CLI 调试隐含 -Debug（DebugConsoleLog / 变量监视等调试能力依赖 DebugMode）
			DebugMode = true;
			UI.CliDebug.CliDebugHost.InitIO();
		}
		var ewpMode = result.GetValueForOption(ewpOption);
		var ewpHeadlessMode = result.GetValueForOption(ewpHeadlessOption);
		EwpHeadlessMode = ewpHeadlessMode;
		EwpMode = ewpMode || ewpHeadlessMode;
		EwpPort = result.GetValueForOption(ewpPortOption);
		// A2（v0.1 定稿）：--ewp 默认＝MessagePack（spec §2 正本）；--ewp-json 调试/抓包形态
		EwpMsgpack = !result.GetValueForOption(ewpJsonOption);
		if (EwpMode)
			UI.Ewp.EwpOut.InitIO(deferWriter: EwpPort > 0, msgpack: EwpMsgpack);

		var genLang = result.GetValueForOption(genLangOption);
		if (genLang)
			Lang.GenerateDefaultLangFile();

		#region EM_私家版_Emuera多言語化改造
		List<string> otherArgs = [];

		//引数の後ろにある他のフラグにマッチしなかった文字列を解析指定されたファイルとみなす
		var fileArgs = result.GetValueForArgument(filesArg);
		var analysisRequestPaths = fileArgs;
		if (analysisRequestPaths.Length > 0)
		{
			/*
			foreach (var arg in args)
			{

				//if ((args.Length > 0) && (args[0].Equals("-DEBUG", StringComparison.CurrentCultureIgnoreCase)))
				if (arg.Equals("-DEBUG", StringComparison.CurrentCultureIgnoreCase))
				{
					// argsStart = 1;//デバッグモードかつ解析モード時に最初の1っこ(-DEBUG)を飛ばす
					DebugMode = true;
				}
				else if (arg.Equals("-GENLANG", StringComparison.CurrentCultureIgnoreCase))
				{
					Lang.GenerateDefaultLangFile();
				}
				else otherArgs.Add(arg);
			}
			//if (args.Length > argsStart)
			if (otherArgs.Count > 0)
			{
			*/
			//必要なファイルのチェックにはConfig読み込みが必須なので、ここではフラグだけ立てておく
			AnalysisMode = true;
			//}
		}
		#endregion

		Application.SetCompatibleTextRenderingDefault(false);
		ProfileOptimization.SetProfileRoot(exeDir ?? ExeDir);
		ProfileOptimization.StartProfile("profile");

		ConfigData.Instance.LoadConfig();
		JSONConfig.Load();

		//Naudioも終了しておく
		GlobalStatic.Bgm.close();
		for (int i = 0; i < GlobalStatic.Sound.Length; i++)
		{
			if (GlobalStatic.Sound[i] != null) GlobalStatic.Sound[i].close();
		}

		#region EM_私家版_Emuera多言語化改造
		Lang.LoadLanguageFiles();
		Lang.SetLanguage();
		#endregion
		#region EM_私家版_Icon指定機能
		Icon icon = null;
		{
			var bmp = Utils.LoadImage(Utils.GetValidPath(Config.EmueraIcon));
			if (bmp != null)
			{
				icon = Utils.MakeIconFromBmpFile(bmp);
				bmp.Dispose();
			}
		}
		#endregion

		//二重起動の禁止かつ二重起動
		if (!EwpHeadlessMode)
			ApplicationConfiguration.Initialize();
		if ((!Config.AllowMultipleInstances) && AssemblyData.PrevInstance())
		{
			ReportStartupError("already_running", Lang.UI.MainWindow.MsgBox.InstaceExists.Text,
				Lang.UI.MainWindow.MsgBox.MultiInstanceInfo.Text);
			return;
		}
		if (!Directory.Exists(CsvDir) || !Directory.Exists(ErbDir))
		{
			// ASCII 先行行：EngineProcess 的 engine_stderr.txt 可直接 grep 路径（避免本地化编码混杂）
			try { Console.Error.WriteLine("[startup] missing_game_data CSV=" + CsvDir + " ERB=" + ErbDir); } catch { }
			var lines = new List<string>();
			if (!Directory.Exists(CsvDir))
				lines.Add(Lang.UI.MainWindow.MsgBox.NoCsvFolder.Text + ": " + CsvDir);
			if (!Directory.Exists(ErbDir))
				lines.Add(Lang.UI.MainWindow.MsgBox.NoErbFolder.Text + ": " + ErbDir);
			lines.Add("请从游戏目录启动 Emuera.exe，或将 CSV/ERB 放到可执行文件同目录。");
			ReportStartupError("missing_game_data", Lang.UI.MainWindow.MsgBox.FolderNotFound.Text,
				string.Join(Environment.NewLine, lines));
			return;
		}
		#region EE_フォントファイル対応
		//フォントファイルを読み込む
		if (Directory.Exists(FontDir))
		{
			foreach (string fontFile in Directory.GetFiles(FontDir, "*.ttf", SearchOption.AllDirectories))
			{
				GlobalStatic.Pfc.AddFontFile(fontFile);
				try
				{
					var typeface = SKTypeface.FromFile(fontFile);
					if (typeface != null)
						GlobalStatic.CustomTypefaces.Add(typeface);
				}
				catch { }
			}

			foreach (string fontFile in Directory.GetFiles(FontDir, "*.otf", SearchOption.AllDirectories))
			{
				GlobalStatic.Pfc.AddFontFile(fontFile);
				try
				{
					var typeface = SKTypeface.FromFile(fontFile);
					if (typeface != null)
						GlobalStatic.CustomTypefaces.Add(typeface);
				}
				catch { }
			}

			foreach (string fontFile in Directory.GetFiles(FontDir, "*.ttc", SearchOption.AllDirectories))
			{
				GlobalStatic.Pfc.AddFontFile(fontFile);
				try
				{
					var typeface = SKTypeface.FromFile(fontFile);
					if (typeface != null)
						GlobalStatic.CustomTypefaces.Add(typeface);
				}
				catch { }
			}
		}
		#endregion

		if (DebugMode)
		{
			ConfigData.Instance.LoadDebugConfig();
			if (!Directory.Exists(DebugDir))
			{
				try
				{
					Directory.CreateDirectory(DebugDir);
				}
				catch
				{
					Dialog.Show(Lang.UI.MainWindow.MsgBox.FolderNotFound.Text, Lang.UI.MainWindow.MsgBox.FailedCreateDebugFolder.Text);
					return;
				}
			}
		}

		if (AnalysisMode)
		{
			AnalysisFiles = [];
			#region EM_私家版_Emuera多言語化改造
			// for (int i = argsStart; i < args.Length; i++)
			foreach (var path in analysisRequestPaths)
			{
				//if (!File.Exists(args[i]) && !Directory.Exists(args[i]))
				if (!File.Exists(path) && !Directory.Exists(path))
				{
					MessageBox.Show(Lang.UI.MainWindow.MsgBox.ArgPathNotExists.Text);
					return;
				}
				//if ((File.GetAttributes(args[i]) & FileAttributes.Directory) == FileAttributes.Directory)
				if ((File.GetAttributes(path) & FileAttributes.Directory) == FileAttributes.Directory)
				{
					//List<KeyValuePair<string, string>> fnames = Config.Config.GetFiles(args[i] + "\\", "*.ERB");
					List<KeyValuePair<string, string>> fnames = Config.GetFiles(path + "\\", "*.ERB");
					for (int j = 0; j < fnames.Count; j++)
					{
						AnalysisFiles.Add(fnames[j].Value);
					}
				}
				else
				{
					//if (Path.GetExtension(args[i]).ToUpper(CultureInfo.InvariantCulture) != ".ERB")
					if (!Path.GetExtension(path).Equals(".ERB", StringComparison.OrdinalIgnoreCase))
					{
						MessageBox.Show(Lang.UI.MainWindow.MsgBox.InvalidArg.Text);
						return;
					}
					//AnalysisFiles.Add(args[i]);
					AnalysisFiles.Add(path);
				}
			}
			#endregion
		}

		// PlatformInterop 委托初始化（WinForms 实现）
		PlatformInterop.DoEvents = () => Application.DoEvents();
		if (EwpMode)
		{
			// EWP 模式：模态对话框转 dialog.request 事件（ShowQuestion 一律应答 No，与 CliDebug 同策略）
			PlatformInterop.ShowMessage = text => UI.Ewp.EwpHost.EmitDialogEvent("message", text, "");
			PlatformInterop.ShowMessageWithTitle = (text, title) => UI.Ewp.EwpHost.EmitDialogEvent("message", text, title);
			PlatformInterop.ShowQuestion = (text, title) =>
			{
				UI.Ewp.EwpHost.EmitDialogEvent("question", text, title);
				return false;
			};
		}
		else if (CliDebugMode)
		{
			// CLI 模式：模态对话框转为 stdout 事件，避免无人值守卡死（ShowQuestion 一律应答 No）
			PlatformInterop.ShowMessage = text => UI.CliDebug.CliDebugHost.EmitDialogEvent("message", text, "");
			PlatformInterop.ShowMessageWithTitle = (text, title) => UI.CliDebug.CliDebugHost.EmitDialogEvent("message", text, title);
			PlatformInterop.ShowQuestion = (text, title) =>
			{
				UI.CliDebug.CliDebugHost.EmitDialogEvent("question", text, title);
				return false;
			};
		}
		else
		{
			PlatformInterop.ShowMessage = text => MessageBox.Show(text);
			PlatformInterop.ShowMessageWithTitle = (text, title) => MessageBox.Show(text, title);
			PlatformInterop.ShowQuestion = (text, title) => MessageBox.Show(text, title, MessageBoxButtons.YesNo) == DialogResult.Yes;
			// B+C（2026-10-27）：GUI 宿主的模态检测源＝ModalTimerGuard 深度计数器。
			// EWP/CliDebug/Headless 不注入 ⇒ 默认 () => false，行为与现状一致。
			PlatformInterop.IsModalOpen = () => Forms.ModalTimerGuard.IsModalOpen;
		}
		PlatformInterop.GetProductVersion = () => Application.ProductVersion;
		PlatformInterop.GetMousePosition = () => Cursor.Position;
		PlatformInterop.SetClipboardText = text => Clipboard.SetText(text);
		PlatformInterop.MeasureText = (text, fontName, fontSize, fontStyle) =>
		{
			using var font = new Font(fontName, fontSize, fontStyle);
			using var g = new Control().CreateGraphics();
			var size = g.MeasureString(text, font);
			return (size.Width, size.Height);
		};
		PlatformInterop.MeasureTextWidth = (text, fontName, fontSize, fontStyle) =>
		{
			using var font = new Font(fontName, fontSize, fontStyle);
			using var g = new Control().CreateGraphics();
			return g.MeasureString(text, font).Width;
		};
		PlatformInterop.DebugLog = msg => System.Diagnostics.Debug.WriteLine(msg);

		// 音频平台工厂：WinForms 使用 NAudioSound
		Runtime.Utils.Sound.Factory = () => new Runtime.Utils.NAudioSound();
		GlobalStatic.Bgm = Runtime.Utils.Sound.Factory();

		if (EwpHeadlessMode)
		{
			// N1-b：无窗宿主。不创建 MainWindow，队列泵驱动 console/EWP。
			UI.Headless.HeadlessHost.Run();
			return;
		}

		using var win = new Forms.MainWindow(args);
		{
			#region EM_私家版_Emuera多言語化改造
			win.TranslateUI();
			#endregion
			#region EM_私家版_Icon指定機能
			if (icon != null)
				win.SetupIcon(icon);
			#endregion

			#region CLI_Debug
			UI.CliDebug.CliDebugHost? cliHost = null;
			if (CliDebugMode)
			{
				// 保留 WinForms 消息循环与渲染管线，仅最小化隐藏窗口（非 headless，见设计文档 §3）
				win.ShowInTaskbar = false;
				win.WindowState = FormWindowState.Minimized;
				cliHost = new UI.CliDebug.CliDebugHost(win);
				cliHost.Start();
			}
			#endregion

			#region EWP_eraWire
			UI.Ewp.EwpHost ewpHost = null;
			if (EwpMode)
			{
				// 与 CliDebug 同法：保留消息循环，最小化窗口（非 headless）；B1 文本级 stdio 宿主
				win.ShowInTaskbar = false;
				win.WindowState = FormWindowState.Minimized;
				ewpHost = new UI.Ewp.EwpHost(win, EwpPort);
				ewpHost.Start();
			}
			#endregion

			Application.Run(win);
			cliHost?.NotifyProcessExit();
			ewpHost?.NotifyProcessExit();
			/* VVII版マージ前の起動処理
			MainWindow win = null;
			StartTime = WinmmTimer.TickCount;
			using (win = new MainWindow())
			{
				#region EM_私家版_Emuera多言語化改造
				win.TranslateUI();
				#endregion
				#region EM_私家版_Icon指定機能
				if (icon != null)
					win.SetupIcon(icon);
				#endregion
				Application.Run(win);
				Content.AppContents.UnloadContents();
				if (!Reboot)
					break;
				RebootWinState = win.WindowState;
				if (win.WindowState == FormWindowState.Normal)
				{
					RebootClientY = win.ClientSize.Height;
					RebootLocation = win.Location;
				}
				else
				{
					RebootClientY = 0;
					RebootLocation = new Point();
				}
			}
			//条件次第ではParserMediatorが空でない状態で再起動になる場合がある
			ParserMediator.ClearWarningList();
			ParserMediator.Initialize(null);
			GlobalStatic.Reset();
			//GC.Collect();
			#region EE_メモリリークの解決
			ConfigData.Instance.ReLoadConfig();

			break;
			*/
		}
		/*
		if (rebootFlag)
			Application.Restart();
		#endregion
		*/
	}


	static Program()
	{
		var baseDirectory = AppContext.BaseDirectory;
		if (Directory.Exists(Path.Combine(baseDirectory, "Data", "erb")))
		{
			baseDirectory = Path.Combine(baseDirectory, "Data");
		}
		SetDirPaths(baseDirectory);
	}
}
