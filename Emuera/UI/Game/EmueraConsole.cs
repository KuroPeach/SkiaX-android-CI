//using System.Drawing.Imaging;
#if WINDOWS
using MinorShift.Emuera.Forms;
#endif
//using MinorShift.Emuera.GameData;
using MinorShift.Emuera.GameProc.Function;
using MinorShift.Emuera.Runtime;
using MinorShift.Emuera.Runtime.Script;

//using System.Diagnostics.Eventing.Reader;
//using System.Linq.Expressions;
//using System.Windows;
using MinorShift.Emuera.Runtime.Config;
using MinorShift.Emuera.Runtime.Config.JSON;
using MinorShift.Emuera.Runtime.Script.Parser;
using MinorShift.Emuera.Runtime.Script.Statements;
using MinorShift.Emuera.Runtime.Script.Statements.Expression;
using MinorShift.Emuera.Runtime.Utils;
using MinorShift.Emuera.Runtime.Utils.EvilMask;
using MinorShift.Emuera.UI;
using MinorShift.Emuera.UI.Game;
using MinorShift.Emuera.UI.Game.Image;
using MinorShift.Emuera.UI.Game.Rendering;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using trerror = MinorShift.Emuera.Runtime.Utils.EvilMask.Lang.Error;
using trmb = MinorShift.Emuera.Runtime.Utils.EvilMask.Lang.MessageBox;
using trsl = MinorShift.Emuera.Runtime.Utils.EvilMask.Lang.SystemLine;
using SkiaSharp;



namespace MinorShift.Emuera.GameView;

//入出力待ちの状況。
//難読化用属性。enum.ToString()やenum.Parse()を行うなら(Exclude=true)にすること。
[System.Reflection.Obfuscation(Exclude = false)]
internal enum ConsoleState
{
	Initializing = 0,
	Quit = 5,//QUIT
	Error = 6,//Exceptionによる強制終了
	Running = 7,
	WaitInput = 20,
	Sleep = 21,//DoEvents
	WaitInputNoFocus = 22,//尊尼获加_NF：不强制滚动的输入等待

	//WaitKey = 1,//WAIT
	//WaitSystemInteger = 2,//Systemが要求するInput
	//WaitInteger = 3,//INPUT
	//WaitString = 4,//INPUTS
	//WaitIntegerWithTimer = 8,
	//WaitStringWithTimer = 9,
	//Timeout = 10,
	//Timeouts = 11,
	//WaitKeyWithTimer = 12,
	//WaitKeyWithTimerF = 13,
	//WaitOneInteger = 14,
	//WaitOneString = 15,
	//WaitOneIntegerWithTimer = 16,
	//WaitOneStringWithTimer = 17,
	//WaitAnyKey = 18,

}

//難読化用属性。enum.ToString()やenum.Parse()を行うなら(Exclude=true)にすること。
[System.Reflection.Obfuscation(Exclude = false)]
internal enum ConsoleRedraw
{
	None = 0,
	Normal = 1,
}

internal sealed partial class EmueraConsole : IDisposable
{
	#region EmuEra-Rikaichan
	public Rikaichan rikaichan = new();
	#endregion

	//Bitmap Cache
	public const nint bitmapCacheArrayCap = 256;
	public ConsoleButtonString[] bitmapCacheArray = new ConsoleButtonString[bitmapCacheArrayCap];
	public nint bitmapCacheArrayIndex = 0;
	public bool bitmapCacheEnabledForNextLine;

	public bool strictFontFallback = false;

	public Color? TextBackgroundColor { get; set; }

	public EmueraConsole(IConsoleSurface parent)
	{
		window = parent;
		#region EE_AnchorのCB機能移植
		CBProc = new ClipboardProcessor(parent);
		#endregion

		//1.713 この段階でsetStBarを使用してはいけない
		//setStBar(StaticConfig.DrawLineString);
		state = ConsoleState.Initializing;
		if (Config.FPS > 0)
			msPerFrame = 1000 / (uint)Config.FPS;
		displayLineList = [];
		printBuffer = new PrintStringBuffer(this);

		genericTimer = new();
		genericTimer.Elapsed += tickTimer;
		genericTimer.Interval = 10;
		genericTimer.Enabled = false;
		CBG_Clear();//文字列描画用ダミー追加

		redrawTimer = new ConsoleTimer
		{
			Enabled = false//TODO:1824アニメ用再描画タイマー有効化関数の追加
		};
		redrawTimer.Tick += new EventHandler(tickRedrawTimer);
		redrawTimer.Interval = 10;
	}
	#region 1823 cbg関連
	private readonly List<ClientBackGroundImage> cbgList = [];
	private readonly ImageLayerManager _imageLayerManager = new();
	private GraphicsImage cbgButtonMap;
	private int selectingCBGButtonInt = -1;
	private int lastSelectingCBGButtonInt = -1;
	//ConsoleButtonString selectingButton = null;
	//ConsoleButtonString lastSelectingButton = null;

	sealed class ClientBackGroundImage : IComparable<ClientBackGroundImage>
	{
		/// <summary>
		/// zdepth == 0は文字列用ダミーなので他で使ってはいけない
		/// </summary>
		/// <param name="zdepth"></param>
		internal ClientBackGroundImage(int zdepth)
		{ this.zdepth = zdepth; }
		public ASprite Img;
		public ASprite ImgB;
		public int x;
		public int y;
		public int width;
		public int height;
		public readonly int zdepth;
		public bool isButton;
		public int buttonValue;
		public string tooltipString;
		public float Opacity = 1.0f;
		public float[] ColorMatrix = null;
		public int CompareTo(ClientBackGroundImage other)
		{
			if (other == null)
				return -1;
			//逆順でSort
			return -zdepth.CompareTo(other.zdepth);
		}
	}
	public void CBG_Clear()
	{
		foreach (ClientBackGroundImage cimg in cbgList)
		{
			//使い捨て無名Imageを一応disposeしておく
			if (cimg.Img != null && cimg.Img.Name.Length == 0)
				cimg.Img.Dispose();
		}
		cbgList.Clear();
		CBG_ClearBMap();
		cbgList.Add(new ClientBackGroundImage(0));
	}

	public void CBG_ClearRange(int zmin, int zmax)
	{
		if (zmin > zmax)
			return;
		for (int i = 0; i < cbgList.Count; i++)
		{
			ClientBackGroundImage cimg = cbgList[i];
			if (cimg.zdepth < zmin || cimg.zdepth > zmax || cimg.zdepth == 0)//0はダミーなので削除しない
				continue;

			//使い捨て無名Imageを一応disposeしておく
			if (cimg.Img != null && cimg.Img.Name.Length == 0)
				cimg.Img.Dispose();
			cbgList.RemoveAt(i);
			i--;
		}
	}

	public void CBG_ClearButton()
	{
		for (int i = 0; i < cbgList.Count; i++)
		{
			ClientBackGroundImage cimg = cbgList[i];
			if (!cimg.isButton)
				continue;

			//使い捨て無名Imageを一応disposeしておく
			if (cimg.Img != null && cimg.Img.Name.Length == 0)
				cimg.Img.Dispose();
			cbgList.RemoveAt(i);
			i--;
		}
		CBG_ClearBMap();
	}

	public void CBG_ClearBMap()
	{
		cbgButtonMap = null;
		selectingCBGButtonInt = -1;
		lastSelectingCBGButtonInt = -1;
	}

	public bool CBG_SetGraphics(GraphicsImage gra, int x, int y, int zdepth, int width = 0, int height = 0, float opacity = 1.0f, float[] colorMatrix = null)
	{
		if (gra == null || !gra.IsCreated)
			return false;
		return CBG_SetImage(new SpriteG("", gra, new Rectangle(0, 0, gra.Width, gra.Height)), x, y, zdepth, width, height, opacity, colorMatrix);
	}
	public bool CBG_SetImage(ASprite image, int x, int y, int zdepth, int width = 0, int height = 0, float opacity = 1.0f, float[] colorMatrix = null)
	{
		if (image == null || !image.IsCreated)
			return false;
		ArgumentOutOfRangeException.ThrowIfZero(zdepth);
		ClientBackGroundImage cbg = new(zdepth)
		{
			Img = image,
			x = x,
			y = y,
			width = width,
			height = height,
			Opacity = opacity,
			ColorMatrix = colorMatrix
		};
		cbgList.Add(cbg);
		cbgList.Sort();
		return true;
	}

	public bool CBG_SetButtonMap(GraphicsImage gra)
	{
		if (gra == null || !gra.IsCreated)
			return false;
		if (cbgButtonMap == gra)
			return false;
		cbgButtonMap = gra;
		selectingCBGButtonInt = -1;
		lastSelectingCBGButtonInt = -1;
		return true;
	}

	public bool CBG_SetButtonImage(int buttonValue, ASprite imageN, ASprite imageB, int x, int y, int zdepth, string tooltip = null)
	{
		ArgumentOutOfRangeException.ThrowIfZero(zdepth);
		ClientBackGroundImage cbg = new(zdepth)
		{
			Img = imageN,
			ImgB = imageB,
			x = x,
			y = y,
			//cbg.zdepth = zdepth;
			isButton = true,
			buttonValue = buttonValue,
			tooltipString = tooltip
		};
		cbgList.Add(cbg);
		cbgList.Sort();
		return true;
	}
	public int ClientWidth { get { return window.RenderWidth; } }
	public int ClientHeight { get { return window.RenderHeight; } }

	public void CBGSetImage(string spriteName)
	{
		var spr = AppContents.GetSprite(spriteName);
		if (spr == null || !spr.IsCreated) return;
		CBG_SetImage(spr, 0, 0, 1);
	}

	public void SetImageLayer(string spriteName, long depth, int x, int y,
		int width, int height, int opacity, float[]? colorMatrix, bool followScroll)
	{
		int currentScrollY = window.ScrollValue * Config.LineHeight;
		// EWP WireHost：仅在实际添加成功（sprite 存在）时通知；colormatrix 留 v1（A1-③）
		var layer = _imageLayerManager.SetLayer(spriteName, depth, x, y, width, height, opacity, colorMatrix, followScroll, currentScrollY);
		if (layer != null)
			CliImageLayerSet?.Invoke(layer);
	}

	public void ClearImageLayer(long depth)
	{
		_imageLayerManager.ClearLayer(depth);
		CliImageLayerCleared?.Invoke(depth);
	}

	public void ClearImageLayerAll()
	{
		_imageLayerManager.ClearAll();
		CliImageLayersCleared?.Invoke();
	}

	public bool ExistsImageLayer(long depth)
	{
		return _imageLayerManager.Exists(depth);
	}
	#endregion

	const string ErrorButtonsText = "__openFileWithDebug__";
	private readonly IConsoleSurface window;
	#region EE_MOUSEB
	public IConsoleSurface Window { get { return window; } }
	#endregion
	#region EE_BINPUT
	public List<ConsoleDisplayLine> DisplayLineList { get { return displayLineList; } }
	#endregion
	#region EE_AnchorのCB機能移植
	public readonly ClipboardProcessor CBProc;
	#endregion
	private List<KeyValuePair<long, ConsoleBackground>> backgroundList = [];
	private SKBitmap bakedBackground;

	GameProc.Process process;
	// ConsoleState state = ConsoleState.Initializing;
	#region EM_私家版_描画拡張
	ConsoleState _state = ConsoleState.Initializing;
	ConsoleState state
	{
		get { return _state; }
		set
		{
			switch (value)
			{
				case ConsoleState.Quit:
				case ConsoleState.Error:
					ConsoleEscapedParts.Clear();
					break;
			}
			_state = value;
			if (value == ConsoleState.Quit || value == ConsoleState.Error)
				CliTerminalStateEntered?.Invoke();
		}
	}
	#endregion
	public bool Enabled { get { return window.Created; } }

	/// <summary>
	/// 現在、Emueraがアクティブかどうか
	/// </summary>
	internal bool IsActive
	{ get { return !(window == null || !window.Created || !window.IsActive); } }

	/// <summary>
	/// スクリプトが継続中かどうか
	/// 入力系はメッセージスキップやマクロも含めてIsInProcessを参照すべき
	/// </summary>
	#region 尊尼获加_NF后缀
	/// <summary>WaitInput または WaitInputNoFocus 状态</summary>
	internal bool IsWaitInputState => state == ConsoleState.WaitInput || state == ConsoleState.WaitInputNoFocus;
	/// <summary>WaitInputNoFocus 状态（NF 等待，不强制滚动）</summary>
	internal bool IsWaitInputNoFocusState => state == ConsoleState.WaitInputNoFocus;
	#endregion
	internal bool IsRunning
	{
		get
		{
			if (state == ConsoleState.Initializing)
				return true;
			if (state == ConsoleState.WaitInput || state == ConsoleState.WaitInputNoFocus)
				return false;
			return state == ConsoleState.Running || runningERBfromMemory;
		}
	}
	#region EM_私家版_INPUT系機能拡張
	internal bool IsWaintingInputWithMouse
	{
		get
		{
			return IsWaitInputState && inputReq.MouseInput;
		}
	}
	#endregion
	internal bool IsInProcess
	{
		get
		{
			if (state == ConsoleState.Initializing)
				return true;
			if (state == ConsoleState.Sleep)
				return true;
			if (state == ConsoleState.WaitInput || state == ConsoleState.WaitInputNoFocus)
				return false;
			if (inProcess)
				return true;
			return state == ConsoleState.Running || runningERBfromMemory;
		}
	}

	internal bool IsError
	{
		get
		{
			return state == ConsoleState.Error;
		}
	}
	#region EE_連続FORCE_QUIT_AND_RESTAR対策
	internal bool IsWaitingEnterKey
	{
		get
		{
			if ((state == ConsoleState.Quit) || (state == ConsoleState.Error))
			{
				GlobalStatic.ForceQuitAndRestart = false;
				return true;
			}
			if (IsWaitInputState)
			{
				GlobalStatic.ForceQuitAndRestart = false;
				return inputReq.InputType == InputType.AnyKey || inputReq.InputType == InputType.EnterKey;
			}
			return false;
		}
	}

	internal bool IsWaitAnyKey
	{
		get
		{
			GlobalStatic.ForceQuitAndRestart = false;
			return IsWaitInputState && inputReq.InputType == InputType.AnyKey;
		}
	}
	#endregion
	internal bool IsWaintingOnePhrase
	{
		get
		{
			return IsWaitInputState && inputReq.OneInput;
		}
	}

	internal bool IsRunningTimer
	{
		get
		{
			return IsWaitInputState && inputReq.Timelimit > 0 && !isTimeout;
		}
	}

	internal bool IsWaitingPrimitive
	{
		get
		{
			if (IsWaitInputState)
				return inputReq.InputType == InputType.PrimitiveMouseKey;
			return false;
		}
	}

	internal string SelectedString
	{
		get
		{
			if (selectingButton == null)
				return null;
			if (state == ConsoleState.Error)
				return selectingButton.Inputs;
			if (!IsWaitInputState)
				return null;
			#region EE_BINPUT
			if ((inputReq.InputType == InputType.IntValue || inputReq.InputType == InputType.IntButton) && selectingButton.IsInteger)
				return selectingButton.Input.ToString();
			if (inputReq.InputType == InputType.StrValue || inputReq.InputType == InputType.StrButton)
				return selectingButton.Inputs;
			#endregion
			#region EE_INPUTANY
			if (inputReq.InputType == InputType.AnyValue && selectingButton.IsInteger)
				return selectingButton.Input.ToString();
			if (inputReq.InputType == InputType.AnyValue)
				return selectingButton.Inputs;
			#endregion
			return null;
		}
	}

