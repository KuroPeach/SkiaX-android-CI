using MinorShift.Emuera.GameView;
using MinorShift.Emuera.UI.Ewp;
using System;
using System.Drawing;

namespace MinorShift.Emuera.UI.Headless;

/// <summary>
/// 无窗宿主表面：虚拟画布 + 输入框内存态 + ToolTip/热键 no-op。
/// 只依赖 <see cref="IConsoleSurface"/>，不引用 WinForms/GDI+。
/// </summary>
internal sealed class HeadlessSurface : IConsoleSurface
{
	// 桌面 MainWindow.Render* = mainPicBox（ClientSize 减去垂直滚动条/菜单等 chrome）。
	// 无窗没有控件，但游戏布局读 CLIENTWIDTH/HEIGHT 时仍应看到与桌面一致的 render 画布；
	// 这里按 EWP 最小化窗口实测的 insets 固定（1280x720 client → 1262x667 render）。
	// N1-c 若需要在不同 FE/平台调整，可改为宿主参数。
	private const int ChromeWidth = 18;
	private const int ChromeHeight = 53;

	private readonly HeadlessSynchronizationContext ctx;
	private int renderWidth;
	private int renderHeight;
	private int clientWidth;
	private int clientHeight;
	private bool ready;
	private bool quit;
	private string title = "Emuera";
	private string textBoxText = "";
	private Color textBoxBackColor = Color.White;
	private Color textBoxForeColor = Color.Black;
	private int scrollValue;
	private int scrollMaximum;
	private bool scrollEnabled;
	private bool toolTipOwnerDraw;
	private int toolTipInitialDelay;
	private int toolTipAutoPopDelay;
	private Color toolTipForeColor = Color.Black;
	private Color toolTipBackColor = Color.White;

	public HeadlessSurface(HeadlessSynchronizationContext context, int initialWidth, int initialHeight)
	{
		ctx = context;
		renderWidth = Math.Max(1, initialWidth);
		renderHeight = Math.Max(1, initialHeight);
		clientWidth = renderWidth + ChromeWidth;
		clientHeight = renderHeight + ChromeHeight;
	}

	public bool QuitRequested => quit;

	public void Attach(EmueraConsole console)
	{
		ready = true;
	}

	// ── 画布 / 视口 ──────────────────────────────────────────────
	public int RenderWidth => renderWidth;
	public int RenderHeight => renderHeight;
	public Size ClientSize
	{
		get => new(clientWidth, clientHeight);
		set
		{
			clientWidth = Math.Max(1, value.Width);
			clientHeight = Math.Max(1, value.Height);
			renderWidth = Math.Max(1, clientWidth - ChromeWidth);
			renderHeight = Math.Max(1, clientHeight - ChromeHeight);
		}
	}
	public void PerformLayout() { }

	// ── 生命周期 / 就绪 ─────────────────────────────────────────
	public bool Created => ready;
	public bool IsHandleCreated => ready;
	public bool IsDisposed => quit;
	public bool IsActive => true;
	public string WindowStateName => "Normal";

	// ── 刷新 / 线程派发 / 退出 ──────────────────────────────────
	public void Refresh() { }
	public void Invalidate() { }
	public void InvalidatePicBox() { }
	public void Invoke(Action action) => ctx.Send(_ => action(), null);
	public void Close()
	{
		EwpDiagnostics.Trace("surface.Close quitBefore=" + quit);
		quit = true;
		ctx.RequestStop();
	}
	public void Reboot() => Close();
	public void Focus() { }
	public void ShowConfigDialog() { }
	public string Text
	{
		get => title;
		set => title = value;
	}

	// ── 输入框 ─────────────────────────────────────────────────
	public bool TextBoxPosChanged => false;
	public bool TextBoxIgnoreScrollBarChanges { get; set; }
	public string TextBoxText
	{
		get => textBoxText;
		set => textBoxText = value;
	}
	public Color TextBoxBackColor { set => textBoxBackColor = value; }
	public Color TextBoxForeColor { set => textBoxForeColor = value; }
	public void SetTextBoxFont(string fontName, float fontSize) { }
	public void ResetTextBoxPos() { }
	public void SetTextBoxPos(int xOffset, int yOffset, int width) { }
	public void ApplyTextBoxChanges() { }
	public void ChangeTextBox(string text) => textBoxText = text;
	public void ClearRichText() => textBoxText = "";
	public void UpdateLastInput() { }

	// ── 滚动条：虚拟跟随末尾 ────────────────────────────────────
	public int ScrollValue
	{
		get => scrollValue;
		set => scrollValue = value;
	}
	public int ScrollMaximum
	{
		get => scrollMaximum;
		set
		{
			scrollMaximum = value;
			// 无窗默认跟随末尾（桌面 vScrollBar.Value 由 setStBar 同步到 Maximum）
			scrollValue = value;
		}
	}
	public bool ScrollEnabled { set => scrollEnabled = value; }

	// ── 主画布几何 / 鼠标 ──────────────────────────────────────
	public Rectangle PicClientRectangle => new(0, 0, renderWidth, renderHeight);
	public int PicHeight => renderHeight;
	public Point MousePosition => Point.Empty;
	public Point ScreenMousePosition => Point.Empty;
	public Size ScreenWorkingArea => new(clientWidth, clientHeight);
	public int CursorHeight => 32;

	// ── ToolTip（no-op）─────────────────────────────────────────
	public bool ToolTipOwnerDraw
	{
		get => toolTipOwnerDraw;
		set => toolTipOwnerDraw = value;
	}
	public int ToolTipInitialDelay
	{
		get => toolTipInitialDelay;
		set => toolTipInitialDelay = value;
	}
	public int ToolTipAutoPopDelay
	{
		get => toolTipAutoPopDelay;
		set => toolTipAutoPopDelay = value;
	}
	public Color ToolTipForeColor
	{
		get => toolTipForeColor;
		set => toolTipForeColor = value;
	}
	public Color ToolTipBackColor
	{
		get => toolTipBackColor;
		set => toolTipBackColor = value;
	}
	public void ToolTipRemoveAll() { }
	public void ToolTipSetText(string text) { }
	public void ToolTipShow(string text, Point clientPoint, int duration) { }
	public void SetCustomToolTip(bool enable) { }

	// ── 热键（no-op）──────────────────────────────────────────
	public void HotkeyStateSet(nint key, nint state) { }
	public void HotkeyStateInit(nint key) { }
}
