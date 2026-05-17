using System;
using MinorShift.Emuera;
using MinorShift.Emuera.UI.Framework.Forms;
using Xamarin.Forms;

namespace XEmuera.Droid
{
    internal class AndroidApp : Application
    {
        private AndroidHost host;
        private FlyoutPage flyoutPage;
        private AndroidGameListPage gameListPage;
        private AndroidMainMenuPage menuPage;
        private ContentPage gamePage;
        private Button floatingMenuButton;

        public AndroidApp(AndroidHost host)
        {
            this.host = host;

            gameListPage = new AndroidGameListPage();
            gameListPage.GameSelected += OnGameSelected;

            menuPage = new AndroidMainMenuPage();
            menuPage.RebootRequested += OnRebootRequested;
            menuPage.GotoTitleRequested += OnGotoTitleRequested;
            menuPage.SettingsRequested += OnSettingsRequested;

            flyoutPage = new FlyoutPage
            {
                Flyout = menuPage,
                Detail = new NavigationPage(gameListPage),
                FlyoutLayoutBehavior = FlyoutLayoutBehavior.Popover,
                IsPresented = false,
            };

            MainPage = flyoutPage;

            gameListPage.RefreshList();
        }

        private void OnGameSelected(string gamePath)
        {
            host.StartGame(gamePath);

            var mainWindow = host.MainWindow as AndroidMainWindow;
            mainWindow.SetFlyoutPage(flyoutPage);

            floatingMenuButton = new Button
            {
                Text = "☰",
                FontSize = 20,
                WidthRequest = 48,
                HeightRequest = 48,
                CornerRadius = 24,
                BackgroundColor = Color.FromRgba(0, 0, 0, 128),
                TextColor = Color.White,
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.Start,
                Margin = new Thickness(0, 8, 8, 0),
            };
            floatingMenuButton.Clicked += (s, e) =>
            {
                flyoutPage.IsPresented = !flyoutPage.IsPresented;
            };

            var gameView = mainWindow.GetView();

            var overlay = new Grid
            {
                Children =
                {
                    gameView,
                    floatingMenuButton,
                },
            };

            gamePage = new ContentPage
            {
                Title = "Emuera",
                Content = overlay,
            };
            NavigationPage.SetHasNavigationBar(gamePage, false);

            flyoutPage.Detail = new NavigationPage(gamePage);
            flyoutPage.IsPresented = false;

            menuPage.RefreshMenu(isInGame: true);
        }

        private void OnRebootRequested()
        {
            GlobalStatic.MainWindow?.MainMenu_Reboot();
        }

        private void OnGotoTitleRequested()
        {
            GlobalStatic.MainWindow?.MainMenu_GotoTitle();
        }

        private void OnSettingsRequested()
        {
            flyoutPage.Detail.Navigation.PushAsync(new AndroidConfigPage());
        }

        protected override void OnStart()
        {
        }

        protected override void OnSleep()
        {
        }

        protected override void OnResume()
        {
        }
    }
}
