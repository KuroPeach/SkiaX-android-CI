using MinorShift.Emuera.Forms;
using MinorShift.Emuera.GameView;
using MinorShift.Emuera.Runtime;
using MinorShift.Emuera.Runtime.Script.Statements;
using MinorShift.Emuera.Runtime.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace MinorShift.Emuera.UI.CliDebug;

/// <summary>
/// CLI 璋冭瘯瀹夸富锛?-CliDebug 鍚姩鍙傛暟鍚敤锛夈€?
/// stdin 鎺ユ敹 JSON Lines 鍛戒护锛宻tdout 鎺ㄩ€?JSON Lines 浜嬩欢锛屼緵澶栭儴 agent 椹卞姩瑙ｉ噴鍣ㄨ皟璇曘€?
/// 璁捐鏂囨。锛歴hared-trae/plans/plan-cli-debug-mode.md
///
/// 绾跨▼妯″瀷锛氫笓鐢?stdin 璇诲彇绾跨▼锛坆ackground锛夛紝鍛戒护缁?MainWindow.Invoke 娲惧彂鍒?UI 绾跨▼鎵ц锛?
/// stdout 缁忛潤鎬侀攣涓茶鍖栵紝浠绘剰绾跨▼鍙啓銆?
/// </summary>
internal sealed class CliDebugHost
{
	private readonly MainWindow win;
	private Thread stdinThread;
	private volatile bool running = true;

	/// <summary>鏈€杩戜竴娆?wait/buttons 浜嬩欢鏃剁紦瀛樼殑鍙€夋寜閽紙click 鍛戒护鐨勪笘浠ｆ牎楠屽熀鍑嗭級銆?/summary>
	private int cachedGeneration = -1;
	private List<(string Text, string Value, bool IsInteger)> cachedButtons = [];

	private static readonly object outLock = new();
	private static StreamWriter writer;

	public CliDebugHost(MainWindow window)
	{
		win = window;
	}

	/// <summary>鍒濆鍖?stdio 缂栫爜锛圲TF-8 鏃?BOM锛変笌 stdout writer銆侾rogram 瑙ｆ瀽鍙傛暟鍚庡敖鏃╄皟鐢ㄣ€?/summary>
	public static void InitIO()
	{
		var utf8 = new UTF8Encoding(false);
		// WinExe 无控制台时设置编码会抛 IOException（句柄无效）——各项独立容错，writer 必须尽量创建
		try { Console.InputEncoding = utf8; } catch { }
		try { Console.OutputEncoding = utf8; } catch { }
		try { writer = new StreamWriter(Console.OpenStandardOutput(), utf8) { AutoFlush = true }; }
		catch
		{
			// 无标准输出句柄时静默失败（全部 Emit 变为 no-op）
		}
	}

	/// <summary>杈撳嚭涓€琛?JSON锛堢嚎绋嬪畨鍏級銆傚涓诲疄渚嬪垱寤哄墠鐨勬棭鏈熶簨浠讹紙瀵硅瘽妗嗘浛浠ｇ瓑锛変篃璧拌繖閲屻€?/summary>
	public static void Emit(string json)
	{
		var w = writer;
		if (w == null)
			return;
		lock (outLock)
		{
			try { w.WriteLine(json); }
			catch { }
		}
	}

	/// <summary>妯℃€佸璇濇鏇夸唬锛欳LI 妯″紡涓?MessageBox 涓€寰嬭浆涓轰簨浠讹紝閬垮厤鏃犱汉鍊煎畧鍗℃銆?/summary>
	public static void EmitDialogEvent(string kind, string text, string title)
	{
		Emit("{\"type\":\"dialog\",\"dialog\":" + J(kind) + ",\"title\":" + J(title ?? "") + ",\"text\":" + J(text ?? "") + "}");
	}

	/// <summary>JSON 瀛楃涓茬紪鐮侊紙鍚袱绔紩鍙凤級銆?/summary>
	private static string J(string s) => JsonSerializer.Serialize(s ?? "");

	private static string InputTypeName(InputType t) => t switch
	{
		InputType.EnterKey => "EnterKey",
		InputType.AnyKey => "AnyKey",
		InputType.IntValue => "IntValue",
		InputType.StrValue => "StrValue",
		InputType.Void => "Void",
		InputType.AnyValue => "AnyValue",
		InputType.IntButton => "IntButton",
		InputType.StrButton => "StrButton",
		InputType.PrimitiveMouseKey => "PrimitiveMouseKey",
		_ => "Unknown",
	};

	private EmueraConsole GameConsole => GlobalStatic.Console;

