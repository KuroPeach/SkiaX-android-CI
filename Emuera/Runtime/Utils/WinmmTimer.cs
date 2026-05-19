using System;
using System.Runtime.InteropServices;

namespace MinorShift.Emuera.Runtime.Utils;

internal sealed class WinmmTimer
{
	static WinmmTimer()
	{
#if WINDOWS
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			instance = new WinmmTimer();
		}
#endif
	}

#if WINDOWS
	private WinmmTimer()
	{
		mm_BeginPeriod(1);
	}
	~WinmmTimer()
	{
		mm_EndPeriod(1);
	}
#endif

	private static volatile WinmmTimer instance;

	public static uint TickCount
	{
		get
		{
#if WINDOWS
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				try { return mm_GetTime(); }
				catch { return (uint)Environment.TickCount64; }
			}
#endif
			return (uint)Environment.TickCount64;
		}
	}

	public static uint CurrentFrameTime;
	public static void FrameStart() { CurrentFrameTime = TickCount; }

#if WINDOWS
	[DllImport("winmm.dll", EntryPoint = "timeGetTime")]
	private static extern uint mm_GetTime();
	[DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
	private static extern uint mm_BeginPeriod(uint uMilliseconds);
	[DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
	private static extern uint mm_EndPeriod(uint uMilliseconds);
#endif
}
