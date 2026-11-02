using System;
using MinorShift.Emuera.Runtime.Script;

// R-AUD-8（P3-E）：6 个新 ERB 内建的模式路由与 fail-closed（spec §1.5/§8）。
// - v1 模式：走 AudioV1Ledger 并发 v1 事件；
// - 握手前（None）：记进 v1 shadow（S1.5 晚连接重建），照常返回乐观值；
// - v0.2 模式：按 §1.5 fail-closed（PLAY 0 / CTL·BUS·DUCK −2 / QUERY −1），计 unsupportedCap。
// ⚠ 分层：Runtime 层，⛔ 不得引用 UI/Ewp。
namespace MinorShift.Emuera.Runtime.Utils.Audio;

internal static class AudioV1Intents
{
    private static AudioMode Mode => AudioHooks.Mode;
    private static AudioV1Ledger V1 => AudioHooks.V1;
    private static bool Emit => Mode == AudioMode.V1;

    private static long FailClosedPlay(AudioV1Ledger v1)
    {
        v1?.NoteUnsupportedCap();
        return 0;
    }

    private static long FailClosedOp(AudioV1Ledger v1)
    {
        v1?.NoteUnsupportedCap();
        return -2;
    }

    private static long FailClosedQuery(AudioV1Ledger v1)
    {
        v1?.NoteUnsupportedCap();
        return -1;
    }

    /// <summary>AUDIO_PLAY（spec §8.2；入场参数原子）。返回 handle/0。</summary>
    public static long Play(string track, string bus, int volume, int loop, int fadeMs,
        int priority, int maxInstances, int flags, int crossfadeFrom, int crossfadeMs,
        int preservePitch, double rate)
    {
        var mode = Mode;
        var v1 = V1;
        if (mode == AudioMode.V02)
            return FailClosedPlay(v1);
        if (v1 == null)
            return 0;
        var req = new AudioV1PlayRequest
        {
            Track = track,
            Bus = bus,
            Volume = volume,
            Loop = loop != 0,
            FadeInMs = fadeMs,
            Priority = priority,
            MaxInstances = maxInstances,
            StealNone = (flags & 1) != 0,
            IfChanged = (flags & 2) != 0,
            CrossfadeFrom = crossfadeFrom,
            CrossfadeMs = crossfadeMs,
            PreservePitch = preservePitch != 0,
            Rate = rate,
        };
        var events = v1.Play(req, Emit, out int handle);
        AudioHooks.EmitV1(events);
        return handle;
    }

    /// <summary>AUDIO_CTL（spec §8.2）。返回 1/0/−1/−2。</summary>
    public static long Ctl(int handle, int op, double value, int durMs,
        bool hasPreservePitch, int preservePitch, string curve)
    {
        var mode = Mode;
        var v1 = V1;
        if (mode == AudioMode.V02)
            return FailClosedOp(v1);
        if (v1 == null)
            return -2;
        var events = v1.Ctl(handle, op, value, durMs, hasPreservePitch,
            preservePitch != 0, curve, Emit, out long code);
        AudioHooks.EmitV1(events);
        return code;
    }

    /// <summary>AUDIO_BUS（spec §8.2）。返回 1/0/−1/−2。</summary>
    public static long Bus(string bus, int op, int value, int durMs)
    {
        var mode = Mode;
        var v1 = V1;
        if (mode == AudioMode.V02)
            return FailClosedOp(v1);
        if (v1 == null)
            return -2;
        var events = v1.Bus(bus, op, value, durMs, Emit, out long code);
        AudioHooks.EmitV1(events);
        return code;
    }

    /// <summary>AUDIO_DUCK（spec §8.2）。返回 1/0/−1/−2。</summary>
    public static long Duck(string sourceBus, string targetBus, int duckDb,
        int attackMs, int releaseMs)
    {
        var mode = Mode;
        var v1 = V1;
        if (mode == AudioMode.V02)
            return FailClosedOp(v1);
        if (v1 == null)
            return -2;
        var events = v1.Duck(sourceBus, targetBus, duckDb, attackMs, releaseMs,
            Emit, out long code);
        AudioHooks.EmitV1(events);
        return code;
    }

    /// <summary>AUDIO_QUERY（spec §8.2）。返回值；unknown＝−1。</summary>
    public static long Query(int handle, int field)
    {
        var mode = Mode;
        var v1 = V1;
        if (mode == AudioMode.V02)
            return FailClosedQuery(v1);
        if (v1 == null)
            return -1;
        return v1.Query(handle, field);
    }

    /// <summary>AUDIO_BUS_QUERY（spec §8.2）。返回值；unknown＝−1；缺 bus cap＝−2。</summary>
    public static long BusQuery(string bus, int field)
    {
        var mode = Mode;
        var v1 = V1;
        if (mode == AudioMode.V02)
            return FailClosedQuery(v1);
        if (v1 == null)
            return -1;
        return v1.BusQuery(bus, field);
    }
}
