using System;
using System.Runtime.InteropServices;

namespace MinorShift._Library
{
    internal static class WinmmTimer
    {
        [DllImport("winmm.dll", EntryPoint = "timeGetTime")]
        private static extern uint TimeGetTime();

        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
        private static extern uint TimeBeginPeriod(uint uPeriod);

        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
        private static extern uint TimeEndPeriod(uint uPeriod);

        private static readonly bool UseWinmm;
        private static readonly long StartTicks;

        static WinmmTimer()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    TimeBeginPeriod(1);
                    UseWinmm = true;
                    StartTicks = TimeGetTime();
                }
                catch
                {
                    UseWinmm = false;
                    StartTicks = Environment.TickCount64;
                }
            }
            else
            {
                UseWinmm = false;
                StartTicks = Environment.TickCount64;
            }
        }

        public static uint TickCount => UseWinmm ? TimeGetTime() - (uint)StartTicks : (uint)(Environment.TickCount64 - StartTicks);
        public static uint CurrentFrameTime;

        public static void Reset() { }
        public static void FrameStart() { CurrentFrameTime = TickCount; }
    }
}
