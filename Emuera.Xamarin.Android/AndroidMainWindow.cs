extern alias skviews;

using System;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.OS;
using MinorShift.Emuera;
using MinorShift.Emuera.GameView;
using MinorShift.Emuera.Runtime.Utils;
using XEmuera;
using ConfigCode = MinorShift.Emuera.Runtime.Config.ConfigCode;
using RConfig = MinorShift.Emuera.Runtime.Config.Config;
using RConfigData = MinorShift.Emuera.Runtime.Config.ConfigData;
using PConfig = MinorShift.Emuera.Config;
using PConfigData = MinorShift.Emuera.ConfigData;
using MinorShift.Emuera.UI.Framework.Forms;
using SkiaSharp;
using Xamarin.Forms;
using XEmuera.Drawing;
using Point = XEmuera.Drawing.Point;
using Timer = System.Timers.Timer;
using XFView = Xamarin.Forms.View;
using SKCanvasView = skviews::SkiaSharp.Views.Forms.SKCanvasView;
using SKPaintSurfaceEventArgs = skviews::SkiaSharp.Views.Forms.SKPaintSurfaceEventArgs;
using SKTouchEventArgs = skviews::SkiaSharp.Views.Forms.SKTouchEventArgs;
using SKTouchAction = skviews::SkiaSharp.Views.Forms.SKTouchAction;

namespace XEmuera.Droid
{
    internal class AndroidMainWindow : MainWindow
    {
        private EmueraConsole console;
        private Activity activity;
        private SKCanvasView canvasView;
        private Entry textEntry;
        private StackLayout quickButtonLayout;
        private Grid mainLayout;
        private ScrollView quickScroll;
        private Label statusLabel;
        private StackLayout entryLayout;

        // Virtual controller
        private Grid virtualControllerLayout;
        private Button virtualConfirmButton;
        private Button virtualReturnButton;
        private StackLayout virtualPageActionGroup;
        private Button virtualPageEnterButton;
        private Button virtualPageConfirmButton;
        private Button virtualPageBackButton;
        private StackLayout virtualKeypadGroup;
        private Button virtualKeypadEnterActionButton;
        private Button virtualKeypadAcceptActionButton;
        private StackLayout virtualAvgButtonGroup;
        private bool virtualControllerVisible = true;
        private bool IsDragScrollBar;
        private Slider vScrollBar;

        private bool IsMouseMoveAction;
        private SKPoint StartPoint;
        private SKPoint PrevPoint;
        private Timer LongPressTimer;
        private bool IsLongPressed;
        private bool pressEnterKeyInProcess;

        public AndroidMainWindow(Activity activity)
        {
            this.activity = activity;

            LongPressTimer = new Timer(500);
            LongPressTimer.Elapsed += LongPressTimer_Elapsed;

            BuildLayout();
        }

        public void UpdateStatus(string text)
        {
            activity?.RunOnUiThread(() =>
            {
                try
                {
                    if (statusLabel == null) return;
                    statusLabel.Text = text;
                    statusLabel.IsVisible = !string.IsNullOrEmpty(text);
                }
                catch { }
            });
        }