	public async Task Initialize()
	{
		var boottimeDebugStopwatch = Stopwatch.StartNew();
		StreamWriter logWriter = null;
		try
		{
			if (Config.DisplayReport)
			{
				using var fs = new FileStream(Program.ExeDir + "time.log", FileMode.OpenOrCreate);
				logWriter = new StreamWriter(fs);
			}
		}
		catch
		{
			ParserMediator.Warn(trerror.TimeLogFileLocked.Text, null, 0);
		}
		logWriter?.WriteLine("Init:Start");
		logWriter?.WriteLine("File:Preload:Start");
		//必要なソースファイルを事前にメモリに一気に読み込む
		_genericTimerStopwatch.Restart();

		Preload.Clear();
		await Preload.Load(Program.ErbDir);
		await Preload.Load(Program.CsvDir);

		logWriter?.WriteLine("File:Preload:End " + boottimeDebugStopwatch.ElapsedMilliseconds + "ms");

		GlobalStatic.Console = this;
		// GlobalStatic.MainWindow = window;
		process = new GameProc.Process(this);
		GlobalStatic.Process = process;
		if (Program.DebugMode && Config.DebugShowWindow)
		{
			OpenDebugDialog();
			window.Focus();
		}
		ClearDisplay();
		if (!await process.Initialize(logWriter))
		{
			state = ConsoleState.Error;
			OutputLog(null, false);
			PrintFlush(false);
			RefreshStrings(true);
			return;
		}
		RunEmueraProgram("");
		RefreshStrings(true);

		logWriter?.WriteLine("Init:End " + boottimeDebugStopwatch.ElapsedMilliseconds + "ms");
	}


	public void Quit() { state = ConsoleState.Quit; }
	#region EE_FORCE_QUIT系
	public void ForceQuit()
	{

		if (GlobalStatic.ForceQuitAndRestart == true)
		{
			var result = PlatformInterop.ShowQuestion(trmb.ForceQuitAndRestart.Text,
				"FORCE_QUIT_AND_RESTART");
			if (result)
			{
				Program.rebootFlag = false;
				throw new CodeEE(trerror.ForceQuitAndRestartError.Text);
			}
		}
		if (Program.rebootFlag)
			window.Reboot();
		else
			window.Close();
		GlobalStatic.ForceQuitAndRestart = true;
		return;
	}
	#endregion

	public void ThrowTitleError(bool error)
	{
		state = ConsoleState.Error;
		notToTitle = true;
		byError = error;
	}
	public void ThrowError(bool playSound)
	{
		if (playSound)
			System.Media.SystemSounds.Hand.Play();
		forceUpdateGeneration();
		UseUserStyle = false;
		PrintFlush(false);
		RefreshStrings(false);
		state = ConsoleState.Error;
	}

	public bool notToTitle;
	public bool byError;
	//public ScriptPosition? ErrPos = null;

	#region button関連
	bool lastButtonIsInput = true;
	public bool updatedGeneration;
	int lastButtonGeneration;//最後に追加された選択肢の世代。これと世代が一致しない選択肢は選択できない。
	#region EE_BINPUT
	public int LastButtonGeneration { get { return lastButtonGeneration; } }
	#endregion
	int newButtonGeneration;//次に追加される選択肢の世代。Input又はInputsごとに増加
							//public int LastButtonGeneration { get { return lastButtonGeneration; } }
	public int NewButtonGeneration { get { return newButtonGeneration; } }
	public void UpdateGeneration() { lastButtonGeneration = newButtonGeneration; updatedGeneration = true; }
	public void forceUpdateGeneration() { newButtonGeneration++; lastButtonGeneration = newButtonGeneration; updatedGeneration = true; }
	LogicalLine lastInputLine;

	private void newGeneration()
	{
		//値の入力を求められない時は更新は必要ないはず
		if (!IsWaitInputState || !inputReq.NeedValue)
			return;
		if (!updatedGeneration && process.getCurrentLine != lastInputLine)
		{
			//ボタン無しで次の入力に来たなら強制で世代更新
			lastButtonGeneration = newButtonGeneration;
		}
		else
			updatedGeneration = false;
		lastInputLine = process.getCurrentLine;
		#region EE_BINPUT
		switch (inputReq.InputType)
		{
			//古い選択肢を選択できないように。INPUTで使った選択肢をINPUTSには流用できないように。
			case InputType.IntValue:
			case InputType.IntButton:
				if (lastButtonGeneration == newButtonGeneration)
					unchecked { newButtonGeneration++; }
				else if (!lastButtonIsInput)
					lastButtonGeneration = newButtonGeneration;
				lastButtonIsInput = true;
				break;
			case InputType.StrValue:
			#region EE_INPUTANY
			case InputType.AnyValue:
			#endregion
			case InputType.StrButton:
				if (lastButtonGeneration == newButtonGeneration)
					unchecked { newButtonGeneration++; }
				else if (lastButtonIsInput)
					lastButtonGeneration = newButtonGeneration;
				lastButtonIsInput = false;
				break;
		}
		#endregion
	}

	/// <summary>
	/// 選択中のボタン。INPUTやINPUTSに対応したものでなければならない
	/// </summary>
	ConsoleButtonString selectingButton;
	ConsoleButtonString lastSelectingButton;
	public ConsoleButtonString SelectingButton { get { return selectingButton; } }
	public bool ButtonIsSelected(ConsoleButtonString button) { return selectingButton == button; }
	public bool ButtonIsPointing(ConsoleButtonString button) { return pointingStrings.Contains(button); }

	/// <summary>
	/// ToolTip表示したフラグ
	/// </summary>
	bool tooltipUsed;
	/// <summary>
	/// マウスの直下にあるテキスト。ボタンであってもよい。
	/// ToolTip表示用。世代無視、履歴中も表示
	/// </summary>
	ConsoleButtonString pointingString;
	// pointingStrings记录鼠标下所有Button图像。当多个Button重叠时，被鼠标划到的图像都会变化
	HashSet<ConsoleButtonString> pointingStrings = [];
	#region EE_MOUSEB
	public ConsoleButtonString PointingSring { get { return pointingString; } }
	#endregion
	ConsoleButtonString lastPointingString;
	#endregion

	#region Input & Timer系

	//bool hasDefValue = false;
	//Int64 defNum;
	//string defStr;

	public InputRequest inputReq;
	#region EE_INPUT第二引数修正
	public InputType NowInputType { get { return inputReq.InputType; } }
	#endregion
	public void Await(int time)
	{
		if (!Enabled || state != ConsoleState.Running)
		{
			Quit();
			return;
		}
		RefreshStrings(true);
		state = ConsoleState.Sleep;
		process.UpdateCheckInfiniteLoopState();
		// Clear latches before DoEvents to prevent leakage from previous
		// input mode (INPUTS/TINPUTS) into AWAIT+GETKEYTRIGGERED loops.
		WinInput.ClearLatches();
		PlatformInterop.DoEvents();
		if (time > 0)
			System.Threading.Thread.Sleep(time);
		////DoEvents()の間にウインドウが閉じられたらおしまい。
		//if (!Enabled || state != ConsoleState.Sleep)
		//{
		//	ReadAnyKey();
		//	return;
		//}

		state = ConsoleState.Running;
	}

	#region 尊尼获加_SEQUENCEINPUT
	// 把 SEQUENCEINPUT 排队的整段字符串作为一次"用户按 Enter"提交，调用 PressEnterKey。
	// PressEnterKey 内部会做 parseInput 宏展开、\n 拆分、\e MesSkip 等全部处理。
	// for 循环会同步处理所有展开后的片段，每段喂入一个 ERB WaitInput。
	// 行为与 textbox 路径类似。
	private void SimulatePressEnter(InputRequest req)
	{
		string raw = process.sequenceInputValue ?? "";
		process.hasSequenceInput = false;
		process.sequenceInputValue = null;
		inputReq = req;
		state = ConsoleState.WaitInput;
		PressEnterKey(false, raw, false);
	}
	#endregion

	public void WaitInput(InputRequest req)
	{
		#region 尊尼获加_SEQUENCEINPUT
		if (process.hasSequenceInput)
		{
			SimulatePressEnter(req);
			return;
		}
		#endregion
		#region EE_AnchorのCB機能移植
		if (Config.CBUseClipboard)
			CBProc.Check(ClipboardProcessor.CBTriggers.InputWait);
		#endregion
		state = ConsoleState.WaitInput;
		inputReq = req;
		// 尊尼获加：非 NF 的 WaitInput 清除上滚标志和偏移量，恢复正常跟随滚动
		nfUserScrolledBack = false;
		nfScrollOffsetFromBottom = 0;
		// 强制滚动到底部，确保从 NF 状态退出后回到最新内容
		if (window.ScrollValue < window.ScrollMaximum)
		{
			window.TextBoxIgnoreScrollBarChanges = true;
			window.ScrollValue = window.ScrollMaximum;
			window.TextBoxIgnoreScrollBarChanges = false;
		}
		if (req.Timelimit > 0)
		{
			if (req.OneInput)
				window.UpdateLastInput();
			presetTimer();
			//				setTimer();
		}
		CliWaitInputEntered?.Invoke();
		//updateMousePosition();
		//Point point = window.MousePosition;
		//if (window.PicClientRectangle.Contains(point))
		//{
		//	PrintFlush(false);
		//	MoveMouse(point);
		//}
	}

	#region 尊尼获加_NF后缀
	// 用户在 NF 等待期间主动上滚的标志
	// wasAtBottom 无法区分"用户主动在底部"和"CLEARLINE 删除行使位置变成底部"
	// 所以需要显式记录用户的滚动意图
	private bool nfUserScrolledBack = false;
	// 用户上滚时保存的滚动偏移（从底部算起的距离）
	// CLEARLINE 会改变 Maximum，WinForms 不允许 Value > Maximum
	// 所以用偏移量在重绘后恢复用户的相对位置
	private int nfScrollOffsetFromBottom = 0;

	/// <summary>
	/// 用户滚动时更新 NF 上滚标志。由 MainWindow.vScrollBar_Scroll 调用。
	/// </summary>
	public void NotifyUserScrolled()
	{
		if (state == ConsoleState.WaitInputNoFocus)
		{
			nfUserScrolledBack = window.ScrollValue < window.ScrollMaximum;
			if (nfUserScrolledBack)
				nfScrollOffsetFromBottom = window.ScrollMaximum - window.ScrollValue;
		}
	}

	public void WaitInputNoFocus(InputRequest req)
	{
		#region 尊尼获加_SEQUENCEINPUT
		if (process.hasSequenceInput)
		{
			SimulatePressEnter(req);
			return;
		}
		#endregion
		if (Config.CBUseClipboard)
			CBProc.Check(ClipboardProcessor.CBTriggers.InputWait);
		// 尊尼获加：NF 上滚状态管理
		// 只有从上一个 WaitInputNoFocus 继承的 nfBack 才保留（动态地图循环场景）
		// 如果是从其他状态（Running/WaitInput）进入，清除 nfBack（demo 退出场景）
		// 因为 Running 期间的滚动由 wasAtBottom 逻辑处理，不需要 NF 机制
		if (nfUserScrolledBack)
		{
			// 检测内容是否被完全替换（如从 demo 退出到标题画面）
			if (displayLineList.Count < nfScrollOffsetFromBottom)
			{
				nfUserScrolledBack = false;
				nfScrollOffsetFromBottom = 0;
			}
			else if (window.ScrollValue < window.ScrollMaximum)
			{
				// 从上一个 WaitInputNoFocus 继承，更新偏移量
				nfScrollOffsetFromBottom = window.ScrollMaximum - window.ScrollValue;
			}
			// Val >= Max 且 nfBack=True：CLEARLINE 把 Value 拉到了 Max，保留 offset
		}
		// nfBack=False 时不设置，即使 Val < Max（可能是 AWAIT 期间的滚动）
		// 但需要确保 ScrollBar 在底部，否则 verticalScrollBarUpdate 的 wasAtBottom 会出错
		if (!nfUserScrolledBack && window.ScrollValue < window.ScrollMaximum)
		{
			window.TextBoxIgnoreScrollBarChanges = true;
			window.ScrollValue = window.ScrollMaximum;
			window.TextBoxIgnoreScrollBarChanges = false;
		}
		state = ConsoleState.WaitInputNoFocus;
		inputReq = req;
		// 重置无限循环检测计数器，避免 TINPUTSNF 循环中 lineCount 累积触发误报
		process.UpdateCheckInfiniteLoopState();
		// 尊尼获加_NF：像 AWAIT 一样在进入等待前刷新 UI，确保用户看到最新 PRINT 的内容
		RefreshStrings(true);
		if (req.Timelimit > 0)
		{
			if (req.OneInput)
				window.UpdateLastInput();
			presetTimer();
		}
		CliWaitInputEntered?.Invoke();
	}
	#endregion

	public void ReadAnyKey(bool anykey = false, bool stopMesskip = false)
	{
		#region EE_AnchorのCB機能移植
		if (Config.CBUseClipboard)
			CBProc.Check(ClipboardProcessor.CBTriggers.AnyKeyWait);
		#endregion
		InputRequest req = new();
		if (!anykey)
			req.InputType = InputType.EnterKey;
		else
			req.InputType = InputType.AnyKey;
		req.StopMesskip = stopMesskip;
		inputReq = req;
		state = ConsoleState.WaitInput;
		process.NeedWaitToEventComEnd = false;
		CliWaitInputEntered?.Invoke();
	}


	public void AddBackgroundImage(string name, long depth, float opacity)
	{
		var spr = AppContents.GetSprite(name);
		if (spr == null || !spr.IsCreated)
		{
			return;
		}
		var bg = new ConsoleBackground(spr, opacity);
		var pair = new KeyValuePair<long, ConsoleBackground>(depth, bg);
		backgroundList.Add(pair);
		backgroundList.Sort((v1, v2) => (v1.Key >= v2.Key) ? -1 : 1);
		BakeBackground();
	}
	public void ClearBackgroundImage()
	{
		backgroundList.Clear();
		BakeBackground();
	}

	public void RemoveBackground(string key)
	{
		backgroundList.RemoveAt(backgroundList.FindIndex((v) => v.Value.bgImage.Name == key));
		BakeBackground();
	}
	public void ValidateBackground(int width, int height)
	{
		if (bakedBackground == null)
		{
			bakedBackground = new SKBitmap(width, height);
			BakeBackground();
		}
		else if (bakedBackground.Width != width || bakedBackground.Height != height)
		{
			bakedBackground.Dispose();
			bakedBackground = new SKBitmap(width, height);
			BakeBackground();
		}
	}

	public void InvalidateBackgroundCache()
	{
		if (bakedBackground != null)
		{
			bakedBackground.Dispose();
			bakedBackground = null;
		}
	}

