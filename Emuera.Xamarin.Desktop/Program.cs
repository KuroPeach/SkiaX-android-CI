using System;
using MinorShift.Emuera;

namespace XEmuera.Desktop
{
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            MinorShift.Emuera.Program.MainDesktop(args);
        }
    }
}
