using System;
using System.Collections.Generic;
using SkiaSharp;

namespace MinorShift.Emuera
{
	public static class PlatformInterop
	{
		public static Action DoEvents { get; set; } = () => { };

		public static Action<string> ShowMessage { get; set; } = _ => { };

		public static Action<string, string> ShowMessageWithTitle { get; set; } = (_, __) => { };

		public static Func<string, string, bool> ShowQuestion { get; set; } = (_, __) => false;

		public static Func<string, string, float, FontStyle, float> MeasureTextWidth { get; set; } = (_, __, ___, ____) => 0f;

		public static Func<string, string, float, FontStyle, (float Width, float Height)> MeasureText { get; set; } = (_, __, ___, ____) => (0f, 0f);

		public static Action<string> SetClipboardText { get; set; } = _ => { };

		public static Func<string> GetProductVersion { get; set; } = () => "0.0.0.0";

		public static Func<Point> GetMousePosition { get; set; } = () => Point.Empty;

		/// <summary>
		/// 图片解码回退：当 SkiaSharp SKBitmap.Decode 返回 null 时调用
		/// 返回 null 表示该平台无回退能力
		/// </summary>
		public static Func<string, SKBitmap> DecodeBitmapFallback { get; set; } = _ => null;

		/// <summary>
		/// 图片信息回退：当 SKCodec.Create 返回 null 时调用
		/// 返回 null 表示该平台无回退能力
		/// </summary>
		public static Func<string, (int Width, int Height)?> GetBitmapInfoFallback { get; set; } = _ => null;

		/// <summary>
		/// 动画解码回退：当 SkiaSharp SKCodec 无法解码动画时调用
		/// 返回 null 表示该平台无回退能力
		/// </summary>
		public static Func<string, List<(SKBitmap Bitmap, int DelayMs)>> DecodeAnimatedFallback { get; set; } = _ => null;

		/// <summary>
		/// 动画信息回退：当 SKCodec 无法获取动画帧信息时调用
		/// 返回 null 表示该平台无回退能力
		/// </summary>
		public static Func<string, (int Width, int Height, int FrameCount, int[] Delays)?> GetAnimInfoFallback { get; set; } = _ => null;

		/// <summary>
		/// 诊断日志输出：Android 端用 Android.Util.Log.Debug，桌面端用 Debug.WriteLine
		/// </summary>
		public static Action<string> DebugLog { get; set; } = msg => System.Diagnostics.Debug.WriteLine(msg);
	}
}
