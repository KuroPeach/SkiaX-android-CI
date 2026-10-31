using System;
using System.Collections.Generic;

// R-AUD-8（P3-E）：EWP audio v1.0 的 VM 账本（spec §3/§4/§5/§6/§8/§9）。
// 与 v0.2 的 AudioLedger 并存：v0.2 模式逐项不变；v1 模式（audio.instances）用本账本。
//
// 线程模型沿用 v0.2：脚本线程写（指令/新内建）、FE 回供传输线程写、查询/DoState 读，
// 共用 gate；**锁内只读写账本，绝不 Emit**（事件由调用方在锁外经 AudioHooks 发）。
// ⚠ 分层：Runtime 层，⛔ 不得引用 UI/Ewp。
namespace MinorShift.Emuera.Runtime.Utils.Audio;

internal sealed class AudioV1Ledger
{
    private const int SeCount = 10;
    private const int LegacyCount = SeCount + 1; // 0＝BGM，1..10＝SE 0..9
    private const int TerminalRetain = 65536;
    private const int StatsTerminalShown = 64;

    private static readonly int[] MaxVoices = { 0, 4, 32, 8 }; // master 不计声部

    // ---------------- 数据模型 ----------------

    private sealed class Instance
    {
        public int Handle;
        public bool IsLegacy;
        public int LegacySlot = -1;
        public long Gen;
        public AudioBusId Bus;
        public string Track;
        public AudioV1State State;
        public bool Loop;
        public int Repeat = 1;
        public int Volume = 100;
        public double Rate = 1.0;
        public int Priority = 128;
        public int MaxInstances;
        public bool PreservePitch = true;
        public bool PitchKnown;
        public bool PitchValue;

        public long PosBaseMs;
        public long PosTickMs;
        public long L1DurMs;      // FE 回供；0＝未知
        public long ProbeDurMs;   // L2
        public int ProbeState;    // 0＝未探测，1＝成功，-1＝失败
        public long LastReportTickMs;
        public bool DeferredPaused;
        public long StolenBy;
        public int EffectiveTenths = -1;
        public string QueryMode = "unknown";

        public bool IsLive => State is AudioV1State.Pending or AudioV1State.Playing
            or AudioV1State.Paused or AudioV1State.Deferred;
    }

    private sealed class LegacySlot
    {
        public long Gen;
        public Instance Inst;     // 终态（Ended/Failed）仍保留，供旧查询读 dur/pos
        public string QueryMode = "unknown";
    }

    private sealed class BusState
    {
        public int Volume = 100;
        public bool Mute;
        public int EffectiveTenths = -1;
        public int DuckFeedbackTenths = -1;
    }

    private sealed class DuckRule
    {
        public AudioBusId Source;
        public AudioBusId Target;
        public double DuckDb;
        public int AttackMs = 80;
        public int ReleaseMs = 300;
        public bool Enabled = true;
    }

    private readonly object gate = new();
    private readonly LegacySlot[] legacy = new LegacySlot[LegacyCount];
    private readonly Dictionary<int, Instance> byHandle = new();
    private readonly Queue<Instance> terminalOrder = new();
    private readonly BusState[] buses = new BusState[4];
    private readonly Dictionary<int, DuckRule> ducks = new();

    private int nextHandle = 1;
    private bool handleSpaceExhausted;
    private bool audioUnavailable;
    private long latencyMs = -1;
    private long nextSyncId = 1;
    private string lastQueryMode = "unknown";

    // caps（握手时锁定；spec §1.2）
    private bool configured;
    private bool busCap;
    private bool transitionsCap;
    private bool metaCap;

    // 计数（spec §9.3）
    private long played, stopped, failed, missing, unsafePath, ignored, staleReports,
        badReports, queryFallback, trackMismatch;
    private long handlesAllocated, rejected, voiceLimit, stolen, degraded,
        unsupportedCap, handleExhausted, unknownHandleReports, syncSent, syncId;

    public AudioV1Ledger()
    {
        for (int i = 0; i < LegacyCount; i++)
            legacy[i] = new LegacySlot();
        for (int i = 0; i < buses.Length; i++)
            buses[i] = new BusState();
    }

    private static long Now() => Environment.TickCount64;
    private static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;
    private static int ClampVolume(int v) => Clamp(v, 0, 100);
    private static int ClampDur(int v) => Clamp(v, 0, 600000);
    private static double ClampRate(double v)
        => double.IsNaN(v) ? 1.0 : v < 0.1 ? 0.1 : v > 10.0 ? 10.0 : v;
    private static double ClampDuck(double v)
        => double.IsNaN(v) ? 0.0 : v < -80.0 ? -80.0 : v > 0.0 ? 0.0 : v;

    public static readonly string[] BusNames = { "master", "bgm", "se", "voice" };

    public static string BusName(AudioBusId bus) => BusNames[(int)bus];
    public static string StateName(AudioV1State s) => s switch
    {
        AudioV1State.Pending => "pending",
        AudioV1State.Playing => "playing",
        AudioV1State.Paused => "paused",
        AudioV1State.Deferred => "deferred",
        AudioV1State.Ended => "ended",
        AudioV1State.Failed => "failed",
        AudioV1State.Stopped => "stopped",
        AudioV1State.Stolen => "stolen",
        _ => "unknown",
    };

    private static bool TryParseBus(string name, out AudioBusId bus)
    {
        switch (name)
        {
            case "master": bus = AudioBusId.Master; return true;
            case "bgm": bus = AudioBusId.Bgm; return true;
            case "se": bus = AudioBusId.Se; return true;
            case "voice": bus = AudioBusId.Voice; return true;
            default: bus = AudioBusId.Master; return false;
        }
    }

    // ---------------- 会话/能力 ----------------

    /// <summary>握手 v1：锁定 caps 并保留握手前的 shadow（S1.5 晚连接重建）。
    /// 默认 duck 仅在 audio.bus 时安装（B3.6）；缺 audio.bus 时丢弃 shadow 里的 voice 实例。</summary>
    public void Configure(bool hasBus, bool hasTransitions, bool hasMeta, bool hasFeedback)
    {
        lock (gate)
        {
            busCap = hasBus;
            transitionsCap = hasTransitions;
            metaCap = hasMeta;
            _ = hasFeedback;
            configured = true;
            if (busCap)
            {
                // 保留握手前记录的自定义规则；默认 voice→bgm 仅在缺省时安装（B3.6）。
                int key = DuckKey(AudioBusId.Voice, AudioBusId.Bgm);
                if (!ducks.ContainsKey(key))
                {
                    ducks[key] = new DuckRule
                    {
                        Source = AudioBusId.Voice,
                        Target = AudioBusId.Bgm,
                        DuckDb = -12.0,
                        AttackMs = 80,
                        ReleaseMs = 300,
                        Enabled = true,
                    };
                }
            }
            else
            {
                // §1.4：voice 需 audio.bus；握手前乐观记录的音部/duck 在缺 cap 时不进 sync。
                ducks.Clear();
                bool any = false;
                foreach (var inst in byHandle.Values)
                {
                    if (inst.IsLive && inst.Bus == AudioBusId.Voice)
                    {
                        MarkTerminalNoLock(inst, AudioV1State.Stopped);
                        any = true;
                    }
                }
                if (any)
                    degraded++;
            }
        }
    }

    /// <summary>v0.2 / 无 audio：丢弃握手前的 shadow（冻结语义：不补发）。</summary>
    public void ResetSession()
    {
        lock (gate)
        {
            ResetNoLock();
            busCap = transitionsCap = metaCap = false;
        }
    }

