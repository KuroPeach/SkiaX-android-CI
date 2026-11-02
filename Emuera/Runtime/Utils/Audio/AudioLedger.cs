using System;
using System.Collections.Generic;

// R-AUD-4：VM 私有音频通道账本（EWP audio 增补草案 v0.2 §2/§3/§4；R-AUD-1 v3 §5.2–§5.3）。
// 11 条逻辑通道：-1＝BGM，0..9＝SE。VM 独占选道/gen/相位；FE 只执行收到的 ch/gen。
//
// 线程模型（spec §3.6）：VM 指令线程写（脚本）、FE 回供传输线程写、脚本查询/DoState 读，
// 共用 audioGate；**锁内只读写账本，绝不 Emit**（事件由调用方在锁外经 AudioHooks.Sink 发）。
// 诊断计数与账本同锁（无跨锁依赖，量级小）。
//
// ⚠ 分层：Runtime 层，⛔ 不得引用 UI/Ewp（headless 与 Xamarin Core 只编译 Runtime/**）。
namespace MinorShift.Emuera.Runtime.Utils.Audio;

internal enum AudioPhase
{
    Idle = 0,
    Playing = 1,
    Paused = 2,
    Ended = 3,
    Failed = 4,
}

internal enum AudioAct
{
    Play = 0,
    Stop = 1,
    StopAll = 2,
    Pause = 3,
    Resume = 4,
    Volume = 5,
    Speed = 6,
}

/// <summary>一条 VM→FE 控制事件（宿主负责序列化；⛔ 本层不碰 wire/队列）。</summary>
internal sealed class AudioEvent
{
    public bool IsBgm;
    public AudioAct Act;
    public int Ch;              // BGM=-1；SE=0..9；stopAll/全局 volume 不使用
    public long Gen;
    public string Track;
    public bool Loop;
    public int Repeat = 1;
    public int Volume;
    public double Speed = 1.0;
    public bool PreservePitch = true;
}

/// <summary>通道只读快照（sys.state.audio.channels[] 数据源）。</summary>
internal readonly struct AudioChannelInfo
{
    public int Ch { get; init; }
    public AudioPhase Phase { get; init; }
    public long Gen { get; init; }
    public string Track { get; init; }
    public string QueryMode { get; init; }
    /// <summary>距上次 FE 报告的毫秒；-1＝本代次无报告（键不出现）。</summary>
    public long FeedbackStaleMs { get; init; }
    /// <summary>0=unknown，1=true，2=false（键仅在 1/2 时出现）。</summary>
    public int PitchPreserved { get; init; }
}

/// <summary>会话级只读快照（sys.state.audio 数据源）。</summary>
internal sealed class AudioStats
{
    public long Played;
    public long Stopped;
    public long Failed;
    public long Missing;
    public long UnsafePath;
    public long Ignored;
    public long StaleReports;
    public long BadReports;
    public long QueryFallback;
    public long TrackMismatch;
    public long NativeSuppressed;
    public string QueryMode = "unknown";
    public AudioChannelInfo[] Channels;
}

internal sealed class AudioLedger
{
    private const int SeCount = 10;
    private const int ChannelCount = SeCount + 1; // 0＝BGM，1..10＝SE 0..9

    private sealed class Channel
    {
        public long Gen;
        public string Track;
        public bool Loop;
        public int Repeat = 1;
        public AudioPhase Phase = AudioPhase.Idle;

        public long L1DurMs;          // FE 回供时长；0＝未知
        public long PosBaseMs;
        public long PosAtTickMs;
        public double Speed = 1.0;
        public int Volume = 100;
        public bool PreservePitch = true;

        public long LastReportTickMs; // 0＝本代次无报告
        public string LastCode;
        public bool PitchKnown;
        public bool PitchValue;

        public bool Deferred;         // Web 首播未解锁（非终态；相保持 Playing）
        public bool DeferredPaused;
        public double DeferredSpeed = 1.0;
        public bool DeferredPreserve = true;

        public int ProbeState;        // 0＝未探测，1＝成功，-1＝失败
        public long ProbeDurMs;

