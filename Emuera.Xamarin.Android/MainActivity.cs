using System;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Provider;
using Android.Views;
using Android.Runtime;
using Android.Widget;

namespace XEmuera.Droid
{
    [Activity(
        Label = "Emuera skiaX",
        Icon = "@mipmap/icon",
        Theme = "@style/MainTheme",
        MainLauncher = true,
        ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize,
        ScreenOrientation = ScreenOrientation.Sensor,
        // 保持 AdjustNothing：改成 AdjustResize 后会让整个布局被压缩，
        // 实测导致顶部图标消失、键盘弹出白屏。改为只上移输入栏本身。
        WindowSoftInputMode = SoftInput.AdjustNothing)]
    public class MainActivity : global::Xamarin.Forms.Platform.Android.FormsAppCompatActivity
    {
        public static Activity Instance { get; private set; }

        private static bool Init;
        private static int UIOptions;
        private static int EmueraUIOptions;

        internal const int ManageStorageRequestCode = 9101;

        internal static bool HasStoragePermission
        {
            get
            {
                if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
                    return Android.OS.Environment.IsExternalStorageManager;
                if (Instance == null) return false;
                return Instance.CheckSelfPermission(Android.Manifest.Permission.ReadExternalStorage) == Permission.Granted
                    && Instance.CheckSelfPermission(Android.Manifest.Permission.WriteExternalStorage) == Permission.Granted;
            }
        }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            Instance = this;

            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            global::Xamarin.Forms.Forms.Init(this, savedInstanceState);

            // 注册编码 Provider，确保 Config 类静态初始化时 SHIFT-JIS 可用
            // 必须在任何访问 MinorShift.Emuera.Config 的代码之前执行（包括游戏列表页面的语言选项）
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            if (!Init)
            {
                Init = true;

                UIOptions = (int)Window.DecorView.SystemUiVisibility;
                EmueraUIOptions = UIOptions
                    | (int)SystemUiFlags.HideNavigation
                    | (int)SystemUiFlags.LayoutHideNavigation
                    | (int)SystemUiFlags.Fullscreen
                    | (int)SystemUiFlags.LayoutFullscreen
                    | (int)SystemUiFlags.LayoutStable
                    | (int)SystemUiFlags.ImmersiveSticky;
            }

            RequestStoragePermission();

            var host = new AndroidHost();
            host.InitEmuera();

            LoadApplication(new AndroidApp(host));
        }

        internal void RequestStoragePermission()
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
            {
                if (!Android.OS.Environment.IsExternalStorageManager)
                {
                    try
                    {
                        var intent = new Intent(Settings.ActionManageAppAllFilesAccessPermission);
                        intent.SetData(Android.Net.Uri.Parse("package:" + PackageName));
                        intent.AddFlags(ActivityFlags.NewTask);
                        StartActivityForResult(intent, ManageStorageRequestCode);
                    }
                    catch (Exception ex)
                    {
                        Toast.MakeText(this, "Failed to request storage permission: " + ex.Message, ToastLength.Long).Show();
                    }
                }
            }
            else
            {
                if (CheckSelfPermission(Android.Manifest.Permission.ReadExternalStorage) != Permission.Granted
                    || CheckSelfPermission(Android.Manifest.Permission.WriteExternalStorage) != Permission.Granted)
                {
                    RequestPermissions(new string[]
                    {
                        Android.Manifest.Permission.ReadExternalStorage,
                        Android.Manifest.Permission.WriteExternalStorage
                    }, 0);
                }
            }
        }

        protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
        {
            base.OnActivityResult(requestCode, resultCode, data);

            if (requestCode == ManageStorageRequestCode)
            {
                var granted = Android.OS.Environment.IsExternalStorageManager;
                if (!granted)
                {
                    Toast.MakeText(this, "Storage permission is required to access game files", ToastLength.Long).Show();
                }
            }
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }

        public override void OnWindowFocusChanged(bool hasFocus)
        {
            base.OnWindowFocusChanged(hasFocus);

            if (!hasFocus)
                return;

            Window.DecorView.SystemUiVisibility = (StatusBarVisibility)EmueraUIOptions;
        }
    }
}
