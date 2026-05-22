using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Xamarin.Essentials;
using Xamarin.Forms;

[assembly: Dependency(typeof(XEmuera.Droid.DroidPlatformService))]
namespace XEmuera.Droid
{
    internal class DroidPlatformService : IPlatformService
    {
        private const string Tag = "DroidPlatformService";

        public void CloseApplication()
        {
            MainActivity.Instance?.FinishAffinity();
        }

        public void EmueraPageAppearing()
        {
        }

        public void EmueraPageDisappearing()
        {
        }

        public string GetStoragePath()
        {
            return Android.OS.Environment.ExternalStorageDirectory?.AbsolutePath ?? "/storage/emulated/0";
        }

        public void LockScreenOrientation()
        {
            var activity = MainActivity.Instance;
            if (activity == null)
            {
                Log.Debug(Tag, "LockScreenOrientation: activity is null, abort");
                return;
            }

            // Use Android native API instead of Xamarin.Essentials for reliability
            var rotation = activity.WindowManager.DefaultDisplay.Rotation;
            ScreenOrientation orientation;
            switch (rotation)
            {
                case SurfaceOrientation.Rotation0:
                    orientation = ScreenOrientation.Portrait;
                    break;
                case SurfaceOrientation.Rotation90:
                    orientation = ScreenOrientation.Landscape;
                    break;
                case SurfaceOrientation.Rotation180:
                    orientation = ScreenOrientation.ReversePortrait;
                    break;
                case SurfaceOrientation.Rotation270:
                    orientation = ScreenOrientation.ReverseLandscape;
                    break;
                default:
                    orientation = ScreenOrientation.Unspecified;
                    break;
            }
            Log.Debug(Tag, $"LockScreenOrientation: rotation={rotation}, orientation={orientation}");
            activity.RequestedOrientation = orientation;
        }

        public bool NeedManageFilesPermissions()
        {
            return (int)Build.VERSION.SdkInt >= 30 && !Android.OS.Environment.IsExternalStorageManager;
        }

        public bool NeedRebootIfLanguageChanged()
        {
            return (int)Build.VERSION.SdkInt <= 32;
        }

        public bool NeedStoragePermissions()
        {
            return (int)Build.VERSION.SdkInt >= 24;
        }

        public void RequestManageFilesPermissions()
        {
            var intent = new Intent(Android.Provider.Settings.ActionManageAppAllFilesAccessPermission);
            intent.SetData(Android.Net.Uri.Parse("package:" + MainActivity.Instance?.PackageName));
            MainActivity.Instance?.StartActivityForResult(intent, GameUtils.ManageFilesPermissionsRequestCode);
        }

        public void UnlockScreenOrientation()
        {
            var activity = MainActivity.Instance;
            if (activity == null)
            {
                Log.Debug(Tag, "UnlockScreenOrientation: activity is null, abort");
                return;
            }
            Log.Debug(Tag, "UnlockScreenOrientation: setting Sensor");
            activity.RequestedOrientation = ScreenOrientation.Sensor;
        }
    }
}
