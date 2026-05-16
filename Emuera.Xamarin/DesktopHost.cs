using SkiaSharp;
using SkiaSharp.Views.Forms;
using System;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using MinorShift.Emuera.GameView;
using MinorShift.Emuera.UI.Framework.Forms;
using MinorShift.Emuera.Runtime.Utils;
using XEmuera;
using XEmuera.Drawing;

namespace MinorShift.Emuera
{
    internal class DesktopHostForm : Form
    {
        private PictureBox pictureBox;
        private EmueraConsole console;
        private VScrollBar vScrollBar;
        private TextBox inputTextBox;
        private DesktopMainWindow mainWindow;
        private FlowLayoutPanel quickButtonPanel;

        private int scrollBarValue = 1;
        private int scrollBarMaximum = 1;
        private int renderWidth = 800;
        private int renderHeight = 560;

        private bool pressEnterKeyInProcess;

        public DesktopHostForm()
        {
            Text = "Emuera.Xamarin";
            ClientSize = new Size(800, 600);
            BackColor = Color.Black;
            FormBorderStyle = FormBorderStyle.Sizable;

            vScrollBar = new VScrollBar
            {
                Dock = DockStyle.Right,
                Minimum = 1,
                Maximum = 1,
                Value = 1,
                SmallChange = 1,
                LargeChange = 5,
                Visible = true
            };
            vScrollBar.ValueChanged += VScrollBar_ValueChanged;

            pictureBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                SizeMode = PictureBoxSizeMode.Normal
            };
            pictureBox.Resize += PictureBox_Resize;
            pictureBox.Paint += PictureBox_Paint;
            pictureBox.MouseMove += PictureBox_MouseMove;
            pictureBox.MouseDown += PictureBox_MouseDown;
            pictureBox.MouseUp += PictureBox_MouseUp;
            pictureBox.MouseWheel += PictureBox_MouseWheel;
            pictureBox.MouseLeave += PictureBox_MouseLeave;
            pictureBox.MouseEnter += PictureBox_MouseEnter;
            pictureBox.TabStop = true;

            KeyDown += DesktopHostForm_KeyDown;
            KeyPreview = true;
            FormClosing += DesktopHostForm_FormClosing;

            inputTextBox = new TextBox
            {
                Dock = DockStyle.Bottom,
                BackColor = Color.Black,
                ForeColor = Color.White,
                Font = new System.Drawing.Font("MS Gothic", 14),
                BorderStyle = BorderStyle.None
            };
            inputTextBox.KeyDown += InputTextBox_KeyDown;

            quickButtonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                BackColor = Color.FromArgb(30, 30, 30),
                AutoScroll = true,
                WrapContents = true,
                FlowDirection = FlowDirection.LeftToRight,
                Height = 40,
                Visible = false
            };

            var panel = new Panel
            {
                Dock = DockStyle.Fill
            };
            panel.Controls.Add(pictureBox);
            panel.Controls.Add(vScrollBar);

            Controls.Add(panel);
            Controls.Add(quickButtonPanel);
            Controls.Add(inputTextBox);