    private void ResetNoLock()
    {
        configured = false;
        for (int i = 0; i < legacy.Length; i++)
        {
            legacy[i].Gen = 0;
            legacy[i].Inst = null;
            legacy[i].QueryMode = "unknown";
        }
        byHandle.Clear();
        terminalOrder.Clear();
        for (int i = 0; i < buses.Length; i++)
        {
            buses[i].Volume = 100;
            buses[i].Mute = false;
            buses[i].EffectiveTenths = -1;
            buses[i].DuckFeedbackTenths = -1;
        }
        ducks.Clear();
        nextHandle = 1;
        handleSpaceExhausted = false;
        audioUnavailable = false;
        latencyMs = -1;
        nextSyncId = 1;
        lastQueryMode = "unknown";
        played = stopped = failed = missing = unsafePath = ignored = staleReports = 0;
        badReports = queryFallback = trackMismatch = 0;
        handlesAllocated = rejected = voiceLimit = stolen = degraded = 0;
        unsupportedCap = handleExhausted = unknownHandleReports = 0;
        syncSent = syncId = 0;
    }

    // ---------------- 计数 ----------------

    public void NoteUnsafePath() { lock (gate) unsafePath++; }
    public void NoteMissing() { lock (gate) missing++; }
    public void NoteIgnored() { lock (gate) ignored++; }
    public void NoteUnsupportedCap() { lock (gate) unsupportedCap++; }
    public void NoteBadReport() { lock (gate) badReports++; }
    public void NoteEventDropped() { lock (gate) degraded++; }
    public void NoteSyncSent() { lock (gate) syncSent++; }
    public void NoteExternalFailure() { lock (gate) failed++; }

    // ---------------- 句柄/实例 ----------------

    private int AllocHandleNoLock()
    {
        if (handleSpaceExhausted || nextHandle <= 0)
        {
            handleSpaceExhausted = true;
            handleExhausted++;
            return 0;
        }
        int h = nextHandle;
        if (nextHandle == int.MaxValue)
            nextHandle = 0;
        else
            nextHandle++;
        handlesAllocated++;
        return h;
    }

    private Instance NewInstanceNoLock(int handle, AudioBusId bus, string track, bool loop,
        int volume, double rate, int priority, int maxInstances)
    {
        long now = Now();
        var inst = new Instance
        {
            Handle = handle,
            Bus = bus,
            Track = track,
            State = AudioV1State.Pending,
            Loop = loop,
            Volume = volume,
            Rate = rate,
            Priority = priority,
            MaxInstances = maxInstances,
            PosBaseMs = 0,
            PosTickMs = now,
        };
        byHandle[handle] = inst;
        return inst;
    }

    private void AddTerminalNoLock(Instance inst)
    {
        terminalOrder.Enqueue(inst);
        while (terminalOrder.Count > TerminalRetain)
        {
            var old = terminalOrder.Dequeue();
            if (byHandle.TryGetValue(old.Handle, out var cur) && ReferenceEquals(cur, old))
                byHandle.Remove(old.Handle);
        }
    }

    private void MarkTerminalNoLock(Instance inst, AudioV1State state)
    {
        inst.State = state;
        inst.DeferredPaused = false;
        if (inst.IsLegacy)
        {
            var slot = legacy[inst.LegacySlot];
            if (ReferenceEquals(slot.Inst, inst) && state is AudioV1State.Stopped or AudioV1State.Stolen)
            {
                slot.Gen++;
                slot.Inst = null;
            }
        }
        AddTerminalNoLock(inst);
    }

    private Instance FindNoLock(int handle)
        => handle > 0 && byHandle.TryGetValue(handle, out var inst) ? inst : null;

    private static AudioV1Event CtlStopEvent(int handle, int durationMs, string curve)
        => new()
        {
            T = "audio.ctl",
            Handle = handle,
            Act = "stop",
            DurationMs = durationMs,
            Curve = curve,
        };

    private AudioV1Event PlayEventNoLock(Instance inst, int crossfadeFrom, int crossfadeMs, int fadeInMs)
        => new()
        {
            T = "audio.play",
            Handle = inst.Handle,
            Bus = BusName(inst.Bus),
            Track = inst.Track,
            Loop = inst.Loop,
            Volume = inst.Volume,
            Rate = inst.Rate,
            Priority = inst.Priority,
            MaxInstances = inst.MaxInstances,
            FadeInMs = fadeInMs,
            CrossfadeFrom = crossfadeFrom,
            CrossfadeMs = crossfadeMs,
            Repeat = inst.Repeat,
            PreservePitch = inst.PreservePitch,
        };

    // ---------------- 旧指令（v1 翻译；spec §7.2/§7.3） ----------------

    private static int SlotOfCh(int ch) => ch == -1 ? 0 : ch + 1;

    private void StopLegacyNoLock(LegacySlot slot, List<AudioV1Event> events, bool emit)
    {
        var inst = slot.Inst;
        if (inst == null)
            return;
        if (!inst.IsLive)
            return;
        MarkTerminalNoLock(inst, AudioV1State.Stopped);
        events.Add(CtlStopEvent(inst.Handle, 0, null));
        if (emit)
            stopped++;
    }

    public AudioV1Event[] LegacyPlayBgm(string track, bool emit)
    {
        var events = new List<AudioV1Event>(2);
        lock (gate)
        {
            if (audioUnavailable)
            {
                rejected++;
                return events.ToArray();
            }
            var slot = legacy[0];
            StopLegacyNoLock(slot, events, emit);
            int h = AllocHandleNoLock();
            if (h == 0)
            {
                rejected++;
                return events.ToArray();
            }
            var inst = NewInstanceNoLock(h, AudioBusId.Bgm, track, true, 100, 1.0, 255, 0);
            inst.IsLegacy = true;
            inst.LegacySlot = 0;
            inst.State = AudioV1State.Playing;
            inst.Repeat = 1;
            slot.Gen++;
            inst.Gen = slot.Gen;
            slot.Inst = inst;
            slot.QueryMode = "intent";
            events.Add(PlayEventNoLock(inst, 0, 0, 0));
            if (emit)
                played++;
        }
        return events.ToArray();
    }

    public AudioV1Event[] LegacyPlaySe(string track, int repeat, bool emit)
    {
        var events = new List<AudioV1Event>(2);
        lock (gate)
        {
            if (audioUnavailable)
            {
                rejected++;
                return events.ToArray();
            }
            int index = -1;
            for (int i = 1; i < LegacyCount; i++)
            {
                var cur = legacy[i].Inst;
                // v0.2 选道：Deferred 仍是 Playing 相位（占槽），Paused 可覆写。
                if (cur == null || (cur.State != AudioV1State.Playing
                    && cur.State != AudioV1State.Deferred))
                {
                    index = i;
                    break;
                }
            }
            if (index < 0)
                index = 1; // 全忙覆写 SE0（v0.2 同形）
            var slot = legacy[index];
            StopLegacyNoLock(slot, events, emit);
            int h = AllocHandleNoLock();
            if (h == 0)
            {
                rejected++;
                return events.ToArray();
            }
            var inst = NewInstanceNoLock(h, AudioBusId.Se, track, false, 100, 1.0, 255, 0);
            inst.IsLegacy = true;
            inst.LegacySlot = index;
            inst.State = AudioV1State.Playing;
            inst.Repeat = Math.Max(1, repeat);
            slot.Gen++;
            inst.Gen = slot.Gen;
            slot.Inst = inst;
            slot.QueryMode = "intent";
            events.Add(PlayEventNoLock(inst, 0, 0, 0));
            if (emit)
                played++;
        }
        return events.ToArray();
    }

    public AudioV1Event[] LegacyStopBgm(bool emit)
    {
        var events = new List<AudioV1Event>(1);
        lock (gate)
        {
            var slot = legacy[0];
            var inst = slot.Inst;
            if (inst != null && inst.IsLive)
            {
                StopLegacyNoLock(slot, events, emit);
            }
        }
        return events.ToArray();
    }

    /// <summary>STOPSOUND：只停旧 10 槽的 playing/deferred（paused 不碰；spec §7.3）。</summary>
    public AudioV1Event[] LegacyStopAllSe(bool emit)
    {
        var events = new List<AudioV1Event>();
        lock (gate)
        {
            for (int i = 1; i < LegacyCount; i++)
            {
                var inst = legacy[i].Inst;
                if (inst == null)
                    continue;
                if (inst.State is AudioV1State.Playing or AudioV1State.Deferred)
                    StopLegacyNoLock(legacy[i], events, emit);
            }
        }
        return events.ToArray();
    }

