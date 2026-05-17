using System;
using System.Collections.Generic;
using System.Windows.Forms;
using MinorShift.Emuera.GameView;
using ConfigCode = MinorShift.Emuera.Runtime.Config.ConfigCode;
using SD = System.Drawing;

namespace MinorShift.Emuera
{
    internal class DesktopConfigDialog : Form
    {
        private readonly ConfigData configData;
        private TreeView treeView;
        private ListView listView;
        private Button btnOK;
        private Button btnCancel;
        private Button btnResetDefault;
        private Dictionary<string, List<ConfigItem>> groups;

        public DesktopConfigDialog()
        {
            this.configData = ConfigData.Instance;

            Text = "Configuration";
            Size = new SD.Size(700, 500);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = false;
            MaximizeBox = false;

            InitializeGroups();
            InitializeComponents();
        }

        private void InitializeGroups()
        {
            groups = new Dictionary<string, List<ConfigItem>>();

            var groupDefs = new (string id, string name, ConfigCode[] codes)[]
            {
                ("Environment", "Environment", new[]
                {
                    ConfigCode.AutoSave, ConfigCode.UseSaveFolder,
                    ConfigCode.EnglishConfigOutput, ConfigCode.MaxLog,
                    ConfigCode.InfiniteLoopAlertTime, ConfigCode.SaveDataNos,
                }),
                ("View", "Display", new[]
                {
                    ConfigCode.FPS, ConfigCode.PrintCPerLine,
                    ConfigCode.PrintCLength, ConfigCode.ButtonWrap,
                }),
                ("Window", "Window", new[]
                {
                    ConfigCode.WindowX, ConfigCode.ScrollHeight, ConfigCode.PanSpeed,
                }),
                ("Font", "Font", new[]
                {
                    ConfigCode.ForeColor, ConfigCode.BackColor,
                    ConfigCode.FocusColor, ConfigCode.LogColor,
                    ConfigCode.FontName, ConfigCode.FontSize, ConfigCode.LineHeight,
                }),
                ("System", "System 1", new[]
                {
                    ConfigCode.IgnoreCase, ConfigCode.UseRenameFile,
                    ConfigCode.UseReplaceFile, ConfigCode.SearchSubdirectory,
                    ConfigCode.SortWithFilename, ConfigCode.AllowFunctionOverloading,
                    ConfigCode.WarnFunctionOverloading, ConfigCode.WarnNormalFunctionOverloading,
                    ConfigCode.SystemAllowFullSpace, ConfigCode.useLanguage,
                }),
                ("System2", "System 2", new[]
                {
                    ConfigCode.SystemIgnoreTripleSymbol, ConfigCode.SystemSaveInBinary,
                    ConfigCode.SystemSaveInUTF8, ConfigCode.ZipSaveData,
                    ConfigCode.SystemNoTarget, ConfigCode.ForbidUpdateCheck,
                    ConfigCode.UseERD, ConfigCode.VarsizeDimConfig, ConfigCode.ValidExtension,
                }),
                ("Compati", "Compatibility", new[]
                {
                    ConfigCode.CompatiErrorLine, ConfigCode.CompatiCALLNAME,
                    ConfigCode.CompatiRAND, ConfigCode.TimesNotRigorousCalculation,
                    ConfigCode.CompatiFunctionNoignoreCase, ConfigCode.CompatiCallEvent,
                    ConfigCode.CompatiSPChara, ConfigCode.CompatiLinefeedAs1739,
                    ConfigCode.CompatiFuncArgOptional, ConfigCode.CompatiFuncArgAutoConvert,
                }),
                ("Debug", "Debug", new[]
                {
                    ConfigCode.WarnBackCompatibility, ConfigCode.DisplayReport,
                    ConfigCode.ReduceArgumentOnLoad, ConfigCode.DisplayWarningLevel,
                    ConfigCode.IgnoreUncalledFunction, ConfigCode.FunctionNotFoundWarning,
                    ConfigCode.FunctionNotCalledWarning,
                }),
                ("QuickButton", "Quick Buttons", new[]
                {
                    ConfigCode.QuickButtonFontSize, ConfigCode.QuickButtonWidth,
                    ConfigCode.QuickButtonSpacing,
                }),
                ("FontAndShape", "Font & Shape", new[]
                {
                    ConfigCode.AdaptiveFont, ConfigCode.AdaptiveFontSize,
                    ConfigCode.FontScale, ConfigCode.TextAntialias,
                    ConfigCode.TextFilterQuality, ConfigCode.ShapeAntialias,
                    ConfigCode.ShapeFilterQuality,
                }),
                ("LongPressSkip", "Long Press Skip", new[]
                {
                    ConfigCode.LongPressSkip, ConfigCode.LongPressSkipTime,
                }),
            };

            foreach (var (id, name, codes) in groupDefs)
            {
                var items = new List<ConfigItem>();
                foreach (var code in codes)
                {
                    var item = configData.GetItem(code);
                    if (item != null)
                        items.Add(item);
                }
                if (items.Count > 0)
                    groups[name] = items;
            }
        }

        private void InitializeComponents()
        {
            var splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                SplitterDistance = 180,
                Panel1MinSize = 120,
                Panel2MinSize = 300,
            };

            treeView = new TreeView
            {
                Dock = DockStyle.Fill,
                ShowLines = true,
                ShowPlusMinus = false,
                ShowRootLines = false,
                HideSelection = false,
            };
            foreach (var groupName in groups.Keys)
            {
                treeView.Nodes.Add(groupName, groupName);
            }
            treeView.AfterSelect += TreeView_AfterSelect;
            splitContainer.Panel1.Controls.Add(treeView);

