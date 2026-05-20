using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using XEmuera.Forms;
using System.IO;
using MinorShift.Emuera.Runtime;
using MinorShift.Emuera.Runtime.Utils;
using MinorShift.Emuera.Runtime.Script;
using MinorShift.Emuera.Runtime.Script.Parser;
using MinorShift.Emuera.Runtime.Script.Data;
using MinorShift.Emuera.Runtime.Script.Statements;
using MinorShift.Emuera.GameProc;
using MinorShift.Emuera.GameProc.Function;
using MinorShift.Emuera.Runtime.Config;
using MinorShift.Emuera.GameData.Variable;
using MinorShift.Emuera.UI.Framework.Forms;
using MinorShift.Emuera.UI.Game.Rendering;
using MinorShift.Emuera.Runtime.Script.Statements.Expression;
using MinorShift.Emuera.Runtime.Script.Statements.Function;
using MinorShift.Emuera.Content;
using MinorShift.Emuera.UI.Game.Image;
using System.Timers;
using XEmuera.Drawing;
using SkiaSharp;
using XEmuera;
using MinorShift.Emuera.Runtime.Utils.EvilMask;
using StringStream = MinorShift.Emuera.Runtime.Utils.CharStream;

using trmb = MinorShift.Emuera.Runtime.Utils.EvilMask.Lang.MessageBox;
using trerror = MinorShift.Emuera.Runtime.Utils.EvilMask.Lang.Error;
using trsl = MinorShift.Emuera.Runtime.Utils.EvilMask.Lang.SystemLine;
using AConsoleDisplayNode = MinorShift.Emuera.UI.Game.AConsoleDisplayNode;

namespace MinorShift.Emuera.GameView
{
	//入出力待ちの状況。
	//難読化用属性。enum.ToString()やenum.Parse()を行うなら(Exclude=true)にすること。
	[global::System.Reflection.Obfuscation(Exclude=false)]
	internal enum ConsoleState
	{
		Initializing = 0,
		Quit = 5,
		Error = 6,
		Running = 7,
		WaitInput = 20,
		Sleep = 21,
	}

	//難読化用属性。enum.ToString()やenum.Parse()を行うなら(Exclude=true)にすること。
	[global::System.Reflection.Obfuscation(Exclude=false)]
	internal enum ConsoleRedraw
	{
		None = 0,
		Normal = 1,
	}

	internal sealed partial class EmueraConsole :IDisposable
	{
		public EmueraConsole(MainWindow parent)
		{
			window = parent;

			//1.713 この段階でsetStBarを使用してはいけない
			//setStBar(StaticConfig.DrawLineString);
			state = ConsoleState.Initializing;
			if (Config.FPS > 0)
				msPerFrame = 1000 / (uint)Config.FPS;
			displayLineList = new List<ConsoleDisplayLine>();
			printBuffer = new PrintStringBuffer(this);

			timer = new Timer();
			timer.Enabled = false;
			timer.Elapsed += tickTimer;
			timer.Interval = 10;
			CBG_Clear();//文字列描画用ダミー追加

			redrawTimer = new Timer();
			redrawTimer.Enabled = false;//TODO:1824アニメ用再描画タイマー有効化関数の追加
			redrawTimer.Elapsed += tickRedrawTimer;
			redrawTimer.Interval = 10;
        }
#region 1823 cbg関連
		private readonly List<ClientBackGroundImage> cbgList = new List<ClientBackGroundImage>();
		private GraphicsImage cbgButtonMap = null;
		private int selectingCBGButtonInt = -1;
		private int lastSelectingCBGButtonInt = -1;
		class ClientBackGroundImage : IComparable<ClientBackGroundImage>
		{
			internal ClientBackGroundImage(int zdepth)
			{ this.zdepth = zdepth; }
			public ASprite Img = null;
			public ASprite ImgB = null;
			public int x;
			public int y;
			public int width;
			public int height;
			public float Opacity = 1.0f;
			public float[] ColorMatrix = null;
			public readonly int zdepth;
			public bool isButton = false;
			public int buttonValue;
			public string tooltipString = null;
			public int CompareTo(ClientBackGroundImage other)
			{
				if (other == null)
					return -1;
				return -zdepth.CompareTo(other.zdepth);
			}
		}
		public void CBG_Clear()
		{
			foreach(ClientBackGroundImage cimg in cbgList)
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
			for (int i = 0; i < cbgList.Count;i++)
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

		public bool CBG_SetGraphics(GraphicsImage gra, int x, int y, int zdepth)
		{
			if (gra == null || !gra.IsCreated)
				return false;
			return CBG_SetImage(new SpriteG("", gra, new Rectangle(0, 0, gra.Width, gra.Height)), x, y, zdepth);
		}
		public bool CBG_SetImage(ASprite image, int x, int y, int zdepth)
		{
			if (image == null || !image.IsCreated)
				return false;
			if (zdepth == 0)
				throw new ArgumentOutOfRangeException();
			ClientBackGroundImage cbg = new ClientBackGroundImage(zdepth);
			cbg.Img = image;
			cbg.x = x;
			cbg.y = y;
			//cbg.zdepth = zdepth;
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
			if (zdepth == 0)
				throw new ArgumentOutOfRangeException();
			ClientBackGroundImage cbg = new ClientBackGroundImage(zdepth);
			cbg.Img = imageN;
			cbg.ImgB = imageB;
			cbg.x = x;
			cbg.y = y;
			//cbg.zdepth = zdepth;
			cbg.isButton = true;
			cbg.buttonValue = buttonValue;
			cbg.tooltipString = tooltip;
			cbgList.Add(cbg);
			cbgList.Sort();
			return true;
		}
		public int ClientWidth { get { return GameUtils.MainPicBox?.Width ?? DisplayUtils.PicBoxWidth; } }
		public int ClientHeight { get { return GameUtils.MainPicBox?.Height ?? DisplayUtils.PicBoxHeight; } }
#endregion

		const string ErrorButtonsText = "__openFileWithDebug__";
        private readonly MainWindow window;
		private readonly ImageLayerManager _imageLayerManager = new();
		private SKBitmap bakedBackground;
		private List<KeyValuePair<long, ConsoleBackground>> backgroundList = [];
		private List<ConsoleDisplayLine> _htmlElementList = new(10);
		public Rikaichan rikaichan = new();

		MinorShift.Emuera.GameProc.Process emuera;
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
			}
		}
		#endregion
		public bool Enabled { get { return window.Created; } }

		/// <summary>
		/// 現在、Emueraがアクティブかどうか
		/// </summary>
		internal bool IsActive
		{ get { return !(window == null || !window.Created); } }

		/// <summary>
		/// スクリプトが継続中かどうか
		/// 入力系はメッセージスキップやマクロも含めてIsInProcessを参照すべき
		/// </summary>
		internal bool IsRunning
		{
			get
			{
				if (state == ConsoleState.Initializing)
					return true;
				return (state == ConsoleState.Running || runningERBfromMemory);
			}
		}
		#region EM_私家版_INPUT系機能拡張
		internal bool IsWaintingInputWithMouse
		{
			get
			{
				return (state == ConsoleState.WaitInput && inputReq.MouseInput);
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
				if (inProcess)
					return true;
				return (state == ConsoleState.Running || runningERBfromMemory);
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
				if (state == ConsoleState.WaitInput)
				{
					GlobalStatic.ForceQuitAndRestart = false;
					return (inputReq.InputType == InputType.AnyKey || inputReq.InputType == InputType.EnterKey);
				}
				return false;
			}
		}

        internal bool IsWaitAnyKey
        {
            get
			{
				GlobalStatic.ForceQuitAndRestart = false;
				return (state == ConsoleState.WaitInput && inputReq.InputType == InputType.AnyKey);
            }
        }
		#endregion
		internal bool IsWaintingOnePhrase
        {
            get
            {
				return (state == ConsoleState.WaitInput && inputReq.OneInput);
            }
        }

		internal bool IsRunningTimer
		{
			get
			{
				return (state == ConsoleState.WaitInput && inputReq.Timelimit > 0 && !isTimeout);
			}
		}

		internal bool IsWaitingPrimitive
		{
			get
			{
				if (state == ConsoleState.WaitInput)
					return (inputReq.InputType == InputType.PrimitiveMouseKey);
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
				if (state != ConsoleState.WaitInput)
					return null;
				if (inputReq.InputType == InputType.IntValue && (selectingButton.IsInteger))
					return selectingButton.Input.ToString();
				if (inputReq.InputType == InputType.StrValue)
					return selectingButton.Inputs;
                #region EE_INPUTANY
                if (inputReq.InputType == InputType.AnyValue && (selectingButton.IsInteger))
					return selectingButton.Input.ToString();
				if (inputReq.InputType == InputType.AnyValue)
					return selectingButton.Inputs;
                #endregion
                return null;
			}
		}

		public void Initialize()
		{
			GlobalStatic.Console = this;
			GlobalStatic.MainWindow = window;
            emuera = new GameProc.Process(this);
			GlobalStatic.Process = emuera;

			if (Program.DebugMode && Config.DebugShowWindow)
			{
				OpenDebugDialog();
			}
			window.Focus();
			ClearDisplay();
			// Force DisplayReport on so loading info shows on screen and in emuera.log
			Runtime.Config.Config.DisplayReport = true;
			System.Diagnostics.Debug.WriteLine("[Emuera] console.Initialize: calling emuera.Initialize()...");
			System.IO.StreamWriter logWriter = null;
			try
			{
				var logPath = Program.ExeDir + "load_time.log";
				var fs = new System.IO.FileStream(logPath, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.Read);
				logWriter = new System.IO.StreamWriter(fs, System.Text.Encoding.UTF8);
			}
			catch { }
			var initTask = emuera.Initialize(logWriter);
			System.Diagnostics.Debug.WriteLine($"[Emuera] emuera.Initialize task created, Status={initTask.Status}");
			if (!initTask.Result)
			{
				logWriter?.Flush();
				logWriter?.Close();
				System.Diagnostics.Debug.WriteLine("[Emuera] ERROR: emuera.Initialize returned false!");
				state = ConsoleState.Error;
				OutputLog(null);
				PrintFlush(false);
				RefreshStrings(true);
				return;
			}
			logWriter?.Flush();
			logWriter?.Close();
			System.Diagnostics.Debug.WriteLine("[Emuera] emuera.Initialize done, calling callEmueraProgram...");
			callEmueraProgram("");
			System.Diagnostics.Debug.WriteLine("[Emuera] callEmueraProgram done");
			RefreshStrings(true);

			RefreshQuickButton();
		}
		

