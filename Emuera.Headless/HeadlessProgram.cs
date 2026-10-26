using MinorShift.Emuera.Runtime.Config;
using MinorShift.Emuera.Runtime.Config.JSON;
using MinorShift.Emuera.Runtime.Utils;
using MinorShift.Emuera.Runtime.Utils.EvilMask;
using MinorShift.Emuera.UI;
using MinorShift.Emuera.UI.Ewp;
using SkiaSharp;
using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;

namespace MinorShift.Emuera;

/// <summary>
/// 无 WinForms 的 EWP 入口（N1-c）：最小参数面 —ExeDir / --ewp-port / --ewp-json|--ewp-msgpack。
/// 配置/语言/字体加载与桌面 Program 同源；消息泵由 HeadlessHost 安装。
/// </summary>
internal static class HeadlessProgram
{
	private static int Main(string[] args)
	{
		SegmentCulture();
		Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

		string gameDir = null;
		int port = 0;
		bool json = false;
		for (int i = 0; i < args.Length; i++)
		{
			switch (args[i])
			{
				case "--ExeDir":
				case "-ExeDir":
					if (i + 1 < args.Length) gameDir = args[++i];
					break;
				case "--ewp-port":
				case "-ewp-port":
					if (i + 1 < args.Length && int.TryParse(args[++i], out var p)) port = p;
					break;
				case "--ewp-json":
				case "-ewp-json":
					json = true;
					break;
				case "--ewp-msgpack":
				case "-ewp-msgpack":
					json = false;
					break;
			}
		}

		Program.InitHeadless(gameDir ?? Directory.GetCurrentDirectory());
		Program.EwpMode = true;
		Program.EwpHeadlessMode = true;
		Program.EwpPort = port;
		Program.EwpMsgpack = !json;

		ConfigData.Instance.LoadConfig();
		JSONConfig.Load();
		Lang.LoadLanguageFiles();
		Lang.SetLanguage();

		LoadFonts();

		EwpOut.InitIO(deferWriter: port > 0, msgpack: !json);
		InitPlatformInterop();

		return UI.Headless.HeadlessHost.Run();
	}

	private static void SegmentCulture()
	{
		CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
		CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
	}

	private static void LoadFonts()
	{
		if (!Directory.Exists(Program.FontDir))
			return;
		foreach (var pattern in new[] { "*.ttf", "*.otf", "*.ttc" })
		{
			foreach (var file in Directory.GetFiles(Program.FontDir, pattern, SearchOption.AllDirectories))
			{
				try { GlobalStatic.Pfc.AddFontFile(file); } catch { }
			}
		}
	}

	private static void InitPlatformInterop()
	{
		PlatformInterop.ShowMessage = text => EwpHost.EmitDialogEvent("message", text, "");
		PlatformInterop.ShowMessageWithTitle = (text, title) => EwpHost.EmitDialogEvent("message", text, title);
		PlatformInterop.ShowQuestion = (text, title) =>
		{
			EwpHost.EmitDialogEvent("question", text, title);
			return false;
		};
		PlatformInterop.GetProductVersion = () =>
			typeof(HeadlessProgram).Assembly.GetName().Version?.ToString() ?? "0.0.0.0";
		PlatformInterop.GetMousePosition = () => Point.Empty;
		PlatformInterop.SetClipboardText = _ => { };
		PlatformInterop.DebugLog = msg => System.Diagnostics.Debug.WriteLine(msg);
		PlatformInterop.MeasureText = MeasureText;
		PlatformInterop.MeasureTextWidth = (text, fontName, fontSize, style) => MeasureText(text, fontName, fontSize, style).Width;

		Runtime.Utils.Sound.Factory = () => new Runtime.Utils.Sound();
		GlobalStatic.Bgm = Runtime.Utils.Sound.Factory();
	}

	private static (float Width, float Height) MeasureText(string text, string fontName, float fontSize, FontStyle style)
	{
		try
		{
			var tf = FontFactory.GetFallbackTypefaceForChar(' ', fontName) ?? SKTypeface.Default;
			using var font = new SKFont(tf, fontSize);
			using var paint = new SKPaint { Typeface = tf, TextSize = fontSize };
			var metrics = font.Metrics;
			return (paint.MeasureText(text ?? ""), -metrics.Ascent + metrics.Descent);
		}
		catch
		{
			return (0f, 0f);
		}
	}
}
