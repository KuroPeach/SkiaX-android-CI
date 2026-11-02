using System;
using System.IO;
using MinorShift.Emuera.Runtime.Script;

// R-AUD-4/R-AUD-8：指令层/方法层的音频语义汇聚点。
// 每条方法返回「是否已由 audio 路径接管」：
//   false ⇒ 调用方继续走原生实现（cap 关闭 / 非 EWP / 旧 FE）——行为逐字节不变；
//   true  ⇒ 已处理：VM 静音本机（Q1），只更新账本并（在锁外）经 AudioHooks 发事件。
// R-AUD-8：v1 模式（audio.instances）下旧 6 指令/5 方法映射为 v1 事件（spec §7.2）；
// 握手前（Mode=None）照常走原生，同时把意图记进 v1 shadow（S1.5 晚连接重建）。
// ⚠ 分层：Runtime 层。
namespace MinorShift.Emuera.Runtime.Utils.Audio;

internal static class AudioIntents
{
    private static AudioLedger Active => AudioHooks.Enabled ? AudioHooks.Ledger : null;
    private static AudioMode Mode => AudioHooks.Mode;
    private static AudioV1Ledger V1 => AudioHooks.V1;

    private static void NoteMissing(AudioLedger v02, AudioV1Ledger v1)
    {
        if (Mode == AudioMode.V1 || Mode == AudioMode.None) v1?.NoteMissing();
        else v02?.NoteMissing();
    }

    private static void NoteUnsafePath(AudioLedger v02, AudioV1Ledger v1)
    {
        if (Mode == AudioMode.V1 || Mode == AudioMode.None) v1?.NoteUnsafePath();
        else v02?.NoteUnsafePath();
    }

    // ---------------- 指令 ----------------

    /// <summary>PLAYSOUND。存在性守卫（Program.FileExists，大小写不敏感）→ 缺失静默；路径拒绝 fail-closed。</summary>
    public static bool PlaySound(string request, int repeat)
    {
        var mode = Mode;
        var v02 = Active;
        var v1 = V1;
        if (mode == AudioMode.V1 && v1 == null)
            return false;
        if (mode == AudioMode.None && v1 == null)
            return false;
        if (mode == AudioMode.V02 && v02 == null)
            return false;
        if (mode == AudioMode.V1)
            v02?.NoteNativeSuppressed();
        else if (mode == AudioMode.V02)
            v02.NoteNativeSuppressed();
        if (!AudioPaths.TryNormalize(request, out var track))
        {
            NoteUnsafePath(v02, v1);
            return mode != AudioMode.None;
        }
        string filepath = Program.MusicDir + request;
        if (!Program.FileExists(ref filepath))
        {
            NoteMissing(v02, v1);
            return mode != AudioMode.None;
        }
        if (!AudioPaths.IsUnderRoot(Program.MusicDir, filepath))
        {
            NoteUnsafePath(v02, v1);
            return mode != AudioMode.None;
        }
        if (mode == AudioMode.V1)
        {
            AudioHooks.EmitV1(v1.LegacyPlaySe(track, repeat, emit: true));
            return true;
        }
        if (mode == AudioMode.None)
        {
            v1.LegacyPlaySe(track, repeat, emit: false);
            return false;
        }
        AudioHooks.Emit(v02.PlaySe(track, repeat));
        return true;
    }

