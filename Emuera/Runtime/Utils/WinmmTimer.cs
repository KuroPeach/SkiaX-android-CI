using System;
using System.Runtime.InteropServices;

namespace MinorShift.Emuera.Runtime.Utils;

internal sealed class WinmmTimer
{
	static WinmmTimer()
	{
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			instance = new WinmmTimer();
		}
	}

	private WinmmTimer()
	{
		mm_BeginPeriod(1);
	}
	~WinmmTimer()
	{
		mm_EndPeriod(1);
	}

	private static volatile WinmmTimer instance;

	public static uint TickCount
	{
		get
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				try { return mm_GetTime(); }
				catch { return (uint)Environment.TickCount64; }
			}
			return (uint)Environment.TickCount64;
		}
	}

	public static uint CurrentFrameTime;
	public static void FrameStart() { CurrentFrameTime = TickCount; }

	[DllImport("winmm.dll", EntryPoint = "timeGetTime")]
	private static extern uint mm_GetTime();
	[DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
	private static extern uint mm_BeginPeriod(uint uMilliseconds);
	[DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
	private static extern uint mm_EndPeriod(uint uMilliseconds);
}