    public AudioV1Event[] LegacySetSoundVolume(int volume, bool emit)
    {
        var events = new List<AudioV1Event>(1);
        lock (gate)
        {
            int v = ClampVolume(volume);
            buses[(int)AudioBusId.Se].Volume = v;
            events.Add(new AudioV1Event
            {
                T = "audio.bus",
                Bus = "se",
                Act = "volume",
                Value = v,
                ValueIsInt = true,
                HasBusValue = true,
                BusValue = v,
            });
        }
        return events.ToArray();
    }

    public AudioV1Event[] LegacySetBgmVolume(int volume, bool emit)
    {
        var events = new List<AudioV1Event>(1);
        lock (gate)
        {
            int v = ClampVolume(volume);
            buses[(int)AudioBusId.Bgm].Volume = v;
            events.Add(new AudioV1Event
            {
                T = "audio.bus",
                Bus = "bgm",
                Act = "volume",
                Value = v,
                ValueIsInt = true,
                HasBusValue = true,
                BusValue = v,
            });
        }
        return events.ToArray();
    }

    public AudioV1Event[] LegacyBgmControl(int action, bool hasExtra, int speedPercent,
        int pitchFlag, bool emit, out long code)
    {
        code = 0;
        var events = new List<AudioV1Event>(1);
        lock (gate)
        {
            var slot = legacy[0];
            var inst = slot.Inst;
            if (!hasExtra)
            {
                switch (action)
                {
                    case 0: // pause
                        if (inst != null && inst.IsLive)
                        {
                            if (inst.State == AudioV1State.Playing)
                            {
                                inst.PosBaseMs = CurrentPosNoLock(inst);
                                inst.PosTickMs = Now();
                                inst.State = AudioV1State.Paused;
                            }
                            else if (inst.State == AudioV1State.Deferred)
                            {
                                inst.DeferredPaused = true;
                            }
                            events.Add(new AudioV1Event
                            {
                                T = "audio.ctl",
                                Handle = inst.Handle,
                                Act = "pause",
                            });
                        }
                        code = 1;
                        break;
                    case 1: // resume
                        if (inst != null && inst.IsLive)
                        {
                            bool emitIt = false;
                            if (inst.State == AudioV1State.Paused)
                            {
                                inst.State = AudioV1State.Playing;
                                inst.PosTickMs = Now();
                                emitIt = true;
                            }
                            else if (inst.State == AudioV1State.Deferred && inst.DeferredPaused)
                            {
                                inst.DeferredPaused = false;
                                emitIt = true;
                            }
                            if (emitIt)
                            {
                                events.Add(new AudioV1Event
                                {
                                    T = "audio.ctl",
                                    Handle = inst.Handle,
                                    Act = "resume",
                                });
                            }
                        }
                        code = 1;
                        break;
                    case 2: // stop
                        StopLegacyNoLock(slot, events, emit);
                        code = 1;
                        break;
                    default:
                        code = -2;
                        break;
                }
            }
            else
            {
                if (action != 3)
                {
                    code = -2;
                    return events.ToArray();
                }
                bool preservePitch = pitchFlag == 0;
                if (inst != null && inst.IsLive)
                {
                    inst.Rate = ClampRate(speedPercent / 100.0);
                    inst.PreservePitch = preservePitch;
                    inst.PitchKnown = false;
                    events.Add(new AudioV1Event
                    {
                        T = "audio.ctl",
                        Handle = inst.Handle,
                        Act = "rate",
                        Value = inst.Rate,
                        ValueIsInt = false,
                        HasPreservePitch = true,
                        PreservePitchValue = preservePitch,
                    });
                }
                code = 1;
            }
        }
        return events.ToArray();
    }

    public AudioV1Event[] LegacySeControl(int ch, int action, bool hasExtra, int speedPercent,
        int pitchFlag, bool emit, out long code)
    {
        code = 0;
        var events = new List<AudioV1Event>(1);
        if (ch < 0 || ch >= SeCount)
        {
            code = -1;
            return events.ToArray();
        }
        lock (gate)
        {
            var slot = legacy[SlotOfCh(ch)];
            var inst = slot.Inst;
            if (!hasExtra)
            {
                switch (action)
                {
                    case 0:
                        if (inst != null && inst.IsLive)
                        {
                            if (inst.State == AudioV1State.Playing)
                            {
                                inst.PosBaseMs = CurrentPosNoLock(inst);
                                inst.PosTickMs = Now();
                                inst.State = AudioV1State.Paused;
                            }
                            else if (inst.State == AudioV1State.Deferred)
                            {
                                inst.DeferredPaused = true;
                            }
                            events.Add(new AudioV1Event
                            {
                                T = "audio.ctl",
                                Handle = inst.Handle,
                                Act = "pause",
                            });
                        }
                        code = 1;
                        break;
                    case 1:
                        if (inst != null && inst.IsLive)
                        {
                            bool emitIt = false;
                            if (inst.State == AudioV1State.Paused)
                            {
                                inst.State = AudioV1State.Playing;
                                inst.PosTickMs = Now();
                                emitIt = true;
                            }
                            else if (inst.State == AudioV1State.Deferred && inst.DeferredPaused)
                            {
                                inst.DeferredPaused = false;
                                emitIt = true;
                            }
                            if (emitIt)
                            {
                                events.Add(new AudioV1Event
                                {
                                    T = "audio.ctl",
                                    Handle = inst.Handle,
                                    Act = "resume",
                                });
                            }
                        }
                        code = 1;
                        break;
                    case 2:
                        StopLegacyNoLock(slot, events, emit);
                        code = 1;
                        break;
                    default:
                        code = -2;
                        break;
                }
            }
            else
            {
                if (action != 3)
                {
                    code = -2;
                    return events.ToArray();
                }
                bool preservePitch = pitchFlag == 0;
                if (inst != null && inst.IsLive)
                {
                    inst.Rate = ClampRate(speedPercent / 100.0);
                    inst.PreservePitch = preservePitch;
                    inst.PitchKnown = false;
                    events.Add(new AudioV1Event
                    {
                        T = "audio.ctl",
                        Handle = inst.Handle,
                        Act = "rate",
                        Value = inst.Rate,
                        ValueIsInt = false,
                        HasPreservePitch = true,
                        PreservePitchValue = preservePitch,
                    });
                }
                code = 1;
            }
        }
        return events.ToArray();
    }

    // ---------------- 新 API：play/ctl/bus/duck ----------------

    private int CountLiveNewNoLock(AudioBusId bus)
    {
        int n = 0;
        foreach (var inst in byHandle.Values)
        {
            if (!inst.IsLegacy && inst.IsLive && inst.Bus == bus)
                n++;
        }
        return n;
    }

    private int CountLiveNewTrackNoLock(AudioBusId bus, string track)
    {
        int n = 0;
        foreach (var inst in byHandle.Values)
        {
            if (!inst.IsLegacy && inst.IsLive && inst.Bus == bus
                && string.Equals(inst.Track, track, StringComparison.Ordinal))
                n++;
        }
        return n;
    }

    private Instance OldestLiveNoLock(AudioBusId bus, bool sameTrackOnly, string track)
    {
        Instance best = null;
        foreach (var inst in byHandle.Values)
        {
            if (!inst.IsLive || inst.Bus != bus)
                continue;
            if (sameTrackOnly && !string.Equals(inst.Track, track, StringComparison.Ordinal))
                continue;
            if (best == null || inst.Handle < best.Handle)
                best = inst;
        }
        return best;
    }

