using MinorShift.Emuera.Runtime.Script;
using MinorShift.Emuera.Runtime.Script.Statements;
using System;

// eraWire B②: channel 名挂钩——D0 契约（UI_CHANNEL.ERB 文件头）的消费方。
// 内核 Process.State.IntoFunction 末尾调用 NotifyFunctionEntry：UI_CHANNEL_* 三宏的
// 函数入口即通道边界（函数体恒 no-op，全部语义由本钩子发 EWP 事件，spec §4.2）。
// 非 EWP 模式 Handler 为 null——Notify 立即短路，普通执行零语义零开销。
namespace MinorShift.Emuera.UI.Ewp;

internal static class EwpChannel
{
    private const string NameBegin = "UI_CHANNEL_BEGIN";
    private const string NameEnd = "UI_CHANNEL_END";
    private const string NameClear = "UI_CHANNEL_CLEAR";

    /// <summary>EwpHost.Start 装机（--ewp）／NotifyProcessExit 卸载。签名：(macro, id, kind)。</summary>
    public static Action<string, string, string> Handler;

    /// <summary>IntoFunction 末尾调用（脚本线程；参数已绑定进形参，从形参变量读回
    /// 与函数体读同名变量同一机制）。macro ∈ begin/end/clear。</summary>
    public static void NotifyFunctionEntry(CalledFunction call, ExpressionMediator exm)
    {
        var h = Handler;
        if (h == null || call == null)
            return;
        string name = call.FunctionName;
        string macro;
        if (name == NameBegin)
            macro = "begin";
        else if (name == NameEnd)
            macro = "end";
        else if (name == NameClear)
            macro = "clear";
        else
            return;
        string id = ReadArg(call, 0, exm);
        if (string.IsNullOrEmpty(id))
            return; // 形参缺绑定＝非正常调用，忽略（函数体照常空展开）
        string kind = macro == "begin" ? ReadArg(call, 1, exm) : null;
        h(macro, id, kind);
    }

    private static string ReadArg(CalledFunction call, int idx, ExpressionMediator exm)
    {
        var args = call.TopLabel.Arg;
        if (args == null || idx >= args.Length || args[idx] == null)
            return null;
        try { return args[idx].GetStrValue(exm); }
        catch { return null; }
    }
}
