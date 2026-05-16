using System;
using System.Collections.ObjectModel;
using MinorShift.Emuera;
using Xamarin.Forms;

namespace XEmuera.Droid
{
    internal class AndroidMainMenuPage : ContentPage
    {
        private readonly ObservableCollection<MainMenuItem> menuItems = new ObservableCollection<MainMenuItem>();

        public event Action RebootRequested;
        public event Action GotoTitleRequested;
        public event Action SettingsRequested;

        public AndroidMainMenuPage()
        {
            Title = "Menu";
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

            Content = new StackLayout { Children = { listView } };

            RefreshMenu(isInGame: false);
        }

        public void RefreshMenu(bool isInGame)
        {
            menuItems.Clear();

            if (isInGame)
            {
                menuItems.Add(new MainMenuItem { Title = "Reboot", Action = "Reboot" });
                menuItems.Add(new MainMenuItem { Title = "Goto Title", Action = "GotoTitle" });
            }

            menuItems.Add(new MainMenuItem { Title = "Settings", Action = "Settings" });
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
            }
        }

        private class MainMenuItem
        {
            public string Title { get; set; }
            public string Action { get; set; }
        }
    }
}