    private void StopInstanceNoLock(Instance inst, List<AudioV1Event> events, bool emit,
        int durationMs, string curve)
    {
        if (inst == null || !inst.IsLive)
            return;
        MarkTerminalNoLock(inst, AudioV1State.Stopped);
        events.Add(CtlStopEvent(inst.Handle, durationMs, curve));
        if (emit)
            stopped++;
    }

    /// <summary>AUDIO_PLAY：返回事件序列（受害者 stop 在前、play 在后）；handle=0 拒绝。</summary>
    public AudioV1Event[] Play(AudioV1PlayRequest req, bool emit, out int handle)
    {
        handle = 0;
        var events = new List<AudioV1Event>(3);
        lock (gate)
        {
            if (audioUnavailable)
            {
                rejected++;
                return events.ToArray();
            }
            if (!TryParseBus(req.Bus, out var bus) || bus == AudioBusId.Master)
            {
                rejected++;
                return events.ToArray();
            }
            if (bus == AudioBusId.Voice && !busCap && configured)
            {
                rejected++;
                return events.ToArray();
            }
            if (!AudioPaths.TryNormalize(req.Track, out var track))
            {
                unsafePath++;
                rejected++;
                return events.ToArray();
            }
            if (!AudioPaths.TryResolveUnderRoot(Program.MusicDir, track, out _))
            {
                unsafePath++;
                rejected++;
                return events.ToArray();
            }
            if (req.IfChanged && req.CrossfadeFrom != 0)
            {
                rejected++;
                return events.ToArray();
            }

            int volume = ClampVolume(req.Volume);
            int priority = Clamp(req.Priority, 0, 255);
            int maxInstances = Clamp(req.MaxInstances, 0, 255);
            double rate = ClampRate(req.Rate);

            // if_changed：VM 侧去重，不发事件、不分配句柄（§5.4）
            if (req.IfChanged)
            {
                var hit = OldestLiveNoLock(bus, true, track);
                if (hit != null)
                {
                    handle = hit.Handle;
                    return events.ToArray();
                }
            }

            // 过渡能力（§1.4/§5.2）
            bool degradedHere = false;
            int fadeIn = req.FadeInMs;
            if (fadeIn > 0 && !transitionsCap)
            {
                if (configured)
                    degradedHere = true;
                fadeIn = 0;
            }
            fadeIn = ClampDur(fadeIn);
            int crossMs = req.CrossfadeMs < 0 ? fadeIn : ClampDur(req.CrossfadeMs);

            Instance cross = null;
            if (req.CrossfadeFrom == -1)
                cross = OldestLiveNoLock(bus, false, null);
            else if (req.CrossfadeFrom > 0)
                cross = FindNoLock(req.CrossfadeFrom);
            if (cross != null && !cross.IsLive)
            {
                cross = null;
                degradedHere = true;
            }
            // T2.3：交叉目标 paused/deferred ⇒ 硬切（先 stop 旧，再普通 play）
            bool crossHardCut = cross != null
                && (cross.State == AudioV1State.Paused || cross.State == AudioV1State.Deferred);
            if (crossHardCut)
                degradedHere = true;
            bool doCross = cross != null && !crossHardCut && transitionsCap && crossMs > 0;
            bool hardStop = cross != null && !doCross;
            if (cross != null && !crossHardCut && !transitionsCap && configured)
                degradedHere = true;
            if (cross != null && !crossHardCut && crossMs == 0 && configured)
                degradedHere = true;

            // 交叉/硬切先停旧（释放声部；spec §5.5/B-01）
            if (doCross)
            {
                MarkTerminalNoLock(cross, AudioV1State.Stopped);
            }
            else if (hardStop)
            {
                StopInstanceNoLock(cross, events, emit, 0, null);
            }

            // 准入（§3.4；只数新 API 实例，旧槽有保留容量）
            bool needBus = CountLiveNewNoLock(bus) >= MaxVoices[(int)bus];
            bool needTrack = maxInstances > 0
                && CountLiveNewTrackNoLock(bus, track) >= maxInstances;
            Instance victim = null;
            if (needBus || needTrack)
            {
                bool narrow = needTrack;
                Instance best = null;
                foreach (var cand in byHandle.Values)
                {
                    if (cand.IsLegacy || !cand.IsLive || cand.Bus != bus)
                        continue;
                    if (narrow && !string.Equals(cand.Track, track, StringComparison.Ordinal))
                        continue;
                    if (cand.Priority >= priority)
                        continue;
                    if (best == null || cand.Priority < best.Priority
                        || (cand.Priority == best.Priority && cand.Handle < best.Handle))
                        best = cand;
                }
                if (req.StealNone || best == null)
                {
                    rejected++;
                    voiceLimit++;
                    if (degradedHere)
                        degraded++;
                    return events.ToArray();
                }
                victim = best;
            }

            int h = AllocHandleNoLock();
            if (h == 0)
            {
                rejected++;
                if (degradedHere)
                    degraded++;
                return events.ToArray();
            }
            if (victim != null)
            {
                victim.State = AudioV1State.Stolen;
                victim.StolenBy = h;
                victim.DeferredPaused = false;
                AddTerminalNoLock(victim);
                events.Add(CtlStopEvent(victim.Handle, 0, null));
                stolen++;
                if (emit)
                    stopped++;
            }

            var inst = NewInstanceNoLock(h, bus, track, req.Loop, volume, rate, priority,
                maxInstances);
            inst.PreservePitch = req.PreservePitch;
            inst.PosBaseMs = 0;
            inst.PosTickMs = Now();
            inst.State = AudioV1State.Pending;

            // §8.2：fadeMs＝淡入，与是否交叉无关（§1.4 只在缺 transitions 时才不发，
            // 上面的 transitionsCap 分支已把它清 0 并计 degraded）。此处⛔ 不要写
            // `doCross ? fadeIn : 0`——那会让普通 play 的淡入被静默吞掉（2026-11-01 修）。
            events.Add(PlayEventNoLock(inst, doCross ? cross.Handle : 0,
                doCross ? crossMs : 0, fadeIn));
            if (emit)
                played++;
            if (degradedHere)
                degraded++;
            handle = h;
        }
        return events.ToArray();
    }

