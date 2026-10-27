using MinorShift.Emuera.UI;
using SkiaSharp.Views.Desktop;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace MinorShift.Emuera.Forms;

/// <summary>
/// <see cref="IConsoleSurface"/> 的 WinForms 实现（桌面行为不变）。
/// 只放「接口适配 + 桌面 ToolTip 自绘」；绘制/输入原逻辑仍在 MainWindow.cs。
/// </summary>
internal sealed partial class MainWindow
{
	// ── 画布 / 视口 ──────────────────────────────────────────────
	// RenderWidth / RenderHeight / ClientSize / PerformLayout 走既有 public 成员。

	void IConsoleSurface.Refresh() => Refresh();
	void IConsoleSurface.Invalidate() => Invalidate();
	void IConsoleSurface.InvalidatePicBox() => mainPicBox.Invalidate();

	void IConsoleSurface.Invoke(Action action)
	{
		if (InvokeRequired)
			base.Invoke((Delegate)action);
		else
			action();
	}

	void IConsoleSurface.Close() => Close();
	void IConsoleSurface.Reboot() => Reboot();
	void IConsoleSurface.Focus() => Focus();

	// ── 生命周期 / 就绪 ─────────────────────────────────────────
	// Created / IsHandleCreated / IsDisposed 走既有 Control 成员。
	bool IConsoleSurface.IsActive => Form.ActiveForm != null;
	string IConsoleSurface.WindowStateName => WindowState.ToString();

	// ── 输入框（richTextBox1 语义） ─────────────────────────────
	bool IConsoleSurface.TextBoxPosChanged => TextBoxPosChanged;
	bool IConsoleSurface.TextBoxIgnoreScrollBarChanges
	{
		get => TextBoxIgnoreScrollBarChanges;
		set => TextBoxIgnoreScrollBarChanges = value;
	}
	string IConsoleSurface.TextBoxText
	{
		get => richTextBox1.Text;
		set => richTextBox1.Text = value;
	}
	Color IConsoleSurface.TextBoxBackColor
	{
		// RichTextBox/Control 不接受 alpha<255；ERB 的 SETBGCOLOR 可能带透明色。
		// 旧路径（ToDrawingColor）在本机实测会落到这里；显式剥 alpha 防止 RefreshStrings 崩。
		set => richTextBox1.BackColor = Color.FromArgb(255, value.R, value.G, value.B);
	}
	Color IConsoleSurface.TextBoxForeColor
	{
		set => richTextBox1.ForeColor = Color.FromArgb(255, value.R, value.G, value.B);
	}
	void IConsoleSurface.SetTextBoxFont(string fontName, float fontSize)
		=> richTextBox1.Font = new Font(fontName, fontSize, FontStyle.Regular, GraphicsUnit.Pixel);
	void IConsoleSurface.ResetTextBoxPos() => ResetTextBoxPos();
	void IConsoleSurface.SetTextBoxPos(int xOffset, int yOffset, int width) => SetTextBoxPos(xOffset, yOffset, width);
	void IConsoleSurface.ApplyTextBoxChanges() => ApplyTextBoxChanges();
	void IConsoleSurface.ChangeTextBox(string text) => ChangeTextBox(text);
	void IConsoleSurface.ClearRichText() => clear_richText();
	void IConsoleSurface.UpdateLastInput() => update_lastinput();

	// ── 滚动条 ─────────────────────────────────────────────────
	int IConsoleSurface.ScrollValue
	{
		get => vScrollBar.Value;
		set => vScrollBar.Value = value;
	}
	int IConsoleSurface.ScrollMaximum
	{
		get => vScrollBar.Maximum;
		set => vScrollBar.Maximum = value;
	}
	bool IConsoleSurface.ScrollEnabled { set => vScrollBar.Enabled = value; }

	// ── 主画布几何 / 鼠标 ──────────────────────────────────────
	Rectangle IConsoleSurface.PicClientRectangle => mainPicBox.ClientRectangle;
	int IConsoleSurface.PicHeight => mainPicBox.Height;
	Point IConsoleSurface.MousePosition => mainPicBox.PointToClient(Cursor.Position);
	Point IConsoleSurface.ScreenMousePosition => Cursor.Position;
	Size IConsoleSurface.ScreenWorkingArea => Screen.FromPoint(Cursor.Position).WorkingArea.Size;
	int IConsoleSurface.CursorHeight => Cursor.Current != null ? Cursor.Current.Size.Height : 32;