        private void BuildLayout()
        {
            canvasView = new SKCanvasView();
            canvasView.EnableTouchEvents = true;
            canvasView.PaintSurface += OnPaintSurface;
            canvasView.Touch += OnTouch;

            statusLabel = new Label
            {
                Text = "",
                IsVisible = false,
                TextColor = Xamarin.Forms.Color.White,
                BackgroundColor = Xamarin.Forms.Color.FromRgba(0, 0, 0, 180),
                FontSize = 14,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                HorizontalTextAlignment = TextAlignment.Center,
                Padding = new Thickness(16, 8),
            };

            textEntry = new Entry
            {
                BackgroundColor = Xamarin.Forms.Color.Black,
                TextColor = Xamarin.Forms.Color.White,
                Placeholder = "",
                ReturnType = ReturnType.Done,
                HorizontalOptions = LayoutOptions.FillAndExpand,
            };
            textEntry.Completed += OnEntryCompleted;
            textEntry.TextChanged += (s, e) =>
            {
                // 同步输入栏文字到 TextBox，使 GETTEXTBOX() 能检测到输入
                if (TextBox != null)
                    TextBox.Text = textEntry.Text ?? "";
                // 输入文字时中断 AWAIT（如 BadApple demo）
                if (console != null && console.IsInProcess && !string.IsNullOrEmpty(textEntry.Text))
                    console.InterruptAwait();
            };

            var sendButton = new Button
            {
                Text = "▶",
                BackgroundColor = Xamarin.Forms.Color.FromHex("#333333"),
                TextColor = Xamarin.Forms.Color.White,
                WidthRequest = 44,
                HeightRequest = 44,
                FontSize = 16,
            };
            sendButton.Clicked += (s, e) =>
            {
                OnEntryCompleted(s, e);
            };

            entryLayout = new StackLayout
            {
                Orientation = StackOrientation.Horizontal,
                Children = { textEntry, sendButton },
                IsVisible = false,
                HeightRequest = 50,
                BackgroundColor = Xamarin.Forms.Color.FromHex("#1a1a1a"),
                Padding = new Thickness(4, 2),
            };

            quickButtonLayout = new QuickButtonGroup
            {
                Orientation = StackOrientation.Horizontal,
                Spacing = 4,
            };

            quickScroll = new ScrollView
            {
                Orientation = ScrollOrientation.Horizontal,
                Content = quickButtonLayout,
                HeightRequest = 0,
                IsVisible = false,
            };

            quickButtonScrollView = quickScroll;

            // --- 工具栏按钮（SVG 图标 + TintImageButton） ---
            // 辅助方法：创建文本工具栏按钮
            Button MakeToolButton(string text, float fontSize = 10f)
            {
                return new Button
                {
                    Text = text,
                    BackgroundColor = Xamarin.Forms.Color.FromRgba(0, 0, 0, 160),
                    TextColor = Xamarin.Forms.Color.White,
                    CornerRadius = 3,
                    Padding = 5,
                    WidthRequest = 34,
                    HeightRequest = 34,
                    FontAttributes = FontAttributes.Bold,
                    FontSize = fontSize,
                };
            }

            // 辅助方法：创建 SVG 图标工具栏按钮
            XEmuera.Views.TintImageButton MakeSvgToolButton(string svgName, XEmuera.Drawing.Color tintColor)
            {
                var btn = new XEmuera.Views.TintImageButton
                {
                    Image = svgName,
                    TintColor = tintColor,
                    WidthRequest = 34,
                    HeightRequest = 34,
                    BackgroundColor = Xamarin.Forms.Color.FromRgba(0, 0, 0, 160),
                    Padding = new Thickness(5),
                    Margin = new Thickness(1, 0),
                };
                return btn;
            }

            var svgTintColor = XEmuera.Drawing.Color.White;

            // menu_button（打开 flyout 侧边栏，使用 ☰ 字符）
            var menuButton = MakeToolButton("☰", 12f);
            menuButton.Clicked += (s, e) => { flyoutPage.IsPresented = true; };

            // edit_button（切换式，SVG 图标）
            var editButton = MakeSvgToolButton("edit.svg", svgTintColor);
            editButton.Clicked += (s, e) =>
            {
                bool show = !entryLayout.IsVisible;
                entryLayout.IsVisible = show;
                editButton.Opacity = show ? 1 : 0.5;
                if (show)
                    textEntry.Focus();
            };

            // gamepad_button（切换虚拟控制器，SVG 图标）
            var gamepadButton = MakeSvgToolButton("gamepad.svg", svgTintColor);
            gamepadButton.Clicked += (s, e) =>
            {
                virtualControllerVisible = !virtualControllerVisible;
                virtualControllerLayout.IsVisible = virtualControllerVisible;
                gamepadButton.Opacity = virtualControllerVisible ? 1 : 0.5;
                if (virtualControllerVisible)
                    RefreshVirtualControllerState();
            };

            // gallery_view_button（切换快捷按钮面板，SVG 图标）
            var galleryViewButton = MakeSvgToolButton("gallery_view.svg", svgTintColor);
            galleryViewButton.Clicked += (s, e) =>
            {
                bool show = !quickScroll.IsVisible;
                quickScroll.IsVisible = show;
                galleryViewButton.Opacity = show ? 1 : 0.5;
                if (show)
                    console?.RefreshQuickButton();
                else
                    console?.ClearQuickButton();
            };

            // scroll_vertical_button（切换滚动条 Slider，SVG 图标）
            var scrollVerticalButton = MakeSvgToolButton("scroll_vertical.svg", svgTintColor);
            // 注意：scrollBarLayout 在后面创建，事件在创建后绑定

            // zoom buttons
            var zoomInButton = MakeToolButton("A+");
            zoomInButton.Clicked += (s, e) => AdjustFontScale(0.1f);

            var zoomOutButton = MakeToolButton("A-");
            zoomOutButton.Clicked += (s, e) => AdjustFontScale(-0.1f);

            // menu_show_button（收起/展开工具栏，SVG 图标）
            var menuShowButton = MakeSvgToolButton("menu_show.svg", svgTintColor);
            var toolbar = new StackLayout
            {
                Orientation = StackOrientation.Horizontal,
                HorizontalOptions = LayoutOptions.FillAndExpand,
                HeightRequest = 48,
                Padding = new Thickness(0, 6, 12, 6),
                Spacing = 4,
                InputTransparent = true,
                CascadeInputTransparent = false,
            };

            // 占位符（将按钮推到右侧，与 XEmuera BoxView HorizontalOptions=StartAndExpand 一致）
            var spacer = new BoxView { InputTransparent = true, HorizontalOptions = LayoutOptions.StartAndExpand };

            // 初始状态：gallery 和 scroll 默认隐藏
            galleryViewButton.Opacity = 0.5;
            scrollVerticalButton.Opacity = 0.5;

            // 收起/展开逻辑（与 XEmuera ButtonVisibleGroup_Clicked 一致）
            menuShowButton.Clicked += (s, e) =>
            {
                bool visible = !editButton.IsVisible;
                editButton.IsVisible = visible;
                gamepadButton.IsVisible = visible;
                galleryViewButton.IsVisible = visible;
                scrollVerticalButton.IsVisible = visible;
                zoomOutButton.IsVisible = visible;
                zoomInButton.IsVisible = visible;
                menuShowButton.Opacity = visible ? 1 : 0.5;
            };

            // 组装工具栏（顺序：spacer, ☰menu, edit, scroll_vertical, gallery, gamepad, A-, A+, menu_show）
            toolbar.Children.Add(spacer);
            toolbar.Children.Add(menuButton);
            toolbar.Children.Add(editButton);
            toolbar.Children.Add(scrollVerticalButton);
            toolbar.Children.Add(galleryViewButton);
            toolbar.Children.Add(gamepadButton);
            toolbar.Children.Add(zoomOutButton);
            toolbar.Children.Add(zoomInButton);
            toolbar.Children.Add(menuShowButton);

            // --- 滚动条 Slider（旋转90°，与 XEmuera 一致） ---
            vScrollBar = new Slider
            {
                Maximum = 1,
                Value = 1,
                MaximumTrackColor = Xamarin.Forms.Color.LightGray,
                MinimumTrackColor = Xamarin.Forms.Color.LightGray,
                HorizontalOptions = LayoutOptions.FillAndExpand,
                // 旋转90°后 WidthRequest 变成可视高度
                // 设置为合理值（约屏幕高度的 60%），避免太小无法操作
                WidthRequest = 400,
            };
            vScrollBar.DragStarted += (s, e) => IsDragScrollBar = true;
            vScrollBar.DragCompleted += (s, e) => IsDragScrollBar = false;
            vScrollBar.ValueChanged += (s, e) =>
            {
                if (!IsDragScrollBar) return;
                int newValue = (int)e.NewValue;
                ScrollBar.Value = newValue;
                if (newValue == (int)vScrollBar.Minimum || newValue == (int)vScrollBar.Maximum || (int)e.OldValue != newValue)
                    RefreshStrings(vScrollBar.Value == vScrollBar.Maximum || vScrollBar.Value == vScrollBar.Minimum);
            };

            var scrollBarLayout = new StackLayout
            {
                IsVisible = false,
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.EndAndExpand,
                Margin = new Thickness(0, 0, 40, 20),
                Rotation = 90,
                AnchorX = 1,
                AnchorY = 0.5,
                Children = { vScrollBar },
            };

            // 延迟绑定 scroll_vertical_button 事件（依赖 scrollBarLayout）
            scrollVerticalButton.Clicked += (s, e) =>
            {
                bool show = !scrollBarLayout.IsVisible;
                scrollBarLayout.IsVisible = show;
                scrollVerticalButton.Opacity = show ? 1 : 0.5;
            };

            // --- QuickButton 面板（与 XEmuera 一致：右下角垂直滚动） ---
            quickScroll.Orientation = ScrollOrientation.Both;
            quickScroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Never;
            quickScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Never;
            quickScroll.Margin = new Thickness(0, 0, 20, 20);
            quickScroll.HorizontalOptions = LayoutOptions.End;
            quickScroll.VerticalOptions = LayoutOptions.EndAndExpand;
            quickButtonLayout.Orientation = StackOrientation.Vertical;
            quickButtonLayout.Spacing = 3;

            // --- uiLayout（叠加层：工具栏 + 滚动条 + 快捷按钮） ---
            var uiLayout = new StackLayout
            {
                InputTransparent = true,
                CascadeInputTransparent = false,
                Orientation = StackOrientation.Vertical,
                Children =
                {
                    toolbar,
                    scrollBarLayout,
                    quickScroll,
                },
            };

            BuildVirtualController();

            var canvasGrid = new Grid
            {
                Children =
                {
                    canvasView,
                    uiLayout,
                    statusLabel,
                    virtualControllerLayout,
                },
            };

            mainLayout = new Grid
            {
                BackgroundColor = Xamarin.Forms.Color.Black,
                RowDefinitions =
                {
                    new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                    new RowDefinition { Height = GridLength.Auto },
                },
                Children =
                {
                    { canvasGrid, 0, 0 },
                    { entryLayout, 0, 1 },
                },
            };
        }