    /// <summary>PLAYBGM。守卫与原生同形（CWD 基准 ".\\sound\\" + 精确 File.Exists）；
    /// 解析后越出 MusicDir ⇒ Q10 fail-closed（本地也不播、不发事件）。</summary>
    public static bool PlayBgm(string request)
    {
        var mode = Mode;
        var v02 = Active;
        var v1 = V1;
        if (mode == AudioMode.V1 && v1 == null)
            return false;
        if (mode == AudioMode.None && v1 == null)
            return false;
        if (mode == AudioMode.V02 && v02 == null)
            return false;
        if (mode == AudioMode.V1)
            v02?.NoteNativeSuppressed();
        else if (mode == AudioMode.V02)
            v02.NoteNativeSuppressed();
        if (!AudioPaths.TryNormalize(request, out var track))
        {
            NoteUnsafePath(v02, v1);
            return mode != AudioMode.None;
        }
        string filepath;
        try
        {
            filepath = Path.GetFullPath(".\\sound\\" + request);
        }
        catch
        {
            NoteMissing(v02, v1);
            return mode != AudioMode.None;
        }
        if (!File.Exists(filepath))
        {
            NoteMissing(v02, v1);
            return mode != AudioMode.None;
        }
        if (!AudioPaths.IsUnderRoot(Program.MusicDir, filepath))
        {
            NoteUnsafePath(v02, v1);
            return mode != AudioMode.None;
        }
        if (mode == AudioMode.V1)
        {
            AudioHooks.EmitV1(v1.LegacyPlayBgm(track, emit: true));
            return true;
        }
        if (mode == AudioMode.None)
        {
            v1.LegacyPlayBgm(track, emit: false);
            return false;
        }
        AudioHooks.Emit(v02.PlayBgm(track));
        return true;
    }

    /// <summary>STOPSOUND：镜像引擎（只停在播；Paused 不动）；无在播不发。</summary>
    public static bool StopSound()
    {
        var mode = Mode;
        var v02 = Active;
        var v1 = V1;
        if (mode == AudioMode.V1 && v1 != null)
        {
            v02?.NoteNativeSuppressed();
            AudioHooks.EmitV1(v1.LegacyStopAllSe(emit: true));
            return true;
        }
        if (mode == AudioMode.None && v1 != null)
        {
            v1.LegacyStopAllSe(emit: false);
            return false;
        }
        if (v02 == null)
            return false;
        v02.NoteNativeSuppressed();
        AudioHooks.Emit(v02.StopAllSe());
        return true;
    }

    /// <summary>STOPBGM：Playing/Paused → Idle；空 stop 不发。</summary>
    public static bool StopBgm()
    {
        var mode = Mode;
        var v02 = Active;
        var v1 = V1;
        if (mode == AudioMode.V1 && v1 != null)
        {
            v02?.NoteNativeSuppressed();
            AudioHooks.EmitV1(v1.LegacyStopBgm(emit: true));
            return true;
        }
        if (mode == AudioMode.None && v1 != null)
        {
            v1.LegacyStopBgm(emit: false);
            return false;
        }
        if (v02 == null)
            return false;
        v02.NoteNativeSuppressed();
        AudioHooks.Emit(v02.StopBgm());
        return true;
    }

    /// <summary>SETSOUNDVOLUME：10 条 SE 统一音量；无 ch/gen。</summary>
    public static bool SetSoundVolume(int volume)
    {
        var mode = Mode;
        var v02 = Active;
        var v1 = V1;
        if (mode == AudioMode.V1 && v1 != null)
        {
            v02?.NoteNativeSuppressed();
            AudioHooks.EmitV1(v1.LegacySetSoundVolume(volume, emit: true));
            return true;
        }
        if (mode == AudioMode.None && v1 != null)
        {
            v1.LegacySetSoundVolume(volume, emit: false);
            return false;
        }
        if (v02 == null)
            return false;
        v02.NoteNativeSuppressed();
        AudioHooks.Emit(v02.SetSoundVolume(volume));
        return true;
    }

    /// <summary>SETBGMVOLUME：BGM 音量；带 gen。</summary>
    public static bool SetBgmVolume(int volume)
    {
        var mode = Mode;
        var v02 = Active;
        var v1 = V1;
        if (mode == AudioMode.V1 && v1 != null)
        {
            v02?.NoteNativeSuppressed();
            AudioHooks.EmitV1(v1.LegacySetBgmVolume(volume, emit: true));
            return true;
        }
        if (mode == AudioMode.None && v1 != null)
        {
            v1.LegacySetBgmVolume(volume, emit: false);
            return false;
        }
        if (v02 == null)
            return false;
        v02.NoteNativeSuppressed();
        AudioHooks.Emit(v02.SetBgmVolume(volume));
        return true;
    }