	// ── ToolTip ────────────────────────────────────────────────
	bool IConsoleSurface.ToolTipOwnerDraw
	{
		get => toolTipButton.OwnerDraw;
		set => toolTipButton.OwnerDraw = value;
	}
	int IConsoleSurface.ToolTipInitialDelay
	{
		get => toolTipButton.InitialDelay;
		set => toolTipButton.InitialDelay = value;
	}
	int IConsoleSurface.ToolTipAutoPopDelay
	{
		get => toolTipButton.AutoPopDelay;
		set => toolTipButton.AutoPopDelay = value;
	}
	Color IConsoleSurface.ToolTipForeColor
	{
		get => toolTipButton.ForeColor;
		set => toolTipButton.ForeColor = value;
	}
	Color IConsoleSurface.ToolTipBackColor
	{
		get => toolTipButton.BackColor;
		set => toolTipButton.BackColor = value;
	}
	void IConsoleSurface.ToolTipRemoveAll() => toolTipButton.RemoveAll();
	void IConsoleSurface.ToolTipSetText(string text) => toolTipButton.SetToolTip(mainPicBox, text);
	void IConsoleSurface.ToolTipShow(string text, Point clientPoint, int duration)
	{
		if (duration == 0)
			toolTipButton.Show(text, mainPicBox, clientPoint);
		else
			toolTipButton.Show(text, mainPicBox, clientPoint, duration);
	}

	void IConsoleSurface.SetCustomToolTip(bool enable)
	{
		if (!enable)
		{
			toolTipButton.Draw -= ConsoleSurfaceToolTip_Draw;
			toolTipButton.Popup -= ConsoleSurfaceToolTip_Popup;
		}
		else if (!toolTipButton.OwnerDraw)
		{
			toolTipButton.Draw += ConsoleSurfaceToolTip_Draw;
			toolTipButton.Popup += ConsoleSurfaceToolTip_Popup;
		}
		toolTipButton.OwnerDraw = enable;
	}

	// ── 热键 ───────────────────────────────────────────────────
	void IConsoleSurface.HotkeyStateSet(nint key, nint state) => hotkeyState.HotkeyStateSet(key, state);
	void IConsoleSurface.HotkeyStateInit(nint key) => hotkeyState.HotkeyStateInit(key);

	// ── 桌面 ToolTip 自绘（原 EmueraConsole.ToolTip_Draw / Popup，2026-10-26 迁入）──
	private void ConsoleSurfaceToolTip_Draw(object sender, DrawToolTipEventArgs e)
	{
		var c = console;
		if (c == null)
			return;

		if (c.TooltipImageRequested && int.TryParse(e.ToolTipText, out int i))
		{
			var g = MinorShift.Emuera.GameData.Function.FunctionMethodCreator.ReadGraphics(i);
			if (g.IsCreated)
			{
				try
				{
					e.Graphics.DrawImage(g.SKBitmap.ToBitmap(), 0, 0);
					return;
				}
				catch (Exception)
				{
					// Fall through to default tooltip handling if conversion fails
				}
			}
		}
		e.DrawBackground();
		e.DrawBorder();
		foreach (var ff in GlobalStatic.Pfc.FamilyNames)
		{
			if (ff == c.TooltipFontName)
			{
				using (Font f = new(ff, (float)c.TooltipFontSize))
				{
					TextRenderer.DrawText(e.Graphics, e.ToolTipText, f, e.Bounds,
						toolTipButton.ForeColor, toolTipButton.BackColor, (TextFormatFlags)c.TooltipFormat);
				}
				return;
			}
		}
		using (Font f = new(c.TooltipFontName, (float)c.TooltipFontSize))
		{
			TextRenderer.DrawText(e.Graphics, e.ToolTipText, f, e.Bounds,
				toolTipButton.ForeColor, toolTipButton.BackColor, (TextFormatFlags)c.TooltipFormat);
		}
	}

	private void ConsoleSurfaceToolTip_Popup(object sender, PopupEventArgs e)
	{
		var c = console;
		if (c == null)
			return;

		if (c.TooltipImageRequested && int.TryParse(toolTipButton.GetToolTip(e.AssociatedControl), out int i))
		{
			var g = MinorShift.Emuera.GameData.Function.FunctionMethodCreator.ReadGraphics(i);
			if (g.IsCreated)
			{
				e.ToolTipSize = new Size(g.Width, g.Height);
				c.TooltipSize = e.ToolTipSize;
				return;
			}
		}
		Font f;
		foreach (var ff in GlobalStatic.Pfc.FamilyNames)
		{
			if (ff == c.TooltipFontName)
			{
				f = new Font(ff, (float)c.TooltipFontSize);
				goto foundfont;
			}
		}
		f = new Font(c.TooltipFontName, (float)c.TooltipFontSize);
	foundfont:
		var size = TextRenderer.MeasureText(toolTipButton.GetToolTip(e.AssociatedControl), f,
			new Size(int.MaxValue, int.MaxValue), (TextFormatFlags)c.TooltipFormat);
		e.ToolTipSize = new Size(size.Width, size.Height);
		c.TooltipSize = e.ToolTipSize;
	}
}
