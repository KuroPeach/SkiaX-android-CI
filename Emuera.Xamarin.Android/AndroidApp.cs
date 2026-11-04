using System;
using Android.Content;
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
        /// 「返回游戏列表」——直接重启整个 App 进程。
        ///
        /// 引擎是单例设计，状态散落在 GlobalStatic、资源缓存、音频对象、
        /// Xamarin 控件树等多处。软清理 StopGame 要逐层复位，漏一处就会在
        /// 下一局表现为 object 丢失 / 图标消失 / STOPBGM 空引用。
        /// 重启进程能一次性清空全部残留，代价仅几百毫秒。
        ///
        /// 用 AlarmManager 延迟拉起自身再退出，不用 Process.KillProcess
        /// （前台被杀会被系统当成崩溃并弹「应用已停止」）。
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

                RestartApp();
            });
        }

        /// <summary>
        /// 重启本应用：排一个延迟 Intent 拉起启动 Activity，
        /// 再结束当前 Activity 并退出进程。
        /// </summary>
        private void RestartApp()
        {
            var activity = MainActivity.Instance;
            if (activity == null)
            {
                Android.Util.Log.Warn("EmueraRestart", "activity is null, cannot restart");
                return;
            }

            try
            {
                var launchIntent = activity.PackageManager.GetLaunchIntentForPackage(activity.PackageName);
                if (launchIntent == null || launchIntent.Component == null)
                {
                    Android.Util.Log.Warn("EmueraRestart", "no launch intent, fallback to Finish");
                    activity.Finish();
                    return;
                }
                launchIntent.AddFlags(Android.Content.ActivityFlags.NewTask | Android.Content.ActivityFlags.ClearTask);
                activity.StartActivity(launchIntent);
            }
            catch (Exception ex)
            {
                Android.Util.Log.Warn("EmueraRestart", string.Concat("restart failed: ", ex.Message));
            }

            // 先让新实例有机会启动，再结束自己并退出进程。
            // 若立即 Exit，可能来不及拉起新实例，导致「返回后没反应」。
            var handler = new Android.OS.Handler(Android.OS.Looper.MainLooper);
            handler.PostDelayed(() =>
            {
                try { activity.Finish(); } catch { }

                // 退出进程，确保单例状态 / 原生资源 / 控件树全部释放
                Java.Lang.JavaSystem.Exit(0);
            }, 500);
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
