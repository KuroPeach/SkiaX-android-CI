using System;
using Android.App;
using Android.Content;
using Android.OS;

namespace XEmuera.Droid
{
    /// <summary>
    /// 独立进程看门狗：主进程「返回游戏列表」时先排一个定时重启，
    /// 然后主进程结束自己。本 Service 运行在 android:process=":watchdog" 的
    /// 独立进程里，主进程死后仍活着，到点后拉起主界面，再自尽。
    ///
    /// 为什么需要它：SkiaX 的引擎是单例、与游戏库同进程，外源 dll 是进程级
    /// 全局加载，软清理永远卸载不掉已加载的程序集，换游戏就会残留脏数据。
    /// 唯一根治办法是让游戏进程整个重启。但 Android 12+（targetSdk 34）禁止
    /// 后台进程通过 AlarmManager / 普通广播拉起 Activity，所以用一个短暂的
    /// 前台服务做「一次性重启代理」。
    /// </summary>
    // 注意：本服务已在 Properties/AndroidManifest.xml 里手工声明
    // （<service android:name="XEmuera.Droid.RestartWatchdogService"
    //   android:process=":watchdog" android:foregroundServiceType="specialUse" …/>）。
    // 因此这里不再使用 [Service] 特性，避免两处声明冲突。
    public class RestartWatchdogService : Service
    {
        // 与 MainActivity 约定的动作名、广播 action
        internal const string ActionRestart = "com.skiax.watchdog.RESTART";
        internal const string ExtraDelayMs = "delay_ms";

        private const string ChannelId = "restart_watchdog";
        private const int NotificationId = 7701;
        private const int DefaultDelayMs = 600;

        private Android.OS.Handler handler;
        private Runnable pendingRestart;

        public override void OnCreate()
        {
            base.OnCreate();

            try
            {
                CreateNotificationChannel();

                var notification = BuildNotification(
                    "SkiaX 正在重启", "稍候…");

                // Android 8+ 必须在 OnCreate 后尽快进入前台，否则会抛异常
                StartForeground(NotificationId, notification);
            }
            catch (Exception ex)
            {
                Android.Util.Log.Warn("Watchdog", $"start foreground failed: {ex.Message}");
            }
        }

        public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
        {
            try
            {
                int delayMs = DefaultDelayMs;
                if (intent != null && intent.HasExtra(ExtraDelayMs))
                    delayMs = intent.GetIntExtra(ExtraDelayMs, DefaultDelayMs);

                // 到点拉起主界面
                var restart = new Intent(this, typeof(MainActivity))
                    .AddFlags(ActivityFlags.NewTask | ActivityFlags.ClearTask);

                handler = handler ?? new Android.OS.Handler(Looper.MainLooper);
                pendingRestart = new Runnable(() =>
                {
                    try
                    {
                        StartActivity(restart);
                    }
                    catch (Exception ex)
                    {
                        Android.Util.Log.Error("Watchdog", $"restart failed: {ex.Message}");
                    }

                    StopSelfAndGoAway();
                });

                handler.PostDelayed(pendingRestart, delayMs);
            }
            catch (Exception ex)
            {
                Android.Util.Log.Warn("Watchdog", $"schedule failed: {ex.Message}");
                StopSelfAndGoAway();
            }

            return StartCommandResult.NotSticky;
        }

        public override IBinder OnBind(Intent intent) => null;

        public override void OnDestroy()
        {
            try
            {
                if (handler != null && pendingRestart != null)
                    handler.RemoveCallbacks(pendingRestart);
            }
            catch { }
            base.OnDestroy();
        }

        private void StopSelfAndGoAway()
        {
            try
            {
                StopForeground(StopForegroundFlags.Remove);
            }
            catch { }
            try { StopSelf(); } catch { }
        }

        /// <summary>
        /// 前式通知。前台服务的强制要求，通知内容尽量简短低调。
        /// </summary>
        private Notification BuildNotification(string title, string text)
        {
            var builder = new Notification.Builder(this)
                .SetContentTitle(title)
                .SetContentText(text)
                .SetSmallIcon(Android.Resource.Drawable.IcDialogInfo) // 避免依赖本包 icon 设计器字段
                .SetOngoing(true);

            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                builder = builder.SetChannelId(ChannelId);

            return builder.Build();
        }

        private void CreateNotificationChannel()
        {
            if (Build.VERSION.SdkInt < BuildVersionCodes.O)
                return;

            var manager = (NotificationManager)GetSystemService(NotificationService);
            var channel = new NotificationChannel(
                ChannelId, "重启看门狗", NotificationImportance.Min);
            manager?.CreateNotificationChannel(channel);
        }
    }

    /// <summary>避免在 PostDelayed 里写 lambda 闭包里的 StopSelf 时序问题。</summary>
    internal sealed class Runnable : Java.Lang.Object, Java.Lang.IRunnable
    {
        private readonly Action action;
        public Runnable(Action action) { this.action = action; }
        public void Run() => action?.Invoke();
    }
}