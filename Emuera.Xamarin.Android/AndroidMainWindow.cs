extern alias skviews;

using System;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.OS;
using MinorShift.Emuera;
using MinorShift.Emuera.GameView;
using MinorShift.Emuera.Runtime.Utils;
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
            canvasView.IgnorePixelScaling = true;
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
                IsVisible = false,
                BackgroundColor = Xamarin.Forms.Color.Black,
                TextColor = Xamarin.Forms.Color.White,
                Placeholder = "",
                ReturnType = ReturnType.Done,
                HorizontalOptions = LayoutOptions.FillAndExpand,
            };
            textEntry.Completed += OnEntryCompleted;

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

            var entryLayout = new StackLayout
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

            var editButton = new Button
            {
                Text = "⌨",
                BackgroundColor = Xamarin.Forms.Color.FromHex("#CC333333"),
                TextColor = Xamarin.Forms.Color.White,
                WidthRequest = 40,
                HeightRequest = 40,
                FontSize = 16,
                CornerRadius = 20,
            };
            editButton.Clicked += (s, e) =>
            {
                entryLayout.IsVisible = !entryLayout.IsVisible;
                if (entryLayout.IsVisible)
                {
                    textEntry.Focus();
                }
            };

            var zoomInButton = new Button
            {
                Text = "A+",
                BackgroundColor = Xamarin.Forms.Color.FromHex("#CC333333"),
                TextColor = Xamarin.Forms.Color.White,
                WidthRequest = 40,
                HeightRequest = 40,
                FontSize = 12,
                CornerRadius = 20,
            };
            zoomInButton.Clicked += (s, e) =>
            {
                AdjustFontScale(0.1f);
            };

            var zoomOutButton = new Button
            {
                Text = "A-",
                BackgroundColor = Xamarin.Forms.Color.FromHex("#CC333333"),
                TextColor = Xamarin.Forms.Color.White,
                WidthRequest = 40,
                HeightRequest = 40,
                FontSize = 12,
                CornerRadius = 20,
            };
            zoomOutButton.Clicked += (s, e) =>
            {
                AdjustFontScale(-0.1f);
            };

            var menuButton = new Button
            {
                Text = "☰",
                BackgroundColor = Xamarin.Forms.Color.FromHex("#CC333333"),
                TextColor = Xamarin.Forms.Color.White,
                WidthRequest = 40,
                HeightRequest = 40,
                FontSize = 16,
                CornerRadius = 20,
            };
            menuButton.Clicked += (s, e) =>
            {
                if (flyoutPage != null)
                    flyoutPage.IsPresented = !flyoutPage.IsPresented;
            };

            var toolbar = new StackLayout
            {
                Orientation = StackOrientation.Horizontal,
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.Start,
                Spacing = 6,
                Padding = new Thickness(8, 8, 8, 0),
                Children = { menuButton, editButton, zoomInButton, zoomOutButton },
                InputTransparent = true,
                CascadeInputTransparent = false,
            };

            var canvasGrid = new Grid
            {
                Children =
                {
                    canvasView,
                    toolbar,
                    statusLabel,
                },
            };

            mainLayout = new Grid
            {
                BackgroundColor = Xamarin.Forms.Color.Black,
                RowDefinitions =
                {
                    new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = GridLength.Auto },
                },
                Children =
                {
                    { canvasGrid, 0, 0 },
                    { entryLayout, 0, 1 },
                    { quickScroll, 0, 2 },
                },
            };
        }

        public XFView GetView() => mainLayout;

        internal void SetConsole(EmueraConsole console)
        {
            this.console = console;
        }

        #region SKCanvasView Events

        private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            if (console == null) return;
            var canvas = e.Surface.Canvas;
            MainPicBox.Width = e.Info.Width;
            MainPicBox.Height = e.Info.Height;
            console.OnPaint(canvas);
        }

        private void OnTouch(object sender, SKTouchEventArgs e)
        {
            if (console == null) return;

            var mouseLocation = new Point((int)e.Location.X, (int)e.Location.Y);

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
                ScrollBar.Value = ScrollBar.Maximum;
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
                    textEntry.IsVisible = true;
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
                    textEntry.IsVisible = false;
                }
                catch { }
            });
        }

        public override void SetTextBoxPos(int x, int y, int width = 0)
        {
        }

        public override void ChangeTextBox(string str)
        {
            activity?.RunOnUiThread(() =>
            {
                try { textEntry.Text = str; } catch { }
            });
        }

        #endregion

        #region ScrollBar Helpers

        private void RefreshStrings(bool force)
        {
            if (console == null) return;
            console.RefreshStrings(force);
            Refresh();
        }

        #endregion

        #region Font Scale

        private void AdjustFontScale(float delta)
        {
            var current = RConfig.FontScale;
            var next = Math.Max(0.5f, Math.Min(5.0f, (float)Math.Round(current + delta, 1)));
            if (Math.Abs(next - current) < 0.01f) return;
            PConfigData.Instance.GetItem(ConfigCode.FontScale).SetValue(next);
            RConfig.SetConfig(RConfigData.Instance);
            if (console != null)
            {
                console.RefreshStrings(true);
                Refresh();
            }
        }

        #endregion
    }
}