	public void ForceFullRedraw()
	{
		InvalidateBackgroundCache();
		window?.Invalidate();
		window?.InvalidatePicBox();
	}
	private void BakeBackground()
	{
		if (bakedBackground == null)
		{
			return;
		}
		var graph = new SKCanvas(bakedBackground);
		graph.Clear(Color.Transparent.ToSKColor());
		foreach (var pair in backgroundList)
		{
			var bg = pair.Value.bgImage;
			var scaleW = bakedBackground.Width / (float)bg.DestBaseSize.Width;
			var scaleH = bakedBackground.Height / (float)bg.DestBaseSize.Height;
			var cropHorizontally = bg.DestBaseSize.Height * scaleW < bakedBackground.Height;
			var newWidth = bg.DestBaseSize.Width * (cropHorizontally ? scaleH : scaleW);
			var newHeight = bg.DestBaseSize.Height * (cropHorizontally ? scaleH : scaleW);
			var paddingX = (int)((bakedBackground.Width - newWidth) / 2);
			var filter = pair.Value.GetColorFilter();
			bg.GraphicsDraw(graph, new Rectangle(paddingX, 0, (int)newWidth, (int)newHeight), filter);
			filter?.Dispose();
		}
	}
	/// <summary>
	/// INPUT中のアニメーション用タイマー
	/// </summary>
	ConsoleTimer redrawTimer;

	public int AnimeTimer => redrawTimer.Enabled ? (int)redrawTimer.Interval : 0;

	private void tickRedrawTimer(object sender, EventArgs e)
	{
		if (!redrawTimer.Enabled)
			return;
		//INPUT待ちでないとき、又はタイマー付きINPUT状態の場合はこれ以外の処理に任せる
		if (!IsWaitInputState || genericTimer.Enabled)
		{
			return;
		}
		window.Refresh();//OnPaint発行
	}

	/// <summary>
	/// アニメーション用タイマーの設定。0以下の値を指定するとタイマー停止
	/// </summary>
	public void setRedrawTimer(int tickcount)
	{
		if (tickcount <= 0)
		{
			redrawTimer.Enabled = false;
			return;
		}
		if (tickcount < 10)
			tickcount = 10;
		redrawTimer.Interval = tickcount;
		redrawTimer.Enabled = true;
	}



	System.Timers.Timer genericTimer = new();
	long timerID = -1;
	readonly Stopwatch _genericTimerStopwatch = new();//現在のタイマーを開始した時のミリ秒数（WinmmTimer.TickCount基準）
	long timer_endTime;//現在のタイマーを終了する時のTickCountミリ秒数
	bool isTimeout;
	long timeDisplayCount;
	bool inputed;
	public bool IsTimeOut { get { return isTimeout; } }

	/// <summary>
	/// 1824 TINPUT時に直接タイマーをセットせずに最初の再描画が終わってからタイマーをセットする（そうしないとTINPUTと再描画だけでループしてしまうので）
	/// </summary>
	bool need_settimer;

	private void presetTimer()
	{
		need_settimer = true;
		if (inputReq.DisplayTime)
		{
			var remainingMs = inputReq.Timelimit - _genericTimerStopwatch.ElapsedMilliseconds;
			PrintSingleLine(trsl.Remaining.Text + $"{remainingMs / 1000.0f:0.0}");
			timeDisplayCount = 0;
			inputed = false;
		}
	}
	private void setTimer()
	{
		isTimeout = false;
		timerID = inputReq.ID;
		genericTimer.Enabled = true;
		_genericTimerStopwatch.Restart();
		timer_endTime = inputReq.Timelimit;
	}

	//汎用
	private void tickTimer(object sender, EventArgs e)
	{
		if (!genericTimer.Enabled)
			return;
		if (!IsWaitInputState || inputReq.Timelimit <= 0 || timerID != inputReq.ID)
		{
			stopTimer();
			return;
		}
		var elapsedMs = _genericTimerStopwatch.ElapsedMilliseconds;
		if (elapsedMs >= timer_endTime)
		{
			endTimer();
			return;
		}

		if (inputReq.DisplayTime)
		{
			var remainingMs = inputReq.Timelimit - _genericTimerStopwatch.ElapsedMilliseconds;
			timeDisplayCount++;
			if (timeDisplayCount%10 == 0 && !inputed)
				window.Invoke(() => changeLastLine(trsl.Remaining.Text + $"{remainingMs / 1000.0f:0.0}"));
		}
	}

	private void stopTimer()
	{
		//if (state == ConsoleState.WaitKeyWithTimerF && countTime < timeLimit)
		//{
		//	wait_timeout = true;
		//	while (countTime < timeLimit)
		//	{
		//		PlatformInterop.DoEvents();
		//	}
		//	wait_timeout = false;
		//}
		genericTimer.Enabled = false;
		//timer.Dispose();
	}

	/// <summary>
	/// tickTimerからのみ呼ぶ
	/// </summary>
	private void endTimer()
	{
		stopTimer();
		isTimeout = true;
		if (IsWaitingPrimitive)
		{
			//callEmueraProgramは呼び出し先で行う。
			#region EE_INPUTMOUSEKEY拡張
			// InputMouseKey(4, 0, 0, 0, 0);
			InputMouseKey(4, 0, 0, 0, 0, 0);
			if (IsWaitInputState && inputReq.NeedValue)
			{
				Point point = window.MousePosition;
				if (window.PicClientRectangle.Contains(point))
					MoveMouse(point);
			}
			RefreshStrings(true);
			#endregion
			return;
		}
		if (inputReq.DisplayTime)
			changeLastLine(inputReq.TimeUpMes);
		else if (inputReq.TimeUpMes != null)
			PrintSingleLine(inputReq.TimeUpMes);
		window.Invoke(() =>
		{
			RunEmueraProgram("");//ディフォルト入力の処理はcallEmueraProgram側で
			if (IsWaitInputState && inputReq.NeedValue)
			{
				window.Invoke(() =>
				{
					Point point = window.MousePosition;
					if (window.PicClientRectangle.Contains(point))
						MoveMouse(point);
				});
			}
			RefreshStrings(true);
		});
	}

	public void forceStopTimer()
	{
		if (genericTimer.Enabled)
		{
			genericTimer.Enabled = false;
		}
	}

	/// <summary>
	/// 暂停定时器并返回是否正在运行，供 ShowConfigDialog 等模态对话框使用
	/// </summary>
	public bool PauseTimer()
	{
		bool wasRunning = genericTimer.Enabled;
		if (wasRunning)
			genericTimer.Enabled = false;
		return wasRunning;
	}

	/// <summary>
	/// 恢复定时器（仅当 wasRunning 为 true 时恢复），重置计时起点避免暂停期间累积超时
	/// </summary>
	public void ResumeTimer(bool wasRunning)
	{
		if (wasRunning && IsWaitInputState && inputReq.Timelimit > 0)
		{
			_genericTimerStopwatch.Restart();
			genericTimer.Enabled = true;
		}
	}
	#endregion

	#region Call系
	/// <summary>
	/// スクリプト実行。RefreshStringsはしないので呼び出し側がすること
	/// </summary>
	/// <param name="input"></param>
	private void RunEmueraProgram(string input)
	{
		//入力文字列の表示処理を行わない場合はstr == null
		if (input != null)
		{
			//INPUT文字列をPRINTする処理など
			if (!doInputToEmueraProgram(input))
				return;
			if (state == ConsoleState.Error)
				return;
		}
		// 尊尼获加：超时恢复执行时不清除 NF 上滚标志
		// nfUserScrolledBack 在 WaitInput（非 NF）中清除
		// 因为超时后的 ERB 代码（CLEARLINE + redraw + TINPUTSNF）仍在 NF 上下文中
		state = ConsoleState.Running;
		process.DoScript();
		if (state == ConsoleState.Running)
		{//RunningならProcessは処理を継続するべき
			state = ConsoleState.Error;
			PrintError(trerror.ProgramStatusError.Text);
		}
		#region EE_OUTPUTLOG
		if (state == ConsoleState.Error && !noOutputLog)
			//OutputLog(Program.ExeDir + "emuera.log");
			OutputSystemLog(Program.ExeDir + "emuera.log");
		#endregion

		PrintFlush(false);
		//1819 Refreshは呼び出し側で行う
		//RefreshStrings(false);
		newGeneration();
	}

	private bool doInputToEmueraProgram(string str)
	{
		if (IsWaitInputState)
		{
			long inputValue;
			List<AConsoleDisplayNode> ep;

			switch (inputReq.InputType)
			{
				case InputType.IntValue:
					if (string.IsNullOrEmpty(str) && inputReq.HasDefValue && !IsRunningTimer)
					{
						inputValue = inputReq.DefIntValue;
						str = inputValue.ToString();
					}
					else if (!long.TryParse(str, out inputValue))
						return false;
					if (inputReq.IsSystemInput)
						process.InputSystemInteger(inputValue);
					else
						process.InputInteger(inputValue);
					break;
				#region EE_BINPUT
				case InputType.IntButton:
					if (string.IsNullOrEmpty(str) && inputReq.HasDefValue && !IsRunningTimer)
					{
						inputValue = inputReq.DefIntValue;
						str = inputValue.ToString();
					}
					else if (!long.TryParse(str, out inputValue))
						return false;
					foreach (ConsoleDisplayLine line in Enumerable.Reverse(displayLineList).ToList())
					{

						foreach (ConsoleButtonString button in line.Buttons)
						{
							if (button.IsInteger && button.Generation == lastButtonGeneration && button.Input == inputValue)
							{
								process.InputInteger(inputValue);
								goto loopendint;
							}
							//後ろから回してるので世代が違うボタンに到達したらもう無い
							else if (button.Generation != 0 && button.Generation != lastButtonGeneration)
								goto loopepint;
						}
					}
				loopepint:
					foreach (var value in escapedParts)
					{
						ep = value.Value;
						foreach (var part in ep)
						{
							if (part is ConsoleDivPart div)
							{
								foreach (ConsoleDisplayLine line in Enumerable.Reverse(div.Children).ToList())
								{
									foreach (ConsoleButtonString button in line.Buttons)
									{
										if (button.IsInteger && button.Input == inputValue)
										{
											process.InputInteger(inputValue);
											goto loopendint;
										}
									}
								}
							}
						}
					}
					return false;
				loopendint:
					break;
				#endregion
				case InputType.StrValue:
					if (string.IsNullOrEmpty(str) && inputReq.HasDefValue && !IsRunningTimer)
						str = inputReq.DefStrValue;
					//空入力と時間切れ
					if (str == null)
						str = "";
					//SHOP等の数値を求められる場面用にFLOWINPUTでINPUTSにしててもRESULTを処理する
					if (inputReq.IsSystemInput)
						process.InputSystemInteger(inputReq.DefIntValue);
					process.InputString(str);
					break;
				#region EE_BINPUT
				case InputType.StrButton:
					if (string.IsNullOrEmpty(str) && inputReq.HasDefValue && !IsRunningTimer)
						str = inputReq.DefStrValue;
					//空入力と時間切れ
					if (str == null)
						str = "";
					foreach (ConsoleDisplayLine line in Enumerable.Reverse(displayLineList).ToList())
					{
						foreach (ConsoleButtonString button in line.Buttons)
						{
							if (button.Generation == lastButtonGeneration && (button.Input.ToString() == str || button.Inputs == str))
							{
								process.InputString(str);
								goto loopendstr;
							}
							//後ろから回してるので世代が違うボタンに到達したらもう無い
							else if (button.Generation != 0 && button.Generation != lastButtonGeneration)
								goto loopepstr;
						}
					}
				loopepstr:
					foreach (var value in escapedParts)
					{
						ep = value.Value;

						foreach (var part in ep)
						{
							if (part is ConsoleDivPart div)
							{
								foreach (ConsoleDisplayLine line in Enumerable.Reverse(div.Children).ToList())
								{
									foreach (ConsoleButtonString button in line.Buttons)
									{
										if ((button.IsInteger && button.Input.ToString() == str) || button.Inputs == str)
										{
											process.InputString(str);
											goto loopendint;
										}
									}
								}
							}
						}
					}
					return false;
				loopendstr:
					break;
				#endregion
				#region EE_INPUTANY
				case InputType.AnyValue:
					if (long.TryParse(str, out inputValue))
					{
						if (inputReq.IsSystemInput)
							process.InputSystemInteger(inputValue);
						else
							process.InputInteger(inputValue);
					}
					else
					{
						process.InputString(str);
					}
					break;
					#endregion

			}
			stopTimer();
		}
		Print(str);
		inputed = true;
		PrintFlush(false);
		#region EM_textbox位置指定拡張
		// 入力成功した
		if (window.TextBoxPosChanged)
			window.ResetTextBoxPos();
		#endregion
		return true;
	}
	#endregion

	#region 入力系
	readonly string[] spliter = ["\\n", "\r\n", "\n", "\r"];//本物の改行コードが来ることは無いはずだけど一応

	public bool MesSkip;
	private bool inProcess;
	volatile public bool KillMacro;

	internal void MouseWheel(Point point, int delta)
	{
		if (!IsWaitingPrimitive)
			return;
		//pointはクライアント左上基準の座標。
		//clientPointをクライアント左下基準の座標に置き換え
		Point clientPoint = point;
		clientPoint.Y = point.Y - ClientHeight;
		#region EE_INPUTMOUSEKEY拡張
		// InputMouseKey(2, delta, clientPoint.X, clientPoint.Y, 0);
		InputMouseKey(2, delta, clientPoint.X, clientPoint.Y, 0, 0);
		#endregion
	}

	internal void MouseDown(Point point, int button)
	{
		if (!IsWaitingPrimitive)
			return;
		//pointはクライアント左上基準の座標。
		//clientPointをクライアント左下基準の座標に置き換え
		Point clientPoint = point;
		clientPoint.Y = point.Y - ClientHeight;
		int buttonNum = -1;
		if (cbgButtonMap != null && cbgButtonMap.IsCreated)
		{
			//マップ画像の左上基準の座標に置き換え
			Point mapPoint = clientPoint;
			mapPoint.Y = clientPoint.Y + cbgButtonMap.Height;
			if (mapPoint.X >= 0 && mapPoint.Y >= 0 && mapPoint.X < cbgButtonMap.Width && mapPoint.Y < cbgButtonMap.Height)
			{
				Color c = cbgButtonMap.SKBitmap.GetPixel(mapPoint.X, mapPoint.Y).ToDrawingColor();
				if (c.A == 255)
				{
					buttonNum = c.ToArgb() & 0xFFFFFF;
				}
			}

		}
		#region EE_INPUTMOUSEKEY拡張
		// InputMouseKey(1, (int)button, clientPoint.X, clientPoint.Y, buttonNum);
		//ボタン押された場合にRESULT:5にボタンの値が代入される
		if (selectingButton != null)
		{
			// マスク色をRESULT:6にボタンの値が代入される
			if (!selectingButton.IsInteger)
			{
				GlobalStatic.VEvaluator.RESULTS = selectingButton.Inputs;
				InputMouseKey(1, button, clientPoint.X, clientPoint.Y, buttonNum, 0);
			}
			else
			{
				InputMouseKey(1, button, clientPoint.X, clientPoint.Y, buttonNum, selectingButton.Input);
			}
		}
		else
		{
			InputMouseKey(1, (int)button, clientPoint.X, clientPoint.Y, buttonNum, 0);
		}
		#endregion
	}

