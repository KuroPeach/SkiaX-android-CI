using System;
using System.Collections.ObjectModel;
using MinorShift.Emuera;
using XEmuera.Resources;
using Xamarin.Forms;

namespace XEmuera.Droid
{
    internal class AndroidMainMenuPage : ContentPage
    {
        private readonly ObservableCollection<MainMenuItem> menuItems = new ObservableCollection<MainMenuItem>();
        private Switch debugSwitch;

        public event Action RebootRequested;
        public event Action GotoTitleRequested;
        public event Action SettingsRequested;
        public event Action LanguageRequested;
        public event Action ViewLogRequested;
        public event Action SaveLogRequested;
        public event Action DebugModeChanged;

        public bool IsDebugModeEnabled => debugSwitch?.IsToggled ?? false;

        public AndroidMainMenuPage()
        {
            Title = StringsText.Settings;
            IconImageSource = "menu.png";
            Padding = new Thickness(0, 40, 0, 0);

            var listView = new ListView
            {
                ItemsSource = menuItems,
                ItemTemplate = new DataTemplate(() =>
                {
                    var label = new Label
                    {
                        FontSize = 16,
                        TextColor = Color.Black,
                        Margin = new Thickness(20, 10),
                    };
                    label.SetBinding(Label.TextProperty, "Title");
                    return new ViewCell { View = label };
                }),
            };

            listView.ItemSelected += OnItemSelected;

            // 调试模式开关
            debugSwitch = new Switch
            {
                IsToggled = GameUtils.GetPreferences("emuera_debug_mode", "false") == "true",
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.Center,
            };
            debugSwitch.Toggled += (s, e) =>
            {
                GameUtils.SetPreferences("emuera_debug_mode", e.Value ? "true" : "false");
                DebugModeChanged?.Invoke();
            };

            var debugLabel = new Label
            {
                Text = StringsText.Debug,
                FontSize = 16,
                TextColor = Color.Black,
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.StartAndExpand,
                Margin = new Thickness(20, 10),
            };

            var debugLayout = new StackLayout
            {
                Orientation = StackOrientation.Horizontal,
                Children = { debugLabel, debugSwitch },
                Margin = new Thickness(0, 10, 20, 10),
            };

            Content = new StackLayout
            {
                Children = { listView, debugLayout }
            };

            RefreshMenu(isInGame: false);
        }

        public void RefreshMenu(bool isInGame)
        {
            menuItems.Clear();

            if (isInGame)
            {
                menuItems.Add(new MainMenuItem { Title = StringsText.Reboot, Action = "Reboot" });
                menuItems.Add(new MainMenuItem { Title = StringsText.GotoTitle, Action = "GotoTitle" });
            }

            menuItems.Add(new MainMenuItem { Title = StringsText.Settings, Action = "Settings" });
            menuItems.Add(new MainMenuItem { Title = StringsText.Language, Action = "Language" });

            if (isInGame)
            {
                menuItems.Add(new MainMenuItem { Title = StringsText.ViewLog, Action = "ViewLog" });
                menuItems.Add(new MainMenuItem { Title = StringsText.SaveLog, Action = "SaveLog" });
            }
        }

        private void OnItemSelected(object sender, SelectedItemChangedEventArgs e)
        {
            if (!(e.SelectedItem is MainMenuItem item)) return;
            ((ListView)sender).SelectedItem = null;

            switch (item.Action)
            {
                case "Reboot": RebootRequested?.Invoke(); break;
                case "GotoTitle": GotoTitleRequested?.Invoke(); break;
                case "Settings": SettingsRequested?.Invoke(); break;
                case "Language": LanguageRequested?.Invoke(); break;
                case "ViewLog": ViewLogRequested?.Invoke(); break;
                case "SaveLog": SaveLogRequested?.Invoke(); break;
            }
        }

        private class MainMenuItem
        {
            public string Title { get; set; }
            public string Action { get; set; }
        }
    }
}
