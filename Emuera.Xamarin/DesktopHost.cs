using SkiaSharp;
using SkiaSharp.Views.Forms;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using MinorShift.Emuera.GameView;
using MinorShift.Emuera.UI.Framework.Forms;
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

            inputTextBox = new TextBox
            {
                Dock = DockStyle.Bottom,
                BackColor = Color.Black,
                ForeColor = Color.White,
                Font = new System.Drawing.Font("MS Gothic", 14),
                BorderStyle = BorderStyle.None
            };
            inputTextBox.KeyDown += InputTextBox_KeyDown;

            var panel = new Panel
            {
                Dock = DockStyle.Fill
            };
            panel.Controls.Add(pictureBox);
            panel.Controls.Add(vScrollBar);

            Controls.Add(inputTextBox);
            Controls.Add(panel);

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
            mainWindow = new DesktopMainWindow(this);
            console = new EmueraConsole(mainWindow);
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

        #endregion
    }

    internal class DesktopMainWindow : MainWindow
    {
        private readonly DesktopHostForm form;
        private readonly DesktopScrollBar scrollBar;
        private readonly DesktopMainPicBox mainPicBox;
        private readonly DesktopTextBox textBox;
        private readonly DesktopToolTip toolTip;

        public DesktopMainWindow(DesktopHostForm form)
        {
            this.form = form;
            this.scrollBar = new DesktopScrollBar(form);
            this.mainPicBox = new DesktopMainPicBox(form);
            this.textBox = new DesktopTextBox(form);
            this.toolTip = new DesktopToolTip(form);
        }

        public override UI.Framework.Forms.ScrollBar ScrollBar => scrollBar;
        public override MainPicBoxStub MainPicBox => mainPicBox;
        public override TextBoxStub TextBox => textBox;
        public override ToolTipStub ToolTip => toolTip;

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

        public override SKColor BackgroundColor
        {
            get => SKColors.Black;
            set { }
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

        public override void update_lastinput()
        {
        }

        public override void clear_richText()
        {
            form.ClearTextBoxText();
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
        public override System.Drawing.Font Font
        {
            get => form.IsDisposed ? null : form.GetInputTextBox().Font;
            set { if (!form.IsDisposed) form.BeginInvoke(new Action(() => form.GetInputTextBox().Font = value)); }
        }
    }

    internal class DesktopToolTip : ToolTipStub
    {
        private readonly DesktopHostForm form;

        public DesktopToolTip(DesktopHostForm form)
        {
            this.form = form;
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
}
