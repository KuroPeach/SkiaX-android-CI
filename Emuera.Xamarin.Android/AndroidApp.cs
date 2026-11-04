using System;
using MinorShift.Emuera;
using MinorShift.Emuera.UI.Framework.Forms;
using XEmuera.Resources;
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
            menuPage.BackToGameListRequested += OnBackToGameListRequested;
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
            host.StartGame(gamePath, menuPage.IsDebugModeEnabled);

            var mainWindow = host.MainWindow as AndroidMainWindow;
            mainWindow.SetFlyoutPage(flyoutPage);

            var gameView = mainWindow.GetView();

            // 关键：mainWindow 只创建一次，GetView() 永远返回同一个 mainLayout 实例。
            // 若每次都 new ContentPage 再把同一个 View 塞进去，Xamarin.Forms 会把它
            // 从原父级剥离，导致顶部工具栏图标逐个消失、渲染对象失效
            // （表现为返回列表后重进新游戏出现 object 丢失一类异常）。
            // 因此只在首次创建页面，之后复用。
            if (gamePage == null)
            {
                gamePage = new ContentPage
                {
                    Title = "Emuera",
                    Content = gameView,
                };
                NavigationPage.SetHasNavigationBar(gamePage, false);
            }
            else if (gamePage.Content == null)
            {
                gamePage.Content = gameView;
            }

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

        /// <summary>
        /// 退出当前游戏并回到游戏列表。
        /// 原先游戏内只有「重新加载」「返回标题界面」，想换游戏只能杀应用；
        /// 这里补上真正的回列表动作：结束引擎会话 → 复位导航 → 刷新列表。
        /// </summary>
        private void OnBackToGameListRequested()
        {
            Device.BeginInvokeOnMainThread(async () =>
            {
                // 引擎正在执行脚本时直接拆会崩，先确认
                var console = host?.Console;
                if (console != null && console.IsInProcess)
                {
                    var proceed = await flyoutPage.DisplayAlert(
                        StringsText.BackToGameList,
                        StringsText.BackToGameListConfirm,
                        "OK", "取消");
                    if (!proceed) return;
                }

                flyoutPage.IsPresented = false;

                // 只结束引擎会话，不销毁 gamePage、不剥离 gameView。
                // 引擎的 MainWindow 是单例，剥离后再挂回会造成控件树错乱
                // （表现为返回列表后重进游戏丢失图标、渲染对象失效）。
                host.StopGame();

                // 切回游戏列表页（Detail 换成列表的 NavigationPage）
                flyoutPage.Detail = new NavigationPage(gameListPage);
                menuPage.RefreshMenu(isInGame: false);
                gameListPage.RefreshList();
            });
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
                    await flyoutPage.Detail.DisplayAlert(StringsText.LanguageChanged,
                        StringsText.NeedRestartToApply, "OK");
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
                    // 同时导出 FontDiag.log
                    try
                    {
                        var diagPath = MinorShift.Emuera.Program.ExeDir + "FontDiag.log";
                        using (var writer = new System.IO.StreamWriter(diagPath, false, System.Text.Encoding.UTF8))
                        {
                            XEmuera.Drawing.FontDiag.FlushTo(writer);
                        }
                    }
                    catch { }

                    Device.BeginInvokeOnMainThread(async () =>
                    {
                        await flyoutPage.Detail.DisplayAlert(StringsText.LogSaved,
                        $"Log saved to:\n{fname}", "OK");
                    });
                }
                else
                {
                    Device.BeginInvokeOnMainThread(async () =>
                    {
                        await flyoutPage.Detail.DisplayAlert(StringsText.SaveFailed,
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
