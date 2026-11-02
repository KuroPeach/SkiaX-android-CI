using System;
using System.Collections.Generic;
using System.Threading;
using MinorShift.Emuera.Runtime.Utils.Audio;

// R-AUD-4/R-AUD-8：音频能力挂钩（Runtime 层）——仿 ChannelHooks 的分层先例：
// 内核只持委托/账本引用，宿主（EwpHost）注入；⛔ Runtime 不得引用 UI/Ewp。
// 非 EWP / FE 未声明 audio 时 Enabled=false & Mode=None ⇒ 指令层立即短路，原生播放逐字节不变。
//
// 账本：v0.2（AudioLedger）与 v1（AudioV1Ledger）并存；Mode 由握手锁定（spec §1.2）。
// 握手前 Mode=None：旧指令照常走原生路径，同时把意图记进 v1 shadow（S1.5 晚连接重建）；
// 若 FE 最终走 v0.2/无 audio ⇒ 丢弃 shadow（v0.2 冻结的「不补发」语义不变）。
namespace MinorShift.Emuera.Runtime.Script;

internal static class AudioHooks
{
    /// <summary>FE 已在 hello.ok.caps 声明 audio（VM 静音本机，只发控制事件）。</summary>
    public static volatile bool Enabled;
    /// <summary>FE 已声明 audio.feedback（接受 audio.state/audio.fail）。</summary>
    public static volatile bool FeedbackEnabled;
    /// <summary>会话音频模式（握手锁定；AudioMode.None 也可表示「尚未握手」）。</summary>
    public static volatile AudioMode Mode;

    private static AudioLedger ledger;
    private static AudioV1Ledger v1Ledger;
    private static Action<AudioEvent> sink;
    private static Action<AudioV1Event> sinkV1;

    private static readonly object v1Gate = new();
    private static readonly List<AudioV1Event> v1Pending = new();
    private static bool v1Ready;

    public static AudioLedger Ledger => Volatile.Read(ref ledger);
    public static AudioV1Ledger V1 => Volatile.Read(ref v1Ledger);

    /// <summary>EwpHost.Start 装：两套账本 + 事件出口（EwpHost 负责序列化并走 EwpOut.Emit）。</summary>
    public static void Attach(AudioLedger audioLedger, AudioV1Ledger audioV1Ledger,
        Action<AudioEvent> eventSink, Action<AudioV1Event> eventSinkV1)
    {
        Volatile.Write(ref ledger, audioLedger);
        Volatile.Write(ref v1Ledger, audioV1Ledger);
        Volatile.Write(ref sink, eventSink);
        Volatile.Write(ref sinkV1, eventSinkV1);
        Enabled = false;
        FeedbackEnabled = false;
        Mode = AudioMode.None;
        lock (v1Gate)
        {
            v1Pending.Clear();
            v1Ready = false;
        }
    }

    /// <summary>NotifyProcessExit 卸；之后指令层立即回落原生分支。</summary>
    public static void Detach()
    {
        Enabled = false;
        FeedbackEnabled = false;
        Mode = AudioMode.None;
        Volatile.Write(ref sink, null);
        Volatile.Write(ref sinkV1, null);
        Volatile.Write(ref ledger, null);
        Volatile.Write(ref v1Ledger, null);
        lock (v1Gate)
        {
            v1Pending.Clear();
            v1Ready = false;
        }
    }

    /// <summary>发一条 v0.2 控制事件（调用方必须在 audioGate 外；⛔ 持锁 Emit）。</summary>
    public static void Emit(AudioEvent ev)
    {
        if (ev == null)
            return;
        var s = Volatile.Read(ref sink);
        if (s == null)
            return;
        try { s(ev); } catch { }
    }

    /// <summary>发一条 v1 控制事件；sync 未发出前先排队（保证 sync 是本会话第一条 v1 事件）。</summary>
    public static void EmitV1(AudioV1Event ev)
    {
        if (ev == null || Mode != AudioMode.V1)
            return;
        lock (v1Gate)
        {
            if (!v1Ready)
            {
                v1Pending.Add(ev);
                return;
            }
            var s = Volatile.Read(ref sinkV1);
            if (s == null)
                return;
            try { s(ev); } catch { }
        }
    }

    public static void EmitV1(IEnumerable<AudioV1Event> events)
    {
        if (events == null)
            return;
        foreach (var ev in events)
            EmitV1(ev);
    }

    /// <summary>握手 v1：进入「sync 先行」状态，之后的事件先排队。</summary>
    public static void BeginV1Sync()
    {
        lock (v1Gate)
        {
            v1Pending.Clear();
            v1Ready = false;
        }
    }

    /// <summary>直接发 sync（绕过排队；它必须是第一条 v1 事件）。</summary>
    public static void EmitV1SyncNow(AudioV1Event sync)
    {
        var s = Volatile.Read(ref sinkV1);
        if (s == null || sync == null)
            return;
        try { s(sync); } catch { }
    }

    /// <summary>sync 已发：按产生顺序补发排队的 v1 事件。</summary>
    public static void ReleaseV1Sync()
    {
        lock (v1Gate)
        {
            var s = Volatile.Read(ref sinkV1);
            if (s != null)
            {
                foreach (var ev in v1Pending)
                {
                    try { s(ev); } catch { }
                }
            }
            v1Pending.Clear();
            v1Ready = true;
        }
    }
}
