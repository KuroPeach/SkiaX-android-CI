namespace System.Windows.Forms
{
	public static class Application
	{
		public static string ProductVersion => "1.0.0.0";
		public static void DoEvents() { }
	}

	public static class MessageBox
	{
		public static void Show(string text) { }
		public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton)
		{
			return DialogResult.None;
		}
	}

	public enum DialogResult
	{
		None,
		OK,
		Cancel,
		Abort,
		Retry,
		Ignore,
		Yes,
		No
	}

	public enum MessageBoxButtons
	{
		OK,
		OKCancel,
		YesNo,
		YesNoCancel
	}

	public enum MessageBoxIcon
	{
		None,
		Hand,
		Question,
		Exclamation,
		Asterisk
	}

	public enum MessageBoxDefaultButton
	{
		Button1,
		Button2,
		Button3
	}

	public static class Clipboard
	{
		public static void SetText(string text) { }
		public static bool ContainsText() => false;
		public static string GetText() => "";
	}
}