    public AudioV1Event[] Ctl(int handle, int op, double value, int durationMs,
        bool hasPreservePitch, bool preservePitch, string curve, bool emit, out long code)
    {
        code = 0;
        var events = new List<AudioV1Event>(1);
        lock (gate)
        {
            var inst = FindNoLock(handle);
            if (inst == null)
            {
                code = -1;
                return events.ToArray();
            }
            if (!inst.IsLive)
            {
                code = -1;
                return events.ToArray();
            }
            int dur = ClampDur(durationMs);
            switch ((AudioV1Op)op)
            {
                case AudioV1Op.Stop:
                    if (dur > 0 && !transitionsCap)
                    {
                        if (configured)
                            degraded++;
                        dur = 0;
                    }
                    StopInstanceNoLock(inst, events, emit, dur, curve);
                    code = 1;
                    break;
                case AudioV1Op.Pause:
                    if (inst.State == AudioV1State.Playing)
                    {
                        inst.PosBaseMs = CurrentPosNoLock(inst);
                        inst.PosTickMs = Now();
                        inst.State = AudioV1State.Paused;
                        events.Add(new AudioV1Event
                        {
                            T = "audio.ctl",
                            Handle = handle,
                            Act = "pause",
                        });
                        code = 1;
                    }
                    else if (inst.State == AudioV1State.Deferred)
                    {
                        inst.DeferredPaused = true;
                        events.Add(new AudioV1Event
                        {
                            T = "audio.ctl",
                            Handle = handle,
                            Act = "pause",
                        });
                        code = 1;
                    }
                    else if (inst.State == AudioV1State.Pending)
                    {
                        code = 0;
                    }
                    else
                    {
                        code = 0;
                    }
                    break;
                case AudioV1Op.Resume:
                    if (inst.State == AudioV1State.Paused)
                    {
                        inst.State = AudioV1State.Playing;
                        inst.PosTickMs = Now();
                        events.Add(new AudioV1Event
                        {
                            T = "audio.ctl",
                            Handle = handle,
                            Act = "resume",
                        });
                        code = 1;
                    }
                    else if (inst.State == AudioV1State.Deferred && inst.DeferredPaused)
                    {
                        inst.DeferredPaused = false;
                        events.Add(new AudioV1Event
                        {
                            T = "audio.ctl",
                            Handle = handle,
                            Act = "resume",
                        });
                        code = 1;
                    }
                    else
                    {
                        code = 0;
                    }
                    break;
                case AudioV1Op.Seek:
                    if (!transitionsCap && configured)
                    {
                        degraded++;
                        code = -2;
                        break;
                    }
                    if (inst.State is not (AudioV1State.Pending or AudioV1State.Playing
                        or AudioV1State.Paused or AudioV1State.Deferred))
                    {
                        code = -1;
                        break;
                    }
                    long seek = value < 0 ? 0 : (long)value;
                    long durMs = DurForNoLock(inst);
                    if (durMs > 0 && seek > durMs)
                        seek = durMs;
                    inst.PosBaseMs = seek;
                    inst.PosTickMs = Now();
                    if (transitionsCap)
                    {
                        events.Add(new AudioV1Event
                        {
                            T = "audio.ctl",
                            Handle = handle,
                            Act = "seek",
                            Value = seek,
                            ValueIsInt = true,
                        });
                    }
                    code = 1;
                    break;
                case AudioV1Op.Volume:
                    int vol = ClampVolume((int)value);
                    inst.Volume = vol;
                    if (dur > 0 && !transitionsCap)
                    {
                        if (configured)
                            degraded++;
                        dur = 0;
                    }
                    events.Add(new AudioV1Event
                    {
                        T = "audio.ctl",
                        Handle = handle,
                        Act = "volume",
                        Value = vol,
                        ValueIsInt = true,
                        DurationMs = dur,
                        Curve = curve,
                    });
                    code = 1;
                    break;
                case AudioV1Op.Fade:
                    if (dur > 0 && !transitionsCap && configured)
                    {
                        code = -2;
                        degraded++;
                        break;
                    }
                    int target = ClampVolume((int)value);
                    inst.Volume = target;
                    events.Add(new AudioV1Event
                    {
                        T = "audio.ctl",
                        Handle = handle,
                        Act = "fade",
                        Value = target,
                        ValueIsInt = true,
                        DurationMs = dur,
                        Curve = curve,
                    });
                    code = 1;
                    break;
                case AudioV1Op.Rate:
                    double rate = ClampRate(value / 100.0);
                    inst.Rate = rate;
                    if (hasPreservePitch)
                        inst.PreservePitch = preservePitch;
                    inst.PitchKnown = false;
                    events.Add(new AudioV1Event
                    {
                        T = "audio.ctl",
                        Handle = handle,
                        Act = "rate",
                        Value = rate,
                        ValueIsInt = false,
                        HasPreservePitch = hasPreservePitch,
                        PreservePitchValue = preservePitch,
                    });
                    code = 1;
                    break;
                case AudioV1Op.Priority:
                    inst.Priority = Clamp((int)value, 0, 255);
                    events.Add(new AudioV1Event
                    {
                        T = "audio.ctl",
                        Handle = handle,
                        Act = "priority",
                        Value = inst.Priority,
                        ValueIsInt = true,
                    });
                    code = 1;
                    break;
                case AudioV1Op.Loop:
                    inst.Loop = value != 0;
                    events.Add(new AudioV1Event
                    {
                        T = "audio.ctl",
                        Handle = handle,
                        Act = "loop",
                        Value = inst.Loop ? 1 : 0,
                        ValueIsInt = true,
                    });
                    code = 1;
                    break;
                default:
                    code = -2;
                    break;
            }
        }
        return events.ToArray();
    }

    public AudioV1Event[] Bus(string busName, int op, int value, int durationMs,
        bool emit, out long code)
    {
        code = 0;
        var events = new List<AudioV1Event>(1);
        lock (gate)
        {
            if (!TryParseBus(busName, out var bus))
            {
                code = -1;
                return events.ToArray();
            }
            if ((bus == AudioBusId.Master || bus == AudioBusId.Voice) && !busCap && configured)
            {
                code = -2;
                return events.ToArray();
            }
            int dur = ClampDur(durationMs);
            switch ((AudioV1BusOp)op)
            {
                case AudioV1BusOp.Volume:
                {
                    int v = ClampVolume(value);
                    if ((bus == AudioBusId.Master || bus == AudioBusId.Voice)
                        && !busCap && configured)
                    {
                        code = -2;
                        return events.ToArray();
                    }
                    if (dur > 0)
                        degraded++; // v1 不支持总线级淡变（§4.2）
                    if (buses[(int)bus].Volume == v)
                    {
                        code = 0;
                        return events.ToArray();
                    }
                    buses[(int)bus].Volume = v;
                    events.Add(new AudioV1Event
                    {
                        T = "audio.bus",
                        Bus = busName,
                        Act = "volume",
                        BusValue = v,
                        HasBusValue = true,
                    });
                    code = 1;
                    break;
                }
                case AudioV1BusOp.Mute:
                {
                    bool mute = value != 0;
                    if (buses[(int)bus].Mute == mute)
                    {
                        code = 0;
                        return events.ToArray();
                    }
                    buses[(int)bus].Mute = mute;
                    events.Add(new AudioV1Event
                    {
                        T = "audio.bus",
                        Bus = busName,
                        Act = "mute",
                        BusValue = mute ? 1 : 0,
                        HasBusValue = true,
                    });
                    code = 1;
                    break;
                }
                case AudioV1BusOp.StopAll:
                {
                    bool includePaused = value != 0;
                    if (dur > 0 && !transitionsCap)
                    {
                        if (configured)
                            degraded++;
                        dur = 0;
                    }
                    bool any = false;
                    foreach (var inst in byHandle.Values)
                    {
                        if (!inst.IsLive)
                            continue;
                        if (bus != AudioBusId.Master && inst.Bus != bus)
                            continue;
                        if (inst.State == AudioV1State.Paused && !includePaused)
                            continue;
                        MarkTerminalNoLock(inst, AudioV1State.Stopped);
                        any = true;
                    }
                    if (!any)
                    {
                        code = 0;
                        return events.ToArray();
                    }
                    events.Add(new AudioV1Event
                    {
                        T = "audio.bus",
                        Bus = busName,
                        Act = "stop_all",
                        IncludePaused = includePaused,
                        DurationMs = dur,
                    });
                    if (emit)
                        stopped++;
                    code = 1;
                    break;
                }
                default:
                    code = -2;
                    break;
            }
        }
        return events.ToArray();
    }

    private static int DuckKey(AudioBusId source, AudioBusId target)
        => (int)source * 10 + (int)target;

    public AudioV1Event[] Duck(string sourceName, string targetName, int duckDb,
        int attackMs, int releaseMs, bool emit, out long code)
    {
        code = 0;
        var events = new List<AudioV1Event>(1);
        lock (gate)
        {
            if (!TryParseBus(sourceName, out var source)
                || !TryParseBus(targetName, out var target)
                || source == AudioBusId.Master || target == AudioBusId.Master
                || source == target)
            {
                code = -1;
                return events.ToArray();
            }
            if (!busCap && configured)
            {
                code = -2;
                return events.ToArray();
            }
            double db = ClampDuck(duckDb);
            int attack = Clamp(attackMs, 0, 60000);
            int release = Clamp(releaseMs, 0, 60000);
            int key = DuckKey(source, target);
            if (!ducks.ContainsKey(key) && db == 0.0)
            {
                code = 0;
                return events.ToArray();
            }
            if (ducks.TryGetValue(key, out var rule)
                && Math.Abs(rule.DuckDb - db) < 0.0001
                && rule.AttackMs == attack && rule.ReleaseMs == release
                && rule.Enabled == (db != 0.0))
            {
                code = 0;
                return events.ToArray();
            }
            if (rule == null)
            {
                rule = new DuckRule { Source = source, Target = target };
                ducks[key] = rule;
            }
            rule.DuckDb = db;
            rule.AttackMs = attack;
            rule.ReleaseMs = release;
            rule.Enabled = db != 0.0;
            events.Add(DuckEventNoLock(rule));
            code = 1;
        }
        return events.ToArray();
    }

