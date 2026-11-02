using System;
using System.IO;
using System.Text;

// R-AUD-4：只读时长探针（L2 权威链；EWP audio 增补草案 v0.2 §4.1/§4.3）。
// 只读文件头/少量字节，**不解码**、不构造 Sound、不依赖平台音频后端。
// 当前覆盖：MP3（帧头 + Xing/Info 帧数；无 Xing 时按首帧码率做 CBR 估算）、WAV（RIFF fmt/data）。
// OGG granule / FLAC 等未实现 → 返回 0（调用方回落 L3 intent，并计 queryFallback）。
// ⚠ 分层：Runtime 层，⛔ 不得引用 UI/Ewp。
namespace MinorShift.Emuera.Runtime.Utils.Audio;

internal static class AudioProbe
{
    /// <summary>探测总长（毫秒）；无法判定返回 0。绝不抛异常。</summary>
    public static long ProbeDurationMs(string fullPath)
    {
        if (string.IsNullOrEmpty(fullPath))
            return 0;
        try
        {
            using var fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            long len = fs.Length;
            if (len < 12)
                return 0;
            var head = new byte[12];
            if (fs.Read(head, 0, 12) != 12)
                return 0;
            if (head[0] == 'R' && head[1] == 'I' && head[2] == 'F' && head[3] == 'F'
                && head[8] == 'W' && head[9] == 'A' && head[10] == 'V' && head[11] == 'E')
                return ProbeWav(fs, len);
            return ProbeMp3(fs, len);
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>track（soundRoot 相对名）→ 实际文件（允许大小写折叠）→ 时长；不可读返回 0。</summary>
    public static long ProbeTrack(string track)
    {
        if (!AudioPaths.TryResolveUnderRoot(Program.MusicDir, track, out var path))
            return 0;
        string probePath = path;
        if (!Program.FileExists(ref probePath))
            return 0;
        return ProbeDurationMs(probePath);
    }

    // ---------------- MP3 ----------------

    private static readonly int[] BitrateV1L1 = { 0, 32, 64, 96, 128, 160, 192, 224, 256, 288, 320, 352, 384, 416, 448 };
    private static readonly int[] BitrateV1L2 = { 0, 32, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 384 };
    private static readonly int[] BitrateV1L3 = { 0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320 };
    private static readonly int[] BitrateV2L1 = { 0, 32, 48, 56, 64, 80, 96, 112, 128, 144, 160, 176, 192, 224, 256 };
    private static readonly int[] BitrateV2L2 = { 0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160 };
    private static readonly int[] SampleRatesV1 = { 44100, 48000, 32000 };
    private static readonly int[] SampleRatesV2 = { 22050, 24000, 16000 };
    private static readonly int[] SampleRatesV25 = { 11025, 12000, 8000 };

    private static long ProbeMp3(FileStream fs, long fileLength)
    {
        long audioStart = 0;
        // ID3v2 header: "ID3" + ver(2) + flags(1) + syncsafe size(4)
        if (fileLength >= 10)
        {
            var id3 = new byte[10];
            fs.Position = 0;
            if (fs.Read(id3, 0, 10) == 10 && id3[0] == 'I' && id3[1] == 'D' && id3[2] == '3')
            {
                long size = ((long)(id3[6] & 0x7F) << 21) | ((long)(id3[7] & 0x7F) << 14)
                    | ((long)(id3[8] & 0x7F) << 7) | (long)(id3[9] & 0x7F);
                audioStart = 10 + size;
                if ((id3[5] & 0x10) != 0) // footer present
                    audioStart += 10;
                if (audioStart >= fileLength)
                    return 0;
            }
        }

        // 扫描第一个合法帧同步（最多 128 KiB）
        long scanLimit = Math.Min(fileLength - 4, audioStart + 131072);
        var buf = new byte[4096];
        long pos = audioStart;
        long frameStart = -1;
        int h0 = -1, h1 = -1, h2 = -1, h3 = -1;
        while (pos + 4 <= scanLimit)
        {
            fs.Position = pos;
            int n = fs.Read(buf, 0, buf.Length);
            if (n < 4)
                break;
            for (int i = 0; i + 4 <= n; i++)
            {
                if (buf[i] != 0xFF || (buf[i + 1] & 0xE0) != 0xE0)
                    continue;
                int b1 = buf[i + 1], b2 = buf[i + 2], b3 = buf[i + 3];
                int version = (b1 >> 3) & 3;
                int layerBits = (b1 >> 1) & 3;
                int brIndex = (b2 >> 4) & 0xF;
                int srIndex = (b2 >> 2) & 3;
                if (version == 1 || layerBits == 0 || brIndex == 0 || brIndex == 15 || srIndex == 3)
                    continue;
                frameStart = pos + i;
                h0 = buf[i]; h1 = b1; h2 = b2; h3 = b3;
                break;
            }
            if (frameStart >= 0)
                break;
            pos += n - 3;
        }
        if (frameStart < 0)
            return 0;

        int mpegVersion = (h1 >> 3) & 3; // 3=MPEG1, 2=MPEG2, 0=MPEG2.5
        int layer = 4 - ((h1 >> 1) & 3); // 1..3（LayerI/II/III）
        int brIdx = (h2 >> 4) & 0xF;
        int srIdx = (h2 >> 2) & 3;
        bool padding = (h2 & 0x2) != 0;
        int channelMode = (h3 >> 6) & 3; // 3 = mono
        bool mono = channelMode == 3;

        int[] bitrates = mpegVersion == 3
            ? (layer == 1 ? BitrateV1L1 : layer == 2 ? BitrateV1L2 : BitrateV1L3)
            : (layer == 1 ? BitrateV2L1 : BitrateV2L2);
        int bitrateKbps = bitrates[brIdx];
        int[] rates = mpegVersion == 3 ? SampleRatesV1 : mpegVersion == 2 ? SampleRatesV2 : SampleRatesV25;
        int sampleRate = rates[srIdx];
        if (bitrateKbps <= 0 || sampleRate <= 0)
            return 0;

        int samplesPerFrame = layer == 1 ? 384 : layer == 2 ? 1152 : (mpegVersion == 3 ? 1152 : 576);

        // Xing/Info 头（帧头 + side info）
        int sideInfo = mpegVersion == 3 ? (mono ? 17 : 32) : (mono ? 9 : 17);
        long xingPos = frameStart + 4 + sideInfo;
        if (xingPos + 12 <= fileLength)
        {
            var tag = new byte[12];
            fs.Position = xingPos;
            if (fs.Read(tag, 0, 12) == 12)
            {
                bool xing = tag[0] == 'X' && tag[1] == 'i' && tag[2] == 'n' && tag[3] == 'g';
                bool info = tag[0] == 'I' && tag[1] == 'n' && tag[2] == 'f' && tag[3] == 'o';
                if (xing || info)
                {
                    long flags = ((long)tag[4] << 24) | ((long)tag[5] << 16) | ((long)tag[6] << 8) | tag[7];
                    if ((flags & 1) != 0)
                    {
                        long frames = ((long)tag[8] << 24) | ((long)tag[9] << 16) | ((long)tag[10] << 8) | tag[11];
                        if (frames > 0)
                            return (long)Math.Round(frames * (double)samplesPerFrame / sampleRate * 1000.0);
                    }
                }
            }
        }

        // 无 Xing：按首帧码率做 CBR 估算（VBR 无 Xing 时为近似值，属 L2 探针口径）
        long audioBytes = fileLength - frameStart;
        if (audioBytes <= 0)
            return 0;
        // 跳过 padding 不影响估算精度
        _ = padding;
        double seconds = audioBytes * 8.0 / (bitrateKbps * 1000.0);
        return (long)Math.Round(seconds * 1000.0);
    }

    // ---------------- WAV ----------------

    private static long ProbeWav(FileStream fs, long fileLength)
    {
        long pos = 12;
        int byteRate = 0;
        long dataSize = -1;
        var hdr = new byte[8];
        int guard = 0;
        while (pos + 8 <= fileLength && guard++ < 64)
        {
            fs.Position = pos;
            if (fs.Read(hdr, 0, 8) != 8)
                break;
            long size = hdr[4] | ((long)hdr[5] << 8) | ((long)hdr[6] << 16) | ((long)hdr[7] << 24);
            bool isFmt = hdr[0] == 'f' && hdr[1] == 'm' && hdr[2] == 't' && hdr[3] == ' ';
            bool isData = hdr[0] == 'd' && hdr[1] == 'a' && hdr[2] == 't' && hdr[3] == 'a';
            if (isFmt && size >= 16)
            {
                var fmt = new byte[16];
                if (fs.Read(fmt, 0, 16) == 16)
                    byteRate = fmt[8] | (fmt[9] << 8) | (fmt[10] << 16) | (fmt[11] << 24);
            }
            else if (isData)
            {
                dataSize = Math.Min(size, fileLength - pos - 8);
            }
            if (isFmt || isData)
            {
                if (dataSize >= 0 && byteRate > 0)
                    return (long)Math.Round(dataSize * 1000.0 / byteRate);
            }
            pos += 8 + size + (size & 1);
        }
        if (dataSize >= 0 && byteRate > 0)
            return (long)Math.Round(dataSize * 1000.0 / byteRate);
        return 0;
    }
}