	//1823 Key入力を捕まえる（キー値は WinForms Keys の int 値。headless からは呼ばれない）
	internal void PressPrimitiveKey(int keycode, int keydata, int keymod)
	{
		if (IsWaitingPrimitive)
			#region EE_INPUTMOUSEKEY拡張
			// InputMouseKey(3, (int)keycode, (int)keydata, 0, 0);
			InputMouseKey(3, keycode, keydata, 0, 0, 0);
		#endregion
	}

	//1823 Key入力を捕まえる
	#region EE_INPUTMOUSEKEY拡張
	//internal void InputMouseKey(int type, int result1, int result2, int result3, int result4)
	internal void InputMouseKey(int type, int result1, int result2, int result3, int result4, long result5)
	{
		// emuera.InputResult5(type, result1, result2, result3, result4);
		process.InputResult5(type, result1, result2, result3, result4, result5);
		inProcess = true;
		try
		{
			//1823 Escキーもマクロも右クリックも不可。単純に押されたキーを送るのみ。
			RunEmueraProgram(null);
			if (IsWaitInputState && inputReq.NeedValue)
			{
				Point point = window.MousePosition;
				if (window.PicClientRectangle.Contains(point))
					MoveMouse(point);
			}
		}
		finally
		{
			inProcess = false;
		}
		RefreshStrings(true);
	}
	#endregion

	public void PressEnterKey(bool keySkip, string input, bool changedByMouse)
	{
		MesSkip = keySkip;
		if ((state == ConsoleState.Running) || (state == ConsoleState.Initializing))
			return;
		else if (state == ConsoleState.Quit)
		{
			if (Program.rebootFlag)
				window.Reboot();
			else
				window.Close();
			return;
		}
		else if (state == ConsoleState.Error)
		{
			if (Program.DebugMode)
               return;
			if (input == ErrorButtonsText && selectingButton != null && selectingButton.ErrPos != null)
			{
				OpenErrorFile(selectingButton.ErrPos);
				return;
			}
			window.Close();
			return;
		}
#if DEBUG
		if (!IsWaitInputState || inputReq == null)
			throw new ExeEE("");
#endif
		KillMacro = false;
		try
		{
			string[] text;
			if (changedByMouse || !process.inputMacroEnabled)//EE_SEQUENCEINPUT: inputMacroEnabled=false 时按字面整段喂入，不解析宏也不按 \n 拆分
			{ text = [input]; }
			else
			{
				//INPUTSでも"@"のみが弾かれないようにおまじない
				if (input.Length > 1 && !inputReq.OneInput && input.StartsWith('@'))
				{
					doSystemCommand(input);
					return;
				}
				if (inputReq.InputType == InputType.Void)
					return;
				if (genericTimer.Enabled &&
						(inputReq.InputType == InputType.AnyKey || inputReq.InputType == InputType.EnterKey))
					stopTimer();
				//if((inputReq.InputType == InputType.IntValue || inputReq.InputType == InputType.StrValue)
				if (input.Contains('(', StringComparison.Ordinal) && process.inputMacroEnabled)
					input = parseInput(new CharStream(input), false);
				text = input.Split(spliter, StringSplitOptions.None);
			}

			inProcess = true;
			//EE_SEQUENCEINPUT: inputMacroEnabled=false 时整段 1 段喂入，不解析宏、不按 \n 拆分、不处理 \e MesSkip
			if (!process.inputMacroEnabled)
			{
				RunEmueraProgram(input);
				RefreshStrings(false);
			}
			else
			{
				for (int i = 0; i < text.Length; i++)
				{
					string inputs = text[i];
					if (inputs.Contains("\\e", StringComparison.Ordinal))
					{
						inputs = inputs.Replace("\\e", "", StringComparison.Ordinal);//\eの除去
						MesSkip = true;
					}

					if (inputReq.OneInput && (!Config.AllowLongInputByMouse || !changedByMouse) && inputs.Length > 1)
						inputs = inputs.Remove(1);
					//1819 TODO:入力無効系（強制待ちTWAIT）でスキップとマクロを止めるかそのままか
					//現在はそのまま。強制待ち中はスキップの開始もできないのにスキップ中なら飛ばせる。
					if (inputReq.InputType == InputType.Void)
					{
						i--;
						inputs = "";
					}
					RunEmueraProgram(inputs);
					RefreshStrings(false);
					while (MesSkip && IsWaitInputState)
					{
						//TODO:入力無効を通していいか？スキップ停止をマクロでは飛ばせていいのか？
						if (inputReq.NeedValue)
							break;
						if (inputReq.StopMesskip)
							break;
						RunEmueraProgram("");
						RefreshStrings(false);
						//EscがマクロストップかつEscがスキップ開始だからEscでスキップを止められても即開始しちゃったりするからあんまり意味ないよね
						//if (KillMacro)
						//	goto endMacro;
					}
					MesSkip = false;
					if (!IsWaitInputState)
						break;
					//マクロループ時は待ち処理が起こらないのでここでシステムキューを捌く
					PlatformInterop.DoEvents();
#if DEBUG
					if (!IsWaitInputState || inputReq == null)
						throw new ExeEE("");
#endif
					if (KillMacro)
					{
						endMacro();
						return;
					}
				}
			}
		}
		finally
		{
			inProcess = false;
		}
		endMacro();

		void endMacro()
		{
			if (IsWaitInputState && inputReq.NeedValue)
			{
				Point point = window.MousePosition;
				if (window.PicClientRectangle.Contains(point))
					MoveMouse(point);
			}
			RefreshStrings(true);
		}
	}

	private void OpenErrorFile(ScriptPosition? pos)
	{
		ProcessStartInfo pInfo = new()
		{
			FileName = Config.TextEditor
		};
		var ignoreCaseCmp = StringComparison.OrdinalIgnoreCase;
		string fname = pos.Value.Filename.ToUpper(CultureInfo.InvariantCulture);
		if (fname.EndsWith(".CSV", ignoreCaseCmp))
		{
			if (fname.Contains(Program.CsvDir, ignoreCaseCmp))
				fname = fname.Replace(Program.CsvDir, "", ignoreCaseCmp);
			fname = Program.CsvDir + fname;
		}
		else
		{
			//解析モードの場合は見ているファイルがERB\の下にあるとは限らないかつフルパスを持っているのでこの補正はしなくてよい
			if (!Program.AnalysisMode)
			{
				if (fname.Contains(Program.ErbDir, ignoreCaseCmp))
					fname = fname.Replace(Program.ErbDir, "", ignoreCaseCmp);
				fname = Path.Combine(Program.ErbDir + fname);
			}
		}
		switch (Config.EditorType)
		{
			case TextEditorType.SAKURA:
				pInfo.Arguments = "-Y=" + pos.Value.LineNo.ToString() + " \"" + fname + "\"";
				break;
			case TextEditorType.TERAPAD:
				pInfo.Arguments = "/jl=" + pos.Value.LineNo.ToString() + " \"" + fname + "\"";
				break;
			case TextEditorType.EMEDITOR:
				pInfo.Arguments = "/l " + pos.Value.LineNo.ToString() + " \"" + fname + "\"";
				break;
			case TextEditorType.USER_SETTING:
				if (!string.IsNullOrEmpty(Config.EditorArg) && Config.EditorArg != null)
					pInfo.Arguments = Config.EditorArg + pos.Value.LineNo.ToString() + " \"" + fname + "\"";
				else
					pInfo.Arguments = fname;
				break;
		}
		try
		{
			Process.Start(pInfo);
		}
		catch (System.ComponentModel.Win32Exception)
		{
			System.Media.SystemSounds.Hand.Play();
			PrintError(trerror.FailedOpenEditor.Text);
			forceUpdateGeneration();
		}
		return;
	}

	static string parseInput(CharStream st, bool isNest)
	{
		StringBuilder sb = new(20);
		StringBuilder num = new(20);
		bool hasRet = false;
		int res;
		while (!st.EOS && (!isNest || st.Current != ')'))
		{
			if (st.Current == '(')
			{
				st.ShiftNext();
				string tstr = parseInput(st, true);

				if (!st.EOS)
				{
					st.ShiftNext();
					if (st.Current == '*')
					{
						st.ShiftNext();
						while (char.IsNumber(st.Current))
						{
							num.Append(st.Current);
							st.ShiftNext();
						}
						if (num.ToString() != "" && num.ToString() != null)
						{
							int.TryParse(num.ToString(), out res);
							for (int i = 0; i < res; i++)
								sb.Append(tstr);
							num.Remove(0, num.Length);
						}
					}
					else
						sb.Append(tstr);
					continue;
				}
				else
				{
					sb.Append(tstr);
					break;
				}
			}
			else if (st.Current == '\\')
			{
				st.ShiftNext();
				switch (st.Current)
				{
					case 'n':
						if (!hasRet)
							sb.Append('\n');
						else
							hasRet = false;
						break;
					case 'r':
						sb.Append('\r');
						break;
					case 'e':
						sb.Append("\\e\n");
						hasRet = true;
						break;
					case '\n':
						break;
					default:
						sb.Append(st.Current);
						break;
				}
			}
			else
				sb.Append(st.Current);
			st.ShiftNext();
		}
		return sb.ToString();
	}


	bool runningERBfromMemory;
	/// <summary>
	/// 通常コンソールからのDebugコマンド、及びデバッグウインドウの変数ウォッチなど、
	/// *.ERBファイルが存在しないスクリプトを実行中
	/// 1750 IsDebugから改名
	/// </summary>
	public bool RunERBFromMemory { get { return runningERBfromMemory; } set { runningERBfromMemory = value; } }
	void doSystemCommand(string command)
	{
		if (genericTimer.Enabled)
		{
			PrintError(trerror.CanNotInputTimerWait.Text);
			PrintError("");//タイマー表示処理に消されちゃうかもしれないので
			RefreshStrings(true);
			return;
		}
		if (IsInProcess)
		{
			PrintError(trerror.CanNotInputScriptRunning.Text);
			RefreshStrings(true);
			return;
		}
		StringComparison sc = Config.StringComparison;
		Print(command);
		PrintFlush(false);
		RefreshStrings(true);
		string com = command[1..];
		if (com.Length == 0)
			return;
		if (com.Equals("REBOOT", sc))
		{
			window.Reboot();
			return;
		}
		else if (com.Equals("OUTPUT", sc) || com.Equals("OUTPUTLOG", sc))
		{
			#region EE_OUTPUTLOG
			// this.OutputLog(Program.ExeDir + "emuera.log");
			OutputSystemLog(Program.ExeDir + "emuera.log");
			#endregion

			return;
		}
		else if (com.Equals("QUIT", sc) || com.Equals("EXIT", sc))
		{
			window.Close();
			return;
		}
		else if (com.Equals("CONFIG", sc))
		{
			window.ShowConfigDialog();
			return;
		}
		else if (com.Equals("DEBUG", sc))
		{
			if (!Program.DebugMode)
			{
				PrintError(trerror.CanNotUseDebugWindow.Text);
				RefreshStrings(true);
				return;
			}
			OpenDebugDialog();
		}
		else
		{
			if (!Config.UseDebugCommand)
			{
				PrintError(trerror.CanNotUseDebugCommand.Text);
				RefreshStrings(true);
				return;
			}
			//処理をDebugMode系へ移動
			DebugCommand(com, Config.ChangeMasterNameIfDebug, false);
			PrintFlush(false);
		}
		RefreshStrings(true);
	}
	#endregion

	#region 描画系
	Stopwatch _frameDeltaTimer = Stopwatch.StartNew();
	uint msPerFrame = 1000 / 60;//60FPS
	ConsoleRedraw redraw = ConsoleRedraw.Normal;
	public ConsoleRedraw Redraw { get { return redraw; } }
	public void SetRedraw(long i)
	{
		if ((i & 1) == 0)
			redraw = ConsoleRedraw.None;
		else
			redraw = ConsoleRedraw.Normal;
		if ((i & 2) != 0)
			RefreshStrings(true);
	}

	string debugTitle;
	public void SetWindowTitle(string str)
	{
		if (Program.DebugMode)
		{
			debugTitle = str;
			window.Text = str + " (Debug Mode)";
		}
		else
			window.Text = str;
	}

	public void SetEmueraVersionInfo(string str)
	{
		window.TextBoxText = str;
	}
	public string GetWindowTitle()
	{
		if (Program.DebugMode && debugTitle != null)
			return debugTitle;
		return window.Text;
	}

	/// <summary>
	/// 1818以前のRefreshStringsからselectingButton部分を抽出
	/// ここでOnPaintを発行
	/// </summary>
	public void RefreshStrings(bool force_Paint)
	{
		bool isBackLog = window.ScrollValue != window.ScrollMaximum;
		//ログ表示はREDRAWの設定に関係なく行うようにする
		if ((redraw == ConsoleRedraw.None) && (!force_Paint) && (!isBackLog))
			return;
		// 尊尼获加：NF 上滚时，跳过 Running 状态的中间渲染（CLEARLINE/PRINT 交替时）
		// 避免在两个行位置之间闪烁，只在 WaitInputNoFocus 的 force_Paint 时渲染
		if (nfUserScrolledBack && !force_Paint && state == ConsoleState.Running)
			return;
		//選択中ボタンの適性チェック
		if (selectingButton != null)
		{
			//履歴表示中は選択肢無効→画面外に出てしまったボタンも履歴から選択できるように
			//if (isBackLog)
			//	selectingButton = null;
			//数値か文字列の入力待ち状態でなければ無効
			if (state != ConsoleState.Error && !IsWaitInputState)
				selectingButton = null;
			else if (IsWaitInputState && !inputReq.NeedValue)
				selectingButton = null;
			//選択肢が最新でないなら無効
			else if (selectingButton.Generation != lastButtonGeneration)
				selectingButton = null;
		}
		if (!force_Paint)
		{//forceならば確実に再描画。
		 //履歴表示中でなく、最終行を表示済みであり、選択中ボタンが変更されていないなら更新不要
			if ((!isBackLog) && (lastDrawnLineNo == lineNo) && (lastSelectingButton == selectingButton))
				return;
			//まだ書き換えるタイミングでないなら次の更新を待ってみる
			//ただし、入力待ちなど、しばらく更新のタイミングがない場合には強制的に書き換えてみる
			if (_frameDeltaTimer.ElapsedMilliseconds < msPerFrame && (state == ConsoleState.Running || state == ConsoleState.Initializing))
				return;
		}
		if (forceTextBoxColor)
	{
		var sec = _genericTimerStopwatch.ElapsedMilliseconds;
		//色変化が速くなりすぎないように一定時間以内の再呼び出しは強制待ちにする
		if (_drawStopwatch == null)
		{
			_drawStopwatch = Stopwatch.StartNew();
		}
		else
		{
			while (_drawStopwatch.ElapsedMilliseconds < msPerFrame)
			{
				PlatformInterop.DoEvents();
			}
		}
		window.TextBoxBackColor = bgColor.ToDrawingColor();
		window.TextBoxForeColor = Config.ForeColor;
		try
		{
			window.SetTextBoxFont(Config.FontName, Config.FontSize);
		}
		catch { }

		_drawStopwatch.Restart();
	}
		window.Invoke(() =>
		{
			verticalScrollBarUpdate();
			window.Refresh();//OnPaint発行
			//window.MainPicBox.Refresh();
			// eraWire B3: 帧信号（EWP §8）——RefreshStrings 的有效通过点＝离屏像素流捕获点；订阅方自理异常
			try { CliFramePainted?.Invoke(); } catch { }
		});
	}

