using System;

namespace MinorShift.Emuera.GameView;

/// <summary>
/// EmueraConsole 重绘定时器的跨平台适配。
/// 桌面沿用 WinForms Timer（回调在 UI 线程）；非 Windows 用 System.Timers.Timer（回调在线程池，
/// 若将来启用重绘定时器需自行经宿主派发）。
/// </summary>
internal sealed class ConsoleTimer
{
#if WINDOWS
	readonly System.Windows.Forms.Timer timer = new();
	public event EventHandler Tick
	{
		add => timer.Tick += value;
		remove => timer.Tick -= value;
	}
#else
	readonly System.Timers.Timer timer = new();
	public event EventHandler Tick;

	public ConsoleTimer()
	{
		timer.Elapsed += (_, __) => Tick?.Invoke(this, EventArgs.Empty);
	}
#endif

	public bool Enabled
	{
		get => timer.Enabled;
		set => timer.Enabled = value;
	}

	public int Interval
	{
		get => (int)timer.Interval;
		set => timer.Interval = value;
	}

	public void Dispose() => timer.Dispose();
}