	public void Start()
	{
		Emit("{\"type\":\"hello\",\"protocol\":1,\"version\":" + J(PlatformInterop.GetProductVersion()) + ",\"pid\":" + Environment.ProcessId + "}");
		stdinThread = new Thread(StdinLoop) { IsBackground = true, Name = "CliDebugHost stdin" };
		stdinThread.Start();
	}

	/// <summary>Application.Run 杩斿洖鍚庣敱 Program 璋冪敤锛氶€氱煡浼氳瘽缁撴潫骞跺仠鎺夎鍙栫嚎绋嬨€?/summary>
	public void NotifyProcessExit()
	{
		running = false;
		Emit("{\"type\":\"exit\"}");
	}

	private void StdinLoop()
	{
		// 绛夊緟绐楀彛鍙ユ焺涓?console 灏辩华锛坈onsole.Initialize 鍦?MainWindow.Shown 鍚庡紓姝ュ畬鎴愶級
		while (running && (!win.IsHandleCreated || GameConsole == null))
			Thread.Sleep(50);
		if (running)
			Subscribe();
		Emit("{\"type\":\"ready\"}");
		while (running)
		{
			string line;
			try { line = Console.In.ReadLine(); }
			catch { break; }
			if (line == null)
			{
				// stdin 琚?agent 鍏抽棴 鈫?浼橀泤閫€鍑烘父鎴?
				DispatchQuit();
				break;
			}
			if (line.Length == 0)
				continue;
			HandleLine(line);
		}
	}

	private void Subscribe()
	{
		var console = GameConsole;
		if (console == null)
			return;
		console.CliLineAdded += line =>
			Emit("{\"type\":\"out\",\"text\":" + J(ClipboardProcessor.StripHTML(line.ToString())) + "}");
		console.CliWaitInputEntered += OnWaitInput;
		console.CliTerminalStateEntered += OnTerminalState;
	}

	private void OnWaitInput()
	{
		var console = GameConsole;
		if (console == null || console.inputReq == null)
			return;
		var req = console.inputReq;
		var buttons = console.GetSelectableButtons();
		cachedButtons = buttons;
		cachedGeneration = console.LastButtonGeneration;
		var sb = new StringBuilder(256);
		sb.Append("{\"type\":\"wait\",\"inputType\":").Append(J(InputTypeName(req.InputType)))
			.Append(",\"needValue\":").Append(req.NeedValue ? "true" : "false")
			.Append(",\"oneInput\":").Append(req.OneInput ? "true" : "false");
		if (req.Timelimit > 0)
			sb.Append(",\"timeoutMs\":").Append(req.Timelimit);
		if (req.HasDefValue)
			sb.Append(",\"defaultInt\":").Append(req.DefIntValue)
				.Append(",\"defaultStr\":").Append(J(req.DefStrValue));
		AppendButtons(sb, buttons);
		sb.Append('}');
		Emit(sb.ToString());
	}

	private void OnTerminalState()
	{
		Emit("{\"type\":\"state\",\"state\":" + J(GameConsole?.CliStateName ?? "Unknown") + "}");
	}

	private static void AppendButtons(StringBuilder sb, List<(string Text, string Value, bool IsInteger)> buttons)
	{
		sb.Append(",\"buttons\":[");
		for (int i = 0; i < buttons.Count; i++)
		{
			if (i > 0)
				sb.Append(',');
			sb.Append("{\"index\":").Append(i)
				.Append(",\"text\":").Append(J(buttons[i].Text))
				.Append(",\"value\":").Append(J(buttons[i].Value))
				.Append(",\"isInteger\":").Append(buttons[i].IsInteger ? "true" : "false")
				.Append('}');
		}
		sb.Append(']');
	}

