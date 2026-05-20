using System;
using System.Collections.Generic;
using System.IO;
using MinorShift.Emuera;
using XEmuera.Resources;
using Xamarin.Forms;

namespace XEmuera.Droid
{
    internal class AndroidGameListPage : ContentPage
    {
        private ListView listView;
        private Label statusLabel;
        private bool permissionsChecked;

        public event Action<string> GameSelected;

        public AndroidGameListPage()
        {
            Title = StringsText.GameList;

            statusLabel = new Label
            {
                FontSize = 14,
                TextColor = Color.Gray,
                HorizontalOptions = LayoutOptions.Center,
                Margin = new Thickness(10, 5),
            };

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
                Children = { statusLabel, listView }
            };
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            if (!permissionsChecked)
            {
                permissionsChecked = true;
                EnsurePermissionsAndRefresh();
            }
        }

        private void EnsurePermissionsAndRefresh()
        {
            if (MainActivity.HasStoragePermission)
            {
                statusLabel.Text = StringsText.ScanningGames;
                RefreshList();
            }
            else
            {
                statusLabel.Text = StringsText.StoragePermissionRequired;
                (MainActivity.Instance as MainActivity)?.RequestStoragePermission();
            }
        }

        private void OnRefreshing(object sender, EventArgs e)
        {
            if (MainActivity.HasStoragePermission)
            {
                RefreshList();
            }
            else
            {
                statusLabel.Text = StringsText.StoragePermissionRequired;
                (MainActivity.Instance as MainActivity)?.RequestStoragePermission();
            }
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
            AndroidGameItemModel.LoadAll();
            if (AndroidGameItemModel.AllModels.Count == 0)
                statusLabel.Text = StringsText.NoGamesFound;
            else
                statusLabel.Text = $"Found {AndroidGameItemModel.AllModels.Count} game(s)";
        }
    }
}