            listView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                HideSelection = false,
            };
            listView.Columns.Add("Property", 180);
            listView.Columns.Add("Value", 250);
            listView.DoubleClick += ListView_DoubleClick;
            splitContainer.Panel2.Controls.Add(listView);

            var buttonPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 40,
            };

            btnResetDefault = new Button
            {
                Text = "Reset Default",
                Location = new SD.Point(10, 8),
                Size = new SD.Size(110, 26),
            };
            btnResetDefault.Click += BtnResetDefault_Click;

            btnOK = new Button
            {
                Text = "OK",
                Location = new SD.Point(440, 8),
                Size = new SD.Size(80, 26),
                DialogResult = DialogResult.OK,
            };
            btnOK.Click += BtnOK_Click;

            btnCancel = new Button
            {
                Text = "Cancel",
                Location = new SD.Point(530, 8),
                Size = new SD.Size(80, 26),
                DialogResult = DialogResult.Cancel,
            };

            buttonPanel.Controls.Add(btnResetDefault);
            buttonPanel.Controls.Add(btnOK);
            buttonPanel.Controls.Add(btnCancel);

            Controls.Add(splitContainer);
            Controls.Add(buttonPanel);

            AcceptButton = btnOK;
            CancelButton = btnCancel;

            if (treeView.Nodes.Count > 0)
                treeView.SelectedNode = treeView.Nodes[0];
        }

        private void TreeView_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (e.Node == null)
                return;

            listView.Items.Clear();
            if (!groups.TryGetValue(e.Node.Text, out var items))
                return;

            foreach (var item in items)
            {
                var lvi = new ListViewItem(item.Name);
                lvi.SubItems.Add(ConfigItem.ValueToString(item.Value));
                lvi.Tag = item;
                lvi.BackColor = item.Fixed ? SD.SystemColors.Control : SD.SystemColors.Window;
                listView.Items.Add(lvi);
            }
        }

        private void ListView_DoubleClick(object sender, EventArgs e)
        {
            if (listView.SelectedItems.Count == 0)
                return;

            var item = listView.SelectedItems[0].Tag as ConfigItem;
            if (item == null || item.Fixed)
                return;

            EditConfigItem(item);
        }

        private void EditConfigItem(ConfigItem item)
        {
            if (item.Value is bool b)
            {
                item.Value = !b;
            }
            else if (item.Value is SD.Color)
            {
                using (var dialog = new ColorDialog())
                {
                    dialog.Color = (SD.Color)item.Value;
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                        item.Value = dialog.Color;
                    else
                        return;
                }
            }
            else if (item.Value is float)
            {
                string input = ShowInputDialog(item.Name + " (0.5~5.0)", item.Value.ToString());
                if (input == null)
                    return;

                if (!ConfigItem.TryParse(item, input))
                {
                    MessageBox.Show(this, "Invalid value.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (item.Code == ConfigCode.FontScale)
                {
                    float val = (float)item.Value;
                    if (val < 0.5f || val > 5.0f)
                    {
                        MessageBox.Show(this, "FontScale must be between 0.5 and 5.0.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        item.Value = item.DefaultValue;
                        return;
                    }
                }
            }
            else
            {
                string input = ShowInputDialog(item.Name, item.Value.ToString());
                if (input == null)
                    return;

                if (!ConfigItem.TryParse(item, input))
                {
                    MessageBox.Show(this, "Invalid value.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            RefreshListView();
        }

        private string ShowInputDialog(string title, string defaultValue)
        {
            using (var dialog = new Form())
            {
                dialog.Text = title;
                dialog.Size = new SD.Size(350, 120);
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MinimizeBox = false;
                dialog.MaximizeBox = false;

                var textBox = new TextBox
                {
                    Location = new SD.Point(10, 10),
                    Size = new SD.Size(310, 24),
                    Text = defaultValue,
                };
                dialog.Controls.Add(textBox);

                var btnOK = new Button
                {
                    Text = "OK",
                    Location = new SD.Point(160, 44),
                    Size = new SD.Size(75, 26),
                    DialogResult = DialogResult.OK,
                };
                dialog.Controls.Add(btnOK);

                var btnCancel = new Button
                {
                    Text = "Cancel",
                    Location = new SD.Point(245, 44),
                    Size = new SD.Size(75, 26),
                    DialogResult = DialogResult.Cancel,
                };
                dialog.Controls.Add(btnCancel);

                dialog.AcceptButton = btnOK;
                dialog.CancelButton = btnCancel;

                if (dialog.ShowDialog(this) == DialogResult.OK)
                    return textBox.Text;
                return null;
            }
        }

        private void BtnOK_Click(object sender, EventArgs e)
        {
            configData.SaveConfig();
        }

        private void BtnResetDefault_Click(object sender, EventArgs e)
        {
            if (listView.SelectedItems.Count == 0)
                return;

            var item = listView.SelectedItems[0].Tag as ConfigItem;
            if (item == null || item.Fixed)
                return;

            item.ResetDefault();
            RefreshListView();
        }

        private void RefreshListView()
        {
            foreach (ListViewItem lvi in listView.Items)
            {
                var item = lvi.Tag as ConfigItem;
                if (item != null)
                    lvi.SubItems[1].Text = ConfigItem.ValueToString(item.Value);
            }
        }
    }
}