	/// <summary>eraWire B3: RefreshStrings 发出重绘时通知（UI 线程）。早退路径（redraw=None/无变化/帧限频）不会到达这里。</summary>
	public event Action CliFramePainted;

	#region EM_私家版_描画拡張
	Dictionary<int, List<AConsoleDisplayNode>> escapedParts;
	#endregion
	#region EE_BINPUT
	public Dictionary<int, List<AConsoleDisplayNode>> EscapedParts { get { return escapedParts; } }
	#endregion
	#region EM_私家版_imgマースク
	public int GetLinePointY(int lineNo)
	{
		int pointY = window.RenderHeight - Config.LineHeight;
		int bottomLineNo = window.ScrollValue - 1;
		if (displayLineList.Count - 1 < bottomLineNo)
			bottomLineNo = displayLineList.Count - 1;//1820 この処理不要な気がするけどエラー報告があったので入れとく
		pointY -= (bottomLineNo - lineNo) * Config.LineHeight;
		return pointY;
	}
	#endregion

	List<ConsoleDisplayLine> _htmlElementList = new(10);

	/// <summary>
	/// 1818以前のRefreshStringsの後半とm_RefreshStringsを融合
	/// 全面Clear法のみにしたのでさっぱりした。ダブルバッファリングはOnPaintが勝手にやるはず
	/// </summary>
	/// <param name="graph"></param>
	public void OnPaint(SKCanvas graph)
	{
		//デバッグ用。描画が超重い環境を想定1
		//System.Threading.Thread.Sleep(100);

		//描画中にEmueraが閉じられると廃棄されたPictureBoxにアクセスしてしまったりするので
		//OnPaintからgraphをもらった直後だから大丈夫だとは思うけど一応
		if (!Enabled)
			return;

		//1824 アニメスプライト用・現在フレームの時間を決定
		_frameDeltaTimer.Restart();

		bool isBackLog = window.ScrollValue != window.ScrollMaximum;
		int pointY = window.RenderHeight - Config.LineHeight;


		int bottomLineNo = window.ScrollValue - 1;
		int topLineNo = bottomLineNo - (pointY / Config.LineHeight + 1);
		if (topLineNo < 0)
			topLineNo = 0;
		pointY -= (bottomLineNo - topLineNo) * Config.LineHeight;
		if (Config.TextDrawingMode == TextDrawingMode.WINAPI)
		{
		}
		else
		{
			
			ValidateBackground((int)graph.LocalClipBounds.Width, (int)graph.LocalClipBounds.Height);
			graph.Clear(bgColor);
			if (bakedBackground != null)
			{
				graph.DrawBitmap(bakedBackground, 0, 0);
			}

			// Unified depth rendering: ImageLayer, CBG, escapedParts all share the same depth system
			//1823 cbg追加
			#region EM_私家版_描画拡張
			if (escapedParts == null) escapedParts = [];
			if (ConsoleEscapedParts.Changed || !ConsoleEscapedParts.TestedInRange(topLineNo, bottomLineNo, lastButtonGeneration))
				ConsoleEscapedParts.GetPartsInRange(topLineNo, bottomLineNo, lastButtonGeneration, escapedParts);
			var edepth = escapedParts.Keys.ToArray();
			Array.Sort(edepth, (int a, int b) => -a.CompareTo(b));
			var idepths = _imageLayerManager.GetDepths();

			// Merge all depth sources into a unified descending list
			var allDepths = new List<int>();
			{
				int ei = 0, ci = 0, ii = 0;
				while (ei < edepth.Length || ci < cbgList.Count || ii < idepths.Count)
				{
					int eVal = ei < edepth.Length ? edepth[ei] : int.MinValue;
					int cVal = ci < cbgList.Count ? cbgList[ci].zdepth : int.MinValue;
					int iVal = ii < idepths.Count ? idepths[ii] : int.MinValue;
					int depth = Math.Max(eVal, Math.Max(cVal, iVal));
					allDepths.Add(depth);
					if (ei < edepth.Length && edepth[ei] == depth) ei++;
					if (ci < cbgList.Count && cbgList[ci].zdepth == depth) ci++;
					if (ii < idepths.Count && idepths[ii] == depth) ii++;
				}
			}

			int eidx = 0, cidx = 0;
			int topPointY = pointY;
			foreach (var depth in allDepths)
			{
				// Draw ImageLayers at this depth
				_imageLayerManager.DrawLayersAtDepth(graph, (int)graph.LocalClipBounds.Width, (int)graph.LocalClipBounds.Height, window.ScrollValue * Config.LineHeight, depth);

				if (cidx < cbgList.Count && cbgList[cidx].zdepth == depth)
				{
					// 先にCBGを描画
					var cbg = cbgList[cidx];
					ASprite img = cbg.Img;
					if (cbg.isButton && cbg.buttonValue == selectingCBGButtonInt)
						img = cbg.ImgB;
					if (img != null && img.IsCreated)
					{
						try
						{
							int destW = cbg.width > 0 ? cbg.width : img.DestBaseSize.Width;
							int destH = cbg.height > 0 ? cbg.height : img.DestBaseSize.Height;
							var destRect = new Rectangle(cbg.x, cbg.y + window.RenderHeight - destH, destW, destH);
							SKColorFilter filter = null;
							if (cbg.ColorMatrix != null && cbg.ColorMatrix.Length == 20)
								filter = SKColorFilter.CreateColorMatrix(cbg.ColorMatrix);
							else if (cbg.Opacity < 1.0f)
								filter = SKColorFilter.CreateColorMatrix(new float[] { 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, cbg.Opacity, 0 });
							img.GraphicsDraw(graph, destRect, filter);
						}
						catch
						{
						}
					}
					cidx++;
				}
				if (depth == 0)
				{
					// 普通のパーツを描画
					for (int i = topLineNo;
					i <= bottomLineNo &&
					i < displayLineList.Count;//何処かで非同期にDisplayLineListを触ってるやつがいる気がする...
					i++)
					{
						try
						{
							displayLineList[i].DrawTo(graph, pointY, isBackLog, true, Config.TextDrawingMode);
						}
						catch
						{
						}
						pointY += Config.LineHeight;
					}
				}
				if (eidx < edepth.Length && edepth[eidx] == depth)
				{
					// 行範囲を超えたパーツを描画
					foreach (var p in escapedParts[edepth[eidx]])
					{
						var baseLineNo = p.Parent.ParentLine.LineNo;
						if (GlobalStatic.Console?.GetLineNo > Config.MaxLog)
						{
							var correction = GlobalStatic.Console.GetLineNo - Config.MaxLog;
							baseLineNo -= correction;
						}
						try
						{
							p.Parent.DrawPartTo(graph, p, topPointY + (baseLineNo - topLineNo) * Config.LineHeight, isBackLog, Config.TextDrawingMode);
						}
						catch
						{
						}
					}
					eidx++;
				}
			}
			#endregion
			//for (int j = 0; j < cbgList.Count; j++)
			//{
			//	if (cbgList[j].zdepth == 0)
			//	{
			//		//1823以前の文字列描画
			//		for (int i = topLineNo; i <= bottomLineNo; i++)
			//		{
			//			displayLineList[i].DrawTo(graph, pointY, isBackLog, true, Config.TextDrawingMode);
			//			pointY += Config.LineHeight;
			//		}
			//		continue;
			//	}
			//	ASprite img = cbgList[j].Img;
			//	if (cbgList[j].isButton && cbgList[j].buttonValue == selectingCBGButtonInt)
			//		img = cbgList[j].ImgB;
			//	if (img == null || !img.IsCreated)
			//		continue;
			//	img.GraphicsDraw(graph, new Point(cbgList[j].x, cbgList[j].y + window.RenderHeight - img.DestBaseSize.Height));
			//	//Bitmap bmp = img.Bitmap;
			//	//graph.DrawImage(bmp,
			//	//	new Rectangle(cbgList[j].x + img.DestBasePosition.X, window.RenderHeight - img.SrcRectangle.Height + cbgList[j].y + img.DestBasePosition.Y, img.SrcRectangle.Width, img.SrcRectangle.Height),
			//	//	img.SrcRectangle, GraphicsUnit.Pixel);
			//}

		}
		#region EmuEra-Rikaichan
		if (Config.RikaiEnabled)
		{
			try
			{
				rikaichan.OnPaint(graph, stringMeasure, window.RenderWidth);
			}
			catch
			{
			}
		}
		#endregion

		//真のHTML描画
		var y = 0;
		foreach (var element in _htmlElementList)
		{
			try
			{
				element.DrawTo(graph, y, false, false, Config.TextDrawingMode);
			}
			catch
			{
			}
			y += Config.LineHeight;
		}

		//ToolTip描画
		if (lastPointingString != pointingString || lastSelectingCBGButtonInt != selectingCBGButtonInt)
		{
			if (tooltipUsed)
				window.ToolTipRemoveAll();

			string title = null;
			if (pointingString != null)
				title = pointingString.Title;
			else if (selectingCBGButtonInt > 0)
			{
				foreach (var cbg in cbgList)
				{
					if (!cbg.isButton || cbg.buttonValue != selectingCBGButtonInt)
						continue;
					if (string.IsNullOrEmpty(cbg.tooltipString))
						continue;
					title = cbg.tooltipString;
					break;
				}
			}
			if (!string.IsNullOrEmpty(title))
			{
				title = title.Replace("<br>", Environment.NewLine);
				if (!window.ToolTipOwnerDraw && window.ToolTipInitialDelay == 0 && tooltip_duration == 0)
				{
					window.ToolTipSetText(title);
				}
				else
				{
				System.Threading.SynchronizationContext context = System.Threading.SynchronizationContext.Current;
				if (context != null)
				{
					Task.Run(async () =>
					{
						ConsoleButtonString savedPointingString = pointingString;
						if (window.ToolTipInitialDelay != 0)
							await Task.Delay(window.ToolTipInitialDelay);
						context.Post((state) =>
						{
							if (window.IsDisposed)
								return;

							MoveMouse(window.MousePosition);
							if (lastPointingString == savedPointingString)
							{
								Point mousePos = window.MousePosition;
								int cursorHeight = window.CursorHeight;
								Point p = new Point(mousePos.X + 2, mousePos.Y + cursorHeight / 2);
								Point absoluteP = window.ScreenMousePosition;
								Size screen = window.ScreenWorkingArea;
								if (absoluteP.Y + tooltip_size.Height > screen.Height)
									p.Y -= cursorHeight * 2;
								if (p.Y < 0)
									p.Y = 0;
								if (p.Y + tooltip_size.Height > screen.Height)
									tooltip_size = new Size(tooltip_size.Width, screen.Height - p.Y);
								window.ToolTipShow(title, p, tooltip_duration);
							}
						}, null);
					});
				}
				}
				tooltipUsed = true;
			}
			lastPointingString = pointingString;
			lastSelectingCBGButtonInt = selectingCBGButtonInt;
		}
		if (isBackLog)
			lastDrawnLineNo = -1;
		else
			lastDrawnLineNo = lineNo;
		lastSelectingButton = selectingButton;
		/*デバッグ用。描画が超重い環境を想定2
		System.Threading.Thread.Sleep(50);
		*/
		forceTextBoxColor = false;
		if (need_settimer)
		{
			need_settimer = false;
			setTimer();
		}
	}

	public void CustomToolTip(bool b)
	{
		window.SetCustomToolTip(b);
	}

	public void SetToolTipColor(Color foreColor, Color backColor)
	{
		window.ToolTipForeColor = foreColor;
		window.ToolTipBackColor = backColor;
	}
	public void SetToolTipDelay(int delay)
	{
		window.ToolTipInitialDelay = delay;
	}

	int tooltip_duration;
	Size tooltip_size;
	string tooltip_fontname = Config.FontName;
	long tooltip_fontsize = Config.FontSize;
	long tooltip_format;
	bool tooltip_img;

	// 桌面 MainWindow 的 ToolTip 自绘需要读这些值；headless 不绘制。
	internal bool TooltipImageRequested => tooltip_img;
	internal string TooltipFontName => tooltip_fontname;
	internal long TooltipFontSize => tooltip_fontsize;
	internal long TooltipFormat => tooltip_format;
	internal Size TooltipSize { get => tooltip_size; set => tooltip_size = value; }

	public void SetToolTipDuration(int duration)
	{
		tooltip_duration = duration;
		window.ToolTipAutoPopDelay = duration;
	}
	public void SetToolTipFontName(string fn)
	{
		tooltip_fontname = fn;
	}
	public void SetToolTipFontSize(long fs)
	{
		tooltip_fontsize = fs;
	}
	public void SetToolTipFormat(long f)
	{
		tooltip_format = f;
	}
	public void SetToolTipImg(bool b)
	{
		tooltip_img = b;
	}

	//private Graphics getGraphics()
	//{
	//	//消したいが怖いので残し
	//	if (!window.Created)
	//		throw new ExeEE("存在しないウィンドウにアクセスした");
	//	//if (Config.UseImageBuffer)
	//	//	return Graphics.FromImage(window.MainPicBox.Image);
	//	//else
	//		return window.MainPicBox.CreateGraphics();
	//}

	#endregion

	#region DebugMode系
#if WINDOWS
	DebugDialog dd;
	public DebugDialog DebugDialog { get { return dd; } }
#endif
	StringBuilder dConsoleLog = new("");
	public string DebugConsoleLog { get { return dConsoleLog.ToString(); } }
	List<string> dTraceLogList = [];
	public string GetDebugTraceLog(bool force)
	{
		//if (!dTraceLogChanged && !force)
		//	return null;
		StringBuilder builder = new("");
		LogicalLine line = process.GetScaningLine();
		builder.AppendLine(trsl.Processing.Text);
		if ((line == null) || (line.Position == null))
		{
			builder.AppendLine(trsl.FileNone.Text);
			builder.AppendLine(trsl.LineFuncNone.Text);
			builder.AppendLine("");
		}
		else
		{
			builder.AppendLine(string.Format(trsl.FileName.Text, line.Position.Value.Filename));
			builder.AppendLine(string.Format(trsl.LineFuncName.Text, line.Position.Value.LineNo.ToString(), line.ParentLabelLine.LabelName));
			builder.AppendLine("");
		}
		builder.AppendLine(trsl.FuncCallStack.Text);
		for (int i = dTraceLogList.Count - 1; i >= 0; i--)
		{
			builder.AppendLine(dTraceLogList[i]);
		}
		return builder.ToString();
	}
	public void OpenDebugDialog()
	{
#if WINDOWS
		if (!Program.DebugMode)
			return;
		if (dd != null)
		{
			if (dd.Created)
			{
				dd.Focus();
				return;
			}
			else
			{
				dd.Dispose();
				dd = null;
			}
		}
		dd = new DebugDialog();
		dd.SetParent(this, process);
		dd.TranslateUI();
		dd.Show();
#endif
	}

	public void DebugPrint(string str)
	{
		if (!Program.DebugMode)
			return;
		dConsoleLog.Append(str);
	}