    /// <summary>SOUNDCONTROL(ch, action[, speed%, pitchFlag])；返回码与原生一致（1/-1/-2）。</summary>
    public static bool SoundControl(int ch, int action, bool hasExtra, int speedPercent,
        int pitchFlag, out long code)
    {
        code = 0;
        var mode = Mode;
        var v02 = Active;
        var v1 = V1;
        if (mode == AudioMode.V1 && v1 != null)
        {
            v02?.NoteNativeSuppressed();
            AudioHooks.EmitV1(v1.LegacySeControl(ch, action, hasExtra, speedPercent,
                pitchFlag, emit: true, out code));
            return true;
        }
        if (mode == AudioMode.None && v1 != null)
        {
            v1.LegacySeControl(ch, action, hasExtra, speedPercent, pitchFlag,
                emit: false, out code);
            return false;
        }
        if (v02 == null)
            return false;
        if (ch < 0 || ch >= 10)
        {
            code = -1;
            return true;
        }
        v02.NoteNativeSuppressed();
        if (!hasExtra)
        {
            switch (action)
            {
                case 0:
                    AudioHooks.Emit(v02.Pause(ch));
                    code = 1;
                    break;
                case 1:
                    AudioHooks.Emit(v02.Resume(ch));
                    code = 1;
                    break;
                case 2:
                    AudioHooks.Emit(v02.StopSe(ch));
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
                return true;
            }
            bool preservePitch = pitchFlag == 0;
            AudioHooks.Emit(v02.Speed(ch, speedPercent / 100.0, preservePitch));
            code = 1;
        }
        return true;
    }

    /// <summary>BGMCONTROL(action[, speed%, pitchFlag])；返回码 1/-2（无 -1）。</summary>
    public static bool BgmControl(int action, bool hasExtra, int speedPercent,
        int pitchFlag, out long code)
    {
        code = 0;
        var mode = Mode;
        var v02 = Active;
        var v1 = V1;
        if (mode == AudioMode.V1 && v1 != null)
        {
            v02?.NoteNativeSuppressed();
            AudioHooks.EmitV1(v1.LegacyBgmControl(action, hasExtra, speedPercent,
                pitchFlag, emit: true, out code));
            return true;
        }
        if (mode == AudioMode.None && v1 != null)
        {
            v1.LegacyBgmControl(action, hasExtra, speedPercent, pitchFlag,
                emit: false, out code);
            return false;
        }
        if (v02 == null)
            return false;
        v02.NoteNativeSuppressed();
        if (!hasExtra)
        {
            switch (action)
            {
                case 0:
                    AudioHooks.Emit(v02.Pause(-1));
                    code = 1;
                    break;
                case 1:
                    AudioHooks.Emit(v02.Resume(-1));
                    code = 1;
                    break;
                case 2:
                    AudioHooks.Emit(v02.StopBgm());
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
                return true;
            }
            bool preservePitch = pitchFlag == 0;
            AudioHooks.Emit(v02.Speed(-1, speedPercent / 100.0, preservePitch));
            code = 1;
        }
        return true;
    }

    // ---------------- 方法 ----------------

    /// <summary>GETSOUNDORBGMINFO。infoType&lt;0 ⇒ 省略第二参（array 填 RESULT_ARRAY[0..4]）。</summary>
    public static bool QueryInfo(int ch, int infoType, out long value, out long[] array)
    {
        value = 0;
        array = null;
        var mode = Mode;
        if (mode == AudioMode.V1 && V1 != null)
        {
            value = V1.QueryLegacyInfo(ch, infoType, out array, out _);
            return true;
        }
        var l = Active;
        if (l == null)
            return false;
        value = l.QueryInfo(ch, infoType, out array, out _);
        return true;
    }

    public static bool IsPlayingSound(int ch, out long value)
    {
        value = -1;
        var mode = Mode;
        if (mode == AudioMode.V1 && V1 != null)
        {
            value = V1.QueryLegacyIsPlayingSound(ch, out _);
            return true;
        }
        var l = Active;
        if (l == null)
            return false;
        value = l.QueryIsPlayingSound(ch, out _);
        return true;
    }

    public static bool IsPlayingBgm(out long value)
    {
        value = 0;
        var mode = Mode;
        if (mode == AudioMode.V1 && V1 != null)
        {
            value = V1.QueryLegacyIsPlayingBgm(out _);
            return true;
        }
        var l = Active;
        if (l == null)
            return false;
        value = l.QueryIsPlayingBgm(out _);
        return true;
    }
}
