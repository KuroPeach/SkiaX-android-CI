using System;
using System.Collections.Generic;
using System.Windows.Forms;
using MinorShift.Emuera.GameView;
using SD = System.Drawing;

namespace MinorShift.Emuera
{
    internal class DesktopDebugDialog : Form
    {
        private TabControl tabControl;
        private TextBox consoleTextBox;
        private ListView variableListView;
        private ListView stackTraceListView;
        private EmueraConsole console;

        public DesktopDebugDialog(EmueraConsole console)
        {
            this.console = console;

            Text = "Debug";
            Size = new SD.Size(600, 400);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = false;

            tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
            };

            var consolePage = new TabPage("Console")
            {
                UseVisualStyleBackColor = true,
            };
            consoleTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                BackColor = SD.Color.Black,
                ForeColor = SD.Color.White,
                Font = new SD.Font("MS Gothic", 10),
                ReadOnly = true,
            };
            consolePage.Controls.Add(consoleTextBox);
            tabControl.TabPages.Add(consolePage);

            var variablePage = new TabPage("Variables")
            {
                UseVisualStyleBackColor = true,
            };
            variableListView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
            };
            variableListView.Columns.Add("Name", 200);
            variableListView.Columns.Add("Value", 300);
            variablePage.Controls.Add(variableListView);
            tabControl.TabPages.Add(variablePage);

            var stackPage = new TabPage("Stack Trace")
            {
                UseVisualStyleBackColor = true,
            };
            stackTraceListView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
            };
            stackTraceListView.Columns.Add("Location", 500);
            stackPage.Controls.Add(stackTraceListView);
            tabControl.TabPages.Add(stackPage);

            var buttonPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 36,
            };
            var btnClose = new Button
            {
                Text = "Close",
                Size = new SD.Size(80, 26),
                Location = new SD.Point(490, 5),
                DialogResult = DialogResult.Cancel,
            };
            var btnRefresh = new Button
            {
                Text = "Refresh",
                Size = new SD.Size(80, 26),
                Location = new SD.Point(400, 5),
            };
            btnRefresh.Click += BtnRefresh_Click;
            buttonPanel.Controls.Add(btnRefresh);
            buttonPanel.Controls.Add(btnClose);

            Controls.Add(tabControl);
            Controls.Add(buttonPanel);

            CancelButton = btnClose;

            UpdateData();
        }

        public void UpdateData()
        {
            if (console == null)
                return;

            try
            {
                consoleTextBox.Text = console.DebugConsoleLog;
                if (consoleTextBox.Text.Length > 0)
                {
                    consoleTextBox.SelectionStart = consoleTextBox.Text.Length;
                    consoleTextBox.ScrollToCaret();
                }
            }
            catch { }
        }

        private void BtnRefresh_Click(object sender, EventArgs e)
        {
            UpdateData();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
            }
            base.OnFormClosing(e);
        }
    }
}
