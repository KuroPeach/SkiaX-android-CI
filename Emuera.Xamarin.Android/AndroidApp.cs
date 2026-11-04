using System;
using Android.Content;
using Android.OS;
using MinorShift.Emuera;
using MinorShift.Emuera.UI.Framework.Forms;
using XEmuera.Resources;
using Xamarin.Forms;

namespace XEmuera.Droid
{
    internal class AndroidApp : Xamarin.Forms.Application
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
        /// 「返回游戏列表」= 重启整个 App。
        ///
        /// 为什么放弃软清理：
        /// 引擎是单例、游戏与游戏库同进程，外源 dll 是进程级全局加载，
        /// 软件清理卸载不掉已加载的程序集。换游戏时上一局的 dll / 静态状态
        /// 残留，会触发大量「object 丢失」类报错。这些是结构性的，补清理
        /// 永远补不完。
        ///
        /// 最终方案：独立进程看门狗（RestartWatchdogService，android:process=":watchdog"）。
        /// 1. 启动看门狗，让它延迟拉起主界面
        /// 2. 结束主进程；看门狗独立进程仍活着
        /// 3. 看门狗到点 startActivity 拉起全新主进程，随后自尽
        ///
        /// 这样游戏进程彻底消亡，dll、native 堆、静态变量全部释放。
        /// </summary>
        private void OnBackToGameListRequested()
        {
            Device.BeginInvokeOnMainThread(async () =>
            {
                // 引擎正在执行脚本时直接重启可能丢存档，先确认
                var console = host?.Console;
                if (console != null && console.IsInProcess)
                {
                    var proceed = await flyoutPage.DisplayAlert(
                        StringsText.BackToGameList,
                        StringsText.BackToGameListConfirm,
                        "OK", "取消");
                    if (!proceed) return;
                }

                try { host?.SaveLog(); } catch { }

                RestartViaWatchdog();
            });
        }

        /// <summary>
        /// 通过前台服务看门狗重启：先启动看门狗服务（独立进程），
        /// 再结束主进程。看门狗会在主进程死后拉起主界面。
        /// </summary>
        private void RestartViaWatchdog()
        {
            var activity = MainActivity.Instance;
            if (activity == null)
            {
                Android.Util.Log.Warn("EmueraRestart", "activity is null, cannot restart");
                return;
            }

            try
            {
                // 看门狗需要前台服务身份才能在主进程死后拉起 Activity
                var svc = new Intent(activity, typeof(RestartWatchdogService));
                svc.PutExtra(RestartWatchdogService.ExtraDelayMs, 600);

                if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                {
                    activity.StartForegroundService(svc);
                }
                else
                {
                    activity.StartService(svc);
                }
            }
            catch (Exception ex)
            {
                Android.Util.Log.Warn("EmueraRestart", $"start watchdog failed: {ex.Message}");
            }

            // 结束主进程：看门狗独立进程继续跑，会拉起新主进程
            try { activity.FinishAffinity(); } catch { }
            try { Java.Lang.JavaSystem.Exit(0); } catch { }
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
