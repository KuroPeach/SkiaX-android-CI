using System;

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
	}
}