	public void DebugClear()
	{
		dConsoleLog.Remove(0, dConsoleLog.Length);
	}

	public void DebugNewLine()
	{
		if (!Program.DebugMode)
			return;
		dConsoleLog.Append(Environment.NewLine);
	}

	public void DebugAddTraceLog(string str)
	{
		//Emueraがデバッグモードで起動されていないなら無視
		//ERBファイル以外のもの(デバッグコマンド、変数ウォッチ)を実行中なら無視
		if (!Program.DebugMode || runningERBfromMemory)
			return;
		dTraceLogList.Add(str);
	}
	public void DebugRemoveTraceLog()
	{
		if (!Program.DebugMode || runningERBfromMemory)
			return;
		if (dTraceLogList.Count > 0)
			dTraceLogList.RemoveAt(dTraceLogList.Count - 1);
	}
	public void DebugClearTraceLog()
	{
		if (!Program.DebugMode || runningERBfromMemory)
			return;
		dTraceLogList.Clear();
	}

	public void DebugCommand(string com, bool munchkin, bool outputDebugConsole)
	{
		ConsoleState temp_state = state;
		runningERBfromMemory = true;
		//スクリプト等が失敗した場合に備えて念のための保存
		GlobalStatic.Process.saveCurrentState(false);
		try
		{
			//デバッグコマンドはReadEnabledLineを通してないのでRename変換を入れる
			if (Config.UseRenameFile && (com.IndexOf("[[", StringComparison.Ordinal) >= 0) && (com.IndexOf("]]", StringComparison.Ordinal) >= 0))
			{
				foreach (KeyValuePair<string, string> pair in ParserMediator.RenameDic)
					com = com.Replace(pair.Key, pair.Value);
			}
			LogicalLine line = null;
			if (!com.StartsWith('@') && !com.StartsWith('"') && !com.StartsWith('\\'))
				line = LogicalLineParser.ParseLine(com, null);
			if (line == null || (line is InvalidLine))
			{
				WordCollection wc = LexicalAnalyzer.Analyse(new CharStream(com), LexEndWith.EoL, LexAnalyzeFlag.None);
				AExpression term = ExpressionParser.ReduceExpressionTerm(wc, TermEndWith.EoL);
				if (term == null)
					throw new CodeEE(trerror.CanNotInterpretedLine.Text);
				if (term.GetEraType() == EraType.Integer)
				{
					if (outputDebugConsole)
						com = "DEBUGPRINTFORML {" + com + "}";
					else
						com = "PRINTVL " + com;
				}
				else
				{
					if (outputDebugConsole)
						com = "DEBUGPRINTFORML %" + com + "%";
					else
						com = "PRINTFORMSL " + com;
				}
				line = LogicalLineParser.ParseLine(com, null);
			}
			if (line == null)
				throw new CodeEE(trerror.CanNotInterpretedLine.Text);
			if (line is InvalidLine)
				throw new CodeEE(line.ErrMes);
			if (!(line is InstructionLine))
				throw new CodeEE(trerror.InvalidDebugCommand.Text);
			InstructionLine func = (InstructionLine)line;
			if (func.Function.IsFlowContorol())
				throw new CodeEE(trerror.CanNotUseFlowInstruction.Text);
			//__METHOD_SAFE__をみるならいらないかも
			if (func.Function.IsWaitInput())
				throw new CodeEE(string.Format(trerror.CanNotUseInstruction.Text, func.Function.Name));
			//1750 __METHOD_SAFE__とほぼ条件同じだよねってことで
			if (!func.Function.IsMethodSafe())
				throw new CodeEE(string.Format(trerror.CanNotUseInstruction.Text, func.Function.Name));
			//1756 SIFの次に来てはいけないものはここでも不可。
			if (func.Function.IsPartial())
				throw new CodeEE(string.Format(trerror.CanNotUseInstruction.Text, func.Function.Name));
			switch (func.FunctionCode)
			{//取りこぼし
			 //逆にOUTPUTLOG、QUITはDebugCommandの前に捕まえる
				case FunctionCode.PUTFORM:
				case FunctionCode.UPCHECK:
				case FunctionCode.CUPCHECK:
				case FunctionCode.SAVEDATA:
					throw new CodeEE(string.Format(trerror.CanNotUseInstruction.Text, func.Function.Name));
			}
			ArgumentParser.SetArgumentTo(func);
			if (func.IsError)
				throw new CodeEE(func.ErrMes);
			process.DoDebugNormalFunction(func, munchkin);
			if (func.FunctionCode == FunctionCode.SET)
			{
				if (!outputDebugConsole)
					PrintSingleLine(com);
				//DebugWindowのほうは少しくどくなるのでいらないかな
			}
		}
		catch (Exception e)
		{
			if (outputDebugConsole)
			{
				DebugPrint(e.Message);
				DebugNewLine();
			}
			else
				PrintError(e.Message);
			process.clearMethodStack();
		}
		finally
		{
			//確実に元の状態に戻す
			GlobalStatic.Process.loadPrevState();
			runningERBfromMemory = false;
			state = temp_state;
		}
	}
	#endregion

	#region Window.Form系

	internal Point GetMousePosition()
	{
		if (window == null || !window.Created)
			return new Point();
		//クライアント左上基準の座標取得
		Point pos = window.MousePosition;
		//クライアント左下基準の座標に置き換え
		pos.Y -= ClientHeight;
		return pos;
	}
	#region EE_MOUSEB
	public bool AlwaysRefresh;
	#endregion

	#region EM_私家版_描画拡張
	int[] dummy = [0];
	#endregion
	/// <summary>
	/// マウス位置をボタンの選択状態に反映させる
	/// </summary>
	/// <param name="point"></param>
	/// <returns>この後でRefreshStringsが必要かどうか</returns>
	public bool MoveMouse(Point point)
	{
		#region EmuEra-Rikaichan
		int curLineY = -1;
		#endregion

		if (cbgButtonMap != null && cbgButtonMap.IsCreated)
		{
			//pointはクライアント左上基準の座標。
			//clientPointをクライアント左下基準の座標に置き換え
			Point clientPoint = point;
			clientPoint.Y = point.Y - ClientHeight;
			int buttonNum = -1;
			//マップ画像の左上基準の座標に置き換え
			Point mapPoint = clientPoint;
			mapPoint.Y = mapPoint.Y + cbgButtonMap.Height;
			if (mapPoint.X >= 0 && mapPoint.Y >= 0 && mapPoint.X < cbgButtonMap.Width && mapPoint.Y < cbgButtonMap.Height)
			{
				Color c = cbgButtonMap.SKBitmap.GetPixel(mapPoint.X, mapPoint.Y).ToDrawingColor();
				if (c.A == 255)
				{
					buttonNum = c.ToArgb() & 0xFFFFFF;
				}
			}
			if (buttonNum >= 0)
			{
				bool ret = pointingString != null || selectingButton != null || buttonNum != selectingCBGButtonInt;
				selectingCBGButtonInt = buttonNum;
				pointingString = null;
				pointingStrings.Clear();
				selectingButton = null;
				return ret;
			}
			else if (selectingCBGButtonInt >= 0)
			{
				selectingCBGButtonInt = -1;
				pointingString = null;
				pointingStrings.Clear();
				selectingButton = null;
				return true;
			}
		}
		selectingCBGButtonInt = -1;
		ConsoleButtonString select = null;
		ConsoleButtonString pointing = null;
		int prevPointingStringsLen = pointingStrings.Count;
		pointingStrings.Clear();
		bool firstPointngSelected = false;
		bool canSelect = false;
		//数値か文字列の入力待ち状態でなければ選択中にはならない
		if (state == ConsoleState.Error)
			canSelect = true;
		else if (IsWaitInputState && inputReq.NeedValue)
			canSelect = true;
		//スクリプト実行中は無視//入力・マクロ処理中は無視
		#region EE_MOUSEB
		if (IsInProcess && AlwaysRefresh == false)
			#endregion
			goto end;
		//履歴表示中は無視
		//if (window.ScrollValue != window.ScrollMaximum)
		//	goto end;
		int pointX = point.X;
		int pointY = point.Y;
		ConsoleDisplayLine curLine;

		int bottomLineNo = window.ScrollValue - 1;
		if (displayLineList.Count - 1 < bottomLineNo)
			bottomLineNo = displayLineList.Count - 1;//1820 この処理不要な気がするけどエラー報告があったので入れとく
		int topLineNo = bottomLineNo - (window.RenderHeight / Config.LineHeight);
		if (topLineNo < 0)
			topLineNo = 0;
		int relPointY = pointY - window.RenderHeight;
		//下から上へ探索し発見次第打ち切り
		#region EM_私家版_描画拡張
		if (ConsoleEscapedParts.Changed || !ConsoleEscapedParts.TestedInRange(topLineNo, bottomLineNo, lastButtonGeneration))
		{
			if (escapedParts == null) escapedParts = [];
			ConsoleEscapedParts.GetPartsInRange(topLineNo, bottomLineNo, lastButtonGeneration, escapedParts);
		}
		var edepth = escapedParts == null || escapedParts.Keys.Count == 0 ? dummy : escapedParts.Keys.ToArray();
		Array.Sort(edepth);
		int eidx = 0;
		bool zeroTested = false;
		var bottomLineBase = window.RenderHeight - Config.LineHeight;
		while (eidx < edepth.Length)
		{
			var depth = edepth[eidx];
			if (!zeroTested && depth >= 0)
			{
				depth = 0;
				zeroTested = true;
				// 普通のパーツのヒットテスト
				for (int i = bottomLineNo; i >= topLineNo; i--)
				{
					relPointY += Config.LineHeight;
					curLine = displayLineList[i];

					for (int b = 0; b < curLine.Buttons.Length; b++)
					{
						ConsoleButtonString button = curLine.Buttons[curLine.Buttons.Length - b - 1];
						if (button == null || button.StrArray == null)
							continue;
						if ((button.PointX <= pointX) && (button.PointX + button.Width >= pointX))
						{
							//if (relPointY >= 0 && relPointY <= Config.Config.FontSize)
							//{
							//	pointing = button;
							//	if(pointing.IsButton)
							//		goto breakfor;
							//}
							foreach (AConsoleDisplayNode part in button.StrArray)
							{
								if (part == null || part is ConsoleDivPart)
									continue;
								if ((part.PointX <= pointX) && (part.PointX + part.Width >= pointX)
									&& (relPointY >= part.Top) && (relPointY <= part.Bottom))
								{
									curLineY = window.RenderHeight - Config.LineHeight * (bottomLineNo - i + 1);
									if (!firstPointngSelected)
										pointing = button;
									if (button.IsButton)
									{
										if (!canSelect)
										{
											goto breakfor;
										}
										pointingStrings.Add(button);
										firstPointngSelected = true;
										break; //退出button.StrArray的for循环
									}
								}
							}
						}
					}
					if (firstPointngSelected && bottomLineNo - i > 100)
					{
						break;
					}
				}
			}
			if (eidx < edepth.Length && edepth[eidx] == depth)
			{
				// 行範囲を超えたパーツのヒットテスト
				var correction = lineNo > Config.MaxLog ? lineNo - Config.MaxLog : 0;
				if (depth != 0 || escapedParts.ContainsKey(depth))
					foreach (var part in escapedParts[depth])
					{
						if (part is ConsoleDivPart div)
						{
							// Y轴剪枝
							// 计算该 div 所在基础行的屏幕绝对 Y 坐标
							var lineY = bottomLineBase + (div.Parent.ParentLine.LineNo - bottomLineNo - correction) * Config.LineHeight;
							// div.Top 和 div.Bottom 是相对偏移量 (ypos 和 ypos+height)。
							// lineY + div.Top 即为该 div 在屏幕上的大致绝对 Y 坐标。
							// 考虑到 margin/padding 可能带来的额外偏移，上下各放宽 200px 的容错范围。
							if (pointY < lineY + div.Top - 200 || pointY > lineY + div.Bottom + 200)
								continue;

							var childPointing = div.TestChildHitbox(pointX, pointY, lineY);
							if (childPointing != null)
							{
								pointing = childPointing;
								if (pointing.IsButton)
									goto breakfor;
							}
						}
						else if ((part.PointX <= pointX) && (part.PointX + part.Width >= pointX)
							&& (relPointY >= part.Top) && (relPointY <= part.Bottom))
						{
							pointing = part.Parent;
							if (pointing.IsButton)
								goto breakfor;
						}
					}
				eidx++;
			}
		}
	#endregion


	//int posy_bottom2up = window.RenderHeight - pointY;
	//int logNum = window.ScrollMaximum - window.ScrollValue;
	////表示中の一番下の行番号
	//int curBottomLineNo = displayLineList.Count - logNum;
	//int curPointingLineNo = curBottomLineNo - (posy_bottom2up / Config.LineHeight + 1);
	//if ((curPointingLineNo < 0) || (curPointingLineNo >= displayLineList.Count))
	//	curLine = null;
	//else
	//	curLine =  displayLineList[curPointingLineNo];
	//if (curLine == null)
	//	goto end;

	//pointing = curLine.GetPointingButton(pointX);
	breakfor:
		#region EE_ボタン判定の改善
		if (pointingStrings.Count > 0)
		{
			foreach (var p in pointingStrings)
			{
				if ((p == null) || (p.Generation != lastButtonGeneration))
					continue;
				else if (!p.IsButton)
					continue;
				else if (IsWaitInputState && !p.IsInteger)
				{
					if ((inputReq.InputType == InputType.IntValue) || (inputReq.InputType == InputType.IntButton))
						continue;
				}
				pointing = p;
				goto end;
			}
			canSelect = false;
		}
		else
		{
			if ((pointing == null) || (pointing.Generation != lastButtonGeneration))
				canSelect = false;
			else if (!pointing.IsButton)
				canSelect = false;
			else if (IsWaitInputState && !pointing.IsInteger)
			{
				if ((inputReq.InputType == InputType.IntValue) || (inputReq.InputType == InputType.IntButton))
					canSelect = false;
			}
		}
	#endregion
	end:
		if (canSelect)
			select = pointing;
		bool needRefresh = select != selectingButton || pointing != pointingString || pointingStrings.Count != prevPointingStringsLen;
		pointingString = pointing;
		selectingButton = select;
		#region EmuEra-Rikaichan
		if (Config.RikaiEnabled && rikaichan.enabled)
		{
			//if (_pointingString != _lastPointingString && 
			if (pointing == null || pointing.StrArray.Length == 0) goto rikaichan_not_found;

			AConsoleDisplayNode cdp;
			ConsoleStyledString css;
			for (int first_subbutton_i = 0; first_subbutton_i < pointing.StrArray.Length; first_subbutton_i++)
			{
				cdp = pointing.StrArray[first_subbutton_i];
				if (cdp.rikaichaned)
				{
					css = cdp as ConsoleStyledString;
					if (css.PointX + css.Width > point.X)
					{
						goto rikaichan_found;
					}
				}
			}

			goto rikaichan_not_found;

		rikaichan_found:
			int xpos = point.X - css.PointX;
			//rikaichan.laststr_css = pointing; //LATER: do I even need this?
			//rikaichan.laststr = rikaichan.laststr_css.ToString();

			rikaichan.laststr = css.Text;
			if (css.NextLine != null)
			{
				rikaichan.laststr += css.NextLine.Text;
			}

			rikaichan.strpos = css.Ends.Length - 1;
			for (int i = css.Ends.Length - 1; i >= 0; i--)
			{
				if (css.Ends[i] < xpos) break;
				rikaichan.strpos = i;
			}

			if (pointingString == lastPointingString && rikaichan.strpos == rikaichan.laststrpos) goto rikaichan_end;

			rikaichan.css = css;
			rikaichan.point = point;
			rikaichan.curLineY = curLineY;

			rikaichan.hidden = false;
			rikaichan.laststrpos = rikaichan.strpos;
			//int show = 3;
			//int showmax = rikaichan.laststr.Length - rikaichan.strpos;
			//if (showmax < show) show = showmax;
			//rikaichan.output = rikaichan.laststr.Substring(rikaichan.strpos, show);
			rikaichan.output = rikaichan.laststr.Substring(rikaichan.strpos);

			//rikaichan.refresh_num++;
			//rikaichan.output = rikaichan.refresh_num.ToString();
			needRefresh = true;
			goto rikaichan_end;

		rikaichan_not_found:
			if (rikaichan.hidden == false)
			{
				rikaichan.hidden = true;
				needRefresh = true;
			}
		} //if rikaichan.enabled
	rikaichan_end:
		#endregion

		if (pointingStrings.Count!= 0)
			select = select;

		return needRefresh;
	}


