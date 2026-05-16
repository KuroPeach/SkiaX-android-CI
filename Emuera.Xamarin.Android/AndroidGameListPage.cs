using System;
using System.Collections.Generic;
using MinorShift.Emuera;
using Xamarin.Forms;

namespace XEmuera.Droid
{
    internal class AndroidGameListPage : ContentPage
    {
        private ListView listView;

        public event Action<string> GameSelected;

        public AndroidGameListPage()
        {
            Title = "Emuera";

            listView = new ListView(ListViewCachingStrategy.RecycleElement)
            {
                HasUnevenRows = true,
                IsPullToRefreshEnabled = true,
                ItemsSource = AndroidGameItemModel.AllModels,
                ItemTemplate = new DataTemplate(() =>
                {
                    var nameLabel = new Label { FontSize = 16, TextColor = Color.HotPink };
                    nameLabel.SetBinding(Label.TextProperty, "Name");

                    var errorLabel = new Label { FontSize = 12, TextColor = Color.PaleVioletRed };
                    errorLabel.SetBinding(Label.TextProperty, "Error");
                    errorLabel.SetBinding(Label.IsVisibleProperty, "HasError");

                    var pathLabel = new Label { FontSize = 12, TextColor = Color.Gray };
                    pathLabel.SetBinding(Label.TextProperty, "Path");

                    var nameRow = new StackLayout
                    {
                        Orientation = StackOrientation.Horizontal,
                        Children = { nameLabel, errorLabel }
                    };

                    return new ViewCell
                    {
                        View = new StackLayout
                        {
                            Padding = new Thickness(10, 5),
                            Children = { nameRow, pathLabel }
                        }
                    };
                }),
            };

            listView.Refreshing += OnRefreshing;
            listView.ItemSelected += OnItemSelected;

            Content = new StackLayout
            {
                Children = { listView }
            };
        }

        private void OnRefreshing(object sender, EventArgs e)
        {
            var basePath = GlobalStatic.PathProvider?.ExeDir;
            if (basePath != null)
                AndroidGameItemModel.Load(basePath);
            listView.EndRefresh();
        }

        private void OnItemSelected(object sender, SelectedItemChangedEventArgs e)
        {
            if (e.SelectedItem is AndroidGameItemModel item)
            {
                listView.SelectedItem = null;
                GameSelected?.Invoke(item.Path);
            }
        }

        public void RefreshList()
        {
            var basePath = GlobalStatic.PathProvider?.ExeDir;
            if (basePath != null)
                AndroidGameItemModel.Load(basePath);
        }
    }
}
