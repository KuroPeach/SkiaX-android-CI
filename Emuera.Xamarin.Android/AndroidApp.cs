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

        public AndroidApp(AndroidHost host)
        {
            this.host = host;

            gameListPage = new AndroidGameListPage();
            gameListPage.GameSelected += OnGameSelected;

            menuPage = new AndroidMainMenuPage();
            menuPage.RebootRequested += OnRebootRequested;
            menuPage.GotoTitleRequested += OnGotoTitleRequested;
            menuPage.SettingsRequested += OnSettingsRequested;
            menuPage.LanguageRequested += OnLanguageRequested;
            menuPage.ViewLogRequested += OnViewLogRequested;
            menuPage.SaveLogRequested += OnSaveLogRequested;

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

            var gameView = mainWindow.GetView();

            gamePage = new ContentPage
            {
                Title = "Emuera",
                Content = gameView,
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

        private void OnLanguageRequested()
        {
            var page = new AndroidLanguagePage();
            page.LanguageChanged += () =>
            {
                // 语言变更后提示重启
                Device.BeginInvokeOnMainThread(async () =>
                {
                    await flyoutPage.Detail.Navigation.PopAsync();
                    await flyoutPage.Detail.DisplayAlert("Language Changed",
                        "Please restart the app for the language change to take full effect.", "OK");
                });
            };
            flyoutPage.Detail.Navigation.PushAsync(page);
        }

        private void OnViewLogRequested()
        {
            flyoutPage.Detail.Navigation.PushAsync(new AndroidLogPage());
        }

        private void OnSaveLogRequested()
        {
            try
            {
                var console = host?.Console;
                if (console == null) return;

                // 生成带时间戳的文件名（与桌面端一致：yyyyMMdd-HHmmss.log）
                var time = DateTime.Now;
                var fname = time.ToString("yyyyMMdd-HHmmss") + ".log";
                var fullPath = MinorShift.Emuera.Program.ExeDir + fname;

                if (console.OutputSystemLog(fullPath))
                {
                    Device.BeginInvokeOnMainThread(async () =>
                    {
                        await flyoutPage.Detail.DisplayAlert("Log Saved",
                            $"Log saved to:\n{fname}", "OK");
                    });
                }
                else
                {
                    Device.BeginInvokeOnMainThread(async () =>
                    {
                        await flyoutPage.Detail.DisplayAlert("Save Failed",
                            "Failed to save log file.", "OK");
                    });
                }
            }
            catch (Exception ex)
            {
                Device.BeginInvokeOnMainThread(async () =>
                {
                    await flyoutPage.Detail.DisplayAlert("Error",
                        $"Failed to save log:\n{ex.Message}", "OK");
                });
            }
        }

        protected override void OnStart()
        {
        }

        protected override void OnSleep()
        {
            host?.SaveLog();
        }

        protected override void OnResume()
        {
        }
    }
}