	public void LeaveMouse()
	{
		bool needRefresh = selectingButton != null || pointingString != null;
		selectingButton = null;
		pointingString = null;
		pointingStrings.Clear();
		if (needRefresh)
		{
			RefreshStrings(true);
		}
	}

	#region EM_textbox位置指定拡張
	private void verticalScrollBarUpdate()
	{
		int max = displayLineList.Count;
		int move = max - window.ScrollMaximum;
		if (move == 0)
			return;
		window.TextBoxIgnoreScrollBarChanges = true;
		// 尊尼获加：NF 上滚时用偏移量保持用户相对位置
		// 始终更新 ScrollBar（包括 CLEARLINE），确保 Maximum 与 displayLineList 同步
		// 闪烁通过 RefreshStrings 中跳过 Running 状态的渲染来避免
		if (nfUserScrolledBack)
		{
			window.ScrollMaximum = max;
			int targetVal = max - nfScrollOffsetFromBottom;
			if (targetVal < 0) targetVal = 0;
			if (targetVal > max) targetVal = max;
			window.ScrollValue = targetVal;
			window.ScrollEnabled = max > 0;
		}
		else
		{
			bool wasAtBottom = window.ScrollValue >= window.ScrollMaximum;
			window.ScrollMaximum = max;
			if (wasAtBottom && move > 0)
				window.ScrollValue += move;
			else if (max < window.ScrollValue)
				window.ScrollValue = max;
			window.ScrollEnabled = max > 0;
		}
		window.TextBoxIgnoreScrollBarChanges = false;
	}
	#endregion
	#endregion



	public void GotoTitle()
	{
		//if (state == ConsoleState.Error)
		//{
		//    MessageBox.Show("エラー発生時はこの機能は使えません");
		//}
		forceStopTimer();
		ClearDisplay();
		//動的作成の分だけは削除する
		AppContents.UnloadGraphicList();
		redraw = ConsoleRedraw.Normal;
		UseUserStyle = false;
		userStyle = new StringStyle(Config.ForeColor, FontStyle.Regular, null);
		process.BeginTitle();
		ReadAnyKey(false, false);
		RunEmueraProgram("");
		RefreshStrings(true);
	}

	// Used by ctrlZ.
	public void GotoTitleAndLoadAndRepeatInput()
	{
		if (!Config.Ctrl_Z_Enabled) return;
		if (GlobalStatic.ctrlZ.mLastSave < 0) return;
		if (GlobalStatic.ctrlZ.mInputs.Count == 0) return;
		if (GlobalStatic.ctrlZ.mRewindInProgress)
		{
			GlobalStatic.ctrlZ.mRepeatedUndoRequested = true;
			return;
		}

	again:

		//GotoTitle
		forceStopTimer();
		ClearDisplay();
		//動的作成の分だけは削除する
		AppContents.UnloadGraphicList();
		redraw = ConsoleRedraw.Normal;
		UseUserStyle = false;
		userStyle = new StringStyle(Config.ForeColor, FontStyle.Regular, null);
		process.BeginTitle();
		ReadAnyKey(false, false);
		RunEmueraProgram("");
		RefreshStrings(true);

		//Load
		GlobalStatic.ctrlZ.mRewindInProgress = true;

		GlobalStatic.VEvaluator.Rand.SetRand(GlobalStatic.ctrlZ.mRandomSeed);

		GlobalStatic.Process.LoadSilent();

		PressEnterKey(true, GlobalStatic.ctrlZ.mLastSave.ToString(), false);

		//RepeatInput
		var inputs = GlobalStatic.ctrlZ.mInputs;
		if (inputs.Count > 0)
		{
			inputs.RemoveAt(inputs.Count - 1);
		}

		for (int i = 0; i < inputs.Count; i++)
		{
			if (GlobalStatic.ctrlZ.mRepeatedUndoRequested)
			{
				GlobalStatic.ctrlZ.mRepeatedUndoRequested = false;
				goto again;
			}

			PressEnterKey(true, inputs[i], false);
		}

		GlobalStatic.ctrlZ.mRewindInProgress = false;
		GlobalStatic.ctrlZ.mRepeatedUndoRequested = false;
		//^ because it's possible to leave it true overwise. I think.
	}

	bool force_temporary;
	bool timer_suspended;
	ConsoleState prevState;
	InputRequest prevReq;

	public async Task ReloadErb()
	{
		if (state == ConsoleState.Error)
		{
			PlatformInterop.ShowMessage(trerror.CanNotUseWhenError.Text);
			return;
		}
		if (state == ConsoleState.Initializing)
		{
			PlatformInterop.ShowMessage(trerror.CanNotUseWhenInitialize.Text);
			return;
		}
		bool notRedraw = false;
		if (redraw == ConsoleRedraw.None)
		{
			notRedraw = true;
			redraw = ConsoleRedraw.Normal;
		}
		if (genericTimer.Enabled)
		{
			genericTimer.Enabled = false;
			timer_suspended = true;
		}
		prevState = state;
		prevReq = inputReq;
		state = ConsoleState.Initializing;
		PrintSingleLine(trsl.ReloadingErb.Text, true);
		force_temporary = true;
		await process.ReloadErb();
		force_temporary = false;
		PrintSingleLine(trsl.ReloadCompleted.Text, true);
		RefreshStrings(true);
		//強制的にボタン世代が切り替わるのを防ぐ
		updatedGeneration = true;
		if (notRedraw)
			redraw = ConsoleRedraw.None;
	}

	public void ReloadErbFinished()
	{
		state = prevState;
		inputReq = prevReq;
		PrintSingleLine(" ");
		if (timer_suspended)
		{
			timer_suspended = false;
			genericTimer.Enabled = true;
			//タイマー待機中の時間ずれは修正しない。タイマー中にリロードしたらほぼ強制タイムアウトする程度は仕様のうちであろう。
		}
	}

	public async Task ReloadPartialErb(List<string> path)
	{
		if (state == ConsoleState.Error)
		{
			PlatformInterop.ShowMessage(trerror.CanNotUseWhenError.Text);
			return;
		}
		if (state == ConsoleState.Initializing)
		{
			PlatformInterop.ShowMessage(trerror.CanNotUseWhenInitialize.Text);
			return;
		}
		bool notRedraw = false;
		if (redraw == ConsoleRedraw.None)
		{
			notRedraw = true;
			redraw = ConsoleRedraw.Normal;
		}
		if (genericTimer.Enabled)
		{
			genericTimer.Enabled = false;
			timer_suspended = true;
		}
		prevState = state;
		prevReq = inputReq;
		state = ConsoleState.Initializing;
		PrintSingleLine(trsl.ReloadingErb.Text, true);
		force_temporary = true;
		await process.ReloadPartialErb(path);
		force_temporary = false;
		PrintSingleLine(trsl.ReloadCompleted.Text, true);
		RefreshStrings(true);
		//強制的にボタン世代が切り替わるのを防ぐ
		updatedGeneration = true;
		if (notRedraw)
			redraw = ConsoleRedraw.None;
	}

	public async Task ReloadFolder(string erbPath)
	{
		if (state == ConsoleState.Error)
		{
			PlatformInterop.ShowMessage(trerror.CanNotUseWhenError.Text);
			return;
		}
		if (state == ConsoleState.Initializing)
		{
			PlatformInterop.ShowMessage(trerror.CanNotUseWhenInitialize.Text);
			return;
		}
		if (genericTimer.Enabled)
		{
			genericTimer.Enabled = false;
			timer_suspended = true;
		}
		List<string> paths = [];
		SearchOption op = SearchOption.AllDirectories;
		if (!Config.SearchSubdirectory)
			op = SearchOption.TopDirectoryOnly;
		var fnames = Directory.EnumerateFiles(erbPath, "*.ERB", op);
		foreach (var fname in fnames)
		{
			if (Ascii.EqualsIgnoreCase(Path.GetExtension(fname), ".ERB"))
				paths.Add(fname);
		}
		bool notRedraw = false;
		if (redraw == ConsoleRedraw.None)
		{
			notRedraw = true;
			redraw = ConsoleRedraw.Normal;
		}
		prevState = state;
		prevReq = inputReq;
		state = ConsoleState.Initializing;
		PrintSingleLine(trsl.ReloadingErb.Text, true);
		force_temporary = true;
		await process.ReloadPartialErb(paths);
		force_temporary = false;
		PrintSingleLine(trsl.ReloadCompleted.Text, true);
		RefreshStrings(true);
		//強制的にボタン世代が切り替わるのを防ぐ
		updatedGeneration = true;
		if (notRedraw)
			redraw = ConsoleRedraw.None;
	}
	public void ReloadResource()
	{
		/*
		if (state == ConsoleState.Error)
		{
			PlatformInterop.ShowMessage(trerror.CanNotUseWhenError.Text);
			return;
		}
		if (state == ConsoleState.Initializing)
		{
			PlatformInterop.ShowMessage(trerror.CanNotUseWhenInitialize.Text);
			return;
		}
		if (genericTimer.Enabled)
		{
			genericTimer.Enabled = false;
			timer_suspended = true;
		}
		bool notRedraw = false;
		if (redraw == ConsoleRedraw.None)
		{
			notRedraw = true;
			redraw = ConsoleRedraw.Normal;
		}

		prevState = state;
		prevReq = inputReq;
		state = ConsoleState.Initializing;
		force_temporary = true;
		*/
		AppContents.LoadContents(true);
		//force_temporary = false;
		PrintSingleLine(trsl.ReloadResourceMessage.Text, true);
		/*
		RefreshStrings(true);
		//強制的にボタン世代が切り替わるのを防ぐ
		updatedGeneration = true;
		if (notRedraw)
			redraw = ConsoleRedraw.None;
		*/
	}

	#region CLI_Debug（--CliDebug モード用フック）
	// 设计文档: shared-trae/plans/plan-cli-debug-mode.md
	// 全部为新增并行订阅点：无订阅者时零开销，GUI 行为不变。

	/// <summary>新显示行产生时触发（addDisplayLine 内，与 CBProc.AddLine 同挂点）。</summary>
	public event Action<ConsoleDisplayLine> CliLineAdded;
	/// <summary>进入输入等待（WaitInput / WaitInputNoFocus / ReadAnyKey）时触发。</summary>
	public event Action CliWaitInputEntered;
	/// <summary>进入 Error / Quit 终态时触发。</summary>
	public event Action CliTerminalStateEntered;
	/// <summary>CLEARLINE 删除行后触发，参数＝请求删除的逻辑行数（WireHost 据此发 legacy.clear）。</summary>
	public event Action<int> CliLinesRemoved;
	/// <summary>ClearDisplay 全量清场前触发（WireHost 同步快照账本与频道状态）。</summary>
	public event Action CliDisplayCleared;
	/// <summary>EWP WireHost：SETIMAGELAYER(L) 实际添加图层后触发（事件携带实际 ImageLayer 对象；
	/// 快照导出必须用该对象，不再按名二次查找）。</summary>
	public event Action<ImageLayer> CliImageLayerSet;
	/// <summary>EWP WireHost：CLEARIMAGELAYER 后触发（参数＝深度）。</summary>
	public event Action<long> CliImageLayerCleared;
	/// <summary>EWP WireHost：CLEARIMAGELAYER_ALL 后触发。</summary>
	public event Action CliImageLayersCleared;
	/// <summary>EWP WireHost：HTML_PRINT_ISLAND 重绘后触发（参数＝原始 HTML 串）。</summary>
	public event Action<string> CliIslandPrinted;
	/// <summary>EWP WireHost：HTML_PRINT_ISLAND_CLEAR 后触发。</summary>
	public event Action CliIslandCleared;

	/// <summary>当前状态名（CLI 协议用；避开 enum.ToString 的混淆约束，手写映射）。</summary>
	internal string CliStateName => state switch
	{
		ConsoleState.Initializing => "Initializing",
		ConsoleState.Quit => "Quit",
		ConsoleState.Error => "Error",
		ConsoleState.Running => "Running",
		ConsoleState.WaitInput => "WaitInput",
		ConsoleState.WaitInputNoFocus => "WaitInputNoFocus",
		ConsoleState.Sleep => "Sleep",
		_ => "Unknown",
	};

	/// <summary>
	/// 当前输入等待世代可选按钮的<b>唯一</b>遍历（按钮对象 + 所属行下标）。
	/// 筛选逻辑移植自 SkiaX RefreshQuickButtonAsync（EmueraConsole.ex.cs）：
	/// 仅值输入系（IntValue/StrValue/AnyValue/IntButton/StrButton）收集；
	/// 自底向上回溯，遇到旧世代按钮（Generation != 0 且 != lastButtonGeneration）即停止——
	/// 与 PressEnterKey 的 IntButton/StrButton 验证分支同规则。
	/// <c>LineIndex</c>＝该按钮所属 <c>displayLineList</c> 下标；div 覆盖层按钮记 <c>-1</c>（不属普通行）。
	/// <para>
	/// <c>LineIndex</c>＝该按钮所属 <c>displayLineList</c> 下标；div 覆盖层按钮记 <c>-1</c>，
	/// 并用 <c>Div</c>／<c>ChildRow</c> 给出所属 div 与 div 内行号（I3-d 的 rect 导出用）。
	/// </para>
	/// ⚠️ 顺序敏感：普通行倒序 → div（CollectDivParts 再倒序）→ 末尾一次 Reverse，改动会改变 wire 按钮顺序。
	/// </summary>
	private List<(ConsoleButtonString Button, int LineIndex, ConsoleDivPart Div, int ChildRow)> CollectSelectableButtons()
	{
		var result = new List<(ConsoleButtonString Button, int LineIndex, ConsoleDivPart Div, int ChildRow)>();
		if (!IsWaitInputState || inputReq == null)
			return result;
		switch (inputReq.InputType)
		{
			case InputType.IntValue:
			case InputType.StrValue:
			case InputType.AnyValue:
			case InputType.IntButton:
			case InputType.StrButton:
				break;
			default:
				return result;
		}
		for (int i = displayLineList.Count - 1; i >= 0; i--)
		{
			foreach (ConsoleButtonString button in displayLineList[i].Buttons)
			{
				if (button == null || !button.IsButton)
					continue;
				if (button.Generation != lastButtonGeneration)
				{
					if (button.Generation != 0)
						goto Done;
					continue;
				}
				result.Add((button, i, null, -1));
			}
		}
	Done:
		// div（HTML rect 覆盖层）内のボタンは displayLineList ではなく
		// ConsoleEscapedParts に登録される（BINPUT / *Button の検証経路と同じ）。
		// 本体側は下→上に集めて最後に反転する流儀なので、div も画面位置の下から積む。
		var divParts = ConsoleEscapedParts.CollectDivParts();
		for (int i = divParts.Count - 1; i >= 0; i--)
		{
			if (divParts[i] is not ConsoleDivPart div)
				continue;
			var children = div.Children;
			for (int j = children.Length - 1; j >= 0; j--)
			{
				foreach (ConsoleButtonString button in children[j].Buttons)
				{
					if (button == null || !button.IsButton)
						continue;
					result.Add((button, -1, div, j));
				}
			}
		}
		result.Reverse();
		return result;
	}

