using System.Windows.Forms;

static class Dialog
{
	public static void Show(string text)
	{
		MessageBox.Show(text);
	}
	public static void Show(string title, string text)
	{
		MessageBox.Show(text, title, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);
	}
	public static bool ShowPrompt(string title, string text)
	{
		var result = MessageBox.Show(text, title, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1);
		return result == DialogResult.Yes;
	}
}
