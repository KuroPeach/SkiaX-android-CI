using System;
using System.Drawing;

namespace MinorShift.Emuera.UI;

/// <summary>
/// EmueraConsole 依赖的宿主表面（跨平台）。桌面由 <c>Forms.MainWindow : Form</c> 直接实现，
/// 无窗由 <c>HeadlessSurface</c> 实现；接口本身不得引用任何 WinForms / GDI+ 类型。
/// 依据＝工作区 specs《2026-10-26-N0-依赖体检与无窗化分层》§3。
/// </summary>
internal interface IConsoleSurface
{
	// ── 画布 / 视口 ──────────────────────────────────────────────
	int RenderWidth { get; }
	int RenderHeight { get; }
	Size ClientSize { get; set; }
	void PerformLayout();

	// ── 生命周期 / 就绪 ─────────────────────────────────────────
	bool Created { get; }
	bool IsHandleCreated { get; }
	bool IsDisposed { get; }
	bool IsActive { get; }
	string WindowStateName { get; }

	// ── 刷新 / 线程派发 / 退出 ──────────────────────────────────
	void Refresh();
	void Invalidate();
	void InvalidatePicBox();
	void Invoke(Action action);
	void Close();
	void Reboot();
	void Focus();
	void ShowConfigDialog();
	string Text { get; set; }

	// ── 输入框（richTextBox1 语义） ─────────────────────────────
	bool TextBoxPosChanged { get; }
	bool TextBoxIgnoreScrollBarChanges { get; set; }
	string TextBoxText { get; set; }
	Color TextBoxBackColor { set; }
	Color TextBoxForeColor { set; }
	void SetTextBoxFont(string fontName, float fontSize);
	void ResetTextBoxPos();
	void SetTextBoxPos(int xOffset, int yOffset, int width);
	void ApplyTextBoxChanges();
	void ChangeTextBox(string text);
	void ClearRichText();
	void UpdateLastInput();

	// ── 滚动条（可见行范围）─────────────────────────────────────
	int ScrollValue { get; set; }
	int ScrollMaximum { get; set; }
	bool ScrollEnabled { set; }

	// ── 主画布几何 / 鼠标 ───────────────────────────────────────
	Rectangle PicClientRectangle { get; }
	int PicHeight { get; }
	/// <summary>主画布客户区坐标下的物理鼠标位置；无窗返回 Point.Empty。</summary>
	Point MousePosition { get; }
	/// <summary>屏幕坐标下的物理鼠标位置；无窗返回 Point.Empty。</summary>
	Point ScreenMousePosition { get; }
	Size ScreenWorkingArea { get; }
	int CursorHeight { get; }

	// ── ToolTip（桌面 GDI 绘制保留在 MainWindow，headless 全 no-op）──
	bool ToolTipOwnerDraw { get; set; }
	int ToolTipInitialDelay { get; set; }
	int ToolTipAutoPopDelay { get; set; }
	Color ToolTipForeColor { get; set; }
	Color ToolTipBackColor { get; set; }
	void ToolTipRemoveAll();
	void ToolTipSetText(string text);
	void ToolTipShow(string text, Point clientPoint, int duration);
	void SetCustomToolTip(bool enable);

	// ── 热键（HotkeyState；headless no-op）──────────────────────
	void HotkeyStateSet(nint key, nint state);
	void HotkeyStateInit(nint key);
}
