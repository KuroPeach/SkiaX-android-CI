using System;
using System.IO;
using System.Text;
using MinorShift.Emuera;
using MinorShift.Emuera.Runtime.Utils;
using XEmuera.Resources;
using Xamarin.Forms;

namespace XEmuera.Droid
{
    internal class AndroidLogPage : ContentPage
    {
        private Label logLabel;
        private ScrollView scrollView;

        public AndroidLogPage()
        {
            Title = StringsText.ViewLog;

            logLabel = new Label
            {
                FontSize = Device.GetNamedSize(NamedSize.Micro, typeof(Label)),
                FontFamily = "monospace",
                TextColor = Color.FromRgb(200, 200, 200),
                LineBreakMode = LineBreakMode.CharacterWrap,
            };

            scrollView = new ScrollView
            {
                BackgroundColor = Color.FromRgb(30, 30, 30),
                Content = new StackLayout
                {
                    Padding = new Thickness(10),
                    Children = { logLabel },
                },
            };

            var refreshButton = new Button
            {
                Text = "Refresh",
                HorizontalOptions = LayoutOptions.Fill,
            };
            refreshButton.Clicked += (s, e) => LoadLog();

            var copyButton = new Button
            {
                Text = "Copy to Clipboard",
                HorizontalOptions = LayoutOptions.Fill,
            };
            copyButton.Clicked += OnCopyToClipboard;

            Content = new StackLayout
            {
                Children =
                {
                    new StackLayout
                    {
                        Orientation = StackOrientation.Horizontal,
                        Padding = new Thickness(5),
                        Children = { refreshButton, copyButton },
                    },
                    scrollView,
                }
            };

            LoadLog();
        }

        private void LoadLog()
        {
            try
            {
                var logPath = Program.ExeDir + "emuera.log";
                if (File.Exists(logPath))
                {
                    var encoding = EncodingHandler.DetectEncoding(logPath) ?? Encoding.UTF8;
                    var text = File.ReadAllText(logPath, encoding);
                    logLabel.Text = text ?? "(empty log)";
                }
                else
                {
                    logLabel.Text = $"Log file not found:\n{logPath}";
                }
            }
            catch (Exception ex)
            {
                logLabel.Text = $"Error reading log:\n{ex.Message}";
            }
        }

        private void OnCopyToClipboard(object sender, EventArgs e)
        {
            try
            {
                var text = logLabel.Text ?? "";
                PlatformInterop.SetClipboardText(text);
            }
            catch { }
        }
    }
}
