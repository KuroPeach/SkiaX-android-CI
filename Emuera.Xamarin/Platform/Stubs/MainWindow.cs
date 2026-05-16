using SkiaSharp;
using System;
using Xamarin.Forms;

namespace MinorShift.Emuera.UI.Framework.Forms
{
	internal class MainWindow
	{
		public MainWindow() { }
		public MainWindow(string[] args) { }

		public virtual string Title { get; set; } = "";
		public virtual SKColor BackgroundColor { get; set; } = SKColors.Black;
		public virtual bool IsEnabled { get; set; } = true;

		public virtual ScrollBar ScrollBar { get; } = new ScrollBar();
		public QuickButtonGroup quickButtonGroup { get; } = new QuickButtonGroup();
		public ScrollView quickButtonScrollView { get; set; } = new ScrollView();
		public MainPicBoxStub MainPicBox { get; } = new MainPicBoxStub();

		public virtual void Reboot() { }
		public virtual void Close() { }
		public virtual void Refresh() { }
		public void RefreshQuickButtonGroup() { }
		public void quickButton_Clicked(object sender, EventArgs e) { }
		public void ApplyTextBoxChanges() { }
		public void Load() { }
		public virtual void update_lastinput() { }
		public virtual void clear_richText() { }
		public void ResetTextBoxPos() { }
		public void SetTextBoxPos(int x, int y, int width = 0) { }
		public void MainMenu_Reboot() { }
		public void MainMenu_GotoTitle() { }

		public HotkeyState hotkeyState { get; } = new HotkeyState();

		public TextBoxStub TextBox { get; } = new TextBoxStub();

		public void ChangeTextBox(int type = 0) { }
		public virtual void ChangeTextBox(string str) { }
	}

	internal class ScrollBar
	{
		public virtual int Minimum { get; set; }
		public virtual int Maximum { get; set; }
		public virtual int Value { get; set; }
		public int LargeChange { get; set; }
		public bool Visible { get; set; }
	}

	internal class QuickButtonGroup : StackLayout
	{
		public void AddButton(string text, int value) { }
		public void Clear() { }
	}

	internal class MainPicBoxStub
	{
		public System.Drawing.Rectangle ClientRectangle => new System.Drawing.Rectangle(0, 0, 0, 0);
		public System.Drawing.Point PointToClient(System.Drawing.Point screenPoint) => screenPoint;
	}

	internal class TextBoxStub
	{
		public System.Drawing.Color BackColor { get; set; }
		public string Text { get; set; } = "";
	}

	internal class HotkeyState
	{
		public void HotkeyStateInit(nint v) { }
		public void HotkeyStateSet(nint v1, nint v2) { }
	}
}