        public XFView GetView() => mainLayout;

        internal void SetConsole(EmueraConsole console)
        {
            this.console = console;
            RefreshVirtualControllerState();
        }

        #region SKCanvasView Events

        private int lastLogicalW = 0;
        private int lastLogicalH = 0;

        private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            var density = DisplayUtils.ScreenDensity;
            int logicalW = density > 0 ? (int)(e.Info.Width / density) : e.Info.Width;
            int logicalH = density > 0 ? (int)(e.Info.Height / density) : e.Info.Height;
            MainPicBox.Width = logicalW;
            MainPicBox.Height = logicalH;
            if (GameUtils.MainPicBox != null)
            {
                GameUtils.MainPicBox.Width = logicalW;
                GameUtils.MainPicBox.Height = logicalH;
            }
            PConfig.RefreshDisplayConfig();

            // 检测方向/尺寸变化，触发 reflow
            bool sizeChanged = lastLogicalW > 0 && lastLogicalH > 0
                && (Math.Abs(logicalW - lastLogicalW) > 10 || Math.Abs(logicalH - lastLogicalH) > 10);
            lastLogicalW = logicalW;
            lastLogicalH = logicalH;

            if (sizeChanged && console != null)
            {
                // 方向变化：重新应用当前缩放以 reflow 所有行
                ApplyRuntimeDisplayScale(PConfig.FontScale, false);
            }

            if (console == null) return;
            var canvas = e.Surface.Canvas;
            console.OnPaint(canvas);
        }

