using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xamarin.Forms;
using XEmuera.Forms;

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
		public virtual void RefreshQuickButtonGroup() { }
		public virtual void quickButton_Clicked(object sender, EventArgs e) { }
		public void ApplyTextBoxChanges() { }
		public void Load() { }
		public virtual void update_lastinput() { }
		public virtual void clear_richText() { }
		public virtual bool TextBoxPosChanged { get; set; }
		public void ResetTextBoxPos() { TextBoxPosChanged = false; }
		public virtual void SetTextBoxPos(int x, int y, int width = 0) { }
		public virtual bool TextBoxIgnoreScrollBarChanges { get; set; }
		public virtual void MainMenu_Reboot() { }
		public virtual void MainMenu_GotoTitle() { }

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
		private bool enabled = false;
		private bool availableStateArray = false;
		private bool availableHotkeyFile = false;
		private nint[] state;
		private List<int> bytecode = new(0x200);

		public bool Enabled => enabled;

		public void HotkeyStateInit(nint size)
		{
			state = new nint[size];
			availableStateArray = true;
		}

		public void HotkeyStateSet(nint index, nint value)
		{
			if (!availableStateArray)
				throw new Exception("use HOTKEY_STATE_INIT before using HOTKEY_STATE");
			state[index] = value;
		}

		private enum Eval
		{
			NUMBER = 0,
			KEYCOMPARE = 1,
			STATECOMPARE = 2,
			STATEGET = 3,
		}

		private enum Command
		{
			RETURN = 0,
			GOTOIFNOT = 1,
		}

		/// <summary>
		/// Maps System.Windows.Forms.Keys enum names to their integer values.
		/// Used for HOTKEY.ERB KEYS: parsing on platforms without WinForms.
		/// </summary>
		private static readonly Dictionary<string, int> KeyNameToValue = new(StringComparer.OrdinalIgnoreCase)
		{
			// Letter keys (VK_A=0x41 .. VK_Z=0x5A)
			{"A", 0x41}, {"B", 0x42}, {"C", 0x43}, {"D", 0x44}, {"E", 0x45},
			{"F", 0x46}, {"G", 0x47}, {"H", 0x48}, {"I", 0x49}, {"J", 0x4A},
			{"K", 0x4B}, {"L", 0x4C}, {"M", 0x4D}, {"N", 0x4E}, {"O", 0x4F},
			{"P", 0x50}, {"Q", 0x51}, {"R", 0x52}, {"S", 0x53}, {"T", 0x54},
			{"U", 0x55}, {"V", 0x56}, {"W", 0x57}, {"X", 0x58}, {"Y", 0x59},
			{"Z", 0x5A},
			// Digit keys (VK_0=0x30 .. VK_9=0x39)
			{"D0", 0x30}, {"D1", 0x31}, {"D2", 0x32}, {"D3", 0x33}, {"D4", 0x34},
			{"D5", 0x35}, {"D6", 0x36}, {"D7", 0x37}, {"D8", 0x38}, {"D9", 0x39},
			// Function keys (VK_F1=0x70 .. VK_F12=0x7B)
			{"F1", 0x70}, {"F2", 0x71}, {"F3", 0x72}, {"F4", 0x73}, {"F5", 0x74},
			{"F6", 0x75}, {"F7", 0x76}, {"F8", 0x77}, {"F9", 0x78}, {"F10", 0x79},
			{"F11", 0x7A}, {"F12", 0x7B},
			// Special keys
			{"Enter", 0x0D}, {"Return", 0x0D}, {"Escape", 0x1B}, {"Tab", 0x09},
			{"Space", 0x20}, {"Back", 0x08}, {"Delete", 0x2E}, {"Insert", 0x2D},
			{"Home", 0x24}, {"End", 0x23}, {"PageUp", 0x21}, {"PageDown", 0x22},
			{"Up", 0x26}, {"Down", 0x28}, {"Left", 0x25}, {"Right", 0x27},
			// Modifier keys
			{"Shift", 0x10}, {"Control", 0x11}, {"Alt", 0x12},
			// Numpad
			{"NumPad0", 0x60}, {"NumPad1", 0x61}, {"NumPad2", 0x62}, {"NumPad3", 0x63},
			{"NumPad4", 0x64}, {"NumPad5", 0x65}, {"NumPad6", 0x66}, {"NumPad7", 0x67},
			{"NumPad8", 0x68}, {"NumPad9", 0x69},
		};

		private static bool TryParseKeyName(string name, out int value)
		{
			return KeyNameToValue.TryGetValue(name, out value);
		}

		private int ParseEval(string line)
		{
			if (line.StartsWith("KEY == KEYS:"))
			{
				line = line.Substring("KEY == KEYS:".Length);
				if (!TryParseKeyName(line, out int keyValue))
					return 1;
				bytecode.Add((int)Eval.KEYCOMPARE);
				bytecode.Add(keyValue);
				return 0;
			}
			if (line.StartsWith("STATE:"))
			{
				line = line.Substring("STATE:".Length);
				int index = line.IndexOf(" == ");
				if (index != -1)
				{
					var line1 = line.Substring(0, index);
					var line2 = line.Substring(index + " == ".Length);
					bytecode.Add((int)Eval.STATECOMPARE);
					bytecode.Add(int.Parse(line1));
					bytecode.Add(int.Parse(line2));
					return 0;
				}
				bytecode.Add((int)Eval.STATEGET);
				bytecode.Add(int.Parse(line));
				return 0;
			}
			bytecode.Add((int)Eval.NUMBER);
			bytecode.Add(int.Parse(line));
			return 0;
		}

		private int Parse(string[] lines)
		{
			if (lines.Length < 2) return 1;
			if (lines[0] != "@HOTKEY(KEY)") return 1;

			bool lastLineWasSIF = false;
			var gotoStack = new Stack<int>();

			for (int i = 1; i < lines.Length; i++)
			{
				var line = lines[i];
				line = line.TrimStart();
				int semicolonIndex = line.IndexOf(';');
				if (semicolonIndex != -1)
					line = line.Substring(0, semicolonIndex);
				line = line.TrimEnd();
				if (line.Length == 0) continue;

				if (line.StartsWith("IF "))
				{
					line = line.Substring("IF ".Length);
					bytecode.Add((int)Command.GOTOIFNOT);
					gotoStack.Push(bytecode.Count);
					bytecode.Add(777);
					int res = ParseEval(line);
					if (res != 0) return res;
				}
				else if (line.StartsWith("RETURN "))
				{
					line = line.Substring("RETURN ".Length);
					bytecode.Add((int)Command.RETURN);
					int res = ParseEval(line);
					if (res != 0) return res;
				}
				else if (line.StartsWith("SIF "))
				{
					lastLineWasSIF = true;
					line = line.Substring("SIF ".Length);
					bytecode.Add((int)Command.GOTOIFNOT);
					gotoStack.Push(bytecode.Count);
					bytecode.Add(777);
					int res = ParseEval(line);
					if (res != 0) return res;
					continue;
				}
				else if (line == "ENDIF")
				{
					int lastGoto = gotoStack.Pop();
					bytecode[lastGoto] = bytecode.Count;
				}
				else
				{
					throw new Exception("invalid line");
				}

				if (lastLineWasSIF)
				{
					int lastGoto = gotoStack.Pop();
					bytecode[lastGoto] = bytecode.Count;
					lastLineWasSIF = false;
				}
			}

			return 0;
		}

		public void Toggle()
		{
			if (enabled)
			{
				enabled = false;
				MessageBox.Show("hotkeys OFF");
				return;
			}

			if (!enabled && availableHotkeyFile)
			{
				enabled = true;
				MessageBox.Show("hotkeys ON");
				return;
			}

			var filename = MinorShift.Emuera.Program.ExeDir + "HOTKEY.ERB";
			if (!File.Exists(filename))
			{
				MessageBox.Show("HOTKEY.ERB not found.");
				return;
			}

			string[] lines = File.ReadAllLines(filename, EncodingHandler.DetectEncoding(filename));
			int res = Parse(lines);
			if (res != 0)
			{
				MessageBox.Show("HOTKEY.ERB parsing failed.");
				return;
			}

			availableHotkeyFile = true;
			enabled = true;
			if (availableStateArray)
				MessageBox.Show("hotkeys ON");
			else
				MessageBox.Show("hotkeys ON, HOTKEY_STATE_INIT not called.");
		}

		private int InterpreterEval(ref int i, int key)
		{
			int b = bytecode[i++];
			if (b == (int)Eval.NUMBER)
			{
				b = bytecode[i++];
				return b;
			}
			else if (b == (int)Eval.KEYCOMPARE)
			{
				b = bytecode[i++];
				return Convert.ToInt32(key == b);
			}
			else if (b == (int)Eval.STATECOMPARE)
			{
				b = bytecode[i++];
				int stateIndex = b;
				b = bytecode[i++];
				int valueToCompare = b;
				return Convert.ToInt32(state[stateIndex] == valueToCompare);
			}
			else if (b == (int)Eval.STATEGET)
			{
				b = bytecode[i++];
				return (int)state[b];
			}

			throw new Exception("something is wrong");
		}

		/// <summary>
		/// Interprets a key code against the HOTKEY.ERB bytecode.
		/// Returns the hotkey number, or -1 if no match.
		/// </summary>
		public int KeyToNumber(int key)
		{
			if (!enabled) return -1;
			if (!availableStateArray) return -1;
			if (!availableHotkeyFile) return -1;

			for (int i = 0; i < bytecode.Count;)
			{
				int b = bytecode[i++];
				if (b == (int)Command.GOTOIFNOT)
				{
					b = bytecode[i++];
					int res = InterpreterEval(ref i, key);
					if (res == 0)
					{
						i = b;
						if (i < 0 || i + 1 > bytecode.Count)
							throw new Exception("something is wrong");
					}
				}
				else if (b == (int)Command.RETURN)
				{
					int res = InterpreterEval(ref i, key);
					return res;
				}
				else
				{
					throw new Exception("something is wrong");
				}
			}
			return -1;
		}
	}
}
