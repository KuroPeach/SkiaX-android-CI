using System;
using System.Threading;
using MinorShift.Emuera.Runtime.Utils.Audio;

// R-AUD-4：音频能力挂钩（Runtime 层）——仿 ChannelHooks 的分层先例：
// 内核只持委托/账本引用，宿主（EwpHost）注入；⛔ Runtime 不得引用 UI/Ewp。
// 非 EWP / FE 未声明 audio 时 Enabled=false ⇒ 指令层立即短路，原生播放逐字节不变。
namespace MinorShift.Emuera.Runtime.Script;

internal static class AudioHooks
{
    /// <summary>FE 已在 hello.ok.caps 声明 audio（VM 静音本机，只发控制事件）。</summary>
    public static volatile bool Enabled;
    /// <summary>FE 已声明 audio.feedback（接受 audio.state/audio.fail）。</summary>
    public static volatile bool FeedbackEnabled;

    private static AudioLedger ledger;
    private static Action<AudioEvent> sink;

    public static AudioLedger Ledger => Volatile.Read(ref ledger);

    /// <summary>EwpHost.Start 装：账本 + 事件出口（EwpHost 负责序列化并走 EwpOut.Emit）。</summary>
    public static void Attach(AudioLedger audioLedger, Action<AudioEvent> eventSink)
    {
        Volatile.Write(ref ledger, audioLedger);
        Volatile.Write(ref sink, eventSink);
        Enabled = false;
        FeedbackEnabled = false;
    }

    /// <summary>NotifyProcessExit 卸；之后指令层立即回落原生分支。</summary>
    public static void Detach()
    {
        Enabled = false;
        FeedbackEnabled = false;
        Volatile.Write(ref sink, null);
        Volatile.Write(ref ledger, null);
    }

    /// <summary>发一条控制事件（调用方必须在 audioGate 外；⛔ 持锁 Emit）。</summary>
    public static void Emit(AudioEvent ev)
    {
        if (ev == null)
            return;
        var s = Volatile.Read(ref sink);
        if (s == null)
            return;
        try { s(ev); } catch { }
    }
}