        private void OnTouch(object sender, SKTouchEventArgs e)
        {
            if (console == null) return;

            // 将物理像素坐标转换为逻辑坐标
            var touchPoint = ToLogicalTouchPoint(e.Location);
            var mouseLocation = new Point((int)touchPoint.X, (int)touchPoint.Y);

            if (console.MoveMouse(mouseLocation))
                RefreshStrings(true);

            switch (e.ActionType)
            {
                case SKTouchAction.Pressed:
                    IsMouseMoveAction = false;
                    PrevPoint = e.Location;
                    StartPoint = PrevPoint;

                    IsLongPressed = false;
                    if (PConfig.LongPressSkip)
                        LongPressTimer.Enabled = true;

                    if (console.IsInProcess)
                    {
                        WinInput.SetKeyPressed(1);   // VK_LBUTTON
                        WinInput.SetKeyPressed(13);  // VK_RETURN
                        WinInput.SetKeyPressed(27);  // VK_ESCAPE
                        // AWAIT (Sleep) 状态下，触摸中断等待
                        console.InterruptAwait();
                    }

                    e.Handled = true;
                    return;

                case SKTouchAction.Moved:
                    IsMouseMoveAction = true;

                    int moveY = (int)(e.Location.Y - PrevPoint.Y);

                    if (RConfig.LineHeight > 0)
                    {
                        int sign = moveY / RConfig.LineHeight;
                        if (sign != 0)
                        {
                            int curValue = ScrollBar.Value;
                            int curMax = ScrollBar.Maximum;
                            int curMin = ScrollBar.Minimum;
                            int newValue = curValue - sign;
                            newValue = Math.Max(curMin, Math.Min(curMax, newValue));
                            if (newValue != curValue)
                            {
                                ScrollBar.Value = newValue;
                                SyncVScrollBar();
                                RefreshStrings(true);
                            }
                        }
                    }

                    PrevPoint = e.Location;
                    e.Handled = true;
                    return;

                case SKTouchAction.Released:
                    if (PConfig.LongPressSkip)
                        LongPressTimer.Enabled = false;

                    if (console.IsInProcess)
                    {
                        if (IsLongPressed)
                            WinInput.SetKeyPressed(2);
                        WinInput.SetKeyReleased(1);
                        WinInput.SetKeyReleased(2);
                        WinInput.SetKeyReleased(27);
                        Task.Delay(300).ContinueWith(_ => WinInput.SetKeyReleased(13));
                    }

                    MouseReleased(e);
                    Task.Run(() => console.LeaveMouse());

                    e.Handled = true;
                    break;

                default:
                    return;
            }
        }

        private void MouseReleased(SKTouchEventArgs e)
        {
            if (IsMouseMove(e.Location))
                return;

            if (console.IsInProcess)
                return;

            var button = IsLongPressed ? SKMouseButton.Right : SKMouseButton.Left;

            if (console.IsWaitingPrimitive)
            {
                var point = new Point((int)e.Location.X, (int)e.Location.Y);
                console.MouseDown(point, button);
                return;
            }

            bool isBacklog = ScrollBar.Value != ScrollBar.Maximum;
            string str = console.SelectedString;

            if (isBacklog)
            {
                // 先取 SelectedString（历史模式下 MoveMouse 已正确设置），
                // 再滚回底部（与 XEmuera 行为一致）
                ScrollBar.Value = ScrollBar.Maximum;
                SyncVScrollBar();
                RefreshStrings(true);
            }

            if (console.IsWaitingEnterKey && !console.IsError && str == null)
            {
                if (isBacklog) return;
                if (button == SKMouseButton.Right)
                    PressEnterKey(true, "", true);
                else
                    PressEnterKey(false, "", true);
                return;
            }

            if (str != null && button == SKMouseButton.Left)
            {
                textEntry.Text = str;
                PressEnterKey(false, str, true);
                return;
            }
        }

        private bool IsMouseMove(SKPoint endPoint)
        {
            return IsMouseMoveAction && SKPoint.Distance(StartPoint, endPoint) >= 10;
        }