    private static AudioV1Event DuckEventNoLock(DuckRule rule)
        => new()
        {
            T = "audio.duck",
            SourceBus = BusName(rule.Source),
            TargetBus = BusName(rule.Target),
            DuckDb = rule.DuckDb,
            AttackMs = rule.AttackMs,
            ReleaseMs = rule.ReleaseMs,
            Enabled = rule.Enabled,
        };

    // ---------------- 查询 ----------------

    private long CurrentPosNoLock(Instance inst)
    {
        long pos = inst.PosBaseMs;
        if (inst.State == AudioV1State.Playing)
            pos += (long)((Now() - inst.PosTickMs) * inst.Rate);
        return pos < 0 ? 0 : pos;
    }

    private static long ClampPos(long pos, long dur)
    {
        if (pos < 0)
            return 0;
        if (dur > 0 && pos > dur)
            return dur;
        return pos;
    }

    private void EnsureProbeNoLock(Instance inst)
    {
        if (inst == null || inst.Track == null || inst.ProbeState != 0)
            return;
        if (inst.L1DurMs > 0)
            return;
        if (!inst.IsLive)
            return;
        long dur = 0;
        try { dur = AudioProbe.ProbeTrack(inst.Track); } catch { dur = 0; }
        if (dur > 0)
        {
            inst.ProbeDurMs = dur;
            inst.ProbeState = 1;
        }
        else
        {
            inst.ProbeState = -1;
        }
    }

    private long DurForNoLock(Instance inst)
    {
        if (inst == null)
            return 0;
        EnsureProbeNoLock(inst);
        return KnownDurNoLock(inst);
    }

    /// <summary>已知时长（不触发探针 IO；sync / sys.state 用）。</summary>
    private static long KnownDurNoLock(Instance inst)
    {
        if (inst == null)
            return 0;
        if (inst.L1DurMs > 0)
            return inst.L1DurMs;
        if (inst.ProbeState == 1 && inst.ProbeDurMs > 0)
            return inst.ProbeDurMs;
        return 0;
    }

    /// <summary>无回供时按 v0.2 §4.3 判自然播完（仅非循环/单次且 L2 有值）。</summary>
    private void EvaluateEndedNoLock(Instance inst)
    {
        if (inst == null || inst.State != AudioV1State.Playing)
            return;
        if (inst.Loop || inst.Repeat > 1)
            return;
        if (inst.LastReportTickMs > 0)
            return;
        if (inst.ProbeState != 1 || inst.ProbeDurMs <= 0)
            return;
        long pos = CurrentPosNoLock(inst);
        if (pos >= inst.ProbeDurMs)
        {
            inst.State = AudioV1State.Ended;
            inst.PosBaseMs = inst.ProbeDurMs;
            inst.PosTickMs = Now();
            AddTerminalNoLock(inst);
        }
    }

    private string ModeForNoLock(Instance inst, bool hasL1, bool probeOk, bool countFallback)
    {
        if (inst == null)
        {
            if (countFallback)
                queryFallback++;
            return "unknown";
        }
        if (hasL1)
            return "fe";
        if (probeOk)
            return "probe";
        if (countFallback)
            queryFallback++;
        return "intent";
    }

    /// <summary>旧 GETSOUNDORBGMINFO（v1 模式；返回码/形状与 v0.2 同）。</summary>
    public long QueryLegacyInfo(int ch, int infoType, out long[] array, out string mode)
    {
        array = null;
        if (ch < -1 || ch >= SeCount)
        {
            mode = "unknown";
            return 0;
        }
        lock (gate)
        {
            var slot = legacy[SlotOfCh(ch)];
            var inst = slot.Inst;
            EnsureProbeNoLock(inst);
            EvaluateEndedNoLock(inst);
            bool hasL1 = inst != null && inst.LastReportTickMs > 0;
            bool probeOk = inst != null && inst.ProbeState == 1;
            long dur = inst == null ? 0 : DurForNoLock(inst);
            long pos = inst == null ? 0 : ClampPos(CurrentPosNoLock(inst), dur);
            AudioBusId bus = ch == -1 ? AudioBusId.Bgm : AudioBusId.Se;
            int busVol = buses[(int)bus].Volume;
            bool idle = inst == null;
            long optimisticPlaying = inst == null ? 0
                : inst.State is AudioV1State.Pending or AudioV1State.Playing
                    or AudioV1State.Deferred ? 1 : 0;
            long speedPct = inst == null ? 100 : (long)Math.Round(inst.Rate * 100.0);
            if (infoType == 1 || infoType == 2 || infoType < 0)
            {
                if (idle) { mode = "unknown"; queryFallback++; }
                else if (hasL1 && inst.L1DurMs > 0) mode = "fe";
                else if (probeOk) mode = "probe";
                else { mode = hasL1 ? "fe" : "intent"; queryFallback++; }
            }
            else if (infoType == 3)
            {
                if (idle) { mode = "unknown"; queryFallback++; }
                else if (hasL1) mode = "fe";
                else if (probeOk) mode = "probe";
                else { mode = "intent"; queryFallback++; }
            }
            else
            {
                mode = idle ? "unknown" : hasL1 ? "fe" : probeOk ? "probe" : "intent";
            }
            slot.QueryMode = mode;
            lastQueryMode = mode;
            switch (infoType)
            {
                case 1: return dur;
                case 2: return pos;
                case 3: return optimisticPlaying;
                case 4: return busVol;
                case 5: return speedPct;
                default:
                    array = new[] { dur, pos, optimisticPlaying, (long)busVol, speedPct };
                    return dur;
            }
        }
    }

    public long QueryLegacyIsPlayingSound(int ch, out string mode)
    {
        mode = "unknown";
        if (ch < 0 || ch >= SeCount)
            return -1;
        lock (gate)
        {
            var slot = legacy[SlotOfCh(ch)];
            var inst = slot.Inst;
            EnsureProbeNoLock(inst);
            EvaluateEndedNoLock(inst);
            bool playing = inst != null && inst.State
                is AudioV1State.Pending or AudioV1State.Playing or AudioV1State.Deferred;
            bool hasL1 = inst != null && inst.LastReportTickMs > 0;
            bool probeOk = inst != null && inst.ProbeState == 1;
            mode = ModeForNoLock(inst, hasL1, probeOk, true);
            slot.QueryMode = mode;
            lastQueryMode = mode;
            return playing ? ch : -1;
        }
    }

    public long QueryLegacyIsPlayingBgm(out string mode)
    {
        lock (gate)
        {
            var slot = legacy[0];
            var inst = slot.Inst;
            EnsureProbeNoLock(inst);
            EvaluateEndedNoLock(inst);
            bool playing = inst != null && inst.State
                is AudioV1State.Pending or AudioV1State.Playing or AudioV1State.Deferred;
            bool hasL1 = inst != null && inst.LastReportTickMs > 0;
            bool probeOk = inst != null && inst.ProbeState == 1;
            mode = ModeForNoLock(inst, hasL1, probeOk, true);
            slot.QueryMode = mode;
            lastQueryMode = mode;
            return playing ? 1 : 0;
        }
    }

