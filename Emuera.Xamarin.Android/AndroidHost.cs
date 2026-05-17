using System;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Widget;
using MinorShift.Emuera;
using MinorShift.Emuera.GameView;
using MinorShift.Emuera.Runtime.Utils;
using MinorShift.Emuera.UI.Framework.Forms;
using XEmuera.Drawing;
using XEmuera.Models;
using SkiaSharp;

namespace XEmuera.Droid
{
    public class AndroidHost
    {
        private EmueraConsole console;
        private AndroidMainWindow mainWindow;
        private bool engineInitialized;

        internal MainWindow MainWindow => mainWindow;

        public void InitEmuera()
        {
            InitPlatformInterop();
            InitAudio();
            InitMainWindow();
        }

        public void StartGame(string gamePath)
        {
            if (engineInitialized) return;

            var context = MainActivity.Instance;
            if (context == null) return;

            Android.Util.Log.Debug("EmueraHost", $"StartGame: path={gamePath}");

            GlobalStatic.PathProvider = new AndroidPathProvider(context, gamePath);
            Android.Util.Log.Debug("EmueraHost", $"PathProvider set: ExeDir={GlobalStatic.PathProvider.ExeDir}");

            DisplayUtils.ScreenDensity = context.Resources.DisplayMetrics.Density;

            GameFolderModel.Instance = new GameFolderModel
            {
                Name = System.IO.Path.GetFileName(gamePath),
                Path = gamePath
            };

            try
            {
                FontModel.Load();
                Android.Util.Log.Debug("EmueraHost", "FontModel.Load() done");
            }
            catch (Exception ex)
            {
                Android.Util.Log.Error("EmueraHost", $"FontModel.Load() failed: {ex}");
            }

            mainWindow.UpdateStatus("Initializing engine...");

            Task.Run(() =>
            {
                try
                {
                    Android.Util.Log.Debug("EmueraHost", "Calling Program.Init()...");
                    mainWindow.UpdateStatus("Program.Init()...");
                    if (!Program.Init())
                    {
                        Android.Util.Log.Error("EmueraHost", "Program.Init() returned false!");
                        mainWindow.UpdateStatus("Init FAILED");
                        return;
                    }
                    Android.Util.Log.Debug("EmueraHost", "Program.Init() done");

                    DrawTextUtils.Reset();
                    DrawBitmapUtils.Reset();

                    Android.Util.Log.Debug("EmueraHost", "Creating EmueraConsole...");
                    mainWindow.UpdateStatus("Creating console...");
                    console = new EmueraConsole(mainWindow);
                    Android.Util.Log.Debug("EmueraHost", "EmueraConsole created");
                    mainWindow.SetConsole(console);
                    Android.Util.Log.Debug("EmueraHost", "SetConsole done");

                    Android.Util.Log.Debug("EmueraHost", "Calling console.Initialize()...");
                    mainWindow.UpdateStatus("Console.Initialize()...");
                    console.Initialize();
                    Android.Util.Log.Debug("EmueraHost", "Initialize done");

                    engineInitialized = true;
                    mainWindow.UpdateStatus("");
                }
                catch (Exception ex)
                {
                    Android.Util.Log.Error("EmueraHost", $"Engine init exception: {ex}");
                    mainWindow.UpdateStatus($"ERROR: {ex.Message}");
                    engineInitialized = false;
                }
            });
        }

        private void InitPlatformInterop()
        {
            PlatformInterop.DoEvents = () => { };

            PlatformInterop.ShowMessage = (msg) =>
            {
                var activity = MainActivity.Instance;
                if (activity == null) return;
                activity.RunOnUiThread(() =>
                {
                    var builder = new AlertDialog.Builder(activity);
                    builder.SetMessage(msg);
                    builder.SetPositiveButton(Android.Resource.String.Ok, (_, __) => { });
                    builder.Show();
                });
            };

            PlatformInterop.ShowMessageWithTitle = (msg, caption) =>
            {
                var activity = MainActivity.Instance;
                if (activity == null) return;
                activity.RunOnUiThread(() =>
                {
                    var builder = new AlertDialog.Builder(activity);
                    builder.SetTitle(caption);
                    builder.SetMessage(msg);
                    builder.SetPositiveButton(Android.Resource.String.Ok, (_, __) => { });
                    builder.Show();
                });
            };

            PlatformInterop.ShowQuestion = (msg, caption) =>
            {
                return false;
            };

            PlatformInterop.GetProductVersion = () =>
            {
                var context = MainActivity.Instance;
                if (context == null) return "0.0.0.0";
                try
                {
                    var pkgInfo = context.PackageManager.GetPackageInfo(context.PackageName, 0);
                    return pkgInfo?.VersionName ?? "0.0.0.0";
                }
                catch
                {
                    return "0.0.0.0";
                }
            };

            PlatformInterop.GetMousePosition = () => Point.Empty;

            PlatformInterop.SetClipboardText = (text) =>
            {
                var context = MainActivity.Instance;
                if (context == null) return;
                try
                {
                    var clipboard = (ClipboardManager)context.GetSystemService(Context.ClipboardService);
                    clipboard.PrimaryClip = ClipData.NewPlainText("Emuera", text);
                }
                catch { }
            };

            PlatformInterop.MeasureTextWidth = (text, fontName, fontSize, fontStyle) =>
            {
                var weight = fontStyle.HasFlag(XEmuera.Drawing.FontStyle.Bold) ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal;
                var slant = fontStyle.HasFlag(XEmuera.Drawing.FontStyle.Italic) ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;
                var style = new SKFontStyle(weight, SKFontStyleWidth.Normal, slant);
                using var typeface = SKTypeface.FromFamilyName(fontName, style) ?? SKTypeface.Default;
                using var paint = new SKPaint { Typeface = typeface, TextSize = fontSize, IsStroke = false };
                return paint.MeasureText(text);
            };

            PlatformInterop.MeasureText = (text, fontName, fontSize, fontStyle) =>
            {
                var weight = fontStyle.HasFlag(XEmuera.Drawing.FontStyle.Bold) ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal;
                var slant = fontStyle.HasFlag(XEmuera.Drawing.FontStyle.Italic) ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;
                var style = new SKFontStyle(weight, SKFontStyleWidth.Normal, slant);
                using var typeface = SKTypeface.FromFamilyName(fontName, style) ?? SKTypeface.Default;
                using var paint = new SKPaint { Typeface = typeface, TextSize = fontSize, IsStroke = false };
                var bounds = new SKRect();
                paint.MeasureText(text, ref bounds);
                return (bounds.Width, bounds.Height);
            };
        }

        private void InitAudio()
        {
            GlobalStatic.Sound = new Sound[16];
            for (int i = 0; i < GlobalStatic.Sound.Length; i++)
                GlobalStatic.Sound[i] = new AndroidSound();
            GlobalStatic.Bgm = new AndroidSound();
        }

        private void InitMainWindow()
        {
            var context = MainActivity.Instance;
            if (context == null) return;
            mainWindow = new AndroidMainWindow(context);
        }
    }
}