        private void LongPressTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            LongPressTimer.Enabled = false;
            if (IsMouseMove(PrevPoint))
                return;
            IsLongPressed = true;
        }

        #endregion

        #region Text Input

        private void OnEntryCompleted(object sender, EventArgs e)
        {
            var text = textEntry.Text ?? "";
            PressEnterKey(false, text, false);
            textEntry.Text = "";
        }

        private void PressEnterKey(bool keySkip, string str, bool changedByMouse)
        {
            if (pressEnterKeyInProcess) return;
            pressEnterKeyInProcess = true;

            Task.Run(() =>
            {
                try
                {
                    console.PressEnterKey(keySkip, str, changedByMouse);
                }
                finally
                {
                    pressEnterKeyInProcess = false;
                }
            });
        }

        #endregion

        #region MainWindow Overrides

        public override QuickButtonGroup QuickButtonGroup => (QuickButtonGroup)quickButtonLayout;

        public new void RefreshQuickButtonGroup()
        {
            activity?.RunOnUiThread(() =>
            {
                try
                {
                    quickScroll.WidthRequest = Math.Min(quickButtonLayout.Width, canvasView.Width * 3 / 5);
                    quickScroll.HeightRequest = Math.Min(quickButtonLayout.Height, canvasView.Height - 48 - 30);
                    quickScroll.ScrollToAsync(0, quickButtonLayout.Height, false);
                }
                catch { }
            });
        }

        public override void quickButton_Clicked(object sender, EventArgs e)
        {
            var inputs = (sender as Xamarin.Forms.View)?.BindingContext as string;
            if (inputs != null)
                PressEnterKey(false, inputs, true);
        }

        public override void Invoke(Action action)
        {
            if (activity == null || activity.IsDestroyed)
            {
                action?.Invoke();
                return;
            }
            activity.RunOnUiThread(() =>
            {
                try { action?.Invoke(); } catch { }
            });
        }

        public override void Refresh()
        {
            activity?.RunOnUiThread(() =>
            {
                try { canvasView?.InvalidateSurface(); } catch { }
            });
        }

        public override void ShowConfigDialog()
        {
            activity?.RunOnUiThread(() =>
            {
                try
                {
                    var navPage = (flyoutPage?.Detail as NavigationPage);
                    navPage?.PushAsync(new AndroidConfigPage());
                }
                catch { }
            });
        }

        private Xamarin.Forms.FlyoutPage flyoutPage;

        internal void SetFlyoutPage(Xamarin.Forms.FlyoutPage fp)
        {
            flyoutPage = fp;
        }

        public override void Reboot()
        {
            if (console == null) return;
            Task.Run(() =>
            {
                console.GotoTitle();
            });
        }

        public override void Close()
        {
            activity?.Finish();
        }

        public override void Focus()
        {
        }

        public override void MainMenu_Reboot()
        {
            Reboot();
        }

        public override void MainMenu_GotoTitle()
        {
            if (console == null || console.IsInProcess) return;
            Reboot();
        }

        public override void update_lastinput()
        {
            activity?.RunOnUiThread(() =>
            {
                try
                {
                    entryLayout.IsVisible = true;
                    textEntry.Focus();
                }
                catch { }
            });
        }

        public override void clear_richText()
        {
            activity?.RunOnUiThread(() =>
            {
                try
                {
                    textEntry.Text = "";
                    entryLayout.IsVisible = false;
                }
                catch { }
            });
        }

        public override void SetTextBoxPos(int x, int y, int width = 0)
        {
        }

        public override void ChangeTextBox(string str)
        {
            // 同步更新 TextBox.Text，使 GETTEXTBOX() 立即可读
            if (TextBox != null)
                TextBox.Text = str ?? "";
            activity?.RunOnUiThread(() =>
            {
                try { textEntry.Text = str ?? ""; } catch { }
            });
        }

        #endregion

        #region ScrollBar Helpers

        private void SyncVScrollBar()
        {
            if (vScrollBar == null) return;
            if (IsDragScrollBar) return;
            vScrollBar.Maximum = ScrollBar.Maximum;
            vScrollBar.Value = ScrollBar.Value;
        }

        private void RefreshStrings(bool force)
        {
            if (console == null) return;
            console.RefreshStrings(force);
            SyncVScrollBar();
            Refresh();
        }

        #endregion

        #region Virtual Controller

        private Button MakeVCButton(string text, EventHandler handler)
        {
            var btn = new Button
            {
                Text = text,
                BackgroundColor = Xamarin.Forms.Color.FromRgba(0, 0, 0, 153),
                TextColor = Xamarin.Forms.Color.White,
                WidthRequest = 48,
                HeightRequest = 48,
                CornerRadius = 24,
                FontAttributes = FontAttributes.Bold,
                FontSize = 14,
                Padding = 0,
                Margin = 2,
                Opacity = 0.9,
            };
            btn.Clicked += handler;
            return btn;
        }

        private Button MakeRoundButton(string text, EventHandler handler)
        {
            var btn = new Button
            {
                Text = text,
                BackgroundColor = Xamarin.Forms.Color.FromRgba(0, 0, 0, 153),
                TextColor = Xamarin.Forms.Color.White,
                WidthRequest = 64,
                HeightRequest = 64,
                CornerRadius = 32,
                FontAttributes = FontAttributes.Bold,
                FontSize = 14,
                Padding = 0,
                Margin = 2,
                Opacity = 0.9,
            };
            btn.Clicked += handler;
            return btn;
        }

        private void BuildVirtualController()
        {
            // Direction pad (left side)
            var dirUp = MakeVCButton("▲", (s, e) => console?.NavigateSelection(EmueraConsole.VirtualSelectionDirection.Up));
            var dirLeft = MakeVCButton("◀", (s, e) => console?.NavigateSelection(EmueraConsole.VirtualSelectionDirection.Left));
            var dirRight = MakeVCButton("▶", (s, e) => console?.NavigateSelection(EmueraConsole.VirtualSelectionDirection.Right));
            var dirDown = MakeVCButton("▼", (s, e) => console?.NavigateSelection(EmueraConsole.VirtualSelectionDirection.Down));

            var dirGrid = new Grid
            {
                RowDefinitions =
                {
                    new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) },
                    new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) },
                    new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) },
                },
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) },
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) },
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) },
                },
                HorizontalOptions = LayoutOptions.Start,
                VerticalOptions = LayoutOptions.End,
                RowSpacing = 0,
                ColumnSpacing = 0,
            };
            dirGrid.Children.Add(dirUp, 1, 0);
            dirGrid.Children.Add(dirLeft, 0, 1);
            dirGrid.Children.Add(new BoxView { WidthRequest = 48, HeightRequest = 48, Opacity = 0 }, 1, 1);
            dirGrid.Children.Add(dirRight, 2, 1);
            dirGrid.Children.Add(dirDown, 1, 2);

            // Page action buttons (center-right)
            virtualPageEnterButton = MakeVCButton("Enter", VirtualPageEnterButton_Clicked);
            virtualPageConfirmButton = MakeVCButton("确定", VirtualPageConfirmButton_Clicked);
            virtualPageBackButton = MakeVCButton("返回", VirtualPageBackButton_Clicked);
            virtualPageActionGroup = new StackLayout
            {
                Orientation = StackOrientation.Vertical,
                Spacing = 4,
                Children = { virtualPageEnterButton, virtualPageConfirmButton, virtualPageBackButton },
                IsVisible = false,
            };

            // Keypad group
            virtualKeypadEnterActionButton = MakeVCButton("Enter", VirtualKeypadEnterAction_Clicked);
            virtualKeypadAcceptActionButton = MakeVCButton("确定", VirtualKeypadAcceptAction_Clicked);
            var keypadConfirmBtn = MakeVCButton("确认", VirtualKeypadConfirm_Clicked);
            var keypadBackBtn = MakeVCButton("返回", VirtualKeypadBack_Clicked);

            var keypadGrid = new Grid
            {
                RowDefinitions =
                {
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = GridLength.Auto },
                },
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Auto },
                },
                RowSpacing = 2,
                ColumnSpacing = 2,
            };
            // Row 0: Enter, Accept
            keypadGrid.Children.Add(virtualKeypadEnterActionButton, 0, 0); Grid.SetColumnSpan(virtualKeypadEnterActionButton, 2);
            keypadGrid.Children.Add(virtualKeypadAcceptActionButton, 2, 0); Grid.SetColumnSpan(virtualKeypadAcceptActionButton, 2);
            // Row 1: / * - +
            keypadGrid.Children.Add(MakeVCButton("/", (s, e) => ActivateKeypadButton("/", "[/]", "／")), 0, 1);
            keypadGrid.Children.Add(MakeVCButton("*", (s, e) => ActivateKeypadButton("*", "[*]", "＊")), 1, 1);
            keypadGrid.Children.Add(MakeVCButton("-", (s, e) => ActivateKeypadButton("-", "[-]", "－")), 2, 1);
            keypadGrid.Children.Add(MakeVCButton("+", (s, e) => ActivateKeypadButton("+", "[+]", "＋")), 3, 1);
            // Row 2: 7 8 9
            keypadGrid.Children.Add(MakeVCButton("7", (s, e) => ActivateKeypadButton("7", "[7]")), 0, 2);
            keypadGrid.Children.Add(MakeVCButton("8", (s, e) => ActivateKeypadButton("8", "[8]")), 1, 2);
            keypadGrid.Children.Add(MakeVCButton("9", (s, e) => ActivateKeypadButton("9", "[9]")), 2, 2);
            // Row 3: 4 5 6
            keypadGrid.Children.Add(MakeVCButton("4", (s, e) => ActivateKeypadButton("4", "[4]")), 0, 3);
            keypadGrid.Children.Add(MakeVCButton("5", (s, e) => ActivateKeypadButton("5", "[5]")), 1, 3);
            keypadGrid.Children.Add(MakeVCButton("6", (s, e) => ActivateKeypadButton("6", "[6]")), 2, 3);
            // Row 4: 1 2 3
            keypadGrid.Children.Add(MakeVCButton("1", (s, e) => ActivateKeypadButton("1", "[1]")), 0, 4);
            keypadGrid.Children.Add(MakeVCButton("2", (s, e) => ActivateKeypadButton("2", "[2]")), 1, 4);
            keypadGrid.Children.Add(MakeVCButton("3", (s, e) => ActivateKeypadButton("3", "[3]")), 2, 4);

            virtualKeypadGroup = new StackLayout
            {
                Orientation = StackOrientation.Vertical,
                Spacing = 4,
                Children = { keypadGrid, new StackLayout { Orientation = StackOrientation.Horizontal, Spacing = 2, Children = { MakeVCButton("0", (s, e) => ActivateKeypadButton("0", "[0]")), MakeVCButton(".", (s, e) => ActivateKeypadButton(".", "[.]", "．")), keypadConfirmBtn, keypadBackBtn } } },
                IsVisible = false,
            };

            // AVG buttons
            virtualAvgButtonGroup = new StackLayout
            {
                Orientation = StackOrientation.Vertical,
                Spacing = 4,
                Children =
                {
                    MakeVCButton("日志", (s, e) => TryActivateKeywordButton("日志")),
                    MakeVCButton("自动", (s, e) => TryActivateKeywordButton("自动")),
                    MakeVCButton("跳过", (s, e) => TryActivateKeywordButton("跳过")),
                    MakeVCButton("设置", (s, e) => TryActivateKeywordButton("设置")),
                },
                IsVisible = false,
            };

            // Confirm/Return round buttons (always visible when VC is on)
            virtualConfirmButton = MakeRoundButton("确认", VirtualConfirmButton_Clicked);
            virtualReturnButton = MakeRoundButton("返回", VirtualReturnButton_Clicked);

            var rightPanel = new StackLayout
            {
                Orientation = StackOrientation.Vertical,
                Spacing = 4,
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.End,
                Children =
                {
                    virtualKeypadGroup,
                    virtualAvgButtonGroup,
                    virtualPageActionGroup,
                    new StackLayout
                    {
                        Orientation = StackOrientation.Horizontal,
                        Spacing = 8,
                        HorizontalOptions = LayoutOptions.End,
                        Children = { virtualConfirmButton, virtualReturnButton },
                    }
                },
            };

            virtualControllerLayout = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                },
                RowDefinitions =
                {
                    new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                    new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                },
                Padding = new Thickness(16, 56, 16, 16),
                IsVisible = true,
                InputTransparent = true,
                CascadeInputTransparent = false,
            };
            virtualControllerLayout.Children.Add(dirGrid, 0, 1);
            virtualControllerLayout.Children.Add(rightPanel, 2, 1);
        }

        public void RefreshVirtualControllerState()
        {
            if (virtualControllerLayout == null)
                return;

            bool controlsEnabled = virtualControllerVisible;
            if (!controlsEnabled || console == null)
            {
                virtualPageActionGroup.IsVisible = false;
                virtualKeypadGroup.IsVisible = false;
                virtualAvgButtonGroup.IsVisible = false;
                return;
            }

            // Enter button
            bool hasEnterAction = HasEnterAction();
            virtualPageEnterButton.IsVisible = hasEnterAction;

            // Confirm/Back labels
            string confirmLabel = console.GetVisibleButtonKeywordLabel("确认", "确定", "调合", "选定", "是");
            virtualPageConfirmButton.IsVisible = !string.IsNullOrEmpty(confirmLabel);
            virtualPageConfirmButton.Text = string.IsNullOrEmpty(confirmLabel) ? "确定" : confirmLabel;

            string backLabel = console.GetVisibleButtonKeywordLabel("返回", "取消", "归返", "结束", "完毕", "否");
            virtualPageBackButton.IsVisible = !string.IsNullOrEmpty(backLabel);
            virtualPageBackButton.Text = string.IsNullOrEmpty(backLabel) ? "返回" : backLabel;

            // Keypad panel detection
            bool isKeypadPanel = console.HasRecentDisplayLineKeyword(20, "[2]", "［2］")
                && console.HasRecentDisplayLineKeyword(20, "[4]", "［4］")
                && console.HasRecentDisplayLineKeyword(20, "[6]", "［6］")
                && console.HasRecentDisplayLineKeyword(20, "[8]", "［8］");

            // AVG panel detection
            string logLabel = console.GetVisibleButtonKeywordLabel("日志");
            string autoLabel = console.GetVisibleButtonKeywordLabel("自动");
            string skipLabel = console.GetVisibleButtonKeywordLabel("跳过");
            string settingLabel = console.GetVisibleButtonKeywordLabel("设置");
            bool isAvgPanel = !string.IsNullOrEmpty(logLabel)
                && !string.IsNullOrEmpty(autoLabel)
                && !string.IsNullOrEmpty(skipLabel)
                && !string.IsNullOrEmpty(settingLabel);

            // Mutually exclusive: keypad > AVG
            virtualKeypadGroup.IsVisible = isKeypadPanel;
            virtualAvgButtonGroup.IsVisible = !isKeypadPanel && isAvgPanel;

            // Keypad action buttons
            virtualKeypadEnterActionButton.IsVisible = isKeypadPanel && hasEnterAction;
            virtualKeypadAcceptActionButton.IsVisible = isKeypadPanel && !string.IsNullOrEmpty(confirmLabel);

            // Page action group
            virtualPageActionGroup.IsVisible = !isKeypadPanel
                && (virtualPageEnterButton.IsVisible || virtualPageConfirmButton.IsVisible || virtualPageBackButton.IsVisible);

            // Round buttons text
            virtualConfirmButton.Text = string.IsNullOrEmpty(confirmLabel) ? "确认" : confirmLabel;
            virtualReturnButton.Text = string.IsNullOrEmpty(backLabel) ? "返回" : backLabel;
        }

        #endregion

        #region Virtual Controller Event Handlers

        private void VirtualConfirmButton_Clicked(object sender, EventArgs e)
        {
            if (console == null || console.IsInProcess) return;
            ScrollBacklogToBottom();
            ExecuteCurrentSelectedButtonOnly();
        }

        private void VirtualReturnButton_Clicked(object sender, EventArgs e)
        {
            if (console == null || console.IsInProcess) return;
            ScrollBacklogToBottom();
            if (!TryActivateKeywordButton("返回", "取消", "归返", "结束", "完毕", "否"))
                PressEnterKey(true, "", true);
        }

        private void VirtualPageEnterButton_Clicked(object sender, EventArgs e)
        {
            TryActivateEnterAction();
        }

        private void VirtualPageConfirmButton_Clicked(object sender, EventArgs e)
        {
            TryActivateKeywordButton("确认", "确定", "调合", "选定", "是");
        }

        private void VirtualPageBackButton_Clicked(object sender, EventArgs e)
        {
            TryActivateKeywordButton("返回", "取消", "归返", "结束", "完毕", "否");
        }

        private void VirtualKeypadEnterAction_Clicked(object sender, EventArgs e)
        {
            TryActivateEnterAction();
        }

        private void VirtualKeypadAcceptAction_Clicked(object sender, EventArgs e)
        {
            TryActivateKeywordButton("确认", "确定", "调合", "选定", "是");
        }

        private void VirtualKeypadConfirm_Clicked(object sender, EventArgs e)
        {
            if (console == null || console.IsInProcess) return;
            ScrollBacklogToBottom();
            ExecuteCurrentSelectedButtonOnly();
        }

        private void VirtualKeypadBack_Clicked(object sender, EventArgs e)
        {
            TryActivateKeywordButton("返回", "取消", "归返", "结束", "完毕", "否");
        }

        #endregion

        #region Smart Button Mapping

        private bool TryActivateKeywordButton(params string[] keywords)
        {
            if (console == null || console.IsInProcess) return false;
            if (!console.TrySelectVisibleButtonByKeywords(keywords))
                return false;
            ExecuteVirtualSelectedButton(false);
            return true;
        }

        private bool TryActivateExactButton(params string[] labels)
        {
            if (console == null || console.IsInProcess) return false;
            if (!console.TrySelectVisibleButtonByLabels(labels))
                return false;
            ExecuteVirtualSelectedButton(false);
            return true;
        }

        private void ActivateKeypadButton(params string[] labels)
        {
            if (console == null || console.IsInProcess) return;
            ScrollBacklogToBottom();
            if (TryActivateExactButton(labels))
                return;
            if (TryActivateKeywordButton(labels))
                return;

            string fallbackInput = GetKeypadFallbackInput(labels);
            if (!string.IsNullOrEmpty(fallbackInput))
                PressEnterKey(false, fallbackInput, true);
        }

        private static string GetKeypadFallbackInput(params string[] labels)
        {
            if (labels == null) return null;
            for (int i = 0; i < labels.Length; i++)
            {
                string label = labels[i];
                if (string.IsNullOrWhiteSpace(label)) continue;
                if (label.StartsWith("[") && label.EndsWith("]")) continue;
                if (label.StartsWith("［") && label.EndsWith("］")) continue;
                return label;
            }
            return null;
        }

        private bool HasEnterAction()
        {
            if (console == null) return false;
            // Check if there's an "Enter" or "[Enter]" button visible
            string enterLabel = console.GetVisibleButtonKeywordLabel("Enter", "[Enter]");
            return !string.IsNullOrEmpty(enterLabel);
        }

        private bool TryActivateEnterAction()
        {
            if (console == null || console.IsInProcess) return false;
            if (TryActivateExactButton("Enter", "[Enter]"))
                return true;
            if (console.IsWaitingEnterKey && !console.IsError)
            {
                PressEnterKey(false, "", true);
                return true;
            }
            return false;
        }

        private void ExecuteCurrentSelectedButtonOnly()
        {
            if (console == null) return;
            if (console.SelectingButton != null)
            {
                console.ClickSelectedButton(SKMouseButton.Left);
            }
        }

        private void ExecuteVirtualSelectedButton(bool mesSkip)
        {
            if (console == null) return;

            if (console.IsWaitingPrimitive)
            {
                console.ClickSelectedButton(SKMouseButton.Left);
                return;
            }

            string str = console.SelectedString;
            if (str != null)
            {
                PressEnterKey(mesSkip, str, true);
                return;
            }

            if (console.IsWaitingEnterKey && !console.IsError)
                PressEnterKey(mesSkip, "", true);
        }

        private bool ScrollBacklogToBottom()
        {
            try
            {
                var sb = ScrollBar;
                if (sb == null) return false;
                if (sb.Value >= sb.Maximum) return false;
                sb.Value = sb.Maximum;
                return true;
            }
            catch { return false; }
        }

        #endregion

        #region Runtime Display Scale

        /// <summary>
        /// 将触摸坐标转换为 MoveMouse 使用的坐标空间。
        /// ClientWidth/ClientHeight 通过 GetRealLength() 乘以了 ScreenDensity，
        /// 所以 MoveMouse 期望物理像素坐标，直接使用 e.Location 即可。
        /// </summary>
        private SKPoint ToLogicalTouchPoint(SKPoint location)
        {
            // 触摸坐标已经是物理像素，与 ClientWidth/ClientHeight（物理像素）一致
            return location;
        }

        public void ApplyRuntimeDisplayScale(float fontScale, bool refreshSurface = true)
        {
            float clampedScale = Math.Max(0.5f, Math.Min(5.0f, fontScale));
            PConfig.SetRuntimeFontScale(clampedScale);

            // Sync new scale back into ConfigData so it persists
            PConfigData.Instance.GetItem(ConfigCode.FontScale).SetValue(clampedScale);

            PConfig.ApplyRuntimeDisplaySettings(PConfigData.Instance);

            if (console != null)
                console.ReflowDisplayLinesForCurrentScale();

            if (refreshSurface)
                Refresh();
        }

        private void AdjustFontScale(float delta)
        {
            var current = PConfig.FontScale;
            var next = Math.Max(0.5f, Math.Min(5.0f, (float)Math.Round(current + delta, 2)));
            if (Math.Abs(next - current) < 0.01f) return;

            ApplyRuntimeDisplayScale(next);
        }

        #endregion
    }
}