    /// <summary>AUDIO_QUERY（§8.2）。unknown handle/field ⇒ -1；终态可查（H1.4）。</summary>
    public long Query(int handle, int field)
    {
        lock (gate)
        {
            var inst = FindNoLock(handle);
            if (inst == null)
            {
                lastQueryMode = "unknown";
                return -1;
            }
            EnsureProbeNoLock(inst);
            EvaluateEndedNoLock(inst);
            long dur = DurForNoLock(inst);
            long pos = ClampPos(CurrentPosNoLock(inst), dur);
            bool hasL1 = inst.LastReportTickMs > 0;
            bool probeOk = inst.ProbeState == 1;
            string mode = (AudioV1QueryField)field switch
            {
                AudioV1QueryField.State => hasL1 ? "fe" : "intent",
                AudioV1QueryField.Pos => hasL1 ? "fe" : probeOk ? "probe" : "intent",
                AudioV1QueryField.Dur => hasL1 && inst.L1DurMs > 0 ? "fe"
                    : probeOk ? "probe" : "intent",
                AudioV1QueryField.EffectiveVolume => inst.EffectiveTenths >= 0 ? "fe" : "unknown",
                AudioV1QueryField.PitchPreserved => inst.PitchKnown ? "fe" : "intent",
                AudioV1QueryField.Latency => latencyMs >= 0 ? "fe" : "unknown",
                _ => "intent",
            };
            lastQueryMode = mode;
            inst.QueryMode = mode;
            switch ((AudioV1QueryField)field)
            {
                case AudioV1QueryField.State: return (long)inst.State;
                case AudioV1QueryField.Pos: return pos;
                case AudioV1QueryField.Dur: return dur > 0 ? dur : -1;
                case AudioV1QueryField.Rate: return (long)Math.Round(inst.Rate * 100.0);
                case AudioV1QueryField.Volume: return inst.Volume;
                case AudioV1QueryField.EffectiveVolume:
                    return inst.EffectiveTenths < 0 ? -1
                        : (long)Math.Round(inst.EffectiveTenths / 10.0,
                            MidpointRounding.AwayFromZero);
                case AudioV1QueryField.Priority: return inst.Priority;
                case AudioV1QueryField.StolenBy: return inst.StolenBy;
                case AudioV1QueryField.PitchPreserved:
                    return inst.PitchKnown ? (inst.PitchValue ? 1 : 0) : -1;
                case AudioV1QueryField.Latency: return latencyMs >= 0 ? latencyMs : -1;
                case AudioV1QueryField.Bus: return (long)inst.Bus;
                case AudioV1QueryField.Loop: return inst.Loop ? 1 : 0;
                default: return -1;
            }
        }
    }

    private int VoicesOnBusNoLock(AudioBusId bus)
    {
        int n = 0;
        foreach (var inst in byHandle.Values)
        {
            if (!inst.IsLive)
                continue;
            if (bus != AudioBusId.Master && inst.Bus != bus)
                continue;
            n++;
        }
        return n;
    }

    private int DuckTenthsNoLock(AudioBusId target)
    {
        double best = 0;
        bool any = false;
        foreach (var rule in ducks.Values)
        {
            if (!rule.Enabled || rule.Target != target)
                continue;
            if (!any || rule.DuckDb < best)
            {
                best = rule.DuckDb;
                any = true;
            }
        }
        return any ? (int)Math.Round(-best * 10.0) : 0;
    }

    /// <summary>AUDIO_BUS_QUERY（§8.2）。返回值即 ERB 返回码（含 -1/-2）。</summary>
    public long BusQuery(string busName, int field)
    {
        lock (gate)
        {
            if (!TryParseBus(busName, out var bus))
            {
                lastQueryMode = "unknown";
                return -1;
            }
            if ((bus == AudioBusId.Master || bus == AudioBusId.Voice) && !busCap && configured)
            {
                lastQueryMode = "unknown";
                return -2;
            }
            var st = buses[(int)bus];
            var f = (AudioV1BusQueryField)field;
            string mode = f == AudioV1BusQueryField.EffectiveVolume
                ? (st.EffectiveTenths >= 0 ? "fe" : "unknown")
                : "intent";
            lastQueryMode = mode;
            switch (f)
            {
                case AudioV1BusQueryField.Volume: return st.Volume;
                case AudioV1BusQueryField.EffectiveVolume:
                    return st.EffectiveTenths < 0 ? -1
                        : (long)Math.Round(st.EffectiveTenths / 10.0,
                            MidpointRounding.AwayFromZero);
                case AudioV1BusQueryField.Mute: return st.Mute ? 1 : 0;
                case AudioV1BusQueryField.DuckDb:
                    if (!busCap)
                        return -2;
                    return DuckTenthsNoLock(bus);
                case AudioV1BusQueryField.Voices: return VoicesOnBusNoLock(bus);
                default: return -1;
            }
        }
    }

    // ---------------- FE 回供 ----------------

    public void ApplyState(int handle, string state, long posMs, long durMs, bool hasDur,
        double rate, int volume, double effectiveVolume, bool hasEffective,
        int pitchPreserved, long latency, string code, string trackEcho)
    {
        lock (gate)
        {
            var inst = FindNoLock(handle);
            if (inst == null)
            {
                unknownHandleReports++;
                return;
            }
            if (!inst.IsLive)
                return; // 终态后迟到回供忽略、不计数（H3.1）
            long now = Now();
            if (hasDur && durMs > 0 && inst.L1DurMs <= 0)
                inst.L1DurMs = durMs;
            if (!string.IsNullOrEmpty(trackEcho)
                && !string.Equals(trackEcho, inst.Track, StringComparison.Ordinal))
                trackMismatch++;
            if (pitchPreserved >= 0)
            {
                inst.PitchKnown = true;
                inst.PitchValue = pitchPreserved == 1;
            }
            if (hasEffective)
                inst.EffectiveTenths = (int)Math.Round(effectiveVolume * 10.0);
            if (latency >= 0)
                latencyMs = latency;
            // rate/volume 是 audio.state 的回显字段：只作诊断，⛔ 不改 VM 意图（C-13）。
            _ = rate;
            _ = volume;
            switch (state)
            {
                case "playing":
                    inst.State = AudioV1State.Playing;
                    inst.DeferredPaused = false;
                    inst.PosBaseMs = posMs;
                    inst.PosTickMs = now;
                    inst.LastReportTickMs = now;
                    break;
                case "paused":
                    inst.State = AudioV1State.Paused;
                    inst.DeferredPaused = false;
                    inst.PosBaseMs = posMs;
                    inst.PosTickMs = now;
                    inst.LastReportTickMs = now;
                    break;
                case "ended":
                    inst.State = AudioV1State.Ended;
                    inst.DeferredPaused = false;
                    inst.PosBaseMs = posMs;
                    inst.PosTickMs = now;
                    inst.LastReportTickMs = now;
                    AddTerminalNoLock(inst);
                    break;
                case "deferred":
                    if (inst.State is AudioV1State.Pending or AudioV1State.Playing)
                    {
                        inst.State = AudioV1State.Deferred;
                        inst.DeferredPaused = false;
                        inst.PosBaseMs = posMs;
                        inst.PosTickMs = now;
                        inst.LastReportTickMs = now;
                    }
                    break;
                default:
                    badReports++;
                    return;
            }
            inst.QueryMode = "fe";
        }
    }

    public void ApplyFail(int handle, string code, string trackEcho)
    {
        lock (gate)
        {
            var inst = FindNoLock(handle);
            if (inst == null)
            {
                unknownHandleReports++;
                return;
            }
            if (!inst.IsLive)
                return;
            if (!string.IsNullOrEmpty(trackEcho)
                && !string.Equals(trackEcho, inst.Track, StringComparison.Ordinal))
                trackMismatch++;
            MarkTerminalNoLock(inst, AudioV1State.Failed);
            failed++;
            inst.QueryMode = "fe";
            if (code == "no_device" && !audioUnavailable)
            {
                audioUnavailable = true;
                foreach (var other in byHandle.Values)
                {
                    if (ReferenceEquals(other, inst) || !other.IsLive)
                        continue;
                    MarkTerminalNoLock(other, AudioV1State.Stopped);
                }
            }
        }
    }

    public void ApplyDevice(string state, long latency)
    {
        lock (gate)
        {
            if (latency >= 0)
                latencyMs = latency;
            if (state == "restored")
                audioUnavailable = false;
        }
    }

