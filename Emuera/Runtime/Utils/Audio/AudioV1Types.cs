using System;
using System.Collections.Generic;

// R-AUD-8（P3-E）：EWP audio v1.0 的 VM 侧类型（句柄/总线/duck/过渡/sync）。
// 与 v0.2 的 AudioLedger/AudioEvent 并存：v0.2 路径逐项不变，v1 只在
// FE 双方声明 audio.instances 时启用（spec §1.2，模式握手锁定）。
// ⚠ 分层：Runtime 层，⛔ 不得引用 UI/Ewp。
namespace MinorShift.Emuera.Runtime.Utils.Audio;

/// <summary>会话音频模式（握手锁定；spec §1.2）。</summary>
internal enum AudioMode
{
    None = 0,   // 任一方未声明 audio（或尚未握手）
    V02 = 1,    // audio 但无 audio.instances
    V1 = 2,     // audio + audio.instances
}

/// <summary>固定四条总线（spec §4.1；值同 §8.3 BUS_*）。</summary>
internal enum AudioBusId
{
    Master = 0,
    Bgm = 1,
    Se = 2,
    Voice = 3,
}

/// <summary>v1 实例状态（值同 spec §8.3 STATE_*；0＝保留的 unknown）。</summary>
internal enum AudioV1State
{
    Unknown = 0,
    Pending = 1,
    Playing = 2,
    Paused = 3,
    Deferred = 4,
    Ended = 5,
    Failed = 6,
    Stopped = 7,
    Stolen = 8,
}

/// <summary>v1 ctl op（值同 §8.3 OP_*）。</summary>
internal enum AudioV1Op
{
    Stop = 0,
    Pause = 1,
    Resume = 2,
    Seek = 3,
    Volume = 4,
    Fade = 5,
    Rate = 6,
    Priority = 7,
    Loop = 8,
}

/// <summary>v1 bus op（值同 §8.3 BUSOP_*）。</summary>
internal enum AudioV1BusOp
{
    Volume = 0,
    Mute = 1,
    StopAll = 2,
}

/// <summary>v1 实例查询字段（§8.3 QF_*）。</summary>
internal enum AudioV1QueryField
{
    State = 0,
    Pos = 1,
    Dur = 2,
    Rate = 3,
    Volume = 4,
    EffectiveVolume = 5,
    Priority = 6,
    StolenBy = 7,
    PitchPreserved = 8,
    Latency = 9,
    Bus = 10,
    Loop = 11,
}

/// <summary>v1 总线查询字段（§8.3 BQF_*）。</summary>
internal enum AudioV1BusQueryField
{
    Volume = 0,
    EffectiveVolume = 1,
    Mute = 2,
    DuckDb = 3,
    Voices = 4,
}

/// <summary>AUDIO_PLAY 的原子入场参数（spec §8.1/§8.2；不得拆成 play+ctl）。</summary>
internal sealed class AudioV1PlayRequest
{
    public string Track;
    public string Bus;
    public int Volume = 100;
    public bool Loop;
    public int FadeInMs;
    public int Priority = 128;
    public int MaxInstances;
    public bool StealNone;
    public bool IfChanged;
    /// <summary>0＝无；-1＝同 bus 最老 live；&gt;0＝具体 handle（spec §8.2 / B-08）。</summary>
    public int CrossfadeFrom;
    /// <summary>-1＝缺省＝FadeInMs；0＝硬切（B-01）。</summary>
    public int CrossfadeMs = -1;
    public bool PreservePitch = true;
    public double Rate = 1.0;
}

/// <summary>一条 VM→FE v1 控制事件（宿主负责序列化；⛔ 本层不碰 wire/队列）。</summary>
internal sealed class AudioV1Event
{
    public string T;

    // audio.play
    public int Handle;
    public string Bus;
    public string Track;
    public bool Loop;
    public int Volume;
    public double Rate = 1.0;
    public int Priority;
    public int MaxInstances;
    public int FadeInMs;
    public int CrossfadeFrom;
    public int CrossfadeMs;
    public int Repeat;
    public bool PreservePitch = true;

    // audio.ctl
    public string Act;
    public double Value;
    public bool ValueIsInt = true;
    public int DurationMs;
    public string Curve;
    public bool HasPreservePitch;
    public bool PreservePitchValue;

    // audio.bus
    public int BusValue;
    public bool HasBusValue;
    public bool IncludePaused;

    // audio.duck
    public string SourceBus;
    public string TargetBus;
    public double DuckDb;
    public int AttackMs;
    public int ReleaseMs;
    public bool Enabled;

    // audio.sync
    public AudioV1Sync Sync;
}

internal sealed class AudioV1Sync
{
    public long SyncId;
    public List<AudioV1SyncHandle> Handles = new();
    public List<AudioV1SyncBus> Buses = new();
    public List<AudioV1SyncDuck> Ducks = new();
}

internal sealed class AudioV1SyncHandle
{
    public int Handle;
    public string Bus;
    public string Track;
    public string State;
    public bool Loop;
    public int Volume;
    public double Rate = 1.0;
    public int Priority;
    public long PosMs;
    public long DurMs;        // 0＝省略
    public int Repeat;        // 0/1＝省略
    public bool PreservePitch;
}

internal sealed class AudioV1SyncBus
{
    public string Bus;
    public int Volume;
    public bool Mute;
}

internal sealed class AudioV1SyncDuck
{
    public string SourceBus;
    public string TargetBus;
    public double DuckDb;
    public int AttackMs;
    public int ReleaseMs;
    public bool Enabled;
}

/// <summary>sys.state.audio.instances[] 一条（spec §9.2）。</summary>
internal sealed class AudioV1InstanceInfo
{
    public int Handle;
    public string Bus;
    public string State;
    public long PosMs;
    public long DurMs;           // 0＝unknown（省略）
    public double Rate;
    public int Priority;
    public int Volume;
    public int EffectiveTenths = -1;   // -1＝unknown（省略）
    public int StolenBy;
    public int PitchPreserved = -1;    // -1＝unknown（省略）
}

/// <summary>sys.state.audio.buses[] 一条（spec §9.2）。</summary>
internal sealed class AudioV1BusInfo
{
    public string Bus;
    public int Volume;
    public bool Mute;
    public int EffectiveTenths = -1;
    public int DuckDbTenths;
    public int DuckFeedbackTenths = -1;
    public int Voices;
}

/// <summary>sys.state.audio 的 v1 快照（v0.2 字段另由 AudioLedger.Stats 提供）。</summary>
internal sealed class AudioV1Stats
{
    public long Played, Stopped, Failed, Missing, UnsafePath, Ignored, StaleReports,
        BadReports, QueryFallback, TrackMismatch;
    public long HandlesAllocated, Rejected, VoiceLimit, Stolen, Degraded,
        UnsupportedCap, HandleExhausted, UnknownHandleReports, SyncSent, SyncId;
    public bool AudioUnavailable;
    public string QueryMode = "unknown";
    public AudioChannelInfo[] Channels;
    public AudioV1InstanceInfo[] Instances;
    public AudioV1BusInfo[] Buses;
}