        public void Quit() { state = ConsoleState.Quit; }
		#region EE_FORCE_QUIT系
		public void ForceQuit()
		{

			if (GlobalStatic.ForceQuitAndRestart == true)
			{
				DialogResult result = MessageBox.Show(trmb.ForceQuitAndRestart.Text,
					"FORCE_QUIT_AND_RESTART",
					MessageBoxButtons.YesNo
					);
				if (result == DialogResult.Yes)
				{
					Program.Reboot = false;
					throw new CodeEE(trerror.ForceQuitAndRestartError.Text);
				}
			}
			GlobalStatic.ForceQuitAndRestart = true;
			window.Close();
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
			//if (playSound)
			//	System.Media.SystemSounds.Hand.Play();
			forceUpdateGeneration();
			UseUserStyle = false;
			PrintFlush(false);
			RefreshStrings(false);
			state = ConsoleState.Error;
		}

        public bool notToTitle = false;
        public bool byError = false;
        //public ScriptPosition ErrPos = null;

		#region button関連
		bool lastButtonIsInput = true;
        public bool updatedGeneration = false;
		int lastButtonGeneration = 0;
		int newButtonGeneration = 0;
		public int LastButtonGeneration { get { return lastButtonGeneration; } }
		public int NewButtonGeneration { get { return newButtonGeneration; } }
        public void UpdateGeneration() { lastButtonGeneration = newButtonGeneration; updatedGeneration = true; }
        public void forceUpdateGeneration() { newButtonGeneration++; lastButtonGeneration = newButtonGeneration; updatedGeneration = true; }
        LogicalLine lastInputLine;

		private void newGeneration()
		{
            //値の入力を求められない時は更新は必要ないはず
			if (state != ConsoleState.WaitInput || !inputReq.NeedValue)
				return;
            if (!updatedGeneration && emuera.getCurrentLine != lastInputLine)
            {
                //ボタン無しで次の入力に来たなら強制で世代更新
                lastButtonGeneration = newButtonGeneration;
            }
            else
                updatedGeneration = false;
            lastInputLine = emuera.getCurrentLine;
			//古い選択肢を選択できないように。INPUTで使った選択肢をINPUTSには流用できないように。
			if (inputReq.InputType == InputType.IntValue)
			{
				if (lastButtonGeneration == newButtonGeneration)
					unchecked { newButtonGeneration++; }
				else if (!lastButtonIsInput)
					lastButtonGeneration = newButtonGeneration;
				lastButtonIsInput = true;
			}
            #region EE_INPUTANY
            if (inputReq.InputType == InputType.StrValue || inputReq.InputType == InputType.AnyValue)
			#endregion
			{
				if (lastButtonGeneration == newButtonGeneration)
					unchecked { newButtonGeneration++; }
				else if (lastButtonIsInput)
					lastButtonGeneration = newButtonGeneration;
				lastButtonIsInput = false;
			}
		}

		/// <summary>
		/// 選択中のボタン。INPUTやINPUTSに対応したものでなければならない
		/// </summary>
		ConsoleButtonString selectingButton = null;
		ConsoleButtonString lastSelectingButton = null;
		public ConsoleButtonString SelectingButton { get { return selectingButton; } }
		public bool ButtonIsSelected(ConsoleButtonString button) { return selectingButton == button; }
		public bool ButtonIsPointing(ConsoleButtonString button) { return pointingStrings.Contains(button); }

		/// <summary>
		/// ToolTip表示したフラグ
		/// </summary>
		bool tooltipUsed = false;
		/// <summary>
		/// マウスの直下にあるテキスト。ボタンであってもよい。
		/// ToolTip表示用。世代無視、履歴中も表示
		/// </summary>
		ConsoleButtonString pointingString = null;
		HashSet<ConsoleButtonString> pointingStrings = new HashSet<ConsoleButtonString>();
		ConsoleButtonString lastPointingString = null;
		#endregion

		#region Input & Timer系

		//bool hasDefValue = false;
		//Int64 defNum;
		//string defStr;

		private InputRequest inputReq = null;
		/// <summary>
		/// AWAIT 中断标志。触摸事件在 Sleep 状态时设置此标志以中断等待。
		/// </summary>
		private volatile bool _awaitInterrupted;

		/// <summary>
		/// 从外部（触摸事件）中断当前 AWAIT 等待。
		/// </summary>
		public void InterruptAwait()
		{
			_awaitInterrupted = true;
		}

		public void Await(int time)
		{
			if (!Enabled || state != ConsoleState.Running)
			{
				this.Quit();
				return;
			}
			RefreshStrings(true);
			state = ConsoleState.Sleep;
			emuera.UpdateCheckInfiniteLoopState();
			_awaitInterrupted = false;
			App.DoEvents();

			// 使用短暂 Sleep 轮询，让 UI 线程有机会处理触摸事件
			// 触摸事件通过 Android Looper 在 Thread.Sleep 间隙被处理
			int elapsed = 0;
			int pollInterval = 8; // 8ms 轮询间隔，平衡响应性和 CPU 占用
			while (elapsed < time && !_awaitInterrupted)
			{
				int sleepTime = Math.Min(pollInterval, time - elapsed);
				System.Threading.Thread.Sleep(sleepTime);
				elapsed += sleepTime;
				App.DoEvents();
			}

			state = ConsoleState.Running;
		}

		public void WaitInput(InputRequest req)
		{
			state = ConsoleState.WaitInput;
			inputReq = req;
			if (req.Timelimit > 0)
			{
				if (req.OneInput)
					window.update_lastinput();
				presetTimer();
//				setTimer();
			}
			//updateMousePosition();
			Point point = window.MainPicBox.PointToClient(PlatformInterop.GetMousePosition());
			if (window.MainPicBox.ClientRectangle.Contains(point))
			{
				PrintFlush(false);
				MoveMouse(point);
			}
		}

		public void ReadAnyKey(bool anykey = false, bool stopMesskip = false)
		{
			InputRequest req = new InputRequest();
			if (!anykey)
				req.InputType = InputType.EnterKey;
			else
				req.InputType = InputType.AnyKey;
			req.StopMesskip = stopMesskip;
			inputReq = req;
			state = ConsoleState.WaitInput;
			emuera.NeedWaitToEventComEnd = false;
		}


		/// <summary>
		/// INPUT中のアニメーション用タイマー
		/// </summary>
		Timer redrawTimer = null;