	/// <summary>
	/// 当前输入等待世代可选的按钮列表（显示文本 / 提交值 / 是否整数），无则返回空列表。
	/// 口径与顺序同 <see cref="CollectSelectableButtons"/>（同一遍历，⛔ 不得各自再写一份筛选）。
	/// </summary>
	public List<(string Text, string Value, bool IsInteger)> GetSelectableButtons()
	{
		var result = new List<(string Text, string Value, bool IsInteger)>();
		foreach (var (button, _, _, _) in CollectSelectableButtons())
		{
			result.Add((ClipboardProcessor.StripHTML(button.ToString()).Trim(),
				button.IsInteger ? button.Input.ToString() : button.Inputs,
				button.IsInteger));
		}
		return result;
	}

	/// <summary>
	/// 同 <see cref="GetSelectableButtons"/>，另给每个按钮的<b>本宿主几何</b>（EWP <c>buttons[].rect</c>，I3-a）。
	/// 坐标系＝<c>MainWindow.RenderWidth</c>×<c>RenderHeight</c> 的 render 逻辑像素（与鼠标 hit-test 同源；
	/// 全屏已由 <c>ToLogicalPoint</c>/<c>RenderWidth</c> 除过 scale，⛔ 再除一次）；
	/// <c>x</c> 已含 <c>Config.DrawingParam_ShapePositionShift</c>（绘制统一偏移），纵向滚动已隐含在行 y 里。
	/// div 覆盖层按钮（<c>LineIndex=-1</c>）本轮<b>不给</b>矩形（<c>HasRect=false</c>）：
	/// 其绘制矩形与 <c>TestChildHitbox</c> 本就分叉（I3 设计卡 §3.3），⛔ 用包围盒冒充。
	/// </summary>
	public List<(ConsoleButtonString Button, string Text, string Value, bool IsInteger, bool HasRect, Rectangle Rect, bool HitTestOracle)> GetSelectableButtonRects()
	{
		var result = new List<(ConsoleButtonString Button, string Text, string Value, bool IsInteger, bool HasRect, Rectangle Rect, bool HitTestOracle)>();
		var items = CollectSelectableButtons();
		if (items.Count == 0)
			return result;

		// 与 GetLinePointY 同一口径：屏幕下沿行号由滚动条决定（hit-test 用的是 displayLineList 下标）。
		int bottomLineNo = 0;
		int mainPicHeight = 0;
		if (window != null)
		{
			bottomLineNo = window.ScrollValue - 1;
			if (displayLineList.Count - 1 < bottomLineNo)
				bottomLineNo = displayLineList.Count - 1;
			try { mainPicHeight = window.PicHeight; } catch { }
		}

		foreach (var (button, lineIndex, div, childRow) in items)
		{
			string text = ClipboardProcessor.StripHTML(button.ToString()).Trim();
			string value = button.IsInteger ? button.Input.ToString() : button.Inputs;
			bool hasRect = false;
			bool hitTestOracle = lineIndex >= 0;   // div 按钮的 hit-test 与绘制分叉（§3.3），不能当 oracle
			Rectangle rect = default;
			if (window != null && button.StrArray != null && button.Width > 0)
			{
				var (partTop, partBottom) = ButtonVerticalRange(button);
				if (lineIndex >= 0)
				{
					int lineY = window.RenderHeight - Config.LineHeight * (bottomLineNo - lineIndex + 1);
					rect = new Rectangle(button.PointX + Config.DrawingParam_ShapePositionShift,
						lineY + partTop, button.Width, partBottom - partTop);
					hasRect = true;
				}
				else if (div != null)
				{
					// I3-d：div 按钮的 rect 由**绘制**矩形推出（GetContentRect，与 DrawTo 同源），
					// ⛔ 不用 TestChildHitbox——绝对定位下它与绘制分叉（§3.3）。
					// <div> 默认 isRelative=true（HtmlManager.cs:1202），相对定位要用父行屏幕 y。
					// 绝对定位的 Y 依赖 MainPicBox.Height（物理）；全屏时它与 render 逻辑高度不同名 ⇒ 不冒充。
					bool posUsable = div.IsRelative
						|| (mainPicHeight > 0 && mainPicHeight == window.RenderHeight);
					if (posUsable && div.Parent?.ParentLine != null)
					{
						// 相对定位的基准＝**父行的屏幕行顶 y**，必须与普通行 rect 同源：
						// 用显示列表下标算（⛔ 不用 MoveMouse 里 ParentLine.LineNo + correction 那套——
						// 那是 Y 剪枝用的粗算式（还带 ±200 容差），不是绘制位置）。
						int parentIdx = displayLineList.IndexOf(div.Parent.ParentLine);
						if (parentIdx >= 0)
						{
							int parentLineY = window.RenderHeight
								- Config.LineHeight * (bottomLineNo - parentIdx + 1);
							var content = div.GetContentRect(mainPicHeight, parentLineY);
							if (content.Height > 0 && content.Width > 0)
							{
								int rowY = content.Top + childRow * Config.LineHeight;
								rect = new Rectangle(button.PointX + Config.DrawingParam_ShapePositionShift,
									rowY + partTop, button.Width, partBottom - partTop);
								hasRect = true;
							}
						}
					}
				}
			}
			result.Add((button, text, value, button.IsInteger, hasRect, rect, hitTestOracle));
		}
		return result;
	}

	/// <summary>按钮在**行内**的纵向范围（各 part 的 Top/Bottom 外包；退化时取字号高）。</summary>
	static (int Top, int Bottom) ButtonVerticalRange(ConsoleButtonString button)
	{
		int top = int.MaxValue;
		int bottom = int.MinValue;
		foreach (var part in button.StrArray)
		{
			if (part == null)
				continue;
			if (part.Top < top)
				top = part.Top;
			if (part.Bottom > bottom)
				bottom = part.Bottom;
		}
		if (top > bottom)
		{
			top = 0;
			bottom = Config.FontSize;
		}
		return (top, bottom);
	}

	/// <summary>
	/// 命中测试（只读，无副作用）：给定 render 逻辑坐标，返回该点命中的按钮（无则 null）。
	/// 几何判据与 <c>MoveMouse</c> 内的 hit-test 分支同源：普通行按 <c>relPointY</c> 自下而上累加，
	/// div 覆盖层走 <see cref="ConsoleDivPart.TestChildHitbox"/>。<c>MoveMouse</c> 另担选中态副作用
	/// （pointingString/selectingButton），本方法只答「命中了谁」，故**不带** IsInProcess 早退
	/// （那是鼠标交互的限制，不是几何限制）。
	/// ⚠️ 改 <c>MoveMouse</c> 的几何判据必须同步改这里——I3-c 的 rect 自检依赖它。
	/// </summary>
	internal ConsoleButtonString HitTestButtonAt(int pointX, int pointY)
	{
		if (window == null || displayLineList.Count == 0)
			return null;
		int bottomLineNo = window.ScrollValue - 1;
		if (displayLineList.Count - 1 < bottomLineNo)
			bottomLineNo = displayLineList.Count - 1;
		int topLineNo = bottomLineNo - (window.RenderHeight / Config.LineHeight);
		if (topLineNo < 0)
			topLineNo = 0;
		int relPointY = pointY - window.RenderHeight;

		// 普通行：自下而上、行内自右向左（与 MoveMouse 同）
		for (int i = bottomLineNo; i >= topLineNo; i--)
		{
			relPointY += Config.LineHeight;
			ConsoleDisplayLine curLine = displayLineList[i];
			for (int b = 0; b < curLine.Buttons.Length; b++)
			{
				ConsoleButtonString button = curLine.Buttons[curLine.Buttons.Length - b - 1];
				if (button == null || button.StrArray == null || !button.IsButton)
					continue;
				if (button.PointX > pointX || button.PointX + button.Width < pointX)
					continue;
				foreach (AConsoleDisplayNode part in button.StrArray)
				{
					if (part == null || part is ConsoleDivPart)
						continue;
					if (part.PointX <= pointX && part.PointX + part.Width >= pointX
						&& relPointY >= part.Top && relPointY <= part.Bottom)
						return button;
				}
			}
		}

		// div（HTML 覆盖层）：与 MoveMouse 同口径（含 200px 容错剪枝）
		if (ConsoleEscapedParts.Changed
			|| !ConsoleEscapedParts.TestedInRange(topLineNo, bottomLineNo, lastButtonGeneration))
		{
			escapedParts ??= [];
			ConsoleEscapedParts.GetPartsInRange(topLineNo, bottomLineNo, lastButtonGeneration, escapedParts);
		}
		if (escapedParts != null && escapedParts.Count > 0)
		{
			var bottomLineBase = window.RenderHeight - Config.LineHeight;
			var correction = lineNo > Config.MaxLog ? lineNo - Config.MaxLog : 0;
			var depths = escapedParts.Keys.ToArray();
			Array.Sort(depths);
			// depth 大的在上层：自顶向下查
			for (int d = depths.Length - 1; d >= 0; d--)
			{
				foreach (var part in escapedParts[depths[d]])
				{
					if (part is not ConsoleDivPart div)
						continue;
					if (div.Parent?.ParentLine == null)
						continue;
					var lineY = bottomLineBase
						+ (div.Parent.ParentLine.LineNo - bottomLineNo - correction) * Config.LineHeight;
					if (pointY < lineY + div.Top - 200 || pointY > lineY + div.Bottom + 200)
						continue;
					var child = div.TestChildHitbox(pointX, pointY, lineY);
					if (child != null && child.IsButton)
						return child;
				}
			}
		}
		return null;
	}

	/// <summary>
	/// rect 自检（I3-c）：在 rect 与既有 hit-test 的**共同覆盖区**内取中心＋四角，逐点跑
	/// <see cref="HitTestButtonAt"/>，要求全部命中同一按钮。用于 <c>input.request</c> 发 rect
	/// 前的生产护栏：自检不过 ⇒ 该按钮不发 rect（FE 回退文本/值），⛔ 不放大成"点得到但点错"。
	/// <para>
	/// 🔴 共同区要右缩 <c>Config.DrawingParam_ShapePositionShift</c>：rect 的 x 对齐**绘制**
	/// （<c>PointX + shift</c>，见 ConsoleStyledString.cs:441），而既有 hit-test 判的是
	/// <c>part.PointX</c>（**不含** shift）⇒ 两者在 x 上本就差 shift（既有缺陷，登记不修）。
	/// 因此 rect 左边界可能比 hit-test 左界多 shift、右边界多 shift：右界内缩 shift 才是共同区。
	/// </para>
	/// 返回失败点数（0＝通过）。
	/// </summary>
	internal int RectSelfCheckPoints(ConsoleButtonString button, Rectangle rect)
	{
		if (button == null || rect.Width <= 0 || rect.Height <= 0)
			return 1;
		int shift = Config.DrawingParam_ShapePositionShift;
		int left = rect.X + 1;
		int right = rect.X + rect.Width - 1 - shift;
		int top = rect.Y + 1;
		int bottom = rect.Y + rect.Height - 1;
		int cx = rect.X + rect.Width / 2;
		int cy = rect.Y + rect.Height / 2;
		var points = new List<(int X, int Y)> { (cx, cy) };
		if (right > left && bottom > top)
		{
			points.Add((left, top));
			points.Add((right, top));
			points.Add((left, bottom));
			points.Add((right, bottom));
		}
		int failed = 0;
		foreach (var (px, py) in points)
		{
			if (!ReferenceEquals(HitTestButtonAt(px, py), button))
				failed++;
		}
		return failed;
	}

	/// <summary>
	/// div（HTML rect 覆盖层）の内容行（CLI screen 用）。
	/// div 内の文字は displayLineList に積まれないため、これが無いと div 画面が読めない。
	/// 返り値は画面位置順（上→下）で、各 div の PointY と物理行テキストを持つ。
	/// </summary>
	internal List<(int Top, string[] Lines)> GetCliDivContents()
	{
		var result = new List<(int, string[])>();
		foreach (var part in ConsoleEscapedParts.CollectDivParts())
		{
			if (part is not ConsoleDivPart div)
				continue;
			var children = div.Children;
			string[] lines = new string[children.Length];
			for (int i = 0; i < children.Length; i++)
				lines[i] = ClipboardProcessor.StripHTML(children[i].ToString());
			result.Add((div.CliPointY, lines));
		}
		return result;
	}

	/// <summary>
	/// 末尾 count 条物理显示行（CLI screen 命令用）。
	/// GetDisplayLines 的参数是「逻辑行」计数语义（且无对应逻辑行时返回 null），
	/// 与整屏读取需求不符——div 类画面每轮 CLEARLINE + 重绘，逻辑行数远小于请求值。
	/// 这里按物理行自底向上取，保持屏幕自上而下的顺序。
	/// </summary>
	internal string[] GetCliTailLines(int count)
	{
		if (count <= 0)
			return [];
		var list = new List<string>(Math.Min(count, displayLineList.Count));
		for (int i = displayLineList.Count - 1; i >= 0 && list.Count < count; i--)
			list.Add(ClipboardProcessor.StripHTML(displayLineList[i].ToString()));
		list.Reverse();
		return [.. list];
	}
	#endregion

	public void Dispose()
	{
		if (genericTimer != null)
			genericTimer.Dispose();
		//timer = null;
		//stringMeasure.Dispose();
	}
}

internal class ConsoleBackground
{
	public readonly ASprite bgImage;

	public ConsoleBackground(ASprite spr, float opacity = 1.0f)
	{
		bgImage = spr;
		Opacity = opacity;
	}

	public float Opacity { get; private set; }

	public void SetOpacity(float opacity)
	{
		Opacity = opacity;
	}

	public SKColorFilter GetColorFilter()
	{
		if (Opacity >= 1.0f)
			return null;
		float[] skiaCM = [
			1, 0, 0, 0, 0,
			0, 1, 0, 0, 0,
			0, 0, 1, 0, 0,
			0, 0, 0, Opacity, 0,
		];
		return SKColorFilter.CreateColorMatrix(skiaCM);
	}
}
