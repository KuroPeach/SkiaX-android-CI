using MinorShift.Emuera.GameProc;
using MinorShift.Emuera.GameView;
using MinorShift.Emuera.Runtime.Config;
using MinorShift.Emuera.Runtime.Script;
using MinorShift.Emuera.Runtime.Script.Parser;
using MinorShift.Emuera.Runtime.Script.Statements.Expression;
using MinorShift.Emuera.Runtime.Script.Statements.Variable;
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
			//打印监视的实时视图刷新(仅在监视期间运行)
			printTimer = new System.Windows.Forms.Timer { Interval = 300 };
			printTimer.Tick += printTimer_Tick;
			FormClosed += (s, e) =>
			{
				if (holdTimer != null)
				{
					holdTimer.Stop();
					holdTimer.Dispose();
					holdTimer = null;
				}
				if (printTimer != null)
				{
					printTimer.Stop();
					printTimer.Dispose();
					printTimer = null;
				}
			};
			loadWatchList();
		}
		private Process emuera;
		private EmueraConsole mainConsole;
		private System.Windows.Forms.Timer printTimer;

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
			else if (tabControlMain.SelectedTab == tabPagePrintWatch)
				updatePrintLog();
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

		/// <summary>刷新「打印监视」页签的实时输出视图(内容与日志文件一致)。</summary>
		private void updatePrintLog()
		{
			if (printLogVersion == PrintWatch.LiveVersion)
				return;
			printLogVersion = PrintWatch.LiveVersion;
			string text = PrintWatch.LiveLog ?? "";
			textBoxPrintLog.Text = text;
			textBoxPrintLog.SelectionStart = textBoxPrintLog.TextLength;
			textBoxPrintLog.ScrollToCaret();
		}
		private int printLogVersion = -1;

		private void printTimer_Tick(object sender, EventArgs e)
		{
			try
			{
				if (!PrintWatch.IsEnabled)
				{
					printTimer.Stop();//监视已停止(或被关闭),停止刷新
					return;
				}
				if (tabControlMain.SelectedTab != tabPagePrintWatch)
					return;//只看当前页签,避免无谓刷新
				updatePrintLog();
			}
			catch
			{
				printTimer.Stop();
			}
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

		#region 变量写入监视(开始监视/停止监视并打印报告)
		private readonly string writeWatchFilepath = Program.DebugDir + "var_write.log";

		private void buttonStartWriteWatch_Click(object sender, EventArgs e)
		{
			StartWriteWatch();
		}

		private void buttonStopWriteWatch_Click(object sender, EventArgs e)
		{
			StopWriteWatch();
		}

		/// <summary>开始监视变量列表中"可赋值变量"的写入情况。</summary>
		private void StartWriteWatch()
		{
			List<string> keys = [];
			List<string> skipped = [];
			List<string> unsupported = [];
			foreach (ListViewItem lvi in listViewWatch.Items)
			{
				string expr = (lvi.SubItems.Count > 1) ? lvi.SubItems[1].Text : "";
				if (string.IsNullOrEmpty(expr) || skipped.Contains(expr) || unsupported.Contains(expr))
					continue;
				string key = TryBuildWriteKey(expr, out bool isVariable);
				if (string.IsNullOrEmpty(key))
				{
					(isVariable ? unsupported : skipped).Add(expr);
					continue;
				}
				if (!keys.Contains(key))
					keys.Add(key);
			}
			if (keys.Count == 0)
			{
				MessageBox.Show("变量列表里没有可监视的变量。\n请在「対象」列填写变量(如 ITEM:6、CFLAG:MASTER:100)。", "变量写入监视");
				return;
			}
			VarWriteWatch.Start(writeWatchFilepath, keys);
			buttonStartWriteWatch.Enabled = false;
			buttonStopWriteWatch.Enabled = true;
			mainConsole.DebugPrint(string.Format("变量写入监视开始: {0} 个对象 → {1}", keys.Count, writeWatchFilepath));
			mainConsole.DebugNewLine();
			if (skipped.Count > 0)
			{
				mainConsole.DebugPrint("以下表达式不是可监视的变量,已跳过: " + string.Join(", ", skipped));
				mainConsole.DebugNewLine();
			}
			if (unsupported.Count > 0)
			{
				mainConsole.DebugPrint("以下变量暂不支持监视(关联数组/字符串下标/数组整体形式),已跳过: " + string.Join(", ", unsupported));
				mainConsole.DebugNewLine();
			}
			updateConsole();
		}

		/// <summary>停止监视,把报告打印到调试窗口控制台(同时已写入日志文件)。</summary>
		private void StopWriteWatch()
		{
			string report = VarWriteWatch.Stop();
			buttonStartWriteWatch.Enabled = true;
			buttonStopWriteWatch.Enabled = false;
			if (string.IsNullOrEmpty(report))
				return;
			mainConsole.DebugPrint(report);
			mainConsole.DebugNewLine();
			tabControlMain.SelectedTab = tabPageConsole;
			updateConsole();
		}

		/// <summary>
		/// 把变量列表里的表达式解析成变量标识(如 ITEM:6)。不是变量(纯表达式/算式)或解析失败时返回 null。
		/// 使用与变量监视相同的解析路径,保证与实际写入时的标识一致。
		/// isVariable 为 true 表示解析出了变量但暂不支持监视(关联数组/字符串下标/无下标的数组整体形式)。
		/// </summary>
		private string TryBuildWriteKey(string expr, out bool isVariable)
		{
			isVariable = false;
			if ((emuera == null) || (GlobalStatic.EMediator == null))
				return null;
			mainConsole.RunERBFromMemory = true;
			try
			{
				CharStream st = new(expr);
				WordCollection wc = LexicalAnalyzer.Analyse(st, LexEndWith.EoL, LexAnalyzeFlag.None);
				AExpression term = ExpressionParser.ReduceExpressionTerm(wc, TermEndWith.EoL);
				if (term is not VariableTerm varTerm)
					return null;
				isVariable = true;
				string key = VarWriteWatch.BuildKey(varTerm, GlobalStatic.EMediator);
				return string.IsNullOrEmpty(key) ? null : key;
			}
			catch
			{
				return null;
			}
			finally
			{
				mainConsole.RunERBFromMemory = false;
			}
		}
		#endregion

		#region 打印监视(开始打印监视/停止打印监视并报告)
		private readonly string printWatchFilepath = Program.DebugDir + "print_trace.log";

		private void buttonStartPrintWatch_Click(object sender, EventArgs e)
		{
			StartPrintWatch();
		}

		private void buttonStopPrintWatch_Click(object sender, EventArgs e)
		{
			StopPrintWatch();
		}

		/// <summary>勾选=打印记录附加完整调用栈(只影响打印监视)。</summary>
		private void checkBoxPrintCallStack_CheckedChanged(object sender, EventArgs e)
		{
			PrintWatch.IncludeCallStack = checkBoxPrintCallStack.Checked;
		}

		/// <summary>开始监视控制台文本输出(记录每一次打印的文本+函数+文件:行号)。</summary>
		private void StartPrintWatch()
		{
			PrintWatch.IncludeCallStack = checkBoxPrintCallStack.Checked;
			PrintWatch.Start(printWatchFilepath);
			buttonStartPrintWatch.Enabled = false;
			buttonStopPrintWatch.Enabled = true;
			printTimer.Start();
			tabControlMain.SelectedTab = tabPagePrintWatch;
			updatePrintLog();
			mainConsole.DebugPrint(string.Format("打印监视开始{0} → {1}",
				PrintWatch.IncludeCallStack ? "(含调用栈)" : "", printWatchFilepath));
			mainConsole.DebugNewLine();
			updateConsole();
		}

		/// <summary>停止打印监视,把报告打印到调试窗口控制台(同时已写入日志文件)。</summary>
		private void StopPrintWatch()
		{
			string report = PrintWatch.Stop();
			buttonStartPrintWatch.Enabled = true;
			buttonStopPrintWatch.Enabled = false;
			printTimer.Stop();
			updatePrintLog();//把最终报告也显示到实时视图
			if (string.IsNullOrEmpty(report))
				return;
			mainConsole.DebugPrint(report);
			mainConsole.DebugNewLine();
			updateConsole();
		}
		#endregion

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
			if (VarWriteWatch.IsEnabled)
				VarWriteWatch.Stop();
			if (PrintWatch.IsEnabled)
				PrintWatch.Stop();
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
