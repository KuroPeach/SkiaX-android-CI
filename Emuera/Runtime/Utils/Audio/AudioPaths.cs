using System;
using System.IO;
using System.Text;

// R-AUD-4：audio.track 的安全相对名与 containment（EWP audio 增补草案 v0.2 §6）。
// 纯函数、无 IO（Parse 阶段），Resolve 只做字符串规范化（不发系统调用）。
// ⚠ 分层：Runtime 层，⛔ 不得引用 UI/Ewp。
namespace MinorShift.Emuera.Runtime.Utils.Audio;

internal static class AudioPaths
{
    /// <summary>track 最大字节数（UTF-8；spec §6.1）。</summary>
    public const int MaxBytes = 512;
    /// <summary>最大段数（含文件名；spec §6.1）。</summary>
    public const int MaxSegments = 32;

    /// <summary>
    /// ERB 请求名 → 安全相对名（A→B 形）。拒绝面严格按 spec §6.1/§6.2：
    /// 空串、绝对路径/盘符/UNC、任何 "://"，控制字符、'.'/'..' 段、空段（"//"）、
    /// 前导/结尾 '/'、UTF-8 &gt;512 B、段数 &gt;32。分隔符 '\' 统一为 '/'；保留请求大小写。
    /// </summary>
    public static bool TryNormalize(string request, out string track)
    {
        track = null;
        if (string.IsNullOrEmpty(request))
            return false;
        // 控制字符（含 NUL/CR/LF/TAB）与超长先拒绝
        for (int i = 0; i < request.Length; i++)
        {
            char c = request[i];
            if (c < 0x20 || c == 0x7F)
                return false;
        }
        if (Encoding.UTF8.GetByteCount(request) > MaxBytes)
            return false;
        string s = request.Replace('\\', '/');
        if (s.IndexOf("://", StringComparison.Ordinal) >= 0)
            return false;
        if (s.IndexOf(':') >= 0) // 盘符/流名；Windows 文件名本身也不允许 ':'
            return false;
        if (s[0] == '/') // 覆盖绝对路径与 UNC（"//server"）
            return false;
        if (s[s.Length - 1] == '/')
            return false;
        var segments = s.Split('/');
        if (segments.Length > MaxSegments)
            return false;
        foreach (var seg in segments)
        {
            if (seg.Length == 0) // "a//b"
                return false;
            if (seg == "." || seg == "..")
                return false;
        }
        track = s;
        return true;
    }

    /// <summary>
    /// 把安全相对名解析到 root 下，并做前缀 containment。
    /// Windows 用 OrdinalIgnoreCase（文件系统语义），其他平台 Ordinal（spec §6.2.3）。
    /// 解析失败（非法字符/抛异常）返回 false。
    /// </summary>
    public static bool TryResolveUnderRoot(string rootDir, string track, out string fullPath)
    {
        fullPath = null;
        if (string.IsNullOrEmpty(rootDir) || string.IsNullOrEmpty(track))
            return false;
        string candidate;
        try
        {
            string root = Path.GetFullPath(rootDir);
            string rel = track.Replace('/', Path.DirectorySeparatorChar);
            candidate = Path.GetFullPath(Path.Combine(root, rel));
        }
        catch
        {
            return false;
        }
        if (!IsUnderRoot(rootDir, candidate))
            return false;
        fullPath = candidate;
        return true;
    }

    /// <summary>candidate 是否严格位于 rootDir 内（不含 root 自身）。</summary>
    public static bool IsUnderRoot(string rootDir, string fullPath)
    {
        if (string.IsNullOrEmpty(rootDir) || string.IsNullOrEmpty(fullPath))
            return false;
        string root;
        try { root = Path.GetFullPath(rootDir); }
        catch { return false; }
        string rootWithSep = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        var cmp = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (fullPath.Length <= rootWithSep.Length)
            return false;
        return fullPath.StartsWith(rootWithSep, cmp);
    }

    /// <summary>绝对路径 → 相对 root 的 '/' 分隔名；不在 root 内返回 false。</summary>
    public static bool TryMakeRelative(string rootDir, string fullPath, out string track)
    {
        track = null;
        if (!IsUnderRoot(rootDir, fullPath))
            return false;
        string root;
        try { root = Path.GetFullPath(rootDir); } catch { return false; }
        string rel = fullPath.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        track = rel.Replace('\\', '/');
        return track.Length > 0;
    }
}