        public string QueryMode = "unknown";
    }

    private readonly object gate = new();
    private readonly Channel[] channels = new Channel[ChannelCount];
    private long played, stopped, failed, missing, unsafePath, ignored,
        staleReports, badReports, queryFallback, trackMismatch, nativeSuppressed;
    private string lastQueryMode = "unknown";

    public AudioLedger()
    {
        for (int i = 0; i < ChannelCount; i++)
            channels[i] = new Channel();
    }

    private static int IndexOf(int ch) => ch == -1 ? 0 : ch + 1;
    private static int ChOf(int index) => index == 0 ? -1 : index - 1;
    private static bool ValidCh(int ch) => ch >= -1 && ch < SeCount;
    private static long Now() => Environment.TickCount64;
    private static long ClampVolume(int v) => Math.Clamp(v, 0, 100);
    private static double ClampSpeed(double v) => double.IsNaN(v) ? 1.0 : Math.Clamp(v, 0.1, 10.0);

    private Channel ChannelAt(int ch)
        => ValidCh(ch) ? channels[IndexOf(ch)] : null;

    // ---------------- 计数（诊断；⛔ 不冒充业务事件，spec §4.5） ----------------

    public void NoteMissing() { lock (gate) missing++; }
    public void NoteUnsafePath() { lock (gate) unsafePath++; }
    public void NoteIgnored() { lock (gate) ignored++; }
    public void NoteBadReport() { lock (gate) badReports++; }
    public void NoteNativeSuppressed() { lock (gate) nativeSuppressed++; }

