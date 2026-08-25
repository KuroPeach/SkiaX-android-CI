using MinorShift.Emuera.GameProc;
using MinorShift.Emuera.GameView;
using MinorShift.Emuera.Runtime.Config;
using MinorShift.Emuera.Runtime.Script;
using MinorShift.Emuera.Runtime.Script.Parser;
using MinorShift.Emuera.Runtime.Script.Statements.Expression;
using MinorShift.Emuera.Runtime.Utils;
using MinorShift.Emuera.Runtime.Utils.EvilMask;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace MinorShift.Emuera.Forms
{
	public partial class DebugDialog : Form
	{
		public DebugDialog()
		{
			InitializeComponent();

			TopMost = Config.DebugWindowTopMost;
			int width = Math.Max(MinimumSize.Width, Config.DebugWindowWidth);
			int height = Math.Max(MinimumSize.Height, Config.DebugWindowHeight);
			Size = new Size(width, height);
			if (Config.DebugSetWindowPos)
			{
				StartPosition = FormStartPosition.Manual;
				Location = new Point(Config.DebugWindowPosX, Config.DebugWindowPosY);
			}
			checkBoxTopMost.Checked = TopMost;
			//锁定=持续赋值：CE 式周期重申（每200ms在安全时机写回锁定值）
			//注意：本窗体 Designer 未初始化 components 容器，定时器不能挂到容器上，改为关闭时显式释放
			holdTimer = new System.Windows.Forms.Timer { Interval = 200 };
			holdTimer.Tick += holdTimer_Tick;
			holdTimer.Start();
			FormClosed += (s, e) =>
			{
				if (holdTimer != null)
				{
					holdTimer.Stop();
					holdTimer.Dispose();
					holdTimer = null;
				}
			};
			loadWatchList();
		}
		private Process emuera;
		private EmueraConsole mainConsole;

		internal void SetParent(EmueraConsole console, Process process)
		{
			emuera = process;
			mainConsole = console;
		}

		public void TranslateUI()
		{
			Text = Lang.UI.DebugDialog.Text;

			toolStripMenuItem1.Text = Lang.UI.MainWindow.File.Text;
			ウォッチリストの保存ToolStripMenuItem.Text = Lang.UI.DebugDialog.File.SaveWatchList.Text;
			ウォッチリストの読込ToolStripMenuItem.Text = Lang.UI.DebugDialog.File.LoadWatchList.Text;
			閉じるToolStripMenuItem.Text = Lang.UI.DebugDialog.Close.Text;

			設定ToolStripMenuItem.Text = Lang.UI.DebugDialog.Setting.Text;
			設定ToolStripMenuItem1.Text = Lang.UI.DebugDialog.Setting.Config.Text;

			tabPageWatch.Text = Lang.UI.DebugDialog.VariableWatch.Text;
			columnHeaderLock.Text = Lang.UI.DebugDialog.VariableWatch.Lock.Text;
			columnHeader1.Text = Lang.UI.DebugDialog.VariableWatch.Object.Text;
			columnHeader3.Text = Lang.UI.DebugDialog.VariableWatch.Value.Text;

			tabPageTrace.Text = Lang.UI.DebugDialog.StackTrace.Text;
			tabPageConsole.Text = Lang.UI.DebugDialog.Console.Text;

			checkBoxTopMost.Text = Lang.UI.DebugDialog.StayOnTop.Text;
			button2.Text = Lang.UI.DebugDialog.UpdateData.Text;
			button1.Text = Lang.UI.DebugDialog.Close.Text;
		}

		public string ConsoleText
		{
			get { return textBoxConsole.Text; }
			set { textBoxConsole.Text = value; }
		}
		public string TraceText
		{
			get { return textBoxTrace.Text; }
			set { textBoxTrace.Text = value; }
		}
		public void AddTraceText(string str)
		{
			SuspendLayout();
			textBoxTrace.Text += str;
			ResumeLayout(false);
		}

		public void UpdateData()
		{
			if (tabControlMain.SelectedTab == tabPageWatch)
				updateVarWatch();
			else if (tabControlMain.SelectedTab == tabPageTrace)
				updateTrace();
			else if (tabControlMain.SelectedTab == tabPageConsole)
				updateConsole();
		}

		private void tabControlMain_Selected(object sender, TabControlEventArgs e)
		{
			UpdateData();
		}

		private void updateTrace()
		{
			string str = mainConsole.GetDebugTraceLog(false);
			if (str != null)
				textBoxTrace.Text = str;
			//textBoxTrace.SelectionStart = textBoxTrace.Text.Length;
			//textBoxTrace.Focus();
			//textBoxTrace.ScrollToCaret();
		}

		private void updateConsole()
		{
			textBoxConsole.Text = mainConsole.DebugConsoleLog;
			//textBoxConsole.SelectionStart = textBoxConsole.Text.Length;
			//textBoxConsole.Focus();
			//textBoxConsole.ScrollToCaret();
		}

		private void updateVarWatch()
		{
			var proc = GlobalStatic.Process;

			GlobalStatic.Process.saveCurrentState(false);

			try
			{
				for (int i = 0; i < listViewWatch.Items.Count - 1; i++)
				{//無名のアイテムを削除（編集中の行は対象外）
					if ((listViewWatch.Items[i] != editingItem) && (listViewWatch.Items[i].SubItems[1].Text.Length == 0))
					{
						listViewWatch.Items.RemoveAt(i);
						i--;
					}
				}
				if ((listViewWatch.Items.Count == 0) || ((listViewWatch.Items[^1] != editingItem) && (!string.IsNullOrEmpty(listViewWatch.Items[^1].SubItems[1].Text))))
				{
					ListViewItem newLVI = new("");
					newLVI.SubItems.Add(new ListViewItem.ListViewSubItem(newLVI, ""));
					newLVI.SubItems.Add(new ListViewItem.ListViewSubItem(newLVI, ""));
					listViewWatch.Items.Add(newLVI);
				}
				foreach (ListViewItem lvi in listViewWatch.Items)
				{
					string expr = lvi.SubItems[1].Text;
					if (string.IsNullOrEmpty(expr))
						continue;
					if (lvi.Checked)
					{
						//锁定=持续赋值：把值单元格中的冻结值写回变量（仅安全时机），防止其漂移
						if (!mainConsole.IsInProcess && lvi.SubItems[2].Text.Length > 0)
							TryHoldWrite(lvi);//失败→值单元格显示错误并自动解锁
						continue;
					}
					string val = getValueString(expr);
					lvi.SubItems[2].Text = val;
				}
			}
			finally
			{
				GlobalStatic.Process.clearMethodStack();
				GlobalStatic.Process.loadPrevState();
			}
			Update();
		}
		private string getValueString(string str)
		{
			TryEvalValue(str, out string val, out _);
			return val ?? "";
		}

		//求值表达式：成功返回true；失败时 value=错误信息。isString=表达式类型为字符串
		private bool TryEvalValue(string str, out string value, out bool isString)
		{
			value = null;
			isString = false;
			if ((emuera == null) || (GlobalStatic.EMediator == null))
				return false;
			if (string.IsNullOrEmpty(str))
			{
				value = "";
				return false;
			}
			mainConsole.RunERBFromMemory = true;
			try
			{
				CharStream st = new(str);
				WordCollection wc = LexicalAnalyzer.Analyse(st, LexEndWith.EoL, LexAnalyzeFlag.None);
				AExpression term = ExpressionParser.ReduceExpressionTerm(wc, TermEndWith.EoL);
				SingleTerm v = term.GetValue(GlobalStatic.EMediator);
				if (v == null)
				{
					value = "<null>";
					return false;
				}
				value = v.ToString();
				isString = term.GetEraType() == EraType.String;
				return true;
			}
			catch (CodeEE e)
			{
				value = e.Message;
				return false;
			}
			catch (Exception e)
			{
				value = e.GetType().ToString() + ":" + e.Message;
				return false;
			}
			finally
			{
				mainConsole.RunERBFromMemory = false;
			}
		}

		//锁定=持续赋值：把值单元格中的冻结值立即写回变量（与调试控制台同一执行管线）
		//成功返回true；失败（不可赋值对象/只读变量等）→ 值单元格显示错误并自动解锁
		private bool TryHoldWrite(ListViewItem item)
		{
			string expr = item.SubItems[1].Text;
			if (string.IsNullOrEmpty(expr))
				return false;
			string held = item.SubItems[2].Text;
			if (string.IsNullOrEmpty(held))
			{//值格为空（如锁定后改过表达式）：重新捕获当前值后再写回
				if (!EvalGuarded(expr, out string v))
				{
					item.SubItems[2].Text = v;
					item.Checked = false;
					return false;
				}
				item.SubItems[2].Text = v;
				held = v;
			}
			//字符串类型需要构造为字符串字面量（含引号时无法安全构造，按原样尝试）
			TryEvalValue(expr, out _, out bool isString);
			string rhs = held;
			if (isString && rhs.IndexOf('"') < 0)
				rhs = "\"" + rhs + "\"";
			int before = mainConsole.DebugConsoleLog.Length;
			mainConsole.DebugCommand(expr + " = " + rhs, false, true);
			string tail = mainConsole.DebugConsoleLog.Substring(before).Trim();
			if (tail.Length > 0)
			{
				item.SubItems[2].Text = tail;
				item.Checked = false;//自动解锁，恢复实时刷新
				return false;
			}
			return true;
		}
		private TextBox watchEditBox;
		private ListViewItem editingItem;
		private int editingSubIndex = 1;//1=对象(表达式) 2=值(赋值)
		private System.Windows.Forms.Timer holdTimer;

		private void BeginEditWatchItem(ListViewItem item, int subIndex)
		{
			if (item == null || subIndex < 1 || subIndex >= item.SubItems.Count)
				return;
			if (watchEditBox == null)
			{
				watchEditBox = new TextBox();
				watchEditBox.BorderStyle = BorderStyle.FixedSingle;
				watchEditBox.Visible = false;
				watchEditBox.KeyDown += watchEditBox_KeyDown;
				watchEditBox.LostFocus += watchEditBox_LostFocus;
				Controls.Add(watchEditBox);//覆盖在listView上的窗体级编辑框
			}
			item.EnsureVisible();
			Rectangle cell = item.SubItems[subIndex].Bounds;
			if (cell.IsEmpty)
				return;
			Point p = listViewWatch.PointToScreen(cell.Location);
			p = PointToClient(p);
			watchEditBox.Font = listViewWatch.Font;
			watchEditBox.SetBounds(p.X, p.Y, cell.Width, cell.Height);
			watchEditBox.Text = item.SubItems[subIndex].Text;
			editingSubIndex = subIndex;
			editingItem = item;
			watchEditBox.Visible = true;
			watchEditBox.BringToFront();
			watchEditBox.Focus();
			watchEditBox.SelectAll();
		}

		private void watchEditBox_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Enter)
			{
				e.SuppressKeyPress = true;
				EndEditWatchItem(true);
			}
			else if (e.KeyCode == Keys.Escape)
			{
				e.SuppressKeyPress = true;
				EndEditWatchItem(false);
			}
		}

		private void watchEditBox_LostFocus(object sender, EventArgs e)
		{
			if (editingItem != null)
				EndEditWatchItem(true);
		}

		private void EndEditWatchItem(bool commit)
		{
			ListViewItem item = editingItem;
			editingItem = null;
			if (watchEditBox != null)
				watchEditBox.Visible = false;
			if (item == null || !commit)
				return;
			string label = watchEditBox.Text;
			if (editingSubIndex == 2)
			{//「值」单元格：赋值（与调试控制台同一执行管线，仅可赋值变量有效）
				if (string.IsNullOrEmpty(label))
					return;//空输入=取消
				string expr = item.SubItems[1].Text;
				if (string.IsNullOrEmpty(expr))
					return;
				if (mainConsole.IsInProcess)
				{//与调试控制台一致：游戏执行中不执行赋值——给出可见反馈而非静默拒绝
					item.SubItems[2].Text = Lang.UI.DebugDialog.CannotAssignWhileRunning.Text;
					listViewWatch.Focus();
					return;
				}
				int before = mainConsole.DebugConsoleLog.Length;
				mainConsole.DebugCommand(expr + " = " + label, false, true);
				string tail = mainConsole.DebugConsoleLog.Substring(before).Trim();
				if (tail.Length > 0)
					item.SubItems[2].Text = tail;//赋值失败：错误信息显示在值单元格
				else
					item.SubItems[2].Text = getValueString(expr);//成功：显示赋值后的实际值
				listViewWatch.Focus();
				return;
			}
			//「对象」单元格：表达式编辑
			if (string.IsNullOrEmpty(label))
			{//清空对象=删除该行（更新循环在实际刷新时移除空行，与原始行为一致）
				item.SubItems[1].Text = "";
				return;
			}
			item.SubItems[1].Text = label;
			if (item.Checked)
				item.SubItems[2].Text = "";//锁定行：表达式已变，旧冻结值作废，待解锁后刷新
			else
				item.SubItems[2].Text = getValueString(label);
			if (item.Index == listViewWatch.Items.Count - 1)
			{
				ListViewItem newLVI = new("");
				newLVI.SubItems.Add(new ListViewItem.ListViewSubItem(newLVI, ""));
				newLVI.SubItems.Add(new ListViewItem.ListViewSubItem(newLVI, ""));
				listViewWatch.Items.Add(newLVI);
			}
			listViewWatch.Focus();
		}

		private void listViewWatch_KeyUp(object sender, KeyEventArgs e)
		{
			//F2キーで名前の変更。
			if (e.KeyCode == Keys.F2 && listViewWatch.FocusedItem != null)
			{
				BeginEditWatchItem(listViewWatch.FocusedItem, 1);
			}
		}

		private void listViewWatch_MouseUp(object sender, MouseEventArgs e)
		{
			if (e.Button != MouseButtons.Left)
				return;
			ListViewItem item = listViewWatch.GetItemAt(e.X, e.Y);
			if (item == null)
				return;
			item.Selected = true;
			//单击「对象」=编辑表达式；单击「值」=赋值（Y 由 GetItemAt 保证在行上，按列 X 区间命中；锁定列勾选框除外）
			for (int i = 1; i < item.SubItems.Count; i++)
			{
				Rectangle cell = item.SubItems[i].Bounds;
				if (!cell.IsEmpty && e.X >= cell.Left && e.X < cell.Right)
				{
					BeginEditWatchItem(item, i);
					return;
				}
			}
		}

		private void listViewWatch_MouseDoubleClick(object sender, MouseEventArgs e)
		{
			if (e.Button != MouseButtons.Left)
				return;
			ListViewItem item = listViewWatch.GetItemAt(e.X, e.Y);
			if (item == null || item.SubItems.Count <= 1)
				return;
			for (int i = 1; i < item.SubItems.Count; i++)
			{
				Rectangle cell = item.SubItems[i].Bounds;
				if (!cell.IsEmpty && e.X >= cell.Left && e.X < cell.Right)
				{
					BeginEditWatchItem(item, i);
					return;
				}
			}
		}

		//带状态保护的求值（与刷新循环相同机制，任意时刻可安全调用）
		private bool EvalGuarded(string expr, out string value)
		{
			GlobalStatic.Process.saveCurrentState(false);
			try
			{
				return TryEvalValue(expr, out value, out _);
			}
			finally
			{
				GlobalStatic.Process.clearMethodStack();
				GlobalStatic.Process.loadPrevState();
			}
		}

		private void listViewWatch_ItemChecked(object sender, ItemCheckedEventArgs e)
		{
			ListViewItem item = e.Item;
			if (!item.Checked)
				return;
			try
			{
				string expr = (item.SubItems.Count > 1) ? item.SubItems[1].Text : "";
				if (string.IsNullOrEmpty(expr))
				{//空行（模板行）不允许锁定
					item.Checked = false;
					return;
				}
				//勾选=锁定：先用与赋值相同的求值路径捕获当前值；可赋值对象立即落实一次写回，失败即报错并解锁
				if (!EvalGuarded(expr, out string captured))
				{//表达式本身求值出错：显示错误且不锁定（与赋值的错误显示一致）
					item.SubItems[2].Text = captured;
					item.Checked = false;
					return;
				}
				item.SubItems[2].Text = captured;
				RestartHoldTimer();
				if (!mainConsole.IsInProcess)
					TryHoldWrite(item);//写入失败时内部显示错误并自动解锁
			}
			catch (Exception ex)
			{//崩溃加固：异常时显示错误并解除锁定，避免勾选框表现为"无反应"
				item.SubItems[2].Text = ex.GetType().ToString() + ":" + ex.Message;
				item.Checked = false;
			}
		}

		//锁定=持续赋值：CE 式周期重申——每隔200ms在安全时机把锁定行的冻结值写回变量
		private void holdTimer_Tick(object sender, EventArgs e)
		{
			try
			{
				if ((mainConsole == null) || mainConsole.IsInProcess)
					return;
				if ((watchEditBox != null) && watchEditBox.Visible)
					return;//正在编辑单元格时不打扰
				bool hasLocked = false;
				foreach (ListViewItem lvi in listViewWatch.Items)
				{
					if (!lvi.Checked)
						continue;
					if (lvi.SubItems.Count < 3 || string.IsNullOrEmpty(lvi.SubItems[1].Text))
						continue;
					hasLocked = true;
					TryHoldWrite(lvi);//失败→值单元格显示错误并自动解锁
				}
				if (!hasLocked)
					holdTimer.Stop();//无锁定行时停止计时器（下次勾选时重启）
			}
			catch
			{
				holdTimer.Stop();//计时器出现异常时停止，避免弹窗刷屏
			}
		}

		//勾选成功时重启周期重申计时器
		private void RestartHoldTimer()
		{
			if ((holdTimer != null) && !holdTimer.Enabled)
				holdTimer.Start();
		}

		private void checkBoxTopMost_CheckedChanged(object sender, EventArgs e)
		{
			TopMost = checkBoxTopMost.Checked;
		}

		private void button1_Click(object sender, EventArgs e)
		{
			Close();
		}

		private void 閉じるToolStripMenuItem_Click(object sender, EventArgs e)
		{
			Close();
		}

		private void ウォッチリストの読込ToolStripMenuItem_Click(object sender, EventArgs e)
		{
			loadWatchList();
			updateVarWatch();
		}

		private void ウォッチリストの保存ToolStripMenuItem_Click(object sender, EventArgs e)
		{
			saveWatchList();
		}


		private readonly string watchFilepath = Program.DebugDir + "watchlist.csv";
		private readonly string consoleFilepath = Program.DebugDir + "console.log";

		private void saveData()
		{
			saveWatchList();

			StreamWriter writer = null;
			//トレースの仕様をいじってるうちに保存する意味が無いものになった
			//try
			//{
			//    writer = new StreamWriter(traceFilepath, false, StaticConfig.Encode);
			//    writer.Write(mainConsole.GetDebugTraceLog(true));
			//}
			//catch
			//{
			//    MessageBox.Show("トレースログの保存に失敗しました", "デバッグウインドウ");
			//    return;
			//}
			//finally
			//{
			//    if (writer != null)
			//        writer.Close();
			//}
			//writer = null;
			try
			{
				writer = new StreamWriter(consoleFilepath, false, Config.Encode);
				writer.Write(mainConsole.DebugConsoleLog);
			}
			catch
			{
				MessageBox.Show("コンソールログの保存に失敗しました", "デバッグウインドウ");
				return;
			}
			finally
			{
				if (writer != null)
					writer.Close();
			}
		}

		private void saveWatchList()
		{
			StreamWriter writer = null;
			try
			{
				writer = new StreamWriter(watchFilepath, false, Config.Encode);
				foreach (ListViewItem lvi in listViewWatch.Items)
				{
					string expr = lvi.SubItems[1].Text;
					if (string.IsNullOrEmpty(expr))
						continue;
					//最原始格式：一行一个表达式（锁定状态是会话内状态，不持久化）
					writer.WriteLine(expr);
				}
			}
			catch
			{
				MessageBox.Show("変数ウォッチリストの保存に失敗しました", "デバッグウインドウ");
				return;
			}
			finally
			{
				if (writer != null)
					writer.Close();
			}
		}

		private void loadWatchList()
		{
			if (!File.Exists(watchFilepath))
				return;
			List<string> saveStrList = [];

			StreamReader reader = null;
			try
			{
				reader = new StreamReader(watchFilepath, Config.Encode);
				string line = null;
				while ((line = reader.ReadLine()) != null)
					if (line.Length > 0)
						saveStrList.Add(line);
			}
			catch
			{
				MessageBox.Show("変数ウォッチリストの読込に失敗しました", "デバッグウインドウ");
				return;
			}
			finally
			{
				if (reader != null)
					reader.Close();
			}

			listViewWatch.Items.Clear();
			foreach (string str in saveStrList)
			{
				if (!string.IsNullOrEmpty(str))
				{
					//最原始格式：整行即表达式；锁定状态不持久化，加载后一律未锁定，随刷新自然求值
					ListViewItem newLVI = new("");
					newLVI.SubItems.Add(new ListViewItem.ListViewSubItem(newLVI, str));
					newLVI.SubItems.Add(new ListViewItem.ListViewSubItem(newLVI, ""));
					listViewWatch.Items.Add(newLVI);
				}
			}
		}

		private void DebugDialog_Activated(object sender, EventArgs e)
		{
			UpdateData();
		}


		private void DebugDialog_FormClosing(object sender, FormClosingEventArgs e)
		{
			saveData();
		}

		private void button2_Click(object sender, EventArgs e)
		{
			//これをクリックする時点で情報が最新でないことは普通ないので実はあんまり意味が無い。
			//最新の情報であることを確認するためのボタンってことで
			UpdateData();
		}

		private void textBoxCommand_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Return)
			{
				e.SuppressKeyPress = true;
				if (!mainConsole.IsInProcess && textBoxCommand.Text.Length > 0)
				{
					mainConsole.DebugPrint(textBoxCommand.Text);
					mainConsole.DebugNewLine();
					mainConsole.DebugCommand(textBoxCommand.Text, false, true);
					updateConsole();
					textBoxConsole.SelectionStart = textBoxConsole.Text.Length;
					textBoxConsole.Focus();
					textBoxConsole.ScrollToCaret();
					updateInputs();
					textBoxCommand.Focus();
				}
				return;
			}
			if (e.KeyCode == Keys.Up)
			{
				e.SuppressKeyPress = true;
				if (mainConsole.IsInProcess)
					return;
				movePrev(-1);
				return;
			}
			if (e.KeyCode == Keys.Down)
			{
				e.SuppressKeyPress = true;
				if (mainConsole.IsInProcess)
					return;
				movePrev(1);
				return;
			}
		}

		List<string> history = [];
		int selectedIndex;
		void updateInputs()
		{
			var input = textBoxCommand.Text;
			if (string.IsNullOrEmpty(input))
				return;
			history.Add(input);
			selectedIndex++;
			textBoxCommand.Text = "";
		}
		void movePrev(int move)
		{
			selectedIndex += move;
			if (selectedIndex < 0)
			{
				selectedIndex = 0;
			}

			if (selectedIndex < history.Count)
			{
				textBoxCommand.Text = history[selectedIndex];
			}
			else
			{
				selectedIndex = history.Count;
				textBoxCommand.Text = "";
				return;
			}
			textBoxCommand.SelectionStart = 0;
			textBoxCommand.SelectionLength = textBoxCommand.Text.Length;
			return;
		}

		private void 設定ToolStripMenuItem1_Click(object sender, EventArgs e)
		{
			bool tempTopMost = TopMost;
			TopMost = false;
			DebugConfigDialog dialog = new();
			dialog.TranslateUI();
			dialog.StartPosition = FormStartPosition.CenterParent;
			dialog.SetConfig(this);
			dialog.ShowDialog();
			TopMost = tempTopMost;
		}

	}
}
