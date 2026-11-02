using System;

namespace MinorShift.Emuera.UI.Ewp;

/// <summary>
/// EWP/headless 诊断输出开关。
/// 默认静默；仅在显式设置 ERAWIRE_L4_TRACE=1 时输出退出链与传输诊断。
/// 业务错误仍由调用点直接写 stderr，不经过本开关。
/// </summary>
internal static class EwpDiagnostics
{
    private static readonly bool enabled = string.Equals(
        Environment.GetEnvironmentVariable("ERAWIRE_L4_TRACE"),
        "1",
        StringComparison.Ordinal);

    public static bool TraceEnabled => enabled;

    public static void Trace(string message)
    {
        if (!enabled)
            return;
        try { Console.Error.WriteLine("[L4] " + message); } catch { }
    }
}
