using System;
using System.IO;
using MinorShift.Emuera.Runtime.Script;

// R-AUD-4：指令层/方法层的音频语义汇聚点。
// 每条方法返回「是否已由 audio 路径接管」：
//   false ⇒ 调用方继续走原生实现（cap 关闭 / 非 EWP / 旧 FE）——行为逐字节不变；
//   true  ⇒ 已处理：VM 静音本机（Q1），只更新账本并（在锁外）经 AudioHooks 发事件。
// ⚠ 分层：Runtime 层。
namespace MinorShift.Emuera.Runtime.Utils.Audio;

internal static class AudioIntents
{
    private static AudioLedger Active => AudioHooks.Enabled ? AudioHooks.Ledger : null;

    // ---------------- 指令 ----------------

    /// <summary>PLAYSOUND。存在性守卫（Program.FileExists，大小写不敏感）→ 缺失静默；路径拒绝 fail-closed。</summary>
    public static bool PlaySound(string request, int repeat)
    {
        var l = Active;
        if (l == null)
            return false;
        l.NoteNativeSuppressed();
        if (!AudioPaths.TryNormalize(request, out var track))
        {
            l.NoteUnsafePath();
            return true;
        }
        string filepath = Program.MusicDir + request;
        if (!Program.FileExists(ref filepath))
        {
            l.NoteMissing();
            return true;
        }
        if (!AudioPaths.IsUnderRoot(Program.MusicDir, filepath))
        {
            l.NoteUnsafePath();
            return true;
        }
        AudioHooks.Emit(l.PlaySe(track, repeat));
        return true;
    }

    /// <summary>PLAYBGM。守卫与原生同形（CWD 基准 ".\\sound\\" + 精确 File.Exists）；
    /// 解析后越出 MusicDir ⇒ Q10 fail-closed（本地也不播、不发事件）。</summary>
    public static bool PlayBgm(string request)
    {
        var l = Active;
        if (l == null)
            return false;
        l.NoteNativeSuppressed();
        if (!AudioPaths.TryNormalize(request, out var track))
        {
            l.NoteUnsafePath();
            return true;
        }
        string filepath;
        try
        {
            filepath = Path.GetFullPath(".\\sound\\" + request);
        }
        catch
        {
            l.NoteMissing();
            return true;
        }
        if (!File.Exists(filepath))
        {
            l.NoteMissing();
            return true;
        }
        if (!AudioPaths.IsUnderRoot(Program.MusicDir, filepath))
        {
            l.NoteUnsafePath();
            return true;
        }
        AudioHooks.Emit(l.PlayBgm(track));
        return true;
    }

    /// <summary>STOPSOUND：镜像引擎（只停在播；Paused 不动）；无在播不发。</summary>
    public static bool StopSound()
    {
        var l = Active;
        if (l == null)
            return false;
        l.NoteNativeSuppressed();
        AudioHooks.Emit(l.StopAllSe());
        return true;
    }

    /// <summary>STOPBGM：Playing/Paused → Idle；空 stop 不发。</summary>
    public static bool StopBgm()
    {
        var l = Active;
        if (l == null)
            return false;
        l.NoteNativeSuppressed();
        AudioHooks.Emit(l.StopBgm());
        return true;
    }

    /// <summary>SETSOUNDVOLUME：10 条 SE 统一音量；无 ch/gen。</summary>
    public static bool SetSoundVolume(int volume)
    {
        var l = Active;
        if (l == null)
            return false;
        l.NoteNativeSuppressed();
        AudioHooks.Emit(l.SetSoundVolume(volume));
        return true;
    }

    /// <summary>SETBGMVOLUME：BGM 音量；带 gen。</summary>
    public static bool SetBgmVolume(int volume)
    {
        var l = Active;
        if (l == null)
            return false;
        l.NoteNativeSuppressed();
        AudioHooks.Emit(l.SetBgmVolume(volume));
        return true;
    }

    /// <summary>SOUNDCONTROL(ch, action[, speed%, pitchFlag])；返回码与原生一致（1/-1/-2）。</summary>
    public static bool SoundControl(int ch, int action, bool hasExtra, int speedPercent,
        int pitchFlag, out long code)
    {
        code = 0;
        var l = Active;
        if (l == null)
            return false;
        if (ch < 0 || ch >= 10)
        {
            code = -1;
            return true;
        }
        l.NoteNativeSuppressed();
        if (!hasExtra)
        {
            switch (action)
            {
                case 0:
                    AudioHooks.Emit(l.Pause(ch));
                    code = 1;
                    break;
                case 1:
                    AudioHooks.Emit(l.Resume(ch));
                    code = 1;
                    break;
                case 2:
                    AudioHooks.Emit(l.StopSe(ch));
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
            // 引擎第 4 参取反：0=保持音调、非 0=改变音调（spec §2.2；事件用正语义）
            bool preservePitch = pitchFlag == 0;
            AudioHooks.Emit(l.Speed(ch, speedPercent / 100.0, preservePitch));
            code = 1;
        }
        return true;
    }

    /// <summary>BGMCONTROL(action[, speed%, pitchFlag])；返回码 1/-2（无 -1）。</summary>
    public static bool BgmControl(int action, bool hasExtra, int speedPercent,
        int pitchFlag, out long code)
    {
        code = 0;
        var l = Active;
        if (l == null)
            return false;
        l.NoteNativeSuppressed();
        if (!hasExtra)
        {
            switch (action)
            {
                case 0:
                    AudioHooks.Emit(l.Pause(-1));
                    code = 1;
                    break;
                case 1:
                    AudioHooks.Emit(l.Resume(-1));
                    code = 1;
                    break;
                case 2:
                    AudioHooks.Emit(l.StopBgm());
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
            // 引擎第 3 参取反（spec §2.2 / C-03）
            bool preservePitch = pitchFlag == 0;
            AudioHooks.Emit(l.Speed(-1, speedPercent / 100.0, preservePitch));
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
        var l = Active;
        if (l == null)
            return false;
        value = l.QueryInfo(ch, infoType, out array, out _);
        return true;
    }

    public static bool IsPlayingSound(int ch, out long value)
    {
        value = -1;
        var l = Active;
        if (l == null)
            return false;
        value = l.QueryIsPlayingSound(ch, out _);
        return true;
    }

    public static bool IsPlayingBgm(out long value)
    {
        value = 0;
        var l = Active;
        if (l == null)
            return false;
        value = l.QueryIsPlayingBgm(out _);
        return true;
    }
}
