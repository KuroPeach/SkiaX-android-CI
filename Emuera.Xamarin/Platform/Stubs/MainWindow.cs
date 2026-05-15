using SkiaSharp;
using System;
using Xamarin.Forms;

namespace MinorShift.Emuera.UI.Framework.Forms
{
	internal class MainWindow
	{
		public MainWindow() { }
		public MainWindow(string[] args) { }

		public string Title { get; set; } = "";
		public SKColor BackgroundColor { get; set; } = SKColors.Black;
		public bool IsEnabled { get; set; } = true;

		public ScrollBar ScrollBar { get; } = new ScrollBar();
		public QuickButtonGroup quickButtonGroup { get; } = new QuickButtonGroup();
		public object quickButtonScrollView { get; set; }

		public void Reboot() { }
		public void Close() { }
		public void Refresh() { }
		public void RefreshQuickButtonGroup() { }
		public void quickButton_Clicked(object sender, EventArgs e) { }
		public void ApplyTextBoxChanges() { }
		public void Load() { }
		public void update_lastinput() { }
		public void clear_richText() { }
	}

	internal class ScrollBar
	{
		public int Minimum { get; set; }
		public int Maximum { get; set; }
		public int Value { get; set; }
		public int LargeChange { get; set; }
		public bool Visible { get; set; }
	}

	internal class QuickButtonGroup : StackLayout
	{
		public void AddButton(string text, int value) { }
		public void Clear() { }
	}
}
