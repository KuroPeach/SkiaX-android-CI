using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Runtime;

namespace XEmuera.Droid
{
    [Activity(
        Label = "Emuera",
        Icon = "@mipmap/icon",
        Theme = "@style/MainTheme",
        MainLauncher = true,
        ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize,
        ScreenOrientation = ScreenOrientation.Sensor)]
    public class MainActivity : global::Xamarin.Forms.Platform.Android.FormsAppCompatActivity
    {
        public static Activity Instance { get; private set; }

        private static bool Init;
        private static int UIOptions;
        private static int EmueraUIOptions;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            Instance = this;

            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            global::Xamarin.Forms.Forms.Init(this, savedInstanceState);

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

            var host = new AndroidHost();
            host.InitEmuera();

            LoadApplication(new AndroidApp(host));
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