	private void HandleLine(string line)
	{
		// 渚挎嵎缂╁啓锛?琛ㄨ揪寮?鈫?debug 鍛戒护
		if (line.StartsWith('!'))
		{
			Dispatch(null, console => DoDebug(console, null, line[1..]));
			return;
		}
		JsonDocument doc;
		try { doc = JsonDocument.Parse(line); }
		catch { EmitError(null, "invalid JSON"); return; }
		using (doc)
		{
			var root = doc.RootElement;
			if (root.ValueKind != JsonValueKind.Object)
			{
				EmitError(null, "command must be a JSON object");
				return;
			}
			string cmd = root.TryGetProperty("cmd", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;
			// id 鍘熸牱鍥炴樉锛氭暟瀛楁寜鍘熸牱杈撳嚭锛屽瓧绗︿覆鎸?JSON 瀛楃涓茶緭鍑?
			string idJson = null;
			if (root.TryGetProperty("id", out var idEl))
				idJson = idEl.ValueKind == JsonValueKind.String ? J(idEl.GetString()) : idEl.GetRawText();
			if (string.IsNullOrEmpty(cmd))
			{
				EmitError(idJson, "missing \"cmd\"");
				return;
			}
			Dispatch(idJson, console => Execute(console, cmd, idJson, root.Clone()));
		}
	}

	private delegate void ConsoleAction(EmueraConsole console);

	private void Dispatch(string idJson, ConsoleAction action)
	{
		try
		{
			if (win.IsDisposed)
				return;
			win.Invoke(new Action(() =>
			{
				var console = GameConsole;
				if (console == null)
				{
					EmitError(idJson, "console not ready");
					return;
				}
				try { action(console); }
				catch (Exception ex) { EmitError(idJson, ex.GetType().Name + ": " + ex.Message); }
			}));
		}
		catch (Exception ex)
		{
			EmitError(idJson, "dispatch failed: " + ex.Message);
		}
	}

	private void Execute(EmueraConsole console, string cmd, string id, JsonElement root)
	{
		switch (cmd)
		{
			case "input": DoInput(console, id, root); break;
			case "click": DoClick(console, id, root); break;
			case "key": DoKey(console, id, root); break;
			case "debug":
				DoDebug(console, id, root.TryGetProperty("expr", out var e) && e.ValueKind == JsonValueKind.String ? e.GetString() : "");
				break;
			case "watch": DoWatch(id, root); break;
			case "trace":
				EmitResult(id, "\"trace\":" + J(console.GetDebugTraceLog(true)));
				break;
			case "screen": DoScreen(console, id, root); break;
			case "state": DoState(console, id); break;
			case "buttons":
				cachedButtons = console.GetSelectableButtons();
				cachedGeneration = console.LastButtonGeneration;
				var bsb = new StringBuilder(128);
				AppendButtons(bsb, cachedButtons);
				EmitResult(id, bsb.ToString()[1..]); // 鍘绘帀鍓嶅閫楀彿
				break;
			case "log": DoLog(console, id, root); break;
			case "quit": DoQuit(console, id); break;
			case "help":
				EmitResult(id, "\"commands\":[\"input\",\"click\",\"key\",\"debug\",\"watch\",\"trace\",\"screen\",\"state\",\"buttons\",\"log\",\"quit\",\"help\"]");
				break;
			default:
				EmitError(id, "unknown cmd: " + cmd);
				break;
		}
	}

	private void DoInput(EmueraConsole console, string id, JsonElement root)
	{
		string text = root.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : "";
		if (!console.IsWaitInputState)
		{
			EmitError(id, "not in input-wait state");
			return;
		}
		console.PressEnterKey(false, text ?? "", false);
		EmitResult(id, null);
	}

	private void DoKey(EmueraConsole console, string id, JsonElement root)
	{
		string key = root.TryGetProperty("key", out var k) && k.ValueKind == JsonValueKind.String ? k.GetString() : "enter";
		console.PressEnterKey(key == "skip", "", false);
		EmitResult(id, null);
	}

	private void DoClick(EmueraConsole console, string id, JsonElement root)
	{
		if (!root.TryGetProperty("index", out var idxEl) || !idxEl.TryGetInt32(out int idx))
		{
			EmitError(id, "missing int \"index\"");
			return;
		}
		if (!console.IsWaitInputState)
		{
			EmitError(id, "not in input-wait state");
			return;
		}
		if (cachedButtons.Count == 0 || console.LastButtonGeneration != cachedGeneration)
		{
			EmitError(id, "button cache stale or empty; issue \"buttons\" to refresh");
			return;
		}
		if (idx < 0 || idx >= cachedButtons.Count)
		{
			EmitError(id, "index out of range: " + idx);
			return;
		}
		// changedByMouse=true锛氫笌 GUI 榧犳爣鐐瑰嚮鍚岃涔夛紝鎸夐挳鍊兼寜瀛楅潰鎻愪氦锛堜笉鍋氬畯灞曞紑/鎹㈣鎷嗗垎锛?
		console.PressEnterKey(false, cachedButtons[idx].Value, true);
		EmitResult(id, null);
	}

	private void DoDebug(EmueraConsole console, string id, string expr)
	{
		// 涓?DebugDialog 鎺у埗鍙伴〉鍚屾锛氭崟鑾?DebugConsoleLog 澧為噺浣滀负鏈杈撳嚭
		int before = console.DebugConsoleLog.Length;
		console.DebugCommand(expr ?? "", false, true);
		string tail = console.DebugConsoleLog.Length > before ? console.DebugConsoleLog[before..] : "";
		EmitResult(id, "\"output\":" + J(tail));
	}

	private static void DoWatch(string id, JsonElement root)
	{
		string action = root.TryGetProperty("action", out var a) && a.ValueKind == JsonValueKind.String ? a.GetString() : "status";
		switch (action)
		{
			case "start":
				var vars = new List<string>();
				if (root.TryGetProperty("vars", out var v) && v.ValueKind == JsonValueKind.Array)
					foreach (var item in v.EnumerateArray())
						if (item.ValueKind == JsonValueKind.String)
							vars.Add(item.GetString());
				if (vars.Count == 0)
				{
					EmitError(id, "watch start requires non-empty \"vars\" array (e.g. [\"FLAG:0\", \"NAME:0\"])");
					return;
				}
				string path = Program.DebugDir + "var_write.log";
				VarWriteWatch.Start(path, vars);
				EmitResult(id, "\"watching\":" + (VarWriteWatch.IsEnabled ? "true" : "false") + ",\"logPath\":" + J(path));
				break;
			case "stop":
				string report = VarWriteWatch.Stop();
				EmitResult(id, "\"report\":" + J(report ?? "(not watching)"));
				break;
			default:
				EmitResult(id, "\"watching\":" + (VarWriteWatch.IsEnabled ? "true" : "false")
					+ ",\"logPath\":" + J(VarWriteWatch.LogPath ?? ""));
				break;
		}
	}

	private static void DoScreen(EmueraConsole console, string id, JsonElement root)
	{
		int lines = 30;
		if (root.TryGetProperty("lines", out var l) && l.TryGetInt32(out var n) && n > 0)
			lines = n;
		var displayLines = console.GetDisplayLines(lines);
		var sb = new StringBuilder(256);
		sb.Append("\"lines\":[");
		if (displayLines != null)
			for (int i = 0; i < displayLines.Length; i++)
			{
				if (i > 0)
					sb.Append(',');
				sb.Append(J(ClipboardProcessor.StripHTML(displayLines[i].ToString())));
			}
		sb.Append(']');
		EmitResult(id, sb.ToString());
	}

	private static void DoState(EmueraConsole console, string id)
	{
		var sb = new StringBuilder(128);
		sb.Append("\"state\":").Append(J(console.CliStateName))
			.Append(",\"isInProcess\":").Append(console.IsInProcess ? "true" : "false")
			.Append(",\"isWaitInput\":").Append(console.IsWaitInputState ? "true" : "false");
		if (console.IsWaitInputState && console.inputReq != null)
			sb.Append(",\"inputType\":").Append(J(InputTypeName(console.inputReq.InputType)));
		EmitResult(id, sb.ToString());
	}

	private static void DoLog(EmueraConsole console, string id, JsonElement root)
	{
		bool hideInfo = root.TryGetProperty("hideInfo", out var h) && h.ValueKind == JsonValueKind.True;
		string log = console.GetLog(hideInfo);
		if (root.TryGetProperty("lines", out var l) && l.TryGetInt32(out var n) && n > 0)
		{
			var arr = log.Split('\n');
			if (arr.Length > n)
				log = string.Join('\n', arr[^n..]);
		}
		EmitResult(id, "\"log\":" + J(log));
	}

	private void DoQuit(EmueraConsole console, string id)
	{
		EmitResult(id, null);
		console.Quit();
		win.Close();
	}

	private void DispatchQuit()
	{
		try
		{
			if (win.IsDisposed)
				return;
			win.Invoke(new Action(() =>
			{
				try
				{
					GameConsole?.Quit();
					win.Close();
				}
				catch { }
			}));
		}
		catch { }
	}

	private static void EmitResult(string idJson, string payload)
	{
		var sb = new StringBuilder(64);
		sb.Append("{\"type\":\"result\"");
		if (idJson != null)
			sb.Append(",\"id\":").Append(idJson);
		sb.Append(",\"ok\":true");
		if (!string.IsNullOrEmpty(payload))
			sb.Append(',').Append(payload);
		sb.Append('}');
		Emit(sb.ToString());
	}

	private static void EmitError(string idJson, string message)
	{
		var sb = new StringBuilder(64);
		sb.Append("{\"type\":\"result\"");
		if (idJson != null)
			sb.Append(",\"id\":").Append(idJson);
		sb.Append(",\"ok\":false,\"error\":").Append(J(message)).Append('}');
		Emit(sb.ToString());
	}
}
