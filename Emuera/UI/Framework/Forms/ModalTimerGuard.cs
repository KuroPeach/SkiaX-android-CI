using MinorShift.Emuera.GameView;
using System;
using System.Threading;

namespace MinorShift.Emuera.Forms
{
	/// <summary>
	/// 模态 UI 与输入超时定时器的互斥闸（2026-10-27，B+C 方案）。
	///
	/// 背景：`dbe563ae` 只给配置窗口单点套了 PauseTimer；其余模态（剪贴板/文件/文件夹/确认框）
	/// 打开期间 `TINPUTNF` 等带时限输入的超时照常到点，`endTimer` 会把 `RunEmueraProgram`
	/// 投递进模态消息循环 ⇒ 标题 DEMO（BADAPPLE 6573 帧）在窗口背后抢跑、无法取消。
	///
	/// 用法（UI 线程）：<c>using (ModalTimerGuard.Enter(console)) { dialog.ShowDialog(); }</c>
	/// 语义＝进入时 `PauseTimer()`（停表），退出时 `ResumeTimer(wasRunning)`（重启秒表，重新计满）。
	/// 计数器同时供 `PlatformInterop.IsModalOpen` 读取，`tickTimer` 用它做**终点兜底**（C）。
	/// </summary>
	internal static class ModalTimerGuard
	{
		private static int _depth;

		/// <summary>当前是否有已登记的模态窗口（可跨线程读取；GUI 宿主注入 PlatformInterop）。</summary>
		public static bool IsModalOpen => Volatile.Read(ref _depth) > 0;

		/// <summary>进入模态：停表 + 计数。必须 Dispose（推荐 using）。</summary>
		public static IDisposable Enter(EmueraConsole console) => new Scope(console);

		/// <summary>便捷包装：包住一次模态调用（MessageBox / ShowDialog）。</summary>
		public static void Run(EmueraConsole console, Action modal)
		{
			using (Enter(console))
				modal();
		}

		private sealed class Scope : IDisposable
		{
			private readonly EmueraConsole console;
			private readonly bool wasRunning;
			private bool disposed;

			public Scope(EmueraConsole console)
			{
				this.console = console;
				// console 为空也要计数：模态确实开着，C 兜底仍应推迟超时。
				wasRunning = console != null && console.PauseTimer();
				Interlocked.Increment(ref _depth);
			}

			public void Dispose()
			{
				if (disposed)
					return;
				disposed = true;
				Interlocked.Decrement(ref _depth);
				console?.ResumeTimer(wasRunning);
			}
		}
	}
}