    public AudioStats Stats()
    {
        lock (gate)
        {
            long now = Now();
            var arr = new AudioChannelInfo[ChannelCount];
            for (int i = 0; i < ChannelCount; i++)
            {
                var c = channels[i];
                arr[i] = new AudioChannelInfo
                {
                    Ch = ChOf(i),
                    Phase = c.Phase,
                    Gen = c.Gen,
                    Track = c.Track,
                    QueryMode = c.QueryMode,
                    FeedbackStaleMs = c.LastReportTickMs > 0 ? now - c.LastReportTickMs : -1,
                    PitchPreserved = c.PitchKnown ? (c.PitchValue ? 1 : 2) : 0,
                };
            }
            return new AudioStats
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
                NativeSuppressed = nativeSuppressed,
                QueryMode = lastQueryMode,
                Channels = arr,
            };
        }
    }

    // ---------------- 指令层（脚本线程） ----------------

    private void ResetForPlay(Channel c)
    {
        c.L1DurMs = 0;
        c.LastReportTickMs = 0;
        c.LastCode = null;
        c.PitchKnown = false;
        c.PitchValue = false;
        c.Deferred = false;
        c.DeferredPaused = false;
        c.DeferredSpeed = 1.0;
        c.DeferredPreserve = true;
        c.ProbeState = 0;
        c.ProbeDurMs = 0;
        c.PosBaseMs = 0;
        c.PosAtTickMs = Now();
        c.Speed = 1.0;
        c.QueryMode = "unknown";
    }

    private void ResetForStop(Channel c)
    {
        c.Track = null;
        c.Loop = false;
        c.Repeat = 1;
        c.Phase = AudioPhase.Idle;
        c.L1DurMs = 0;
        c.LastReportTickMs = 0;
        c.LastCode = null;
        c.PitchKnown = false;
        c.Deferred = false;
        c.DeferredPaused = false;
        c.ProbeState = 0;
        c.ProbeDurMs = 0;
        c.PosBaseMs = 0;
        c.PosAtTickMs = Now();
        c.QueryMode = "unknown";
    }

    /// <summary>PLAYSOUND：选道→gen+1→绑定→发 play。全忙取 ch0（覆写）。</summary>
    public AudioEvent PlaySe(string track, int repeat)
    {
        lock (gate)
        {
            int index = -1;
            for (int i = 1; i < ChannelCount; i++)
            {
                if (channels[i].Phase != AudioPhase.Playing)
                {
                    index = i;
                    break;
                }
            }
            if (index < 0)
                index = 1; // ch 0：全忙覆写
            var c = channels[index];
            c.Gen++;
            ResetForPlay(c);
            c.Track = track;
            c.Loop = false;
            c.Repeat = Math.Max(1, repeat);
            c.Phase = AudioPhase.Playing;
            played++;
            return new AudioEvent
            {
                IsBgm = false,
                Act = AudioAct.Play,
                Ch = ChOf(index),
                Gen = c.Gen,
                Track = track,
                Repeat = c.Repeat,
                Volume = c.Volume,
                Speed = 1.0,
                PreservePitch = c.PreservePitch,
            };
        }
    }

    /// <summary>PLAYBGM：gen+1→loop=true→发 play（speed 重置 1.0，volume/preserve 持久）。</summary>
    public AudioEvent PlayBgm(string track)
    {
        lock (gate)
        {
            var c = channels[0];
            c.Gen++;
            ResetForPlay(c);
            c.Track = track;
            c.Loop = true;
            c.Repeat = 1;
            c.Phase = AudioPhase.Playing;
            played++;
            return new AudioEvent
            {
                IsBgm = true,
                Act = AudioAct.Play,
                Ch = -1,
                Gen = c.Gen,
                Track = track,
                Loop = true,
                Volume = c.Volume,
                Speed = 1.0,
                PreservePitch = c.PreservePitch,
            };
        }
    }

    /// <summary>STOPSOUND：只停 Playing（deferred 视为 Playing）；Paused 不动。无 Playing 不发。</summary>
    public AudioEvent StopAllSe()
    {
        lock (gate)
        {
            bool any = false;
            for (int i = 1; i < ChannelCount; i++)
            {
                var c = channels[i];
                if (c.Phase != AudioPhase.Playing)
                    continue;
                c.Gen++;
                ResetForStop(c);
                any = true;
            }
            if (!any)
                return null;
            stopped++;
            return new AudioEvent { IsBgm = false, Act = AudioAct.StopAll };
        }
    }

    /// <summary>STOPBGM：Playing/Paused → Idle；空 stop 不发。</summary>
    public AudioEvent StopBgm()
    {
        lock (gate)
        {
            var c = channels[0];
            if (c.Phase != AudioPhase.Playing && c.Phase != AudioPhase.Paused)
                return null;
            c.Gen++;
            ResetForStop(c);
            stopped++;
            return new AudioEvent { IsBgm = true, Act = AudioAct.Stop, Ch = -1, Gen = c.Gen };
        }
    }

    /// <summary>SOUNDCONTROL(ch,2)：Playing/Paused → Idle；空 stop 不发。</summary>
    public AudioEvent StopSe(int ch)
    {
        lock (gate)
        {
            var c = ChannelAt(ch);
            if (c == null || (c.Phase != AudioPhase.Playing && c.Phase != AudioPhase.Paused))
                return null;
            c.Gen++;
            ResetForStop(c);
            stopped++;
            return new AudioEvent { IsBgm = false, Act = AudioAct.Stop, Ch = ch, Gen = c.Gen };
        }
    }

    /// <summary>pause：仅 Playing；deferred 记录暂停意图（保持乐观 Playing）。</summary>
    public AudioEvent Pause(int ch)
    {
        lock (gate)
        {
            var c = ChannelAt(ch);
            if (c == null || c.Phase != AudioPhase.Playing)
                return null;
            if (c.Deferred)
            {
                c.DeferredPaused = true;
            }
            else
            {
                c.PosBaseMs = CurrentPosNoLock(c, c.PosBaseMs);
                c.PosAtTickMs = Now();
                c.Phase = AudioPhase.Paused;
            }
            return BuildActEvent(c, ch, AudioAct.Pause);
        }
    }

    /// <summary>resume：仅 Paused（或 deferred 暂停意图）。</summary>
    public AudioEvent Resume(int ch)
    {
        lock (gate)
        {
            var c = ChannelAt(ch);
            if (c == null)
                return null;
            if (c.Phase == AudioPhase.Paused)
            {
                c.PosAtTickMs = Now();
                c.Phase = AudioPhase.Playing;
            }
            else if (c.Phase == AudioPhase.Playing && c.Deferred && c.DeferredPaused)
            {
                c.DeferredPaused = false;
            }
            else
            {
                return null;
            }
            return BuildActEvent(c, ch, AudioAct.Resume);
        }
    }

    /// <summary>speed：有效通道（Playing/Paused）才发；deferred 记录意图参数。不改 gen。</summary>
    public AudioEvent Speed(int ch, double speed, bool preservePitch)
    {
        lock (gate)
        {
            var c = ChannelAt(ch);
            if (c == null || (c.Phase != AudioPhase.Playing && c.Phase != AudioPhase.Paused))
                return null;
            double s = ClampSpeed(speed);
            c.Speed = s;
            c.PreservePitch = preservePitch;
            if (c.Deferred)
            {
                c.DeferredSpeed = s;
                c.DeferredPreserve = preservePitch;
            }
            var ev = BuildActEvent(c, ch, AudioAct.Speed);
            ev.Speed = s;
            ev.PreservePitch = preservePitch;
            return ev;
        }
    }

    /// <summary>SETSOUNDVOLUME：10 条 SE 通道统一音量；无 ch/gen。</summary>
    public AudioEvent SetSoundVolume(int volume)
    {
        lock (gate)
        {
            int v = (int)ClampVolume(volume);
            for (int i = 1; i < ChannelCount; i++)
                channels[i].Volume = v;
            return new AudioEvent { IsBgm = false, Act = AudioAct.Volume, Volume = v };
        }
    }

    /// <summary>SETBGMVOLUME：BGM 音量；带 gen。</summary>
    public AudioEvent SetBgmVolume(int volume)
    {
        lock (gate)
        {
            var c = channels[0];
            c.Volume = (int)ClampVolume(volume);
            return new AudioEvent
            {
                IsBgm = true,
                Act = AudioAct.Volume,
                Ch = -1,
                Gen = c.Gen,
                Volume = c.Volume,
            };
        }
    }

    private AudioEvent BuildActEvent(Channel c, int ch, AudioAct act)
        => new()
        {
            IsBgm = ch == -1,
            Act = act,
            Ch = ch,
            Gen = c.Gen,
        };

    // ---------------- FE 回供（传输线程；spec §3） ----------------

    public void ApplyState(int ch, long gen, string state, long posMs, long durMs, bool hasDur,
        string trackEcho, int pitchPreserved /* -1 unknown */, string code)
    {
        lock (gate)
        {
            var c = ChannelAt(ch);
            if (c == null) { badReports++; return; }
            if (gen != c.Gen) { staleReports++; return; }
            // Ended/Failed 为终态：同 gen 迟到报告忽略、不计数（spec §3.3.2/§2.6.5）
            if (c.Phase == AudioPhase.Ended || c.Phase == AudioPhase.Failed)
                return;
            if (c.Phase == AudioPhase.Idle)
                return;
            long now = Now();
            if (hasDur && durMs > 0 && c.L1DurMs <= 0)
                c.L1DurMs = durMs; // 0＝未知；L1 仅在 >0 时覆盖 L2（B-02）
            if (!string.IsNullOrEmpty(trackEcho) && !string.Equals(trackEcho, c.Track, StringComparison.Ordinal))
                trackMismatch++;
            if (pitchPreserved >= 0)
            {
                c.PitchKnown = true;
                c.PitchValue = pitchPreserved == 1;
            }
            switch (state)
            {
                case "playing":
                    c.Phase = AudioPhase.Playing;
                    c.Deferred = false;
                    c.DeferredPaused = false;
                    c.LastCode = null;
                    c.PosBaseMs = posMs;
                    c.PosAtTickMs = now;
                    c.LastReportTickMs = now;
                    break;
                case "paused":
                    c.Phase = AudioPhase.Paused;
                    c.Deferred = false;
                    c.DeferredPaused = false;
                    c.LastCode = null;
                    c.PosBaseMs = posMs;
                    c.PosAtTickMs = now;
                    c.LastReportTickMs = now;
                    break;
                case "ended":
                    c.Phase = AudioPhase.Ended;
                    c.Deferred = false;
                    c.DeferredPaused = false;
                    c.LastCode = null;
                    c.PosBaseMs = posMs;
                    c.PosAtTickMs = now;
                    c.LastReportTickMs = now;
                    break;
                case "deferred":
                    // 非终态：保持乐观 Playing；同 ch 新 gen 才作废（spec §3.4）
                    if (c.Phase == AudioPhase.Playing)
                    {
                        c.Deferred = true;
                        c.DeferredPaused = false;
                        c.LastCode = code ?? "autoplay_blocked";
                        c.PosBaseMs = posMs;
                        c.PosAtTickMs = now;
                        c.LastReportTickMs = now;
                    }
                    break;
            }
            c.QueryMode = "fe";
        }
    }

    public void ApplyFail(int ch, long gen, string code, string trackEcho)
    {
        lock (gate)
        {
            var c = ChannelAt(ch);
            if (c == null) { badReports++; return; }
            if (gen != c.Gen) { staleReports++; return; }
            if (c.Phase == AudioPhase.Ended || c.Phase == AudioPhase.Failed)
                return;
            if (c.Phase == AudioPhase.Idle)
                return;
            if (!string.IsNullOrEmpty(trackEcho) && !string.Equals(trackEcho, c.Track, StringComparison.Ordinal))
                trackMismatch++;
            c.Phase = AudioPhase.Failed;
            c.Deferred = false;
            c.DeferredPaused = false;
            c.LastCode = code;
            c.PosBaseMs = 0;
            c.PosAtTickMs = Now();
            c.LastReportTickMs = Now();
            failed++;
            c.QueryMode = "fe";
        }
    }

    // ---------------- 脚本查询（脚本线程；spec §4） ----------------

    private void EnsureProbe(int ch)
    {
        string track;
        long gen;
        lock (gate)
        {
            var c = ChannelAt(ch);
            if (c == null || c.Track == null || c.ProbeState != 0)
                return;
            if (c.Phase != AudioPhase.Playing && c.Phase != AudioPhase.Paused)
                return;
            track = c.Track;
            gen = c.Gen;
        }
        long dur = 0;
        try { dur = AudioProbe.ProbeTrack(track); } catch { dur = 0; }
        lock (gate)
        {
            var c = ChannelAt(ch);
            if (c == null || c.Gen != gen || c.ProbeState != 0)
                return;
            if (dur > 0)
            {
                c.ProbeDurMs = dur;
                c.ProbeState = 1;
            }
            else
            {
                c.ProbeState = -1;
            }
        }
    }

    private long CurrentPosNoLock(Channel c, long baseMs)
    {
        long pos = baseMs;
        if (c.Phase == AudioPhase.Playing && !c.Deferred)
            pos += (long)((Now() - c.PosAtTickMs) * c.Speed);
        return pos < 0 ? 0 : pos;
    }

    private static long CurrentPosAt(Channel c, long now)
    {
        long pos = c.PosBaseMs;
        if (c.Phase == AudioPhase.Playing && !c.Deferred)
            pos += (long)((now - c.PosAtTickMs) * c.Speed);
        return pos < 0 ? 0 : pos;
    }

    private static long ClampPos(long pos, long dur)
    {
        if (pos < 0) return 0;
        if (dur > 0 && pos > dur) return dur;
        return pos;
    }

    /// <summary>无 L1 时用 L2 判自然播完（spec §4.3；⛔ loop/多 repeat/无探针时不猜）。</summary>
    private void EvaluateEnded(Channel c)
    {
        if (c.Phase != AudioPhase.Playing || c.Deferred || c.Loop || c.Repeat > 1)
            return;
        if (c.LastReportTickMs > 0) // 有 L1 时以 FE 为准
            return;
        if (c.ProbeState != 1 || c.ProbeDurMs <= 0)
            return;
        long now = Now();
        long pos = CurrentPosAt(c, now);
        if (pos >= c.ProbeDurMs)
        {
            c.Phase = AudioPhase.Ended;
            c.PosBaseMs = c.ProbeDurMs;
            c.PosAtTickMs = now;
        }
    }

    private string ModeFor(Channel c, bool hasL1, bool probeOk, bool countFallback)
    {
        if (c.Phase == AudioPhase.Idle || c.Track == null)
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

    /// <summary>
    /// GETSOUNDORBGMINFO。infoType：-1＝省略第二参（写 RESULT_ARRAY[0..4] 并返回总长），
    /// 1 总长 / 2 当前 / 3 播放中 / 4 音量 / 5 速率百分比。
    /// 非法 ch 返回 0（array=null）。
    /// </summary>
    public long QueryInfo(int ch, int infoType, out long[] array, out string mode)
    {
        array = null;
        var c = ChannelAt(ch);
        if (c == null)
        {
            mode = "unknown";
            return 0;
        }
        EnsureProbe(ch);
        lock (gate)
        {
            EvaluateEnded(c);
            bool hasL1 = c.LastReportTickMs > 0;
            bool probeOk = c.ProbeState == 1;
            long l2 = probeOk ? c.ProbeDurMs : 0;
            long dur = hasL1 && c.L1DurMs > 0 ? c.L1DurMs : l2;
            long now = Now();
            long pos = ClampPos(CurrentPosAt(c, now), dur);
            bool idle = c.Phase == AudioPhase.Idle || c.Track == null;
            if (infoType == 1 || infoType == 2 || infoType < 0)
            {
                // dur/pos 层序：L1(>0) ＞ L2 ＞ 0（B-02：L1 的 0 不覆盖 L2）
                if (idle) { mode = "unknown"; queryFallback++; }
                else if (hasL1 && c.L1DurMs > 0) mode = "fe";
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
                // 4/5（音量/速率）权威＝账本，不需要 L1/L2，也不计 queryFallback
                mode = idle ? "unknown" : hasL1 ? "fe" : probeOk ? "probe" : "intent";
            }
            c.QueryMode = mode;
            lastQueryMode = mode;
            long playing = c.Phase == AudioPhase.Playing ? 1 : 0;
            long speedPct = (long)Math.Round(ClampSpeed(c.Speed) * 100.0);
            switch (infoType)
            {
                case 1: return dur;
                case 2: return pos;
                case 3: return playing;
                case 4: return c.Volume;
                case 5: return speedPct;
                default:
                    array = new[] { dur, pos, playing, (long)c.Volume, speedPct };
                    return dur;
            }
        }
    }

    /// <summary>ISPLAYINGSOUND：在播返回 ch，否则 -1；非法 ch 返回 -1。</summary>
    public long QueryIsPlayingSound(int ch, out string mode)
    {
        var c = ChannelAt(ch);
        if (c == null)
        {
            mode = "unknown";
            return -1;
        }
        EnsureProbe(ch);
        lock (gate)
        {
            EvaluateEnded(c);
            bool hasL1 = c.LastReportTickMs > 0;
            bool probeOk = c.ProbeState == 1;
            mode = ModeFor(c, hasL1, probeOk, true);
            if (c.Phase == AudioPhase.Idle || c.Track == null)
                mode = "unknown";
            c.QueryMode = mode;
            lastQueryMode = mode;
            return c.Phase == AudioPhase.Playing ? ch : -1;
        }
    }

    /// <summary>ISPLAYINGBGM：1/0（Paused/Idle/Ended/Failed 均 0）。</summary>
    public long QueryIsPlayingBgm(out string mode)
    {
        EnsureProbe(-1);
        lock (gate)
        {
            var c = channels[0];
            EvaluateEnded(c);
            bool hasL1 = c.LastReportTickMs > 0;
            bool probeOk = c.ProbeState == 1;
            mode = ModeFor(c, hasL1, probeOk, true);
            c.QueryMode = mode;
            lastQueryMode = mode;
            return c.Phase == AudioPhase.Playing ? 1 : 0;
        }
    }
}
