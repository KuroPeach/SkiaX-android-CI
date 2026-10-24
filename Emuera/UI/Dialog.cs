using MinorShift.Emuera;

/// <summary>
/// 模态提示统一入口。2026-10-24（eraWire B2-3）起全部改走 PlatformInterop 委托：
/// GUI 模式＝MessageBox（行为不变）；EWP/--CliDebug 模式＝PlatformInterop 已被
/// Program 换成事件转发（dialog.request），无人值守不再被原生弹窗阻塞——
/// 此前「不適正なフォント」「無限ループ警告」等 Dialog.Show 直弹 MessageBox，
/// 会刷在值守者屏幕上并卡死自动化测试（pixtest 实锤）。
/// </summary>
static class Dialog
{
	public enum Result
	{
		Yes,
		No
	}
	public static void Show(string text)
	{
		PlatformInterop.ShowMessage(text);
	}
	public static void Show(string title, string text)
	{
		PlatformInterop.ShowMessageWithTitle(text, title);
	}
	public static bool ShowPrompt(string title, string text)
	{
		return PlatformInterop.ShowQuestion(text, title);
	}
}
