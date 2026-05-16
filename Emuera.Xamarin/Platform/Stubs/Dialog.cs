using MinorShift.Emuera;

static class Dialog
{
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
