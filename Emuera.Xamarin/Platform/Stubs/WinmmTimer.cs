using System;

namespace MinorShift._Library
{
    internal static class WinmmTimer
    {
        private static readonly long StartTicks = Environment.TickCount64;

        public static uint TickCount => (uint)(Environment.TickCount64 - StartTicks);
        public static uint CurrentFrameTime;

        public static void Reset() { }
        public static void FrameStart() { CurrentFrameTime = TickCount; }
    }
}
