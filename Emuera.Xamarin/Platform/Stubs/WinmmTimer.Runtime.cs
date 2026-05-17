using System;
using System.Runtime.InteropServices;

namespace MinorShift.Emuera.Runtime.Utils
{
    internal sealed class WinmmTimer
    {
        private static readonly long StartTicks;

        static WinmmTimer()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    mm_BeginPeriod(1);
                    StartTicks = mm_GetTime();
                }
                catch
                {
                    StartTicks = Environment.TickCount64;
                }
            }
            else
            {
                StartTicks = Environment.TickCount64;
            }
        }

        public static uint TickCount
        {
            get
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    try { return mm_GetTime(); }
                    catch { return (uint)(Environment.TickCount64 - StartTicks); }
                }
                return (uint)(Environment.TickCount64 - StartTicks);
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
}