    public void ApplyBusState(string busName, double effectiveVolume, bool hasEffective,
        int mute, double duckDb, bool hasDuck)
    {
        lock (gate)
        {
            if (!TryParseBus(busName, out var bus))
            {
                badReports++;
                return;
            }
            var st = buses[(int)bus];
            if (hasEffective)
                st.EffectiveTenths = (int)Math.Round(effectiveVolume * 10.0);
            if (hasDuck)
                st.DuckFeedbackTenths = (int)Math.Round(duckDb * 10.0);
            _ = mute; // 回显只诊断；意图权威在 VM（§3.6）
        }
    }

    // ---------------- sync / 状态快照 ----------------

    /// <summary>构造全量重建（spec §6.2）；syncId 每会话从 1 单调递增。</summary>
    public AudioV1Sync BuildSync()
    {
        lock (gate)
        {
            var sync = new AudioV1Sync { SyncId = nextSyncId++ };
            syncId = sync.SyncId;
            var live = new List<Instance>();
            foreach (var inst in byHandle.Values)
            {
                if (inst.IsLive)
                    live.Add(inst);
            }
            live.Sort((a, b) => a.Handle.CompareTo(b.Handle));
            foreach (var inst in live)
            {
                long dur = KnownDurNoLock(inst);
                sync.Handles.Add(new AudioV1SyncHandle
                {
                    Handle = inst.Handle,
                    Bus = BusName(inst.Bus),
                    Track = inst.Track,
                    State = inst.DeferredPaused ? "paused" : StateName(inst.State),
                    Loop = inst.Loop,
                    Volume = inst.Volume,
                    Rate = inst.Rate,
                    Priority = inst.Priority,
                    PosMs = ClampPos(CurrentPosNoLock(inst), dur),
                    DurMs = dur,
                    Repeat = inst.Repeat > 1 ? inst.Repeat : 0,
                    PreservePitch = inst.PreservePitch,
                });
            }
            AddBusSyncNoLock(sync, AudioBusId.Bgm);
            AddBusSyncNoLock(sync, AudioBusId.Se);
            if (busCap)
            {
                AddBusSyncNoLock(sync, AudioBusId.Master);
                AddBusSyncNoLock(sync, AudioBusId.Voice);
            }
            if (busCap)
            {
                foreach (var rule in ducks.Values)
                {
                    if (!rule.Enabled)
                        continue;
                    sync.Ducks.Add(new AudioV1SyncDuck
                    {
                        SourceBus = BusName(rule.Source),
                        TargetBus = BusName(rule.Target),
                        DuckDb = rule.DuckDb,
                        AttackMs = rule.AttackMs,
                        ReleaseMs = rule.ReleaseMs,
                        Enabled = rule.Enabled,
                    });
                }
            }
            return sync;
        }
    }

    private void AddBusSyncNoLock(AudioV1Sync sync, AudioBusId bus)
    {
        var st = buses[(int)bus];
        sync.Buses.Add(new AudioV1SyncBus
        {
            Bus = BusName(bus),
            Volume = st.Volume,
            Mute = st.Mute,
        });
    }

    public AudioV1Stats Stats()
    {
        lock (gate)
        {
            var chans = new AudioChannelInfo[LegacyCount];
            for (int i = 0; i < LegacyCount; i++)
            {
                var slot = legacy[i];
                var inst = slot.Inst;
                AudioV1State state = inst?.State ?? AudioV1State.Unknown;
                AudioPhase phase = state switch
                {
                    AudioV1State.Pending or AudioV1State.Playing or AudioV1State.Deferred
                        => AudioPhase.Playing,
                    AudioV1State.Paused => AudioPhase.Paused,
                    AudioV1State.Ended => AudioPhase.Ended,
                    AudioV1State.Failed => AudioPhase.Failed,
                    _ => AudioPhase.Idle,
                };
                chans[i] = new AudioChannelInfo
                {
                    Ch = i == 0 ? -1 : i - 1,
                    Phase = phase,
                    Gen = slot.Gen,
                    Track = inst?.Track,
                    QueryMode = slot.QueryMode,
                    FeedbackStaleMs = inst != null && inst.LastReportTickMs > 0
                        ? Now() - inst.LastReportTickMs : -1,
                    PitchPreserved = inst != null && inst.PitchKnown
                        ? (inst.PitchValue ? 1 : 2) : 0,
                };
            }

            var all = new List<Instance>(byHandle.Values);
            all.Sort((a, b) => a.Handle.CompareTo(b.Handle));
            var liveInfos = new List<AudioV1InstanceInfo>();
            var terminalInfos = new List<AudioV1InstanceInfo>();
            foreach (var inst in all)
            {
                if (!inst.IsLive)
                {
                    terminalInfos.Add(InstanceInfoNoLock(inst));
                    continue;
                }
                liveInfos.Add(InstanceInfoNoLock(inst));
            }
            int skip = terminalInfos.Count > StatsTerminalShown
                ? terminalInfos.Count - StatsTerminalShown : 0;
            var instances = new List<AudioV1InstanceInfo>(liveInfos.Count + StatsTerminalShown);
            instances.AddRange(liveInfos);
            for (int i = skip; i < terminalInfos.Count; i++)
                instances.Add(terminalInfos[i]);

            var busInfos = new List<AudioV1BusInfo>(4);
            AddBusInfoNoLock(busInfos, AudioBusId.Bgm);
            AddBusInfoNoLock(busInfos, AudioBusId.Se);
            if (busCap)
            {
                AddBusInfoNoLock(busInfos, AudioBusId.Master);
                AddBusInfoNoLock(busInfos, AudioBusId.Voice);
            }

            return new AudioV1Stats
            {
                Played = played,
                Stopped = stopped,
                Failed = failed,
                Missing = missing,
                UnsafePath = unsafePath,
                Ignored = ignored,
                StaleReports = staleReports,
                BadReports = badReports,
                QueryFallback = queryFallback,
                TrackMismatch = trackMismatch,
                HandlesAllocated = handlesAllocated,
                Rejected = rejected,
                VoiceLimit = voiceLimit,
                Stolen = stolen,
                Degraded = degraded,
                UnsupportedCap = unsupportedCap,
                HandleExhausted = handleExhausted,
                UnknownHandleReports = unknownHandleReports,
                SyncSent = syncSent,
                SyncId = syncId,
                AudioUnavailable = audioUnavailable,
                QueryMode = lastQueryMode,
                Channels = chans,
                Instances = instances.ToArray(),
                Buses = busInfos.ToArray(),
            };
        }
    }

    private AudioV1InstanceInfo InstanceInfoNoLock(Instance inst)
    {
        long dur = KnownDurNoLock(inst);
        long pos = ClampPos(CurrentPosNoLock(inst), dur);
        return new AudioV1InstanceInfo
        {
            Handle = inst.Handle,
            Bus = BusName(inst.Bus),
            State = StateName(inst.State),
            PosMs = pos,
            DurMs = dur,
            Rate = inst.Rate,
            Priority = inst.Priority,
            Volume = inst.Volume,
            EffectiveTenths = inst.EffectiveTenths,
            StolenBy = (int)inst.StolenBy,
            PitchPreserved = inst.PitchKnown ? (inst.PitchValue ? 1 : 0) : -1,
        };
    }

    private void AddBusInfoNoLock(List<AudioV1BusInfo> list, AudioBusId bus)
    {
        var st = buses[(int)bus];
        list.Add(new AudioV1BusInfo
        {
            Bus = BusName(bus),
            Volume = st.Volume,
            Mute = st.Mute,
            EffectiveTenths = st.EffectiveTenths,
            DuckDbTenths = busCap ? DuckTenthsNoLock(bus) : 0,
            DuckFeedbackTenths = st.DuckFeedbackTenths,
            Voices = VoicesOnBusNoLock(bus),
        });
    }
}
