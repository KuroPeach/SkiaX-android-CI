using System;
using System.IO;

namespace MinorShift._Library
{
    internal static class Sys
    {
        public static readonly string ExeDir = AppContext.BaseDirectory;
        public static readonly string WorkingDir = AppContext.BaseDirectory;
        public static readonly string ExeName = "Emuera";

        public static bool PrevInstance()
        {
            return false;
        }
    }
}