            SetupDisplayUtils();
        }

        private void SetupDisplayUtils()
        {
            GameUtils.MainPicBox = new EraPictureBox
            {
                Width = renderWidth,
                Height = renderHeight
            };
            GameUtils.MainLayout = null;
        }

        private void PictureBox_Resize(object sender, EventArgs e)
        {
            renderWidth = pictureBox.ClientSize.Width;
            renderHeight = pictureBox.ClientSize.Height;
            DisplayUtils._desktopWidth = renderWidth;
            DisplayUtils._desktopHeight = renderHeight;
            if (GameUtils.MainPicBox != null)
            {
                GameUtils.MainPicBox.Width = renderWidth;
                GameUtils.MainPicBox.Height = renderHeight;
            }
        }

        public void InitEmuera()
        {
            PlatformInterop.DoEvents = () => System.Windows.Forms.Application.DoEvents();
            PlatformInterop.ShowMessage = (msg) => System.Windows.Forms.MessageBox.Show(msg);
            PlatformInterop.ShowMessageWithTitle = (msg, caption) => System.Windows.Forms.MessageBox.Show(msg, caption);
            PlatformInterop.ShowQuestion = (msg, caption) => System.Windows.Forms.MessageBox.Show(msg, caption, System.Windows.Forms.MessageBoxButtons.YesNo, System.Windows.Forms.MessageBoxIcon.None, System.Windows.Forms.MessageBoxDefaultButton.Button2) == System.Windows.Forms.DialogResult.Yes;
            PlatformInterop.GetProductVersion = () => System.Windows.Forms.Application.ProductVersion;
            PlatformInterop.GetMousePosition = () =>
            {
                var pos = System.Windows.Forms.Control.MousePosition;
                return new Point(pos.X, pos.Y);
            };
            PlatformInterop.SetClipboardText = (text) => System.Windows.Forms.Clipboard.SetDataObject(text, false, 3, 200);
            PlatformInterop.MeasureTextWidth = (text, fontName, fontSize, fontStyle) =>
            {
                var weight = fontStyle.HasFlag(XEmuera.Drawing.FontStyle.Bold) ? SkiaSharp.SKFontStyleWeight.Bold : SkiaSharp.SKFontStyleWeight.Normal;
                var slant = fontStyle.HasFlag(XEmuera.Drawing.FontStyle.Italic) ? SkiaSharp.SKFontStyleSlant.Italic : SkiaSharp.SKFontStyleSlant.Upright;
                var style = new SkiaSharp.SKFontStyle(weight, SkiaSharp.SKFontStyleWidth.Normal, slant);
                using var typeface = SkiaSharp.SKTypeface.FromFamilyName(fontName, style) ?? SkiaSharp.SKTypeface.Default;
                using var paint = new SkiaSharp.SKPaint { Typeface = typeface, TextSize = fontSize, IsStroke = false };
                return paint.MeasureText(text);
            };
            PlatformInterop.MeasureText = (text, fontName, fontSize, fontStyle) =>
            {
                var weight = fontStyle.HasFlag(XEmuera.Drawing.FontStyle.Bold) ? SkiaSharp.SKFontStyleWeight.Bold : SkiaSharp.SKFontStyleWeight.Normal;
                var slant = fontStyle.HasFlag(XEmuera.Drawing.FontStyle.Italic) ? SkiaSharp.SKFontStyleSlant.Italic : SkiaSharp.SKFontStyleSlant.Upright;
                var style = new SkiaSharp.SKFontStyle(weight, SkiaSharp.SKFontStyleWidth.Normal, slant);
                using var typeface = SkiaSharp.SKTypeface.FromFamilyName(fontName, style) ?? SkiaSharp.SKTypeface.Default;
                using var paint = new SkiaSharp.SKPaint { Typeface = typeface, TextSize = fontSize, IsStroke = false };
                var bounds = new SkiaSharp.SKRect();
                paint.MeasureText(text, ref bounds);
                return (bounds.Width, bounds.Height);
            };

            mainWindow = new DesktopMainWindow(this);
            console = new EmueraConsole(mainWindow);

#if USE_NAUDIO
            GlobalStatic.Sound = new Runtime.Utils.Sound[16];
            for (int i = 0; i < GlobalStatic.Sound.Length; i++)
                GlobalStatic.Sound[i] = new NAudioSound();
            GlobalStatic.Bgm = new NAudioSound();
#endif

            Task.Run(() =>
            {
                DrawTextUtils.Reset();
                DrawBitmapUtils.Reset();
                console.Initialize();
            });
        }

        public void TriggerRefresh()
        {
            if (pictureBox != null && !pictureBox.IsDisposed)
                pictureBox.Invalidate();
        }

        private void PictureBox_Paint(object sender, PaintEventArgs e)
        {
            RenderSkiaToPictureBox();
        }

        private void RenderSkiaToPictureBox()
        {
            if (console == null || renderWidth <= 0 || renderHeight <= 0)
                return;

            using (var surface = SKSurface.Create(new SKImageInfo(renderWidth, renderHeight, SKColorType.Bgra8888, SKAlphaType.Premul)))
            {
                if (surface == null)
                    return;

                var canvas = surface.Canvas;
                canvas.Clear(SKColors.Black);
                console.OnPaint(canvas);

                using (var image = surface.Snapshot())
                using (var bitmap = new System.Drawing.Bitmap(renderWidth, renderHeight, PixelFormat.Format32bppPArgb))
                {
                    var info = new SKImageInfo(renderWidth, renderHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
                    var pixels = bitmap.LockBits(new Rectangle(0, 0, renderWidth, renderHeight), ImageLockMode.WriteOnly, bitmap.PixelFormat);
                    try
                    {
                        image.ReadPixels(info, pixels.Scan0, info.RowBytes, 0, 0);
                    }
                    finally
                    {
                        bitmap.UnlockBits(pixels);
                    }

                    var oldImage = pictureBox.Image;
                    pictureBox.Image = (System.Drawing.Bitmap)bitmap.Clone();
                    oldImage?.Dispose();
                }
            }
        }

        #region Mouse Event Handling

        private void PictureBox_MouseMove(object sender, MouseEventArgs e)
        {
            if (console == null)
                return;

            var point = e.Location;
            if (console.MoveMouse(point))
                RefreshStrings(true);
        }

        private void PictureBox_MouseDown(object sender, MouseEventArgs e)
        {
            if (console == null)
                return;

            if (console.IsWaitingPrimitive)
            {
                var point = e.Location;
                var button = WinFormsMouseButtonToSK(e.Button);
                console.MouseDown(point, button);
            }
        }

        private void PictureBox_MouseUp(object sender, MouseEventArgs e)
        {
            if (console == null)
                return;

            MouseReleased(e);
            Task.Run(() => console.LeaveMouse());
        }

        private void PictureBox_MouseWheel(object sender, MouseEventArgs e)
        {
            if (console == null)
                return;

            if (console.IsWaitingPrimitive)
            {
                var point = e.Location;
                console.MouseWheel(point, e.Delta);
                return;
            }

            int lines = e.Delta / SystemInformation.MouseWheelScrollDelta;
            int newValue = scrollBarValue - lines;
            newValue = Math.Max(vScrollBar.Minimum, Math.Min(vScrollBar.Maximum, newValue));
            if (newValue != scrollBarValue)
            {
                scrollBarValue = newValue;
                if (!vScrollBar.IsDisposed)
                {
                    if (vScrollBar.InvokeRequired)
                        vScrollBar.BeginInvoke(new Action(() => { if (!vScrollBar.IsDisposed) vScrollBar.Value = newValue; }));
                    else
                        vScrollBar.Value = newValue;
                }
                TriggerRefresh();
            }
        }

        private void PictureBox_MouseLeave(object sender, EventArgs e)
        {
            if (console == null)
                return;

            Task.Run(() => console.LeaveMouse());
        }

        private void PictureBox_MouseEnter(object sender, EventArgs e)
        {
            if (!pictureBox.IsDisposed)
                pictureBox.Focus();
        }

        private void MouseReleased(MouseEventArgs e)
        {
            if (console.IsInProcess)
                return;

            if (console.IsWaitingPrimitive)
                return;

            bool isBacklog = scrollBarValue != scrollBarMaximum;
            string str = console.SelectedString;

            if (isBacklog)
            {
                if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Right)
                {
                    UpdateScrollBar(scrollBarMaximum, scrollBarMaximum);
                    RefreshStrings(true);
                }
            }

            if (console.IsWaitingEnterKey && !console.IsError && str == null)
            {
                if (isBacklog)
                    return;
                if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Right)
                {
                    if (e.Button == MouseButtons.Right)
                        PressEnterKey(true, "", true);
                    else
                        PressEnterKey(false, "", true);
                    return;
                }
            }

            if (str != null && e.Button == MouseButtons.Left)
            {
                SetTextBoxText(str);
                PressEnterKey(false, str, true);
                return;
            }
        }

        private SKMouseButton WinFormsMouseButtonToSK(MouseButtons button)
        {
            return button switch
            {
                MouseButtons.Right => SKMouseButton.Right,
                MouseButtons.Middle => SKMouseButton.Middle,
                _ => SKMouseButton.Left
            };
        }

        #endregion

        #region Input Handling

        private void DesktopHostForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            GlobalStatic.Reset();
        }

        private void DesktopHostForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (console == null || console.IsInProcess)
                return;

            if (e.KeyCode == Keys.Z && e.Control)
            {
                console.GotoTitleAndLoadAndRepeatInput();
                e.SuppressKeyPress = true;
                e.Handled = true;
                return;
            }

            if (console.IsWaitingPrimitive)
            {
                console.PressPrimitiveKey((int)e.KeyCode, (int)e.KeyData, (int)e.Modifiers);
                e.SuppressKeyPress = true;
                e.Handled = true;
            }
        }

        private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (console == null || console.IsInProcess)
                return;

            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                string str = inputTextBox.Text ?? "";
                PressEnterKey(e.Shift, str, false);
                ClearTextBoxText();
            }
            else if (e.KeyCode == Keys.Escape)
            {
                e.SuppressKeyPress = true;
                console.KillMacro = true;
                if (!console.IsInProcess)
                    console.PressEnterKey(true, "", false);
            }
        }

        private void PressEnterKey(bool mesSkip, string str, bool inputsByMouse)
        {
            if (console == null || console.IsInProcess)
                return;
            if (pressEnterKeyInProcess)
                return;

            pressEnterKeyInProcess = true;
            Task.Run(() =>
            {
                try
                {
                    console.PressEnterKey(mesSkip, str, inputsByMouse);
                }
                finally
                {
                    pressEnterKeyInProcess = false;
                }
            });
        }

        private void RefreshStrings(bool force_Paint)
        {
            if (console == null)
                return;
            Task.Run(() => console.RefreshStrings(force_Paint));
        }

        #endregion

        #region ScrollBar

        private void VScrollBar_ValueChanged(object sender, EventArgs e)
        {
            scrollBarValue = vScrollBar.Value;
            TriggerRefresh();
        }

        public void UpdateScrollBar(int maximum, int value)
        {
            if (vScrollBar.IsDisposed)
                return;
            if (vScrollBar.InvokeRequired)
            {
                vScrollBar.BeginInvoke(new Action(() => UpdateScrollBar(maximum, value)));
                return;
            }
            if (maximum > 0)
                vScrollBar.Maximum = maximum;
            if (value >= vScrollBar.Minimum && value <= vScrollBar.Maximum)
                vScrollBar.Value = value;
            scrollBarValue = vScrollBar.Value;
            scrollBarMaximum = vScrollBar.Maximum;
        }

        public int GetScrollBarValue() => scrollBarValue;
        public int GetScrollBarMaximum() => scrollBarMaximum;

        #endregion

        #region Accessors

        public int GetRenderWidth() => renderWidth;
        public int GetRenderHeight() => renderHeight;
        public System.Windows.Forms.PictureBox GetPictureBox() => pictureBox;
        public System.Windows.Forms.TextBox GetInputTextBox() => inputTextBox;
        public FlowLayoutPanel GetQuickButtonPanel() => quickButtonPanel;
        public string GetTextBoxText() => inputTextBox?.Text ?? "";
        public void SetTextBoxText(string text)
        {
            if (inputTextBox.IsDisposed)
                return;
            if (inputTextBox.InvokeRequired)
                inputTextBox.BeginInvoke(new Action(() => inputTextBox.Text = text));
            else
                inputTextBox.Text = text;
        }
        public void ClearTextBoxText()
        {
            if (inputTextBox.IsDisposed)
                return;
            if (inputTextBox.InvokeRequired)
                inputTextBox.BeginInvoke(new Action(() => inputTextBox.Text = ""));
            else
                inputTextBox.Text = "";
        }

        public void AddQuickButton(string text, int value)
        {
            if (quickButtonPanel.IsDisposed)
                return;
            if (quickButtonPanel.InvokeRequired)
            {
                quickButtonPanel.BeginInvoke(new Action(() => AddQuickButton(text, value)));
                return;
            }
            var btn = new Button
            {
                Text = text,
                Tag = value,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                Font = new System.Drawing.Font("MS Gothic", 10),
                AutoSize = true,
                Padding = new Padding(4, 0, 4, 0),
                Margin = new Padding(2),
            };
            btn.FlatAppearance.BorderColor = Color.Gray;
            btn.Click += (s, e) =>
            {
                if (console == null || console.IsInProcess)
                    return;
                int btnValue = (int)((Button)s).Tag;
                string btnText = ((Button)s).Text;
                SetTextBoxText(btnText);
                PressEnterKey(false, btnText, true);
            };
            quickButtonPanel.Controls.Add(btn);
            quickButtonPanel.Visible = quickButtonPanel.Controls.Count > 0;
        }

        public void ClearQuickButtons()
        {
            if (quickButtonPanel.IsDisposed)
                return;
            if (quickButtonPanel.InvokeRequired)
            {
                quickButtonPanel.BeginInvoke(new Action(() => ClearQuickButtons()));
                return;
            }
            quickButtonPanel.Controls.Clear();
            quickButtonPanel.Visible = false;
        }

        public void DoUpdateLastInput()
        {
            if (inputTextBox.IsDisposed)
                return;
            inputTextBox.KeyDown -= InputTextBox_KeyDown;
            System.Windows.Forms.Application.DoEvents();
            inputTextBox.KeyDown += InputTextBox_KeyDown;
        }

        public void SetTextBoxPosition(int x, int y, int width)
        {
            if (inputTextBox.IsDisposed)
                return;
            var newX = Math.Max(0, Math.Min(x, pictureBox.Width - 50));
            var newY = Math.Min(Math.Max(pictureBox.Height - y - inputTextBox.Height, 0), pictureBox.Height - inputTextBox.Height);
            inputTextBox.Location = new System.Drawing.Point(newX, newY);
            if (width > 0)
                inputTextBox.Width = Math.Max(50, Math.Min(width, pictureBox.Width - newX));
        }

        #endregion
    }

    internal class DesktopMainWindow : MainWindow
    {
        private readonly DesktopHostForm form;
        private readonly DesktopScrollBar scrollBar;
        private readonly DesktopMainPicBox mainPicBox;
        private readonly DesktopTextBox textBox;
        private readonly DesktopToolTip toolTip;
        private readonly DesktopQuickButtonGroup _quickButtonGroup;

        public DesktopMainWindow(DesktopHostForm form)
        {
            this.form = form;
            this.scrollBar = new DesktopScrollBar(form);
            this.mainPicBox = new DesktopMainPicBox(form);
            this.textBox = new DesktopTextBox(form);
            this.toolTip = new DesktopToolTip(form);
            this._quickButtonGroup = new DesktopQuickButtonGroup(form);
        }

        public override UI.Framework.Forms.ScrollBar ScrollBar => scrollBar;
        public override MainPicBoxStub MainPicBox => mainPicBox;
        public override TextBoxStub TextBox => textBox;
        public override ToolTipStub ToolTip => toolTip;
        public override QuickButtonGroup QuickButtonGroup => _quickButtonGroup;
        public override void Refresh()
        {
            form.TriggerRefresh();
        }

        public override void Close()
        {
            if (form.IsDisposed)
                return;
            if (form.InvokeRequired)
                form.BeginInvoke(new Action(form.Close));
            else
                form.Close();
        }

        public override void Reboot()
        {
            Program.Reboot = true;
            Close();
        }

        public override string Title
        {
            get => form.IsDisposed ? "Emuera" : form.Text;
            set { if (!form.IsDisposed) form.BeginInvoke(new Action(() => form.Text = value)); }
        }

        private SKColor _backgroundColor = SKColors.Black;

        public override SKColor BackgroundColor
        {
            get => _backgroundColor;
            set { _backgroundColor = value; }
        }

        public override bool IsEnabled => !form.IsDisposed;
        public override bool Created => !form.IsDisposed;

        public override void Invoke(Action action)
        {
            if (form.IsDisposed)
                return;
            if (!form.InvokeRequired)
            {
                action?.Invoke();
                return;
            }
            var mre = new System.Threading.ManualResetEventSlim(false);
            Exception captured = null;
            form.BeginInvoke(new Action(() =>
            {
                try { action?.Invoke(); }
                catch (Exception ex) { captured = ex; }
                finally { mre.Set(); }
            }));
            mre.Wait();
            if (captured != null)
                throw captured;
        }

        public override void Focus()
        {
            if (!form.IsDisposed)
                form.BeginInvoke(new Action(() => form.Activate()));
        }

        public override void ShowConfigDialog()
        {
            if (form.IsDisposed)
                return;
            form.BeginInvoke(new Action(() =>
            {
                using (var dialog = new DesktopConfigDialog())
                {
                    dialog.ShowDialog(form);
                }
                if (GlobalStatic.Console != null)
                    GlobalStatic.Console.RefreshStrings(true);
            }));
        }

        public override void ShowDebugDialog()
        {
            if (form.IsDisposed)
                return;
            var console = GlobalStatic.Console;
            if (console == null)
                return;
            form.BeginInvoke(new Action(() =>
            {
                using (var dialog = new DesktopDebugDialog(console))
                {
                    dialog.ShowDialog(form);
                }
            }));
        }

        private string _lastInputed = "";

        public override void update_lastinput()
        {
            var tb = form.GetInputTextBox();
            if (tb == null || tb.IsDisposed)
                return;
            form.DoUpdateLastInput();
            _lastInputed = tb.Text;
        }

        public override void clear_richText()
        {
            form.ClearTextBoxText();
        }

        public override void SetTextBoxPos(int x, int y, int width = 0)
        {
            form.SetTextBoxPosition(x, y, width);
        }

        public override void ChangeTextBox(string str)
        {
            form.SetTextBoxText(str);
        }
    }

    internal class DesktopMainPicBox : MainPicBoxStub
    {
        private readonly DesktopHostForm form;

        public DesktopMainPicBox(DesktopHostForm form)
        {
            this.form = form;
        }

        public override int Width => form.GetRenderWidth();
        public override int Height => form.GetRenderHeight();
        public override Rectangle ClientRectangle => new Rectangle(0, 0, Width, Height);
        public override Point PointToClient(Point screenPoint)
        {
            if (form.IsDisposed)
                return screenPoint;
            if (form.InvokeRequired)
                return form.GetPictureBox().PointToClient(screenPoint);
            return form.GetPictureBox().PointToClient(screenPoint);
        }
    }

    internal class DesktopTextBox : TextBoxStub
    {
        private readonly DesktopHostForm form;

        public DesktopTextBox(DesktopHostForm form)
        {
            this.form = form;
        }

        public override Color BackColor
        {
            get => form.IsDisposed ? Color.Black : form.GetInputTextBox().BackColor;
            set { if (!form.IsDisposed) form.BeginInvoke(new Action(() => form.GetInputTextBox().BackColor = value)); }
        }
        public override Color ForeColor
        {
            get => form.IsDisposed ? Color.White : form.GetInputTextBox().ForeColor;
            set { if (!form.IsDisposed) form.BeginInvoke(new Action(() => form.GetInputTextBox().ForeColor = value)); }
        }
        public override Font Font
        {
            get => form.IsDisposed ? null : form.GetInputTextBox().Font;
            set { if (!form.IsDisposed) form.BeginInvoke(new Action(() => form.GetInputTextBox().Font = value)); }
        }
    }

    internal class DesktopToolTip : ToolTipStub
    {
        private readonly DesktopHostForm form;
        private readonly System.Windows.Forms.ToolTip winToolTip;

        public DesktopToolTip(DesktopHostForm form)
        {
            this.form = form;
            this.winToolTip = new System.Windows.Forms.ToolTip();
        }

        public override int InitialDelay
        {
            get => winToolTip.InitialDelay;
            set => winToolTip.InitialDelay = value;
        }

        public override int AutoPopDelay
        {
            get => winToolTip.AutoPopDelay;
            set => winToolTip.AutoPopDelay = value;
        }

        public override Color ForeColor
        {
            get => winToolTip.ForeColor;
            set => winToolTip.ForeColor = value;
        }

        public override Color BackColor
        {
            get => winToolTip.BackColor;
            set => winToolTip.BackColor = value;
        }

        public override bool OwnerDraw
        {
            get => winToolTip.OwnerDraw;
            set => winToolTip.OwnerDraw = value;
        }

        public override void RemoveAll()
        {
            winToolTip.RemoveAll();
        }

        public override void Show(string text, object control, Point point)
        {
            if (form.IsDisposed)
                return;
            var pb = form.GetPictureBox();
            if (pb == null || pb.IsDisposed)
                return;
            form.BeginInvoke(new Action(() =>
            {
                if (!pb.IsDisposed)
                    winToolTip.Show(text, pb, point);
            }));
        }

        public override void Show(string text, object control, Point point, int duration)
        {
            if (form.IsDisposed)
                return;
            var pb = form.GetPictureBox();
            if (pb == null || pb.IsDisposed)
                return;
            form.BeginInvoke(new Action(() =>
            {
                if (!pb.IsDisposed)
                    winToolTip.Show(text, pb, point, duration);
            }));
        }
    }

    internal class DesktopScrollBar : UI.Framework.Forms.ScrollBar
    {
        private readonly DesktopHostForm form;

        public DesktopScrollBar(DesktopHostForm form)
        {
            this.form = form;
        }

        public override int Value
        {
            get => form.GetScrollBarValue();
            set => form.UpdateScrollBar(form.GetScrollBarMaximum(), value);
        }

        public override int Maximum
        {
            get => form.GetScrollBarMaximum();
            set => form.UpdateScrollBar(value, form.GetScrollBarValue());
        }
    }

    internal class DesktopQuickButtonGroup : QuickButtonGroup
    {
        private readonly DesktopHostForm form;

        public DesktopQuickButtonGroup(DesktopHostForm form)
        {
            this.form = form;
        }

        public override void AddButton(string text, int value)
        {
            form.AddQuickButton(text, value);
        }

        public override void Clear()
        {
            form.ClearQuickButtons();
        }
    }
}
