using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Graphics;
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

            CopyExternalConfigIfNeeded(context, gamePath);

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
                    var debugLogPath = Android.App.Application.Context.FilesDir.Path + "/render_debug.log";
                    try { System.IO.File.Delete(debugLogPath); } catch { }
                    EmueraConsole.SetDebugLogPath(debugLogPath);
                    console = new EmueraConsole(mainWindow);
                    Android.Util.Log.Debug("EmueraHost", "EmueraConsole created");

                    // SetConsole modifies UI elements (RefreshVirtualControllerState), must run on UI thread
                    var activity = MainActivity.Instance;
                    if (activity != null)
                    {
                        var setConsoleEvent = new System.Threading.ManualResetEventSlim(false);
                        Exception setConsoleEx = null;
                        activity.RunOnUiThread(() =>
                        {
                            try
                            {
                                mainWindow.SetConsole(console);
                                Android.Util.Log.Debug("EmueraHost", "SetConsole done (UI thread)");
                            }
                            catch (Exception ex)
                            {
                                setConsoleEx = ex;
                                Android.Util.Log.Error("EmueraHost", $"SetConsole failed: {ex.Message}");
                            }
                            finally
                            {
                                setConsoleEvent.Set();
                            }
                        });
                        setConsoleEvent.Wait();
                        if (setConsoleEx != null)
                            throw setConsoleEx;
                    }
                    else
                    {
                        mainWindow.SetConsole(console);
                        Android.Util.Log.Debug("EmueraHost", "SetConsole done (fallback)");
                    }

                    Android.Util.Log.Debug("EmueraHost", "Calling console.Initialize()...");
                    mainWindow.UpdateStatus("Console.Initialize()...");
                    console.Initialize();
                    Android.Util.Log.Debug("EmueraHost", "Initialize done");

                    engineInitialized = true;
                    mainWindow.UpdateStatus("");
                }
                catch (Exception ex)
                {
                    Android.Util.Log.Error("EmueraHost", $"Engine init exception: {ex.GetType().Name}: {ex.Message}");
                    Android.Util.Log.Error("EmueraHost", $"Stack: {ex.StackTrace}");
                    if (ex.InnerException != null)
                        Android.Util.Log.Error("EmueraHost", $"Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
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
                var resetEvent = new System.Threading.ManualResetEvent(false);
                bool result = false;
                var activity = MainActivity.Instance;
                if (activity == null) return false;
                activity.RunOnUiThread(() =>
                {
                    var builder = new AlertDialog.Builder(activity);
                    builder.SetTitle(caption);
                    builder.SetMessage(msg);
                    builder.SetPositiveButton("Yes", (_, __) =>
                    {
                        result = true;
                        resetEvent.Set();
                    });
                    builder.SetNegativeButton("No", (_, __) =>
                    {
                        result = false;
                        resetEvent.Set();
                    });
                    builder.SetCancelable(false);
                    builder.Show();
                });
                resetEvent.WaitOne();
                return result;
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

            PlatformInterop.GetMousePosition = () => XEmuera.Drawing.Point.Empty;

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

            // 图片解码回退：Android BitmapFactory 原生支持 WebP（API 14+）
            PlatformInterop.DecodeBitmapFallback = (filepath) =>
            {
                try
                {
                    var options = new BitmapFactory.Options { InJustDecodeBounds = false };
                    var androidBmp = BitmapFactory.DecodeFile(filepath, options);
                    if (androidBmp == null) return null;

                    var skBmp = AndroidBitmapToSKBitmap(androidBmp);
                    androidBmp.Recycle();
                    androidBmp.Dispose();
                    return skBmp;
                }
                catch { return null; }
            };

            PlatformInterop.GetBitmapInfoFallback = (filepath) =>
            {
                try
                {
                    var options = new BitmapFactory.Options { InJustDecodeBounds = true };
                    BitmapFactory.DecodeFile(filepath, options);
                    if (options.OutWidth > 0 && options.OutHeight > 0)
                        return (options.OutWidth, options.OutHeight);
                }
                catch { }
                return null;
            };

            PlatformInterop.GetAnimInfoFallback = (filepath) =>
            {
                try
                {
                    // Android BitmapFactory 不支持动画帧信息，仅返回静态尺寸
                    var options = new BitmapFactory.Options { InJustDecodeBounds = true };
                    BitmapFactory.DecodeFile(filepath, options);
                    if (options.OutWidth > 0 && options.OutHeight > 0)
                    {
                        // 无法获取帧数，返回 null 表示不支持动画
                        return null;
                    }
                }
                catch { }
                return null;
            };

            PlatformInterop.DecodeAnimatedFallback = (filepath) =>
            {
                // Android BitmapFactory 不支持动画帧解码
                // 静态 WebP 会由 DecodeBitmapFallback 处理
                return null;
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
            GameUtils.MainPicBox = new EraPictureBox();
        }

        private void CopyExternalConfigIfNeeded(Context context, string gamePath)
        {
            if (string.IsNullOrEmpty(gamePath)) return;

            var sep = System.IO.Path.DirectorySeparatorChar;
            var gameConfigPath = gamePath.TrimEnd(sep) + sep + "emuera.config";
            var internalConfigPath = System.IO.Path.Combine(context.FilesDir.AbsolutePath, "emuera.config");

            if (System.IO.File.Exists(gameConfigPath))
                return;

            if (!System.IO.File.Exists(internalConfigPath))
                return;

            try
            {
                System.IO.File.Copy(internalConfigPath, gameConfigPath, false);
                Android.Util.Log.Info("EmueraHost", $"Copied emuera.config from {internalConfigPath} to {gameConfigPath}");
            }
            catch (Exception ex)
            {
                Android.Util.Log.Warn("EmueraHost", $"Failed to copy emuera.config: {ex.Message}");
            }
        }

        public void SaveLog()
        {
            if (console == null) return;
            try
            {
                console.OutputSystemLog(MinorShift.Emuera.Program.ExeDir + "emuera.log");
            }
            catch (Exception ex)
            {
                Android.Util.Log.Warn("EmueraHost", $"Failed to save log: {ex.Message}");
            }
        }

        /// <summary>
        /// 将 Android.Graphics.Bitmap 转换为 SkiaSharp SKBitmap
        /// 通过 PNG 中间格式确保像素格式正确
        /// </summary>
        private static SKBitmap AndroidBitmapToSKBitmap(Bitmap androidBmp)
        {
            using var ms = new MemoryStream();
            androidBmp.Compress(Bitmap.CompressFormat.Png, 100, ms);
            ms.Position = 0;
            return SKBitmap.Decode(ms);
        }
    }
}
