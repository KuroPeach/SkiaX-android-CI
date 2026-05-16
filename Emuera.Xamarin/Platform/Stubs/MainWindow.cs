using SkiaSharp;
using System;
using Xamarin.Forms;

namespace MinorShift.Emuera.UI.Framework.Forms
{
	internal class MainWindow : ContentPage
	{
		public MainWindow() { }
		public MainWindow(string[] args) { }

		public new virtual string Title { get; set; } = "";
		public virtual string Text { get => Title; set => Title = value; }
		public new virtual SKColor BackgroundColor { get; set; } = SKColors.Black;
		public new virtual bool IsEnabled { get; set; } = true;
		public virtual bool Created { get => IsEnabled; }

		public virtual ScrollBar ScrollBar { get; } = new ScrollBar();
		public virtual QuickButtonGroup QuickButtonGroup { get; } = new QuickButtonGroup();
		public QuickButtonGroup quickButtonGroup => QuickButtonGroup;
		public Xamarin.Forms.ScrollView quickButtonScrollView { get; set; } = new Xamarin.Forms.ScrollView();
		public virtual MainPicBoxStub MainPicBox { get; } = new MainPicBoxStub();

		public virtual void Reboot() { }
		public virtual void Close() { }
		public virtual void Refresh() { }
		public virtual void Invoke(Action action) { action?.Invoke(); }
		public new virtual void Focus() { }
		public virtual void ShowConfigDialog() { }
		public virtual void ShowDebugDialog() { }
		public void RefreshQuickButtonGroup() { }
		public virtual void quickButton_Clicked(object sender, EventArgs e) { }
		public void ApplyTextBoxChanges() { }
		public void Load() { }
		public virtual void update_lastinput() { }
		public virtual void clear_richText() { }
		public virtual bool TextBoxPosChanged { get; set; }
		public void ResetTextBoxPos() { TextBoxPosChanged = false; }
		public virtual void SetTextBoxPos(int x, int y, int width = 0) { }
		public virtual bool TextBoxIgnoreScrollBarChanges { get; set; }
		public void MainMenu_Reboot() { }
		public void MainMenu_GotoTitle() { }

		public HotkeyState hotkeyState { get; } = new HotkeyState();

		public virtual TextBoxStub TextBox { get; } = new TextBoxStub();

		public virtual ToolTipStub ToolTip { get; } = new ToolTipStub();

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
		public virtual bool Enabled { get; set; } = true;
	}

	internal class QuickButtonGroup : Xamarin.Forms.StackLayout
	{
		public virtual void AddButton(string text, int value) { }
		public virtual void Clear() { }
	}

	internal class MainPicBoxStub
	{
		public virtual int Width { get; set; }
		public virtual int Height { get; set; }
		public virtual Rectangle ClientRectangle => new Rectangle(0, 0, Width, Height);
		public virtual Point PointToClient(Point screenPoint) => screenPoint;
	}

	internal class TextBoxStub
	{
		public virtual Color BackColor { get; set; }
		public virtual Color ForeColor { get; set; }
		public virtual Font Font { get; set; }
		public string Text { get; set; } = "";
	}

	internal class ToolTipStub
	{
		public virtual int InitialDelay { get; set; }
		public virtual int AutoPopDelay { get; set; }
		public virtual Color ForeColor { get; set; }
		public virtual Color BackColor { get; set; }
		public virtual bool OwnerDraw { get; set; }
		public event EventHandler Draw { add { } remove { } }
		public event EventHandler Popup { add { } remove { } }
		public virtual void RemoveAll() { }
		public virtual void Show(string text, object control, Point point) { }
		public virtual void Show(string text, object control, Point point, int duration) { }
	}

	internal class HotkeyState
	{
		public void HotkeyStateInit(nint v) { }
		public void HotkeyStateSet(nint v1, nint v2) { }
	}
}