		private void tickRedrawTimer(object sender, EventArgs e)
		{
			if (!redrawTimer.Enabled)
				return;
			//INPUT待ちでないとき、又はタイマー付きINPUT状態の場合はこれ以外の処理に任せる
			if (state != ConsoleState.WaitInput || timer.Enabled)
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



		Timer timer = null;
		Int64 timerID = -1;
		Int64 timer_startTime;//現在のタイマーを開始した時のミリ秒数（WinmmTimer.TickCount基準）
		Int64 timer_nextDisplayTime;//TINPUT系で次に残り時間を表示する時のTickCountミリ秒数
		Int64 timer_endTime;//現在のタイマーを終了する時のTickCountミリ秒数
        bool wait_timeout = false;
        bool isTimeout = false;
        public bool IsTimeOut { get { return isTimeout; } }

		/// <summary>
		/// 1824 TINPUT時に直接タイマーをセットせずに最初の再描画が終わってからタイマーをセットする（そうしないとTINPUTと再描画だけでループしてしまうので）
		/// </summary>
		bool need_settimer = false;

		private void presetTimer()
		{
			need_settimer = true;
			if (inputReq.DisplayTime)
			{
				//100ms未満の場合、一瞬だけ残り0が表示されて終了
				//timer_nextDisplayTime = timer_startTime + 100;
				long start = inputReq.Timelimit / 100;
				string timeString1 = trsl.Remaining.Text;
				string timeString2 = ((double)start / 10.0).ToString();
				PrintSingleLine(timeString1 + timeString2);
			}
		}
		private void setTimer()
		{
			isTimeout = false;
			timerID = inputReq.ID;
			timer.Enabled = true;
			timer_startTime = WinmmTimer.TickCount;
			timer_endTime = timer_startTime + inputReq.Timelimit;
			//if (inputReq.DisplayTime)
			//次に残り時間を表示するタイミングの設定。inputReq.DisplayTime==tureでないなら設定するだけで参照はされない（はず
			timer_nextDisplayTime = timer_startTime + 100;

		}

		//汎用
		private void tickTimer(object sender, EventArgs e)
		{
			if (!timer.Enabled)
				return;
			if (state != ConsoleState.WaitInput || inputReq.Timelimit <= 0 || timerID != inputReq.ID)
			{
#if DEBUG
				throw new ExeEE("");
#else
				stopTimer();
				return;
#endif
			}
			long curtime = WinmmTimer.TickCount;
			if (curtime >= timer_endTime)
			{
				endTimer();
				return;
			}
			if (inputReq.DisplayTime && curtime >= timer_nextDisplayTime)
			{
				//表示に時間がかかってタイマーが止まるので次の描画は100ms後。場合によっては表示が0.2一気に飛ぶ。
				timer_nextDisplayTime = curtime + 100;
				long time = (timer_endTime - curtime) / 100;
				string timeString1 = trsl.Remaining.Text;
				string timeString2 = ((double)time / 10.0).ToString();
				changeLastLine(timeString1 + timeString2);
			}
		}

		private void stopTimer()
		{
			timer.Enabled = false;
		}

		/// <summary>
		/// tickTimerからのみ呼ぶ
		/// </summary>
		private void endTimer()
		{
            if (wait_timeout)
                return;
			stopTimer();
            isTimeout = true;

			ClearQuickButton();

			if(IsWaitingPrimitive)
			{
				//callEmueraProgramは呼び出し先で行う。
				#region EE_INPUTMOUSEKEY拡張
				// InputMouseKey(4, 0, 0, 0, 0);
				InputMouseKey(4, 0, 0, 0, 0, 0);
				if (state == ConsoleState.WaitInput && inputReq.NeedValue)
				{
					Point point = window.MainPicBox.PointToClient(PlatformInterop.GetMousePosition());
					if (window.MainPicBox.ClientRectangle.Contains(point))
						MoveMouse(point);
				}
				//RefreshStrings(true);
				#endregion
				return;
			}
			if (inputReq.DisplayTime)
				changeLastLine(inputReq.TimeUpMes);
			else if (inputReq.TimeUpMes != null)
				PrintSingleLine(inputReq.TimeUpMes);
			callEmueraProgram("");//ディフォルト入力の処理はcallEmueraProgram側で
			if (state == ConsoleState.WaitInput && inputReq.NeedValue)
			{
				Point point = window.MainPicBox.PointToClient(PlatformInterop.GetMousePosition());
				if (window.MainPicBox.ClientRectangle.Contains(point))
					MoveMouse(point);
			}
			RefreshStrings(true);

			RefreshQuickButton();
		}

        public void forceStopTimer()
        {
            if (timer.Enabled)
            {
                timer.Enabled = false;
            }
        }
		#endregion

		#region Call系
		/// <summary>
		/// スクリプト実行。RefreshStringsはしないので呼び出し側がすること
		/// </summary>
		/// <param name="str"></param>
		private void callEmueraProgram(string str)
		{
			//入力文字列の表示処理を行わない場合はstr == null
			if (str != null)
			{
				//INPUT文字列をPRINTする処理など
				if (!doInputToEmueraProgram(str))
					return;
				if (state == ConsoleState.Error)
					return;
			}
			state = ConsoleState.Running;
			emuera.DoScript();
			if (state == ConsoleState.Running)
			{//RunningならProcessは処理を継続するべき
				state = ConsoleState.Error;
                PrintError(trerror.ProgramStatusError.Text);
			}
			if (state == ConsoleState.Error && !noOutputLog)
				#region EE_OUTPUTLOG
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
			if (state == ConsoleState.WaitInput)
			{
				Int64 inputValue;

				switch (inputReq.InputType)
				{
					case InputType.IntValue:
						if (string.IsNullOrEmpty(str) && inputReq.HasDefValue && !IsRunningTimer)
						{
							inputValue = inputReq.DefIntValue;
							str = inputValue.ToString();
						}
						else if (!Int64.TryParse(str, out inputValue))
							return false;
						if (inputReq.IsSystemInput)
							emuera.InputSystemInteger(inputValue);
						else
							emuera.InputInteger(inputValue);
						break;
					case InputType.StrValue:
						if (string.IsNullOrEmpty(str) && inputReq.HasDefValue && !IsRunningTimer)
							str = inputReq.DefStrValue;
						//空入力と時間切れ
						if (str == null)
							str = "";
						emuera.InputString(str);
						break;
                    #region EE_INPUTANY
                    case InputType.AnyValue:
						if (Int64.TryParse(str, out inputValue))
                        {
							if (inputReq.IsSystemInput)
								emuera.InputSystemInteger(inputValue);
							else
								emuera.InputInteger(inputValue);
						}
						else
                        {
							emuera.InputString(str);
						}
						break;
                    #endregion

                }
                stopTimer();
			}
			Print(str);
			PrintFlush(false);
			return true;
		}
		#endregion

		#region 入力系
		readonly string[] spliter = new string[] { "\\n", "\r\n", "\n", "\r" };//本物の改行コードが来ることは無いはずだけど一応

		public bool MesSkip = false;
		private bool inProcess = false;
		volatile public bool KillMacro = false;
		
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

		internal void MouseDown(Point point, SKMouseButton button)
		{
			if (!IsWaitingPrimitive)
				return;
			//pointはクライアント左上基準の座標。
			//clientPointをクライアント左下基準の座標に置き換え
			Point clientPoint = point;
			clientPoint.Y = point.Y - ClientHeight;
			int buttonNum = -1;
			if(cbgButtonMap != null && cbgButtonMap.IsCreated)
			{
				//マップ画像の左上基準の座標に置き換え
				Point mapPoint = clientPoint;
				mapPoint.Y = clientPoint.Y + cbgButtonMap.Height;
				if(mapPoint.X >= 0 && mapPoint.Y >= 0 && mapPoint.X < cbgButtonMap.Width && mapPoint.Y < cbgButtonMap.Height)
				{
					Color c = DisplayUtils.ToColor(cbgButtonMap.Bitmap.GetPixel(mapPoint.X, mapPoint.Y));
					if(c.A == 255)
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
				if (!selectingButton.IsInteger)
                {
					GlobalStatic.VEvaluator.RESULTS = selectingButton.Inputs;
					InputMouseKey(1, (int)button, clientPoint.X, clientPoint.Y, buttonNum, 0);
				}
                else
                {
					InputMouseKey(1, (int)button, clientPoint.X, clientPoint.Y, buttonNum, selectingButton.Input);
				}
			}
			else
			{
				InputMouseKey(1, (int)button, clientPoint.X, clientPoint.Y, buttonNum, 0);
			}
			#endregion
		}

		//1823 Key入力を捕まえる
		internal void PressPrimitiveKey(int keycode, int keydata, int keymod)
		{
			if (IsWaitingPrimitive)
				#region EE_INPUTMOUSEKEY拡張
				InputMouseKey(3, keycode, keydata, 0, 0, 0);
				#endregion
		}

		//1823 Key入力を捕まえる
		#region EE_INPUTMOUSEKEY拡張
		//internal void InputMouseKey(int type, int result1, int result2, int result3, int result4)
		internal void InputMouseKey(int type, int result1, int result2, int result3, int result4, long result5)
		{
			// emuera.InputResult5(type, result1, result2, result3, result4);
			emuera.InputResult5(type, result1, result2, result3, result4, result5);
			inProcess = true;
			try
			{
				//1823 Escキーもマクロも右クリックも不可。単純に押されたキーを送るのみ。
				callEmueraProgram(null);
				if (state == ConsoleState.WaitInput && inputReq.NeedValue)
				{
					Point point = window.MainPicBox.PointToClient(PlatformInterop.GetMousePosition());
					if (window.MainPicBox.ClientRectangle.Contains(point))
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

		public void PressEnterKey(bool keySkip, string str, bool changedByMouse)
		{
			MesSkip = keySkip;
			ClearQuickButton();

			if ((state == ConsoleState.Running) || (state == ConsoleState.Initializing))
				return;
			else if ((state == ConsoleState.Quit))
			{
				window.Close();
				return;
			}
			else if (state == ConsoleState.Error)
			{
				if (str == ErrorButtonsText && selectingButton != null && selectingButton.ErrPos != null)
				{
					openErrorFile(selectingButton.ErrPos);
					return;
				}
				window.Close();
				return;
			}
#if DEBUG
			if (state != ConsoleState.WaitInput || inputReq == null)
				throw new ExeEE("");
#endif
			KillMacro = false;
			try
			{
				string[] text;
				if(changedByMouse)//1823 マウスによって入力されたならマクロ解析を行わない
				{ text = new string[] { str }; }
				else
				{
					if (str.StartsWith("@") && !inputReq.OneInput)
					{
						doSystemCommand(str);
						return;
					}
					if (inputReq.InputType == InputType.Void)
						return;
					if (timer.Enabled &&
						(inputReq.InputType == InputType.AnyKey || inputReq.InputType == InputType.EnterKey))
						stopTimer();
					//if((inputReq.InputType == InputType.IntValue || inputReq.InputType == InputType.StrValue)
					if (str.Contains("("))
						str = parseInput(new StringStream(str), false);
					text = str.Split(spliter, StringSplitOptions.None);
				}
				
				inProcess = true;
				for (int i = 0; i < text.Length; i++)
				{
					string inputs = text[i];
					if (inputs.IndexOf("\\e") >= 0)
					{
						inputs = inputs.Replace("\\e", "");//\eの除去
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
					callEmueraProgram(inputs);
					RefreshStrings(false);
					while (MesSkip && state == ConsoleState.WaitInput)
					{
						//TODO:入力無効を通していいか？スキップ停止をマクロでは飛ばせていいのか？
						if (inputReq.NeedValue)
							break;
						if (inputReq.StopMesskip)
							break;
						callEmueraProgram("");
						RefreshStrings(false);
						//DoEventを呼ばないと描画処理すらまったく行われない
						App.DoEvents();
						//EscがマクロストップかつEscがスキップ開始だからEscでスキップを止められても即開始しちゃったりするからあんまり意味ないよね
						//if (KillMacro)
						//	goto endMacro;
					}
					MesSkip = false;
					if (state != ConsoleState.WaitInput)
						break;
					//マクロループ時は待ち処理が起こらないのでここでシステムキューを捌く
					App.DoEvents();
#if DEBUG
					if (state != ConsoleState.WaitInput || inputReq == null)
						throw new ExeEE("");
#endif
					if (KillMacro)
						goto endMacro;
				}
			}
			finally
			{
				inProcess = false;

				RefreshQuickButton();
			}
			endMacro:
			if(state == ConsoleState.WaitInput && inputReq.NeedValue)
			{
				Point point = window.MainPicBox.PointToClient(PlatformInterop.GetMousePosition());
				if (window.MainPicBox.ClientRectangle.Contains(point))
					MoveMouse(point);
			}
			RefreshStrings(true);
		}

		private void openErrorFile(ScriptPosition pos)
		{
			ProcessStartInfo pInfo = new ProcessStartInfo();
			pInfo.FileName = Config.TextEditor;
			string fname = pos.Filename.ToUpper();
			if (fname.EndsWith(".CSV"))
			{
				if (fname.Contains(Program.CsvDir.ToUpper()))
					fname = fname.Replace(Program.CsvDir.ToUpper(), "");
				fname = Program.CsvDir + fname;
			}
			else
			{
				//解析モードの場合は見ているファイルがERB\の下にあるとは限らないかつフルパスを持っているのでこの補正はしなくてよい
				if (!Program.AnalysisMode)
				{
					if (fname.Contains(Program.ErbDir.ToUpper()))
						fname = fname.Replace(Program.ErbDir.ToUpper(), "");
					fname = Program.ErbDir + fname;
				}
			}
			switch (Config.EditorType)
			{
				case TextEditorType.SAKURA:
					pInfo.Arguments = "-Y=" + pos.LineNo.ToString() + " \"" + fname + "\"";
					break;
				case TextEditorType.TERAPAD:
					pInfo.Arguments = "/jl=" + pos.LineNo.ToString() + " \"" + fname + "\"";
					break;
				case TextEditorType.EMEDITOR:
					pInfo.Arguments = "/l " + pos.LineNo.ToString() + " \"" + fname + "\"";
					break;
				case TextEditorType.USER_SETTING:
					if (Config.EditorArg != "" && Config.EditorArg != null)
						pInfo.Arguments = Config.EditorArg + pos.LineNo.ToString() + " \"" + fname + "\"";
					else
						pInfo.Arguments = fname;
					break;
			}
			try
			{
				System.Diagnostics.Process.Start(pInfo);
			}
			catch (System.ComponentModel.Win32Exception)
			{
				//System.Media.SystemSounds.Hand.Play();
				PrintError("エディタを開くことができませんでした");
				forceUpdateGeneration();
			}
			catch (System.Exception)
			{
				PrintError("エディタを開くことができませんでした");
				forceUpdateGeneration();
			}
			return;
		}

        string parseInput(StringStream st, bool isNest)
        {
            StringBuilder sb = new StringBuilder(20);
            StringBuilder num = new StringBuilder(20);
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


		bool runningERBfromMemory = false;
		/// <summary>
		/// 通常コンソールからのDebugコマンド、及びデバッグウインドウの変数ウォッチなど、
		/// *.ERBファイルが存在しないスクリプトを実行中
		/// 1750 IsDebugから改名
		/// </summary>
		public bool RunERBFromMemory { get { return runningERBfromMemory; } set { runningERBfromMemory = value; } }
		void doSystemCommand(string command)
		{
			if(timer.Enabled)
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
			StringComparison sc = Config.SCVariable;
			Print(command);
			PrintFlush(false);
			RefreshStrings(true);
			string com = command.Substring(1);
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
				this.OutputSystemLog(Program.ExeDir + "emuera.log");
				#endregion

				return;
			}
			else if ((com.Equals("QUIT", sc)) || (com.Equals("EXIT", sc)))
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
		uint lastUpdate = 0;
		uint msPerFrame = 1000 / 60;//60FPS
		ConsoleRedraw redraw = ConsoleRedraw.Normal;
        public ConsoleRedraw Redraw { get { return redraw; } }
		public void SetRedraw(Int64 i)
		{
			if ((i & 1) == 0)
				redraw = ConsoleRedraw.None;
			else
				redraw = ConsoleRedraw.Normal;
			if ((i & 2) != 0)
				RefreshStrings(true);
		}

		string debugTitle = null;
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
            window.TextBox.Text = str;
        }
		public string GetWindowTitle()
		{
			if (Program.DebugMode && debugTitle != null)
				return debugTitle;
			return window.Title;
		}


		/// <summary>
		/// 1818以前のRefreshStringsからselectingButton部分を抽出
		/// ここでOnPaintを発行
		/// </summary>
		public void RefreshStrings(bool force_Paint)
		{
			bool isBackLog = window.ScrollBar.Value != window.ScrollBar.Maximum;
			//ログ表示はREDRAWの設定に関係なく行うようにする
			if ((redraw == ConsoleRedraw.None) && (!force_Paint) && (!isBackLog))
				return;
			//選択中ボタンの適性チェック
			if (selectingButton != null)
			{
				//履歴表示中は選択肢無効→画面外に出てしまったボタンも履歴から選択できるように
				//if (isBackLog)
				//	selectingButton = null;
				//数値か文字列の入力待ち状態でなければ無効
				if(state != ConsoleState.Error && state != ConsoleState.WaitInput)
					selectingButton = null;
				else if((state == ConsoleState.WaitInput) && !inputReq.NeedValue)
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
				//Environment.TickCountは分解能が悪すぎるのでwinmmのタイマーを呼んで来る
				uint sec = WinmmTimer.TickCount - lastUpdate;
				//まだ書き換えるタイミングでないなら次の更新を待ってみる
				//ただし、入力待ちなど、しばらく更新のタイミングがない場合には強制的に書き換えてみる
				if (sec < msPerFrame && (state == ConsoleState.Running || state == ConsoleState.Initializing))
					return;
			}
			if (forceTextBoxColor)
			{
				uint sec = WinmmTimer.TickCount - lastBgColorChange;
				//色変化が速くなりすぎないように一定時間以内の再呼び出しは強制待ちにする
				while (sec < 200)
				{
					App.DoEvents();
					sec = WinmmTimer.TickCount - lastBgColorChange;
				}
				window.TextBox.BackColor = this.bgColor;
				window.Invoke(() =>
				{
					window.BackgroundColor = DisplayUtils.ToSKColor(this.bgColor);
				});
				lastBgColorChange = WinmmTimer.TickCount;
			}
			verticalScrollBarUpdate();
			window.Refresh();//OnPaint発行

		}

		public System.Threading.SpinLock displayLineListSpinLock = new System.Threading.SpinLock();

		/// <summary>
		/// 1818以前のRefreshStringsの後半とm_RefreshStringsを融合
		/// 全面Clear法のみにしたのでさっぱりした。ダブルバッファリングはOnPaintが勝手にやるはず
		/// </summary>
		/// <param name="graph"></param>
		private static readonly string _debugLogPath = System.IO.Path.Combine(
			System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal),
			"render_debug.log");
		private static int _debugLogFrameCount = 0;
		private const int _debugLogMaxFrames = 5; // 只记录前5帧

		public void OnPaint(SKCanvas graph)
		{

			//デバッグ用。描画が超重い環境を想定1
			//System.Threading.Thread.Sleep(100);
			
			//描画中にEmueraが閉じられると廃棄されたPictureBoxにアクセスしてしまったりするので
			//OnPaintからgraphをもらった直後だから大丈夫だとは思うけど一応
			if (!this.Enabled)
				return;

			bool lockTaken = false;
			displayLineListSpinLock.Enter(ref lockTaken);
			try
			{

			//1824 アニメスプライト用・現在フレームの時間を決定
			WinmmTimer.FrameStart();
			lastUpdate = WinmmTimer.CurrentFrameTime;//WinmmTimer.TickCount;

			bool isBackLog = window.ScrollBar.Value != window.ScrollBar.Maximum;
			// IgnorePixelScaling=false 时 Canvas 自动缩放为逻辑像素坐标系，
			// ClientWidth/ClientHeight = MainPicBox.Width/Height = 逻辑像素，
			// Config.LineHeight/FontSize 也是逻辑像素，坐标系一致。
			// entryLayout 已移到 canvasGrid 覆盖层，不再压缩 MainPicBox 高度。
			int pointY = ClientHeight - DisplayUtils.HeightOffset;

			// === 诊断日志：记录坐标系关键参数 ===
			bool shouldLog = _debugLogFrameCount < _debugLogMaxFrames && displayLineList.Count > 0;
			if (shouldLog)
			{
				_debugLogFrameCount++;
				var sb = new System.Text.StringBuilder();
				sb.AppendLine($"=== Frame {_debugLogFrameCount} ===");
				sb.AppendLine($"  ScreenDensity={DisplayUtils.ScreenDensity}");
				sb.AppendLine($"  MainPicBox.Width={GameUtils.MainPicBox?.Width ?? 0} Height={GameUtils.MainPicBox?.Height ?? 0}");
				sb.AppendLine($"  ClientWidth={ClientWidth} ClientHeight={ClientHeight} (logical px)");
				sb.AppendLine($"  Config.FontSize={Config.FontSize} Config.LineHeight={Config.LineHeight} (logical px)");
				sb.AppendLine($"  Config.WindowX={Config.WindowX} Config.WindowY={Config.WindowY}");
				sb.AppendLine($"  Config.DrawableWidth={Config.DrawableWidth}");
				sb.AppendLine($"  HeightOffset={DisplayUtils.HeightOffset}");
				sb.AppendLine($"  ScrollBar.Value={window.ScrollBar.Value} Maximum={window.ScrollBar.Maximum}");
				sb.AppendLine($"  displayLineList.Count={displayLineList.Count}");
				sb.AppendLine($"  initial pointY={pointY} (logical px)");
				sb.AppendLine($"  pointY/LineHeight={pointY / (float)Config.LineHeight:F2} (visible lines)");

				System.Diagnostics.Debug.WriteLine(sb.ToString());
				try { System.IO.File.AppendAllText(_debugLogPath, sb.ToString()); } catch { }
			}


			int bottomLineNo = (int)window.ScrollBar.Value - 1;
			if (displayLineList.Count - 1 < bottomLineNo)
				bottomLineNo = displayLineList.Count - 1;//1820 この処理不要な気がするけどエラー報告があったので入れとく
			int topLineNo = bottomLineNo - (pointY / Config.LineHeight + 1);
			if (topLineNo < 0)
				topLineNo = 0;
			pointY -= (bottomLineNo - topLineNo) * Config.LineHeight;

			// === 诊断日志：记录行号计算结果 ===
			if (shouldLog)
			{
				var sb2 = new System.Text.StringBuilder();
				sb2.AppendLine($"  topLineNo={topLineNo} bottomLineNo={bottomLineNo}");
				sb2.AppendLine($"  adjusted pointY={pointY} (first line top Y)");
				sb2.AppendLine($"  total visible lines={bottomLineNo - topLineNo + 1}");
				sb2.AppendLine($"  last line bottom Y={pointY + (bottomLineNo - topLineNo) * Config.LineHeight + Config.LineHeight}");
				sb2.AppendLine($"  ClientHeight={ClientHeight} (logical px)");
				sb2.AppendLine($"  last line exceeds canvas? {pointY + (bottomLineNo - topLineNo + 1) * Config.LineHeight > ClientHeight}");

				// 记录前3行的按钮坐标
				int logEnd = Math.Min(topLineNo + 2, bottomLineNo);
				for (int li = topLineNo; li <= logEnd; li++)
				{
					var line = displayLineList[li];
					int lineY = pointY + (li - topLineNo) * Config.LineHeight;
					sb2.AppendLine($"  line[{li}] Y={lineY} buttons={line.Buttons?.Length ?? 0}");
					if (line.Buttons != null)
					{
						foreach (var btn in line.Buttons)
						{
							if (btn.StrArray != null)
							{
								foreach (var part in btn.StrArray)
								{
									sb2.AppendLine($"    part: PointX={part.PointX} Width={part.Width} Top={part.Top} Bottom={part.Bottom} Type={part.GetType().Name}");
								}
							}
						}
					}
				}
				// 后3行
				int logStart2 = Math.Max(bottomLineNo - 2, topLineNo);
				if (logStart2 > logEnd)
				{
					for (int li = logStart2; li <= bottomLineNo; li++)
					{
						var line = displayLineList[li];
						int lineY = pointY + (li - topLineNo) * Config.LineHeight;
						sb2.AppendLine($"  line[{li}] Y={lineY} buttons={line.Buttons?.Length ?? 0}");
					}
				}

				// HTML 元素坐标
				sb2.AppendLine($"  _htmlElementList.Count={_htmlElementList.Count}");
				int htmlY = 0;
				int htmlIdx = 0;
				foreach (var element in _htmlElementList)
				{
					if (htmlIdx < 3 || htmlIdx >= _htmlElementList.Count - 2)
						sb2.AppendLine($"    html[{htmlIdx}] Y={htmlY}");
					htmlY += Config.LineHeight;
					htmlIdx++;
				}

				System.Diagnostics.Debug.WriteLine(sb2.ToString());
				try { System.IO.File.AppendAllText(_debugLogPath, sb2.ToString()); } catch { }
			}

			//if (Config.TextDrawingMode == TextDrawingMode.WINAPI)
			//{
			//	GDI.GDIStart(graph, this.bgColor);
			//	GDI.FillRect(new Rectangle(0, 0, window.MainPicBox.Width, window.MainPicBox.Height));
			//	//for (int i = bottomLineNo; i >= topLineNo; i--)
			//	//{
			//	//	displayLineList[i].GDIDrawTo(pointY, isBackLog);
			//	//	pointY -= Config.LineHeight;
			//	//}
			//	//1820a12 上から下へ描画する方向へ変更
			//	for (int i = topLineNo; i <= bottomLineNo; i++)
			//	{
			//		displayLineList[i].GDIDrawTo(pointY, isBackLog);
			//		pointY += Config.LineHeight;
			//	}
			//	GDI.GDIEnd(graph);
			//}
			//else
			//{
				graph.Clear(DisplayUtils.ToSKColor(this.bgColor));
				ValidateBackground(ClientWidth, ClientHeight);
				if (bakedBackground != null)
					graph.DrawBitmap(bakedBackground, 0, 0);
				_imageLayerManager.DrawTo(graph, ClientWidth, ClientHeight, window.ScrollBar.Value * Config.LineHeight);
				//1823 cbg追加
				#region EM_私家版_描画拡張
				var escapedParts = EscapedParts;
				if (ConsoleEscapedParts.Changed || !ConsoleEscapedParts.TestedInRange(topLineNo, bottomLineNo, lastButtonGeneration))
					ConsoleEscapedParts.GetPartsInRange(topLineNo, bottomLineNo, lastButtonGeneration, escapedParts);
				var edepth = escapedParts.Keys.ToArray();
				Array.Sort(edepth, (int a, int b) => -a.CompareTo(b));
				int eidx = 0, cidx = 0;
				int topPointY = pointY;
				while (eidx < edepth.Length || cidx < cbgList.Count)
				{
					var depth = Math.Max(eidx < edepth.Length ? edepth[eidx] : int.MinValue,
						cidx < cbgList.Count ? cbgList[cidx].zdepth : int.MinValue);

					if (cidx < cbgList.Count && cbgList[cidx].zdepth == depth)
					{
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
								var destRect = new Rectangle(cbg.x, cbg.y + ClientHeight - destH, destW, destH);
								SKColorFilter filter = null;
								if (cbg.ColorMatrix != null && cbg.ColorMatrix.Length == 20)
									filter = SKColorFilter.CreateColorMatrix(cbg.ColorMatrix);
								else if (cbg.Opacity < 1.0f)
									filter = SKColorFilter.CreateColorMatrix([1,0,0,0,0, 0,1,0,0,0, 0,0,1,0,0, 0,0,0,cbg.Opacity,0]);
								img.GraphicsDraw(graph, destRect, filter);
							}
							catch { }
						}
						cidx++;
					}

					if (depth == 0)
					{
						for (int i = topLineNo; i <= bottomLineNo && i < displayLineList.Count; i++)
						{
							try { displayLineList[i].DrawTo(graph, pointY, isBackLog, true, Config.TextDrawingMode); }
							catch { }
							pointY += Config.LineHeight;
						}
					}

					if (eidx < edepth.Length && edepth[eidx] == depth)
					{
						foreach (var p in escapedParts[edepth[eidx]])
						{
							var btn = p.Parent as ConsoleButtonString;
							if (btn == null) continue;
							var baseLineNo = btn.ParentLine.LineNo;
							if (GlobalStatic.Console?.GetLineNo > Config.MaxLog)
							{
								var correction = GlobalStatic.Console.GetLineNo - Config.MaxLog;
								baseLineNo -= correction;
							}
							try
							{
								btn.DrawPartTo(graph, p, topPointY + (baseLineNo - topLineNo) * Config.LineHeight, isBackLog, Config.TextDrawingMode);
							}
							catch { }
						}
						eidx++;
					}
				}
				#endregion

				#region EmuEra-Rikaichan
				if (Config.RikaiEnabled)
				{
					try
					{
						rikaichan.OnPaint(graph, stringMeasure, window.MainPicBox.Width);
					}
					catch
					{
					}
				}
				#endregion

				var y = 0;
				foreach (var element in _htmlElementList)
				{
					try
					{
						element.DrawTo(graph, y, false, true, Config.TextDrawingMode);
					}
					catch { }
					y += Config.LineHeight;
				}

			//}
			//ToolTip描画
			if (lastPointingString != pointingString || lastSelectingCBGButtonInt != selectingCBGButtonInt)
			{
				if (tooltipUsed)
					window.ToolTip.RemoveAll();
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
					if (tooltip_duration == 0)
						window.ToolTip.Show(title, window.MainPicBox, new Point(0, 0));
					else
					{
						if (window.ToolTip.InitialDelay == 0)
						{
							Point mousePos = window.MainPicBox.PointToClient(PlatformInterop.GetMousePosition());
							window.ToolTip.Show(title, window.MainPicBox, new Point(mousePos.X, mousePos.Y + 18), tooltip_duration);
						}
						else
						{
							System.Threading.SynchronizationContext context = System.Threading.SynchronizationContext.Current;
							System.Threading.Tasks.Task.Run(async () =>
							{
								ConsoleButtonString savedPointingString = pointingString;
								await System.Threading.Tasks.Task.Delay(window.ToolTip.InitialDelay);
								context.Post((state) =>
								{
									MoveMouse(GetMousePosition());
									if (lastPointingString == savedPointingString)
									{
										Point mousePos = window.MainPicBox.PointToClient(PlatformInterop.GetMousePosition());
										window.ToolTip.Show(title, window.MainPicBox, new Point(mousePos.X, mousePos.Y + 18), tooltip_duration);
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
			if(need_settimer)
			{
				need_settimer = false;
				setTimer();
			}
			}
			finally
			{
				if (lockTaken)
					displayLineListSpinLock.Exit();
			}
		}

		public void SetToolTipColor(Color foreColor, Color backColor)
		{
			window.ToolTip.ForeColor = foreColor;
			window.ToolTip.BackColor = backColor;

		}
		public void SetToolTipDelay(int delay)
		{
			window.ToolTip.InitialDelay = delay;
		}

        int tooltip_duration = 0;
        public void SetToolTipDuration(int duration)
        {
            tooltip_duration = duration;
        }


        #endregion

        #region DebugMode系
		public object DebugDialog { get { return null; } }
		StringBuilder dConsoleLog = new StringBuilder("");
		public string DebugConsoleLog { get { return dConsoleLog.ToString(); } }
		List<string> dTraceLogList = new List<string>();
#pragma warning disable CS0414 // フィールド 'EmueraConsole.dTraceLogChanged' が割り当てられていますが、値は使用されていません。
		bool dTraceLogChanged = true;
#pragma warning restore CS0414 // フィールド 'EmueraConsole.dTraceLogChanged' が割り当てられていますが、値は使用されていません。
		public string GetDebugTraceLog(bool force)
		{
			//if (!dTraceLogChanged && !force)
			//	return null;
			StringBuilder builder = new StringBuilder("");
			LogicalLine line = emuera.GetScaningLine();
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
			if (!Program.DebugMode)
				return;
			window.ShowDebugDialog();
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
			dTraceLogChanged = true;
			dTraceLogList.Add(str);
		}
		public void DebugRemoveTraceLog()
		{
			if (!Program.DebugMode || runningERBfromMemory)
				return;
			dTraceLogChanged = true;
			if(dTraceLogList.Count > 0)
				dTraceLogList.RemoveAt(dTraceLogList.Count-1);
		}
		public void DebugClearTraceLog()
		{
			if (!Program.DebugMode || runningERBfromMemory)
				return;
			dTraceLogChanged = true;
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
				if (Config.UseRenameFile && (com.IndexOf("[[") >= 0) && (com.IndexOf("]]") >= 0))
				{
					foreach (KeyValuePair<string, string> pair in ParserMediator.RenameDic)
						com = com.Replace(pair.Key, pair.Value);
				}
				LogicalLine line = null;
				if (!com.StartsWith("@") && !com.StartsWith("\"") && !com.StartsWith("\\"))
					line = LogicalLineParser.ParseLine(com, null);
				if (line == null || (line is InvalidLine))
				{
					WordCollection wc = LexicalAnalyzer.Analyse(new StringStream(com), LexEndWith.EoL, LexAnalyzeFlag.None);
					AExpression term = ExpressionParser.ReduceExpressionTerm(wc, TermEndWith.EoL);
					if (term == null)
						throw new CodeEE(trerror.CanNotInterpretedLine.Text);
					if (term.GetOperandType() == typeof(Int64))
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
				emuera.DoDebugNormalFunction(func, munchkin);
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
				emuera.clearMethodStack();
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
			Point pos = window.MainPicBox.PointToClient(PlatformInterop.GetMousePosition());
			pos.Y -= ClientHeight;
			return pos;
		}

		/// <summary>
		/// マウス位置をボタンの選択状態に反映させる
		/// </summary>
		/// <param name="point"></param>
		/// <returns>この後でRefreshStringsが必要かどうか</returns>
		public bool MoveMouse(Point point)
		{

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
					Color c = DisplayUtils.ToColor(cbgButtonMap.Bitmap.GetPixel(mapPoint.X, mapPoint.Y));
					if (c.A == 255)
					{
						buttonNum = c.ToArgb() & 0xFFFFFF;
					}
				}
				if (buttonNum >= 0)
				{
					bool ret = (pointingString != null || selectingButton != null || buttonNum != selectingCBGButtonInt);
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
			bool canSelect = false;
			//数値か文字列の入力待ち状態でなければ選択中にはならない
			if (state == ConsoleState.Error)
				canSelect = true;
			else if (state == ConsoleState.WaitInput && inputReq.NeedValue)
				canSelect = true;
			int curLineY = 0;
			//スクリプト実行中は無視//入力・マクロ処理中は無視
			if(this.IsInProcess)
				goto end;
			//履歴表示中は無視
			//if (window.ScrollBar.Value != window.ScrollBar.Maximum)
			//	goto end;
			int pointX = point.X;
			int pointY = point.Y;
			ConsoleDisplayLine curLine;

			int bottomLineNo = (int)window.ScrollBar.Value - 1;
			if (displayLineList.Count - 1 < bottomLineNo)
				bottomLineNo = displayLineList.Count - 1;//1820 この処理不要な気がするけどエラー報告があったので入れとく
			//int topLineNo = bottomLineNo - (window.MainPicBox.Height/ Config.LineHeight);
			int topLineNo = bottomLineNo - (ClientHeight/ Config.LineHeight);
			if (topLineNo < 0)
				topLineNo = 0;

			//int relPointY = pointY - GameUtils.GetRealLength(window.MainPicBox.Height);
			int relPointY = pointY - (ClientHeight - DisplayUtils.HeightOffset);

			//下から上へ探索し発見次第打ち切り
			for (int i = bottomLineNo; i >= topLineNo; i--)
			{
				relPointY += Config.LineHeight;
				curLine = displayLineList[i];
				
				for (int b = 0; b < curLine.Buttons.Length; b++)
				{
					ConsoleButtonString button = curLine.Buttons[curLine.Buttons.Length - b - 1];
					if(button == null || button.StrArray == null)
						continue;
					if ((button.PointX <= pointX) && (button.PointX + button.Width >= pointX))
					{
						//if (relPointY >= 0 && relPointY <= Config.FontSize)
						//{
						//	pointing = button;
						//	if(pointing.IsButton)
						//		goto breakfor;
						//}
						foreach(AConsoleDisplayNode part in button.StrArray)
						{
							if(part == null)
								continue;
							if ((part.PointX <= pointX) && (part.PointX + part.Width >= pointX)
								&& (relPointY >= part.Top) && (relPointY <= part.Bottom))
							{
								pointing = button;
								curLineY = window.MainPicBox.Height - Config.LineHeight * (bottomLineNo - i + 1);
								if (pointing.IsButton)
								{
									pointingStrings.Add(button);
									goto breakfor;
								}
							}
						}
					}
				}
			}


			//int posy_bottom2up = window.MainPicBox.Height - pointY;
			//int logNum = window.ScrollBar.Maximum - window.ScrollBar.Value;
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
			if ((pointing == null) || (pointing.Generation != lastButtonGeneration))
				canSelect = false;
			else if (!pointing.IsButton)
				canSelect = false;
			else if ((state == ConsoleState.WaitInput && inputReq.InputType == InputType.IntValue) && (!pointing.IsInteger))
				canSelect = false;
		end:
			if (canSelect)
				select = pointing;
			bool needRefresh = select != selectingButton || pointing != pointingString || pointingStrings.Count != prevPointingStringsLen;
			pointingString = pointing;
			selectingButton = select;
			#region EmuEra-Rikaichan
			if (Config.RikaiEnabled && rikaichan.enabled)
			{
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
				rikaichan.output = rikaichan.laststr.Substring(rikaichan.strpos);

				needRefresh = true;
				goto rikaichan_end;

			rikaichan_not_found:
				if (rikaichan.hidden == false)
				{
					rikaichan.hidden = true;
					needRefresh = true;
				}
			}
		rikaichan_end:
			#endregion
			return needRefresh;
		}


		public void LeaveMouse()
		{
			bool needRefresh = selectingButton != null || pointingString != null;
			selectingButton = null;
			pointingString = null;
			pointingStrings.Clear();
			if(needRefresh)
			{
				RefreshStrings(true);
			}
		}

		private void verticalScrollBarUpdate()
		{
			window.Invoke(() =>
			{
				verticalScrollBarUpdateAsync();
			});
		}

		private void verticalScrollBarUpdateAsync()
		{
			int max = displayLineList.Count;
			if (max == 0)
				return;

			int move = max - (int)window.ScrollBar.Maximum;
			if (move == 0)
				return;

			if (move > 0)
			{
				window.ScrollBar.Maximum = max;
				window.ScrollBar.Value += move;
			}
			else
			{
				if (max > window.ScrollBar.Value)
					window.ScrollBar.Value = max;
				window.ScrollBar.Maximum = max;
			}
			window.ScrollBar.Enabled = max > 0;
		}
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
            emuera.BeginTitle();
			ReadAnyKey(false, false);
			callEmueraProgram("");
			RefreshStrings(true);
		}

		bool force_temporary = false;
        bool timer_suspended = false;
		ConsoleState prevState;
		InputRequest prevReq;

		public void ReloadErb()
		{
			if (state == ConsoleState.Error)
			{
				MessageBox.Show(trerror.CanNotUseWhenError.Text);
				return;
			}
			if (state == ConsoleState.Initializing)
			{
				MessageBox.Show(trerror.CanNotUseWhenInitialize.Text);
				return;
			}
            bool notRedraw = false;
            if (redraw == ConsoleRedraw.None)
            {
                notRedraw = true;
                redraw = ConsoleRedraw.Normal;
            }
            if (timer.Enabled)
            {
				timer.Enabled = false;
                timer_suspended = true;
            }
            prevState = state;
			prevReq = inputReq;
			state = ConsoleState.Initializing;
			PrintSingleLine(trsl.ReloadingErb.Text, true);
			force_temporary = true;
			emuera.ReloadErb();
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
                timer.Enabled = true;
				//タイマー待機中の時間ずれは修正しない。タイマー中にリロードしたらほぼ強制タイムアウトする程度は仕様のうちであろう。
            }
		}

		public void ReloadPartialErb(List<string> path)
		{
			if (state == ConsoleState.Error)
			{
				MessageBox.Show(trerror.CanNotUseWhenError.Text);
				return;
			}
			if (state == ConsoleState.Initializing)
			{
				MessageBox.Show(trerror.CanNotUseWhenInitialize.Text);
				return;
			}
            bool notRedraw = false;
            if (redraw == ConsoleRedraw.None)
            {
                notRedraw = true;
                redraw = ConsoleRedraw.Normal;
            }
            if (timer.Enabled)
            {
				timer.Enabled = false;
                timer_suspended = true;
            }
			prevState = state;
			prevReq = inputReq;
			state = ConsoleState.Initializing;
            PrintSingleLine(trsl.ReloadingErb.Text, true);
			force_temporary = true;
			emuera.ReloadPartialErb(path);
			force_temporary = false;
            PrintSingleLine(trsl.ReloadCompleted.Text, true);
			RefreshStrings(true);
            //強制的にボタン世代が切り替わるのを防ぐ
            updatedGeneration = true;
            if (notRedraw)
                redraw = ConsoleRedraw.None;
        }

		public void ReloadFolder(string erbPath)
		{
            if (state == ConsoleState.Error)
			{
				MessageBox.Show(trerror.CanNotUseWhenError.Text);
				return;
			}
			if (state == ConsoleState.Initializing)
			{
				MessageBox.Show(trerror.CanNotUseWhenInitialize.Text);
				return;
			}
            if (timer.Enabled)
            {
				timer.Enabled = false;
                timer_suspended = true;
            }
            List<string> paths = new List<string>();
			SearchOption op = SearchOption.AllDirectories;
			if (!Config.SearchSubdirectory)
				op = SearchOption.TopDirectoryOnly;
			//string[] fnames = Directory.GetFiles(erbPath, "*.ERB", op);
			string[] fnames = FileUtils.GetFiles(erbPath, "*.ERB", op);
			for (int i = 0; i < fnames.Length; i++)
				if (Path.GetExtension(fnames[i]).ToUpper() == ".ERB")
					paths.Add(fnames[i]);
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
            emuera.ReloadPartialErb(paths);
			force_temporary = false;
            PrintSingleLine(trsl.ReloadCompleted.Text, true);
			RefreshStrings(true);
            //強制的にボタン世代が切り替わるのを防ぐ
            updatedGeneration = true;
            if (notRedraw)
                redraw = ConsoleRedraw.None;
        }

		public void Dispose()
		{
			if(timer != null)
				timer.Dispose();
		}

		#region LazyLoading 内核缺失方法存根

		public MainWindow Window { get { return window; } }

		public Color? TextBackgroundColor { get; set; }

		public void AddBackgroundImage(string name, long depth, float opacity)
		{
			var spr = AppContents.GetSprite(name);
			if (spr == null || !spr.IsCreated) return;
			var bg = new ConsoleBackground(spr, opacity);
			var pair = new KeyValuePair<long, ConsoleBackground>(depth, bg);
			backgroundList.Add(pair);
			backgroundList.Sort((v1, v2) => (v1.Key >= v2.Key) ? -1 : 1);
			BakeBackground();
		}

		public void RemoveBackground(string key)
		{
			var idx = backgroundList.FindIndex((v) => v.Value.bgImage.Name == key);
			if (idx >= 0) backgroundList.RemoveAt(idx);
			BakeBackground();
		}

		public void ClearBackgroundImage()
		{
			backgroundList.Clear();
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

		private void BakeBackground()
		{
			if (bakedBackground == null) return;
			using var canvas = new SKCanvas(bakedBackground);
			canvas.Clear(SKColors.Transparent);
			foreach (var pair in backgroundList)
			{
				var bg = pair.Value.bgImage;
				var scaleW = bakedBackground.Width / (float)bg.DestBaseSize.Width;
				var scaleH = bakedBackground.Height / (float)bg.DestBaseSize.Height;
				var cropHorizontally = bg.DestBaseSize.Height * scaleW < bakedBackground.Height;
				var newWidth = (int)(bg.DestBaseSize.Width * (cropHorizontally ? scaleH : scaleW));
				var newHeight = (int)(bg.DestBaseSize.Height * (cropHorizontally ? scaleH : scaleW));
				var paddingX = (bakedBackground.Width - newWidth) / 2;
				var filter = pair.Value.GetColorFilter();
				bg.GraphicsDraw(canvas, new Rectangle(paddingX, 0, newWidth, newHeight), filter);
				filter?.Dispose();
			}
		}

		public PrintStringBuffer PrintBuffer { get { return printBuffer; } }

		public List<ConsoleDisplayLine> DisplayLineList { get { return displayLineList; } }

		public Dictionary<int, List<AConsoleDisplayNode>> EscapedParts { get; } = new Dictionary<int, List<AConsoleDisplayNode>>();

		public int GetLineNo { get { return lineNo; } }

		public void ClearText()
		{
			window.clear_richText();
		}

		public ConsoleButtonString PointingSring => pointingString;

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
			forceStopTimer();
			ClearDisplay();
			AppContents.UnloadGraphicList();
			redraw = ConsoleRedraw.Normal;
			UseUserStyle = false;
			userStyle = new StringStyle(Config.ForeColor, FontStyle.Regular, null);
			emuera.BeginTitle();
			ReadAnyKey(false, false);
			callEmueraProgram("");
			RefreshStrings(true);

			GlobalStatic.ctrlZ.mRewindInProgress = true;
			GlobalStatic.VEvaluator.Rand.SetRand(GlobalStatic.ctrlZ.mRandomSeed);
			GlobalStatic.Process.LoadSilent();
			PressEnterKey(true, GlobalStatic.ctrlZ.mLastSave.ToString(), false);

			var inputs = GlobalStatic.ctrlZ.mInputs;
			if (inputs.Count > 0)
				inputs.RemoveAt(inputs.Count - 1);

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
		}

		public bool CBG_SetImage(ASprite img, int x, int y, int zdepth, int width, int height, float opacity, float[] colorMatrix)
		{
			if (img == null || !img.IsCreated) return false;
			if (zdepth == 0) return false;
			var cbg = new ClientBackGroundImage(zdepth)
			{
				Img = img,
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

		public bool AlwaysRefresh { get; set; }

		public long AnimeTimer => redrawTimer.Enabled ? (long)redrawTimer.Interval : 0;

		public void SetImageLayer(string spriteName, long depth, int x, int y, int width, int height, int opacity, float[] colorMatrix, bool followScroll)
		{
			_imageLayerManager.SetLayer(spriteName, depth, x, y, width, height, opacity, colorMatrix, followScroll, window.ScrollBar.Value * Config.LineHeight);
		}

		public void OutputLog(string path, bool append)
		{
			if (path == null)
				OutputLog((string)null);
			else
				OutputLog(path);
		}

		public void SetToolTipFontName(string fontName) { _tooltipFontName = fontName; }

		public bool ExistsImageLayer(long depth) => _imageLayerManager.Exists(depth);

		public bool bitmapCacheEnabledForNextLine { get; set; }
		public void ClearImageLayerAll() { _imageLayerManager.ClearAll(); }
		public void ClearImageLayer(long zdepth) { _imageLayerManager.ClearLayer(zdepth); }
		public bool strictFontFallback { get; set; }
		public void SetToolTipImg(ASprite sprite) { _tooltipImg = sprite; }
		public void SetToolTipImg(bool value) { _tooltipImgEnabled = value; }
		public void SetToolTipFormat(long format) { _tooltipFormat = format; }
		public void CustomToolTip(bool b)
		{
			window.ToolTip.OwnerDraw = b;
		}
		public void SetToolTipFontSize(float size) { _tooltipFontSize = size; }

		private string _tooltipFontName = null;
		private float _tooltipFontSize = 0;
		private long _tooltipFormat = 0;
		private bool _tooltipImgEnabled = false;
		private ASprite _tooltipImg = null;
		private int _tooltipDuration = 0;

		#endregion


		#region Virtual Selection Navigation

		internal enum VirtualSelectionDirection
		{
			Left = 0,
			Up = 1,
			Right = 2,
			Down = 3,
		}

		struct VirtualSelectionCandidate
		{
			public ConsoleButtonString Button;
			public int LineNo;
			public int Left;
			public int Right;
			public int Top;
			public int Bottom;
			public int AnchorX;
			public int AnchorY;
			public int CenterX;
			public int CenterY;
		}

		public void NavigateSelection(VirtualSelectionDirection direction)
		{
			if (!CanNavigateSelection())
				return;

			bool lockTaken = false;
			displayLineListSpinLock.Enter(ref lockTaken);
			try
			{
				List<VirtualSelectionCandidate> candidates = CollectVisibleSelectionCandidates();
				VirtualSelectionCandidate? nearest = FindNearestSelectionCandidate(candidates, direction);
				if (nearest != null)
				{
					bool needRefresh = selectingButton != nearest.Value.Button || pointingString != nearest.Value.Button || selectingCBGButtonInt >= 0;
					selectingCBGButtonInt = -1;
					selectingButton = nearest.Value.Button;
					pointingString = nearest.Value.Button;
					if (needRefresh)
						RefreshStrings(true);
				}
			}
			finally
			{
				if (lockTaken)
					displayLineListSpinLock.Exit();
			}
		}

		public void ClickSelectedButton(SKMouseButton button)
		{
			if (selectingButton == null)
				return;
			InputMouseKey(1, (int)button, 0, 0, 0, selectingButton.Input);
		}

		private bool CanNavigateSelection()
		{
			if (IsInProcess)
				return false;
			if (state == ConsoleState.Error)
				return true;
			return state == ConsoleState.WaitInput && inputReq != null && inputReq.NeedValue;
		}

		private List<VirtualSelectionCandidate> CollectVisibleSelectionCandidates()
		{
			List<VirtualSelectionCandidate> candidates = new List<VirtualSelectionCandidate>();
			if (displayLineList.Count == 0)
				return candidates;

			int pointY = ClientHeight - DisplayUtils.HeightOffset;
			int bottomLineNo = GetScrollBarValueSafe() - 1;
			if (bottomLineNo < 0)
				return candidates;
			if (displayLineList.Count - 1 < bottomLineNo)
				bottomLineNo = displayLineList.Count - 1;
			int topLineNo = bottomLineNo - (pointY / Config.LineHeight + 1);
			if (topLineNo < 0)
				topLineNo = 0;
			pointY -= (bottomLineNo - topLineNo) * Config.LineHeight;

			for (int i = topLineNo; i <= bottomLineNo; i++)
			{
				ConsoleDisplayLine curLine = displayLineList[i];
				foreach (ConsoleButtonString button in curLine.Buttons)
				{
					if (!IsSelectableNavigationButton(button))
						continue;

					int minTop = int.MaxValue;
					int maxBottom = int.MinValue;
					if (button.StrArray != null)
					{
						foreach (AConsoleDisplayNode part in button.StrArray)
						{
							if (part == null) continue;
							if (part.Top < minTop) minTop = part.Top;
							if (part.Bottom > maxBottom) maxBottom = part.Bottom;
						}
					}
					if (minTop == int.MaxValue || maxBottom == int.MinValue)
					{
						minTop = 0;
						maxBottom = Config.FontSize;
					}

					candidates.Add(new VirtualSelectionCandidate
					{
						Button = button,
						LineNo = i,
						Left = button.PointX,
						Right = button.PointX + button.Width,
						Top = pointY + minTop,
						Bottom = pointY + maxBottom,
						AnchorX = button.PointX,
						AnchorY = pointY + (minTop + maxBottom) / 2,
						CenterX = button.PointX + button.Width / 2,
						CenterY = pointY + (minTop + maxBottom) / 2,
					});
				}
				pointY += Config.LineHeight;
			}
			return candidates;
		}

		private bool IsSelectableNavigationButton(ConsoleButtonString button)
		{
			if (button == null || !button.IsButton)
				return false;
			if (button.Width <= 0)
				return false;
			if (button.Generation != lastButtonGeneration)
				return false;
			if (state == ConsoleState.WaitInput && inputReq != null
				&& (inputReq.InputType == InputType.IntValue || inputReq.InputType == InputType.IntButton)
				&& !button.IsInteger)
				return false;
			return true;
		}

		private int GetScrollBarValueSafe()
		{
			try { return (int)window.ScrollBar.Value; }
			catch { return 0; }
		}

		private VirtualSelectionCandidate? FindNearestSelectionCandidate(List<VirtualSelectionCandidate> candidates, VirtualSelectionDirection direction)
		{
			if (candidates.Count == 0)
				return null;

			int fromX = pointingString != null ? pointingString.PointX + pointingString.Width / 2 : 0;
			int fromY = 0;
			if (pointingString != null && selectingButton != null)
			{
				// Use current selection center as origin
				foreach (AConsoleDisplayNode part in selectingButton.StrArray)
				{
					if (part != null) { fromY += part.Top + part.Bottom; break; }
				}
				fromY /= 2;
			}

			VirtualSelectionCandidate? best = null;
			int bestScore = int.MaxValue;

			foreach (var c in candidates)
			{
				if (selectingButton != null && c.Button == selectingButton)
					continue;
				if (!IsCandidateInDirection(direction, fromX, fromY, c))
					continue;

				int score = GetDirectionalEdgeDistance(direction, fromX, fromY, c);
				if (score < bestScore)
				{
					bestScore = score;
					best = c;
				}
			}

			// Fallback: if no candidate in direction, try orthogonal
			if (best == null)
			{
				foreach (var c in candidates)
				{
					if (selectingButton != null && c.Button == selectingButton)
						continue;
					int dist = Math.Abs(c.CenterX - fromX) + Math.Abs(c.CenterY - fromY);
					if (dist < bestScore)
					{
						bestScore = dist;
						best = c;
					}
				}
			}

			return best;
		}

		private bool IsCandidateInDirection(VirtualSelectionDirection direction, int fromX, int fromY, VirtualSelectionCandidate c)
		{
			switch (direction)
			{
				case VirtualSelectionDirection.Left: return c.Right <= fromX;
				case VirtualSelectionDirection.Right: return c.Left >= fromX;
				case VirtualSelectionDirection.Up: return c.Bottom <= fromY;
				case VirtualSelectionDirection.Down: return c.Top >= fromY;
				default: return false;
			}
		}

		private int GetDirectionalEdgeDistance(VirtualSelectionDirection direction, int fromX, int fromY, VirtualSelectionCandidate c)
		{
			switch (direction)
			{
				case VirtualSelectionDirection.Left: return fromX - c.Right;
				case VirtualSelectionDirection.Right: return c.Left - fromX;
				case VirtualSelectionDirection.Up: return fromY - c.Bottom;
				case VirtualSelectionDirection.Down: return c.Top - fromY;
				default: return int.MaxValue;
			}
		}

		#endregion

		#region Smart Button Mapping

		private static string NormalizeVirtualButtonText(string text)
		{
			if (string.IsNullOrEmpty(text))
				return string.Empty;
			return text.Replace(" ", "").Replace("\t", "").Replace("\r", "").Replace("\n", "").Trim();
		}

		internal string GetVisibleButtonKeywordLabel(params string[] keywords)
		{
			ConsoleButtonString button = FindVisibleButtonByKeywords(keywords, out _);
			return NormalizeVirtualButtonText(button?.ToString());
		}

		internal bool HasRecentDisplayLineKeyword(int recentLineCount, params string[] keywords)
		{
			if (keywords == null || keywords.Length == 0)
				return false;

			bool lockTaken = false;
			displayLineListSpinLock.Enter(ref lockTaken);
			try
			{
				if (displayLineList.Count == 0)
					return false;

				int bottomLineNo = displayLineList.Count - 1;
				int topLineNo = Math.Max(0, bottomLineNo - Math.Max(0, recentLineCount - 1));
				for (int i = bottomLineNo; i >= topLineNo; i--)
				{
					string lineText = NormalizeVirtualButtonText(displayLineList[i]?.ToString());
					if (string.IsNullOrEmpty(lineText))
						continue;

					for (int keywordIndex = 0; keywordIndex < keywords.Length; keywordIndex++)
					{
						string keyword = NormalizeVirtualButtonText(keywords[keywordIndex]);
						if (string.IsNullOrEmpty(keyword))
							continue;
						if (lineText.Contains(keyword))
							return true;
					}
				}
			}
			finally
			{
				if (lockTaken)
					displayLineListSpinLock.Exit();
			}
			return false;
		}

		internal bool TrySelectVisibleButtonByKeywords(params string[] keywords)
		{
			ConsoleButtonString matchedButton = FindVisibleButtonByKeywords(keywords, out _);
			if (matchedButton == null)
				return false;

			bool needRefresh = selectingButton != matchedButton || pointingString != matchedButton || selectingCBGButtonInt >= 0;
			selectingCBGButtonInt = -1;
			selectingButton = matchedButton;
			pointingString = matchedButton;
			if (needRefresh)
				RefreshStrings(true);
			return true;
		}

		internal bool TrySelectVisibleButtonByLabels(params string[] labels)
		{
			ConsoleButtonString matchedButton = FindVisibleButtonByLabels(labels);
			if (matchedButton == null)
				return false;

			bool needRefresh = selectingButton != matchedButton || pointingString != matchedButton || selectingCBGButtonInt >= 0;
			selectingCBGButtonInt = -1;
			selectingButton = matchedButton;
			pointingString = matchedButton;
			if (needRefresh)
				RefreshStrings(true);
			return true;
		}

		private ConsoleButtonString FindVisibleButtonByKeywords(string[] keywords, out string matchedKeyword)
		{
			matchedKeyword = null;
			if (keywords == null || keywords.Length == 0 || !CanNavigateSelection())
				return null;

			bool lockTaken = false;
			displayLineListSpinLock.Enter(ref lockTaken);
			try
			{
				List<VirtualSelectionCandidate> candidates = CollectVisibleSelectionCandidates();
				for (int i = 0; i < candidates.Count; i++)
				{
					string buttonText = NormalizeVirtualButtonText(candidates[i].Button?.ToString());
					if (string.IsNullOrEmpty(buttonText))
						continue;

					for (int keywordIndex = 0; keywordIndex < keywords.Length; keywordIndex++)
					{
						string keyword = NormalizeVirtualButtonText(keywords[keywordIndex]);
						if (string.IsNullOrEmpty(keyword))
							continue;
						if (buttonText.Contains(keyword))
						{
							matchedKeyword = keywords[keywordIndex];
							return candidates[i].Button;
						}
					}
				}
			}
			finally
			{
				if (lockTaken)
					displayLineListSpinLock.Exit();
			}
			return null;
		}

		private ConsoleButtonString FindVisibleButtonByLabels(string[] labels)
		{
			if (labels == null || labels.Length == 0 || !CanNavigateSelection())
				return null;

			bool lockTaken = false;
			displayLineListSpinLock.Enter(ref lockTaken);
			try
			{
				List<VirtualSelectionCandidate> candidates = CollectVisibleSelectionCandidates();
				for (int i = 0; i < candidates.Count; i++)
				{
					string buttonText = NormalizeVirtualButtonText(candidates[i].Button?.ToString());
					if (string.IsNullOrEmpty(buttonText))
						continue;

					for (int labelIndex = 0; labelIndex < labels.Length; labelIndex++)
					{
						string label = NormalizeVirtualButtonText(labels[labelIndex]);
						if (string.IsNullOrEmpty(label))
							continue;
						if (buttonText == label)
							return candidates[i].Button;
					}
				}
			}
			finally
			{
				if (lockTaken)
					displayLineListSpinLock.Exit();
			}
			return null;
		}

		#endregion

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

		public void SetOpacity(float opacity) { Opacity = opacity; }

		public SKColorFilter GetColorFilter()
		{
			if (Opacity >= 1.0f) return null;
			return SKColorFilter.CreateColorMatrix([
				1, 0, 0, 0, 0,
				0, 1, 0, 0, 0,
				0, 0, 1, 0, 0,
				0, 0, 0, Opacity, 0,
			]);
		}
	}
}