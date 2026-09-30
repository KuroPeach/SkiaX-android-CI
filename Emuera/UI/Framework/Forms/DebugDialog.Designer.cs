namespace MinorShift.Emuera.Forms
{
	partial class DebugDialog
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing && (components != null))
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.menuStrip1 = new System.Windows.Forms.MenuStrip();
			this.toolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
			this.ウォッチリストの保存ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.ウォッチリストの読込ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.閉じるToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.設定ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.設定ToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
			this.tabControlMain = new System.Windows.Forms.TabControl();
			this.tabPageWatch = new System.Windows.Forms.TabPage();
			this.listViewWatch = new System.Windows.Forms.ListView();
			this.columnHeaderLock = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.columnHeader1 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.columnHeader3 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.panelWatchControl = new System.Windows.Forms.FlowLayoutPanel();
			this.checkBoxWriteCallStack = new System.Windows.Forms.CheckBox();
			this.buttonStartWriteWatch = new System.Windows.Forms.Button();
			this.buttonStopWriteWatch = new System.Windows.Forms.Button();
			this.tabPagePrintWatch = new System.Windows.Forms.TabPage();
			this.textBoxPrintLog = new System.Windows.Forms.TextBox();
			this.panelPrintControl = new System.Windows.Forms.FlowLayoutPanel();
			this.checkBoxPrintCallStack = new System.Windows.Forms.CheckBox();
			this.buttonStartPrintWatch = new System.Windows.Forms.Button();
			this.buttonStopPrintWatch = new System.Windows.Forms.Button();
			this.panelBottom = new System.Windows.Forms.Panel();
			this.tabPageTrace = new System.Windows.Forms.TabPage();
			this.textBoxTrace = new System.Windows.Forms.TextBox();
			this.tabPageConsole = new System.Windows.Forms.TabPage();
			this.textBoxCommand = new System.Windows.Forms.TextBox();
			this.textBoxConsole = new System.Windows.Forms.TextBox();
			this.checkBoxTopMost = new System.Windows.Forms.CheckBox();
			this.button1 = new System.Windows.Forms.Button();
			this.button2 = new System.Windows.Forms.Button();
			this.menuStrip1.SuspendLayout();
			this.tabControlMain.SuspendLayout();
			this.tabPageWatch.SuspendLayout();
			this.tabPagePrintWatch.SuspendLayout();
			this.tabPageTrace.SuspendLayout();
			this.tabPageConsole.SuspendLayout();
			this.panelWatchControl.SuspendLayout();
			this.panelPrintControl.SuspendLayout();
			this.panelBottom.SuspendLayout();
			this.SuspendLayout();
			// 
			// menuStrip1
			// 
			this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
			this.toolStripMenuItem1,
			this.設定ToolStripMenuItem});
			this.menuStrip1.Location = new System.Drawing.Point(0, 0);
			this.menuStrip1.Name = "menuStrip1";
			this.menuStrip1.Size = new System.Drawing.Size(384, 24);
			this.menuStrip1.TabIndex = 3;
			this.menuStrip1.Text = "menuStrip1";
			// 
			// デバッグモードで再起動ToolStripMenuItem
			// 
			this.toolStripMenuItem1.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
			this.ウォッチリストの保存ToolStripMenuItem,
			this.ウォッチリストの読込ToolStripMenuItem,
			this.閉じるToolStripMenuItem});
			this.toolStripMenuItem1.Name = "toolStripMenuItem1";
			this.toolStripMenuItem1.Size = new System.Drawing.Size(67, 20);
			this.toolStripMenuItem1.Text = "ファイル(&F)";
			// 
			// ウォッチリストの保存ToolStripMenuItem
			// 
			this.ウォッチリストの保存ToolStripMenuItem.Name = "ウォッチリストの保存ToolStripMenuItem";
			this.ウォッチリストの保存ToolStripMenuItem.Size = new System.Drawing.Size(167, 22);
			this.ウォッチリストの保存ToolStripMenuItem.Text = "ウォッチリストの保存";
			this.ウォッチリストの保存ToolStripMenuItem.Click += new System.EventHandler(this.ウォッチリストの保存ToolStripMenuItem_Click);
			// 
			// ウォッチリストの読込ToolStripMenuItem
			// 
			this.ウォッチリストの読込ToolStripMenuItem.Name = "ウォッチリストの読込ToolStripMenuItem";
			this.ウォッチリストの読込ToolStripMenuItem.Size = new System.Drawing.Size(167, 22);
			this.ウォッチリストの読込ToolStripMenuItem.Text = "ウォッチリストの読込";
			this.ウォッチリストの読込ToolStripMenuItem.Click += new System.EventHandler(this.ウォッチリストの読込ToolStripMenuItem_Click);
			// 
			// 閉じるToolStripMenuItem
			// 
			this.閉じるToolStripMenuItem.Name = "閉じるToolStripMenuItem";
			this.閉じるToolStripMenuItem.Size = new System.Drawing.Size(167, 22);
			this.閉じるToolStripMenuItem.Text = "閉じる";
			this.閉じるToolStripMenuItem.Click += new System.EventHandler(this.閉じるToolStripMenuItem_Click);
			// 
			// 設定ToolStripMenuItem
			// 
			this.設定ToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
			this.設定ToolStripMenuItem1});
			this.設定ToolStripMenuItem.Name = "設定ToolStripMenuItem";
			this.設定ToolStripMenuItem.Size = new System.Drawing.Size(58, 20);
			this.設定ToolStripMenuItem.Text = "設定(&C)";
			// 
			// 設定ToolStripMenuItem1
			// 
			this.設定ToolStripMenuItem1.Name = "設定ToolStripMenuItem1";
			this.設定ToolStripMenuItem1.Size = new System.Drawing.Size(130, 22);
			this.設定ToolStripMenuItem1.Text = "コンフィグ(&C)";
			this.設定ToolStripMenuItem1.Click += new System.EventHandler(this.設定ToolStripMenuItem1_Click);
			// 
			// tabControlMain
			// 
			this.tabControlMain.Controls.Add(this.tabPageConsole);
			this.tabControlMain.Controls.Add(this.tabPageWatch);
			this.tabControlMain.Controls.Add(this.tabPageTrace);
			this.tabControlMain.Controls.Add(this.tabPagePrintWatch);
			this.tabControlMain.Dock = System.Windows.Forms.DockStyle.Fill;
			this.tabControlMain.Location = new System.Drawing.Point(0, 24);
			this.tabControlMain.Name = "tabControlMain";
			this.tabControlMain.SelectedIndex = 0;
			this.tabControlMain.TabIndex = 4;
			this.tabControlMain.Selected += new System.Windows.Forms.TabControlEventHandler(this.tabControlMain_Selected);
			// 
			// tabPageWatch
			// 
			this.tabPageWatch.Controls.Add(this.listViewWatch);
			this.tabPageWatch.Controls.Add(this.panelWatchControl);
			this.tabPageWatch.Location = new System.Drawing.Point(4, 22);
			this.tabPageWatch.Name = "tabPageWatch";
			this.tabPageWatch.Padding = new System.Windows.Forms.Padding(3);
			this.tabPageWatch.Size = new System.Drawing.Size(376, 171);
			this.tabPageWatch.TabIndex = 0;
			this.tabPageWatch.Text = "変数ウォッチ";
			this.tabPageWatch.UseVisualStyleBackColor = true;
			// 
			// listViewWatch
			// 
			this.listViewWatch.CheckBoxes = true;
			this.listViewWatch.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
			this.columnHeaderLock,
			this.columnHeader1,
			this.columnHeader3});
			this.listViewWatch.Dock = System.Windows.Forms.DockStyle.Fill;
			this.listViewWatch.FullRowSelect = true;
			this.listViewWatch.HideSelection = false;
			this.listViewWatch.LabelEdit = false;
			this.listViewWatch.Location = new System.Drawing.Point(3, 3);
			this.listViewWatch.Name = "listViewWatch";
			this.listViewWatch.Size = new System.Drawing.Size(370, 165);
			this.listViewWatch.TabIndex = 0;
			this.listViewWatch.UseCompatibleStateImageBehavior = false;
			this.listViewWatch.View = System.Windows.Forms.View.Details;
			this.listViewWatch.KeyUp += new System.Windows.Forms.KeyEventHandler(this.listViewWatch_KeyUp);
			this.listViewWatch.MouseUp += new System.Windows.Forms.MouseEventHandler(this.listViewWatch_MouseUp);
			this.listViewWatch.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.listViewWatch_MouseDoubleClick);
			this.listViewWatch.ItemChecked += new System.Windows.Forms.ItemCheckedEventHandler(this.listViewWatch_ItemChecked);
			// 
			// columnHeaderLock
			// 
			this.columnHeaderLock.Text = "锁定";
			this.columnHeaderLock.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
			this.columnHeaderLock.Width = 60;
			// 
			// columnHeader1
			// 
			this.columnHeader1.Text = "対象";
			this.columnHeader1.Width = 240;
			// 
			// columnHeader3
			// 
			this.columnHeader3.Text = "値";
			this.columnHeader3.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
			this.columnHeader3.Width = 130;
			// 
			// panelWatchControl
			// 
			// 流式布局 + 控件自动宽度:按文字实际占位排版,换字体/改文案都不会截字;窗口过窄时自动换行
			this.panelWatchControl.AutoSize = true;
			this.panelWatchControl.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
			this.panelWatchControl.Controls.Add(this.checkBoxWriteCallStack);
			this.panelWatchControl.Controls.Add(this.buttonStartWriteWatch);
			this.panelWatchControl.Controls.Add(this.buttonStopWriteWatch);
			this.panelWatchControl.Dock = System.Windows.Forms.DockStyle.Bottom;
			this.panelWatchControl.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
			this.panelWatchControl.Location = new System.Drawing.Point(3, 140);
			this.panelWatchControl.Name = "panelWatchControl";
			this.panelWatchControl.Padding = new System.Windows.Forms.Padding(3, 3, 3, 0);
			this.panelWatchControl.Size = new System.Drawing.Size(370, 28);
			this.panelWatchControl.TabIndex = 1;
			this.panelWatchControl.WrapContents = true;
			// 
			// checkBoxWriteCallStack
			// 
			this.checkBoxWriteCallStack.AutoSize = true;
			this.checkBoxWriteCallStack.Location = new System.Drawing.Point(6, 6);
			this.checkBoxWriteCallStack.Margin = new System.Windows.Forms.Padding(3, 3, 10, 3);
			this.checkBoxWriteCallStack.Name = "checkBoxWriteCallStack";
			this.checkBoxWriteCallStack.Size = new System.Drawing.Size(94, 23);
			this.checkBoxWriteCallStack.TabIndex = 0;
			this.checkBoxWriteCallStack.Text = "记录调用栈";
			this.checkBoxWriteCallStack.UseVisualStyleBackColor = true;
			this.checkBoxWriteCallStack.CheckedChanged += new System.EventHandler(this.checkBoxWriteCallStack_CheckedChanged);
			// 
			// buttonStartWriteWatch
			// 
			this.buttonStartWriteWatch.AutoSize = true;
			this.buttonStartWriteWatch.Location = new System.Drawing.Point(113, 6);
			this.buttonStartWriteWatch.Margin = new System.Windows.Forms.Padding(3, 3, 10, 3);
			this.buttonStartWriteWatch.Name = "buttonStartWriteWatch";
			this.buttonStartWriteWatch.Size = new System.Drawing.Size(90, 23);
			this.buttonStartWriteWatch.TabIndex = 1;
			this.buttonStartWriteWatch.Text = "开始监视";
			this.buttonStartWriteWatch.UseVisualStyleBackColor = true;
			this.buttonStartWriteWatch.Click += new System.EventHandler(this.buttonStartWriteWatch_Click);
			// 
			// buttonStopWriteWatch
			// 
			this.buttonStopWriteWatch.AutoSize = true;
			this.buttonStopWriteWatch.Enabled = false;
			this.buttonStopWriteWatch.Location = new System.Drawing.Point(216, 6);
			this.buttonStopWriteWatch.Margin = new System.Windows.Forms.Padding(3, 3, 3, 3);
			this.buttonStopWriteWatch.Name = "buttonStopWriteWatch";
			this.buttonStopWriteWatch.Size = new System.Drawing.Size(170, 23);
			this.buttonStopWriteWatch.TabIndex = 2;
			this.buttonStopWriteWatch.Text = "停止监视并打印报告";
			this.buttonStopWriteWatch.UseVisualStyleBackColor = true;
			this.buttonStopWriteWatch.Click += new System.EventHandler(this.buttonStopWriteWatch_Click);
			// 
			// tabPagePrintWatch
			// 
			this.tabPagePrintWatch.Controls.Add(this.textBoxPrintLog);
			this.tabPagePrintWatch.Controls.Add(this.panelPrintControl);
			this.tabPagePrintWatch.Location = new System.Drawing.Point(4, 22);
			this.tabPagePrintWatch.Name = "tabPagePrintWatch";
			this.tabPagePrintWatch.Padding = new System.Windows.Forms.Padding(3);
			this.tabPagePrintWatch.Size = new System.Drawing.Size(376, 171);
			this.tabPagePrintWatch.TabIndex = 3;
			this.tabPagePrintWatch.Text = "打印监视";
			this.tabPagePrintWatch.UseVisualStyleBackColor = true;
			// 
			// textBoxPrintLog
			// 
			this.textBoxPrintLog.Dock = System.Windows.Forms.DockStyle.Fill;
			this.textBoxPrintLog.Font = new System.Drawing.Font("ＭＳ ゴシック", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(128)));
			this.textBoxPrintLog.Location = new System.Drawing.Point(3, 3);
			this.textBoxPrintLog.Multiline = true;
			this.textBoxPrintLog.Name = "textBoxPrintLog";
			this.textBoxPrintLog.ReadOnly = true;
			this.textBoxPrintLog.ScrollBars = System.Windows.Forms.ScrollBars.Both;
			this.textBoxPrintLog.Size = new System.Drawing.Size(370, 137);
			this.textBoxPrintLog.TabIndex = 0;
			this.textBoxPrintLog.WordWrap = false;
			// 
			// panelPrintControl
			// 
			// 流式布局 + 控件自动宽度:按文字实际占位排版,换字体/改文案都不会截字;窗口过窄时自动换行
			this.panelPrintControl.AutoSize = true;
			this.panelPrintControl.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
			this.panelPrintControl.Controls.Add(this.checkBoxPrintCallStack);
			this.panelPrintControl.Controls.Add(this.buttonStartPrintWatch);
			this.panelPrintControl.Controls.Add(this.buttonStopPrintWatch);
			this.panelPrintControl.Dock = System.Windows.Forms.DockStyle.Bottom;
			this.panelPrintControl.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
			this.panelPrintControl.Location = new System.Drawing.Point(3, 140);
			this.panelPrintControl.Name = "panelPrintControl";
			this.panelPrintControl.Padding = new System.Windows.Forms.Padding(3, 3, 3, 0);
			this.panelPrintControl.Size = new System.Drawing.Size(370, 28);
			this.panelPrintControl.TabIndex = 3;
			this.panelPrintControl.WrapContents = true;
			// 
			// checkBoxPrintCallStack
			// 
			this.checkBoxPrintCallStack.AutoSize = true;
			this.checkBoxPrintCallStack.Location = new System.Drawing.Point(6, 6);
			this.checkBoxPrintCallStack.Margin = new System.Windows.Forms.Padding(3, 3, 10, 3);
			this.checkBoxPrintCallStack.Name = "checkBoxPrintCallStack";
			this.checkBoxPrintCallStack.Size = new System.Drawing.Size(94, 23);
			this.checkBoxPrintCallStack.TabIndex = 0;
			this.checkBoxPrintCallStack.Text = "记录调用栈";
			this.checkBoxPrintCallStack.UseVisualStyleBackColor = true;
			this.checkBoxPrintCallStack.CheckedChanged += new System.EventHandler(this.checkBoxPrintCallStack_CheckedChanged);
			// 
			// buttonStartPrintWatch
			// 
			this.buttonStartPrintWatch.AutoSize = true;
			this.buttonStartPrintWatch.Location = new System.Drawing.Point(113, 6);
			this.buttonStartPrintWatch.Margin = new System.Windows.Forms.Padding(3, 3, 10, 3);
			this.buttonStartPrintWatch.Name = "buttonStartPrintWatch";
			this.buttonStartPrintWatch.Size = new System.Drawing.Size(100, 23);
			this.buttonStartPrintWatch.TabIndex = 1;
			this.buttonStartPrintWatch.Text = "开始打印监视";
			this.buttonStartPrintWatch.UseVisualStyleBackColor = true;
			this.buttonStartPrintWatch.Click += new System.EventHandler(this.buttonStartPrintWatch_Click);
			// 
			// buttonStopPrintWatch
			// 
			this.buttonStopPrintWatch.AutoSize = true;
			this.buttonStopPrintWatch.Enabled = false;
			this.buttonStopPrintWatch.Location = new System.Drawing.Point(226, 6);
			this.buttonStopPrintWatch.Margin = new System.Windows.Forms.Padding(3, 3, 3, 3);
			this.buttonStopPrintWatch.Name = "buttonStopPrintWatch";
			this.buttonStopPrintWatch.Size = new System.Drawing.Size(150, 23);
			this.buttonStopPrintWatch.TabIndex = 2;
			this.buttonStopPrintWatch.Text = "停止打印监视并报告";
			this.buttonStopPrintWatch.UseVisualStyleBackColor = true;
			this.buttonStopPrintWatch.Click += new System.EventHandler(this.buttonStopPrintWatch_Click);
			// 
			// tabPageTrace
			// 
			this.tabPageTrace.Controls.Add(this.textBoxTrace);
			this.tabPageTrace.Location = new System.Drawing.Point(4, 22);
			this.tabPageTrace.Name = "tabPageTrace";
			this.tabPageTrace.Padding = new System.Windows.Forms.Padding(3);
			this.tabPageTrace.Size = new System.Drawing.Size(376, 171);
			this.tabPageTrace.TabIndex = 1;
			this.tabPageTrace.Text = "スタックトレース";
			this.tabPageTrace.UseVisualStyleBackColor = true;
			// 
			// textBoxTrace
			// 
			this.textBoxTrace.Dock = System.Windows.Forms.DockStyle.Fill;
			this.textBoxTrace.Font = new System.Drawing.Font("ＭＳ ゴシック", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(128)));
			this.textBoxTrace.Location = new System.Drawing.Point(3, 3);
			this.textBoxTrace.Multiline = true;
			this.textBoxTrace.Name = "textBoxTrace";
			this.textBoxTrace.ReadOnly = true;
			this.textBoxTrace.ScrollBars = System.Windows.Forms.ScrollBars.Both;
			this.textBoxTrace.Size = new System.Drawing.Size(370, 165);
			this.textBoxTrace.TabIndex = 0;
			// 
			// tabPageConsole
			// 
			this.tabPageConsole.Controls.Add(this.textBoxConsole);
			this.tabPageConsole.Controls.Add(this.textBoxCommand);
			this.tabPageConsole.Location = new System.Drawing.Point(4, 22);
			this.tabPageConsole.Name = "tabPageConsole";
			this.tabPageConsole.Padding = new System.Windows.Forms.Padding(3);
			this.tabPageConsole.Size = new System.Drawing.Size(376, 171);
			this.tabPageConsole.TabIndex = 2;
			this.tabPageConsole.Text = "コンソール";
			this.tabPageConsole.UseVisualStyleBackColor = true;
			// 
			// textBoxCommand
			// 
			this.textBoxCommand.Dock = System.Windows.Forms.DockStyle.Bottom;
			this.textBoxCommand.Location = new System.Drawing.Point(3, 149);
			this.textBoxCommand.Name = "textBoxCommand";
			this.textBoxCommand.Size = new System.Drawing.Size(370, 19);
			this.textBoxCommand.TabIndex = 0;
			this.textBoxCommand.KeyDown += new System.Windows.Forms.KeyEventHandler(this.textBoxCommand_KeyDown);
			// 
			// textBoxConsole
			// 
			this.textBoxConsole.Dock = System.Windows.Forms.DockStyle.Fill;
			this.textBoxConsole.Font = new System.Drawing.Font("ＭＳ ゴシック", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(128)));
			this.textBoxConsole.Location = new System.Drawing.Point(3, 3);
			this.textBoxConsole.Multiline = true;
			this.textBoxConsole.Name = "textBoxConsole";
			this.textBoxConsole.ReadOnly = true;
			this.textBoxConsole.ScrollBars = System.Windows.Forms.ScrollBars.Both;
			this.textBoxConsole.Size = new System.Drawing.Size(370, 143);
			this.textBoxConsole.TabIndex = 1;
			// 
			// panelBottom
			// 
			this.panelBottom.AutoSize = true;
			this.panelBottom.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
			this.panelBottom.Controls.Add(this.checkBoxTopMost);
			this.panelBottom.Controls.Add(this.button1);
			this.panelBottom.Controls.Add(this.button2);
			this.panelBottom.Dock = System.Windows.Forms.DockStyle.Bottom;
			this.panelBottom.Location = new System.Drawing.Point(0, 263);
			this.panelBottom.Name = "panelBottom";
			this.panelBottom.Padding = new System.Windows.Forms.Padding(12, 6, 12, 6);
			this.panelBottom.Size = new System.Drawing.Size(520, 36);
			this.panelBottom.TabIndex = 9;
			// 
			// checkBoxTopMost
			// 
			this.checkBoxTopMost.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)));
			this.checkBoxTopMost.Appearance = System.Windows.Forms.Appearance.Button;
			this.checkBoxTopMost.AutoSize = true;
			this.checkBoxTopMost.Location = new System.Drawing.Point(12, 6);
			this.checkBoxTopMost.Name = "checkBoxTopMost";
			this.checkBoxTopMost.Size = new System.Drawing.Size(84, 22);
			this.checkBoxTopMost.TabIndex = 6;
			this.checkBoxTopMost.Text = "最前面に表示";
			this.checkBoxTopMost.UseVisualStyleBackColor = true;
			this.checkBoxTopMost.CheckedChanged += new System.EventHandler(this.checkBoxTopMost_CheckedChanged);
			// 
			// button1
			// 
			this.button1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.button1.AutoSize = true;
			this.button1.Location = new System.Drawing.Point(419, 6);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(89, 22);
			this.button1.TabIndex = 7;
			this.button1.Text = "閉じる";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(this.button1_Click);
			// 
			// button2
			// 
			this.button2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.button2.AutoSize = true;
			this.button2.Location = new System.Drawing.Point(324, 6);
			this.button2.Name = "button2";
			this.button2.Size = new System.Drawing.Size(89, 22);
			this.button2.TabIndex = 8;
			this.button2.Text = "データ更新";
			this.button2.UseVisualStyleBackColor = true;
			this.button2.Click += new System.EventHandler(this.button2_Click);
			// 
			// DebugDialog
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
			this.ClientSize = new System.Drawing.Size(520, 330);
			this.Controls.Add(this.tabControlMain);
			this.Controls.Add(this.panelBottom);
			this.Controls.Add(this.menuStrip1);
			this.MainMenuStrip = this.menuStrip1;
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.MinimumSize = new System.Drawing.Size(420, 240);
			this.Name = "DebugDialog";
			this.ShowIcon = false;
			this.ShowInTaskbar = false;
			this.Text = "Emuera - デバッグウインドウ";
			this.Activated += new System.EventHandler(this.DebugDialog_Activated);
			this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.DebugDialog_FormClosing);
			this.menuStrip1.ResumeLayout(false);
			this.menuStrip1.PerformLayout();
			this.tabControlMain.ResumeLayout(false);
			this.tabPageWatch.ResumeLayout(false);
			this.tabPagePrintWatch.ResumeLayout(false);
			this.tabPagePrintWatch.PerformLayout();
			this.tabPageTrace.ResumeLayout(false);
			this.tabPageTrace.PerformLayout();
			this.tabPageConsole.ResumeLayout(false);
			this.tabPageConsole.PerformLayout();
			this.panelWatchControl.ResumeLayout(false);
			this.panelWatchControl.PerformLayout();
			this.panelPrintControl.ResumeLayout(false);
			this.panelPrintControl.PerformLayout();
			this.panelBottom.ResumeLayout(false);
			this.panelBottom.PerformLayout();
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.MenuStrip menuStrip1;
		private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem1;
		private System.Windows.Forms.ToolStripMenuItem ウォッチリストの保存ToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem ウォッチリストの読込ToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem 閉じるToolStripMenuItem;
		private System.Windows.Forms.TabControl tabControlMain;
		private System.Windows.Forms.TabPage tabPageWatch;
		private System.Windows.Forms.ListView listViewWatch;
		private System.Windows.Forms.TabPage tabPageTrace;
		private System.Windows.Forms.CheckBox checkBoxTopMost;
		private System.Windows.Forms.Button button1;
		private System.Windows.Forms.ColumnHeader columnHeaderLock;
		private System.Windows.Forms.ColumnHeader columnHeader1;
		private System.Windows.Forms.ColumnHeader columnHeader3;
		private System.Windows.Forms.FlowLayoutPanel panelWatchControl;
		private System.Windows.Forms.CheckBox checkBoxWriteCallStack;
		private System.Windows.Forms.Button buttonStartWriteWatch;
		private System.Windows.Forms.Button buttonStopWriteWatch;
		private System.Windows.Forms.Button buttonStartPrintWatch;
		private System.Windows.Forms.Button buttonStopPrintWatch;
		private System.Windows.Forms.CheckBox checkBoxPrintCallStack;
		private System.Windows.Forms.TabPage tabPagePrintWatch;
		private System.Windows.Forms.TextBox textBoxPrintLog;
		private System.Windows.Forms.FlowLayoutPanel panelPrintControl;
		private System.Windows.Forms.Panel panelBottom;
		private System.Windows.Forms.TextBox textBoxTrace;
		private System.Windows.Forms.TabPage tabPageConsole;
		private System.Windows.Forms.TextBox textBoxConsole;
		private System.Windows.Forms.Button button2;
		private System.Windows.Forms.TextBox textBoxCommand;
		private System.Windows.Forms.ToolStripMenuItem 設定ToolStripMenuItem;
		private System.Windows.Forms.ToolStripMenuItem 設定ToolStripMenuItem1;

	}
}