using MinorShift.Emuera.Runtime.Script.Parser;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Essentials;

namespace XEmuera.Forms
{
	public static class MessageBox
	{
		public static void Show(string message, string title = "")
		{
			if (MainThread.IsMainThread)
			{
				MainThread.BeginInvokeOnMainThread(async () =>
				{
					await DisplayAlert(title, message, MessageBoxButtons.OK);
				});
			}
			else
			{
				var task = MainThread.InvokeOnMainThreadAsync(async () =>
				{
					await DisplayAlert(title, message, MessageBoxButtons.OK);
				});
				task.Wait();
			}
		}

		public static DialogResult Show(string message, string title, MessageBoxButtons messageBoxButtons)
		{
			if (MainThread.IsMainThread)
				throw new CodeEE("在主线程上调用了DisplayAlert并等待。");

			var task = MainThread.InvokeOnMainThreadAsync(async () =>
			{
				return GetDialogResult(messageBoxButtons, await DisplayAlert(title, message, messageBoxButtons));
			});
			task.Wait();
			return task.Result;
		}

		public static void ShowOnMainThread(string message, string title, Action<bool> action, MessageBoxButtons messageBoxButtons)
		{
			MainThread.BeginInvokeOnMainThread(async () =>
			{
				action?.Invoke(await DisplayAlert(title, message, messageBoxButtons));
			});
		}

		private static async Task<bool> DisplayAlert(string title, string message, MessageBoxButtons messageBoxButtons)
		{
			switch (messageBoxButtons)
			{
				case MessageBoxButtons.OKCancel:
					return true;
				case MessageBoxButtons.YesNo:
					return true;
				default:
					return false;
			}
		}

		private static DialogResult GetDialogResult(MessageBoxButtons messageBoxButtons, bool result)
		{
			switch (messageBoxButtons)
			{
				case MessageBoxButtons.OKCancel:
					return result ? DialogResult.OK : DialogResult.Cancel;
				case MessageBoxButtons.YesNo:
					return result ? DialogResult.Yes : DialogResult.No;
				default:
					return DialogResult.None;
			}
		}
	}

	public enum MessageBoxButtons
	{
		OK,
		OKCancel,
		AbortRetryIgnore,
		YesNoCancel,
		YesNo,
		RetryCancel
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
}
