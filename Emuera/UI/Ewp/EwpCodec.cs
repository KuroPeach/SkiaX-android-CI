using System;
using System.IO;
using System.Text;
using System.Text.Json;

// eraWire B3: L0.5 编码抽象（EWP §2）。JSON-Lines＝换行定界（B1/B2 现行，哑前端/抓包零依赖）；
// MessagePack＝4 字节大端长度前缀帧。引擎不引 MessagePack 包依赖——本文件手写双方子集：
// 编码面＝JsonElement 递归（nil/bool/int/float/str/map/array）；解码面在解码器开头列全格式清单。
// pixel.frame 在 MsgPack 模式下直构 map（data 为 bin），不经 JSON 往返。
namespace MinorShift.Emuera.UI.Ewp;

/// <summary>pixel.frame 事件载荷（EwpPixelStreamer 编码完成后投递给 EwpOut）。</summary>
internal sealed class EwpPixelPayload
{
    public long FrameId;
    public int Width, Height;
    /// <summary>累计丢弃帧数（捕获队列＋写出队列两级，发送时刻快照）。</summary>
    public int Dropped;
    public int EncodeMs;
    public byte[] Png;
}

/// <summary>编码器：VM→FE 按事件写；FE→VM 经 CreateReader 按命令读（返回 JSON 文本喂 HandleLine）。</summary>
internal interface IEwpCodec
{
    void WriteText(Stream s, string json);
    void WritePixel(Stream s, EwpPixelPayload px);
    IEwpCommandReader CreateReader(Stream s);
}

internal interface IEwpCommandReader : IDisposable
{
    /// <summary>读一条命令的 JSON 文本；传输关闭返回 null。</summary>
    string Read();
}

internal sealed class EwpJsonCodec : IEwpCodec
{
    public void WriteText(Stream s, string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        s.Write(bytes, 0, bytes.Length);
        s.WriteByte((byte)'\n');
    }

    public void WritePixel(Stream s, EwpPixelPayload px)
    {
        var sb = new StringBuilder(96);
        sb.Append("{\"t\":\"pixel.frame\",\"frameId\":").Append(px.FrameId)
            .Append(",\"w\":").Append(px.Width)
            .Append(",\"h\":").Append(px.Height)
            .Append(",\"fmt\":\"png\",\"stats\":{\"dropped\":").Append(px.Dropped)
            .Append(",\"encMs\":").Append(px.EncodeMs)
            .Append("},\"data\":\"");
        var head = Encoding.UTF8.GetBytes(sb.ToString());
        s.Write(head, 0, head.Length);
        var b64 = Encoding.ASCII.GetBytes(Convert.ToBase64String(px.Png));
        s.Write(b64, 0, b64.Length);
        s.WriteByte((byte)'"');
        s.WriteByte((byte)'}');
        s.WriteByte((byte)'\n');
    }

    public IEwpCommandReader CreateReader(Stream s)
        => new LineReader(new StreamReader(s, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true));

    private sealed class LineReader : IEwpCommandReader
    {
        private readonly StreamReader r;
        public LineReader(StreamReader r) { this.r = r; }
        public string Read() => r.ReadLine();
        public void Dispose() { }
    }
}

internal sealed class EwpMsgpackCodec : IEwpCodec
{
    public void WriteText(Stream s, string json)
    {
        using var doc = JsonDocument.Parse(json);
        using var ms = new MemoryStream(json.Length + 16);
        Pack(ms, doc.RootElement);
        WriteFramed(s, ms.ToArray());
    }

    public void WritePixel(Stream s, EwpPixelPayload px)
    {
        using var ms = new MemoryStream(96 + px.Png.Length);
        WriteMapHeader(ms, 6);
        WriteStr(ms, "t"); WriteStr(ms, "pixel.frame");
        WriteStr(ms, "frameId"); WriteInt(ms, px.FrameId);
        WriteStr(ms, "w"); WriteInt(ms, px.Width);
        WriteStr(ms, "h"); WriteInt(ms, px.Height);
        WriteStr(ms, "fmt"); WriteStr(ms, "png");
        WriteStr(ms, "stats");
        WriteMapHeader(ms, 2);
        WriteStr(ms, "dropped"); WriteInt(ms, px.Dropped);
        WriteStr(ms, "encMs"); WriteInt(ms, px.EncodeMs);
        WriteStr(ms, "data"); WriteBin(ms, px.Png);
        WriteFramed(s, ms.ToArray());
    }

    public IEwpCommandReader CreateReader(Stream s) => new FrameReader(s);

    #region pack（JSON → msgpack）

    private static void Pack(MemoryStream ms, JsonElement e)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.Object:
                int n = 0;
                foreach (var _ in e.EnumerateObject()) n++;
                WriteMapHeader(ms, n);
                foreach (var p in e.EnumerateObject())
                {
                    WriteStr(ms, p.Name);
                    Pack(ms, p.Value);
                }
                break;
            case JsonValueKind.Array:
                WriteArrayHeader(ms, e.GetArrayLength());
                foreach (var item in e.EnumerateArray())
                    Pack(ms, item);
                break;
            case JsonValueKind.String:
                WriteStr(ms, e.GetString());
                break;
            case JsonValueKind.Number:
                if (e.TryGetInt64(out var i64)) WriteInt(ms, i64);
                else
                {
                    ms.WriteByte(0xcb);
                    var d = BitConverter.DoubleToInt64Bits(e.GetDouble());
                    ms.Write(Be8(d), 0, 8);
                }
                break;
            case JsonValueKind.True:
                ms.WriteByte(0xc3);
                break;
            case JsonValueKind.False:
                ms.WriteByte(0xc2);
                break;
            case JsonValueKind.Null:
                ms.WriteByte(0xc0);
                break;
            default:
                throw new IOException("unsupported JSON kind for msgpack: " + e.ValueKind);
        }
    }

    private static byte[] Be2(int v) => new[] { (byte)(v >> 8), (byte)v };
    private static byte[] Be4(long v) => new[] { (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v };
    private static byte[] Be8(long v)
        => new[] { (byte)(v >> 56), (byte)(v >> 48), (byte)(v >> 40), (byte)(v >> 32), (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v };

    private static void WriteMapHeader(MemoryStream ms, int n)
    {
        if (n <= 15) ms.WriteByte((byte)(0x80 | n));
        else if (n <= 0xFFFF) { ms.WriteByte(0xde); ms.Write(Be2(n), 0, 2); }
        else { ms.WriteByte(0xdf); ms.Write(Be4(n), 0, 4); }
    }

    private static void WriteArrayHeader(MemoryStream ms, int n)
    {
        if (n <= 15) ms.WriteByte((byte)(0x90 | n));
        else if (n <= 0xFFFF) { ms.WriteByte(0xdc); ms.Write(Be2(n), 0, 2); }
        else { ms.WriteByte(0xdd); ms.Write(Be4(n), 0, 4); }
    }

    private static void WriteStr(MemoryStream ms, string s)
    {
        var b = Encoding.UTF8.GetBytes(s ?? "");
        if (b.Length <= 31) ms.WriteByte((byte)(0xa0 | b.Length));
        else if (b.Length <= 0xFF) { ms.WriteByte(0xd9); ms.WriteByte((byte)b.Length); }
        else if (b.Length <= 0xFFFF) { ms.WriteByte(0xda); ms.Write(Be2(b.Length), 0, 2); }
        else { ms.WriteByte(0xdb); ms.Write(Be4(b.Length), 0, 4); }
        ms.Write(b, 0, b.Length);
    }

    private static void WriteBin(MemoryStream ms, byte[] b)
    {
        if (b.Length <= 0xFF) { ms.WriteByte(0xc4); ms.WriteByte((byte)b.Length); }
        else if (b.Length <= 0xFFFF) { ms.WriteByte(0xc5); ms.Write(Be2(b.Length), 0, 2); }
        else { ms.WriteByte(0xc6); ms.Write(Be4(b.Length), 0, 4); }
        ms.Write(b, 0, b.Length);
    }

    private static void WriteInt(MemoryStream ms, long v)
    {
        if (v >= 0)
        {
            if (v <= 127) ms.WriteByte((byte)v);
            else if (v <= 0xFF) { ms.WriteByte(0xcc); ms.WriteByte((byte)v); }
            else if (v <= 0xFFFF) { ms.WriteByte(0xcd); ms.Write(Be2((int)v), 0, 2); }
            else if (v <= 0xFFFFFFFFL) { ms.WriteByte(0xce); ms.Write(Be4(v), 0, 4); }
            else { ms.WriteByte(0xcf); ms.Write(Be8(v), 0, 8); }
        }
        else
        {
            if (v >= -32) ms.WriteByte((byte)v);
            else if (v >= sbyte.MinValue) { ms.WriteByte(0xd0); ms.WriteByte((byte)v); }
            else if (v >= short.MinValue) { ms.WriteByte(0xd1); ms.Write(Be2((int)v), 0, 2); }
            else if (v >= int.MinValue) { ms.WriteByte(0xd2); ms.Write(Be4(v), 0, 4); }
            else { ms.WriteByte(0xd3); ms.Write(Be8(v), 0, 8); }
        }
    }

    private static void WriteFramed(Stream s, byte[] body)
    {
        var n = body.Length;
        s.WriteByte((byte)(n >> 24)); s.WriteByte((byte)(n >> 16));
        s.WriteByte((byte)(n >> 8)); s.WriteByte((byte)n);
        s.Write(body, 0, n);
    }

    #endregion

    #region unpack（msgpack → JSON 文本）

    private sealed class FrameReader : IEwpCommandReader
    {
        private readonly Stream s;
        private readonly byte[] len = new byte[4];
        public FrameReader(Stream s) { this.s = s; }
        public string Read()
        {
            if (!ReadFull(len, 4)) return null;
            long n = ((long)len[0] << 24) | ((long)len[1] << 16) | ((long)len[2] << 8) | len[3];
            if (n > 16 * 1024 * 1024) throw new IOException("msgpack frame too large: " + n);
            var body = new byte[n];
            if (!ReadFull(body, (int)n)) return null;
            return DecodeToJson(body);
        }
        private bool ReadFull(byte[] buf, int count)
        {
            int off = 0;
            while (off < count)
            {
                int r = s.Read(buf, off, count - off);
                if (r <= 0) return false;
                off += r;
            }
            return true;
        }
        public void Dispose() { }
    }

    /// <summary>子集解码：posfix/negfix int、fixmap/fixarray/fixstr、nil/bool、bin8/16/32、
    /// f32/f64、uint8-64、int8-64、str8/16/32、array16/32、map16/32。ext/fixext 抛异常。</summary>
    private static string DecodeToJson(byte[] data)
    {
        using var outMs = new MemoryStream(data.Length * 2);
        int pos = 0;
        using (var w = new Utf8JsonWriter(outMs))
        {
            Decode(w, data, ref pos);
            if (pos != data.Length) throw new IOException("trailing bytes in msgpack frame");
        }
        return Encoding.UTF8.GetString(outMs.ToArray());
    }

    private static void Decode(Utf8JsonWriter w, byte[] d, ref int pos)
    {
        byte f = d[pos++];
        if (f <= 0x7f) { w.WriteNumberValue(f); return; }
        if (f >= 0xe0) { w.WriteNumberValue((sbyte)f); return; }
        if (f >= 0xa0 && f <= 0xbf) { WriteString(w, ReadRaw(d, ref pos, f & 0x1f)); return; }
        if (f >= 0x90 && f <= 0x9f)
        {
            w.WriteStartArray();
            int n = f & 0x0f;
            for (int i = 0; i < n; i++) Decode(w, d, ref pos);
            w.WriteEndArray();
            return;
        }
        if (f >= 0x80 && f <= 0x8f)
        {
            w.WriteStartObject();
            int n = f & 0x0f;
            for (int i = 0; i < n; i++)
            {
                w.WritePropertyName(ReadString(d, ref pos));
                Decode(w, d, ref pos);
            }
            w.WriteEndObject();
            return;
        }
        switch (f)
        {
            case 0xc0: w.WriteNullValue(); return;
            case 0xc2: w.WriteBooleanValue(false); return;
            case 0xc3: w.WriteBooleanValue(true); return;
            case 0xc4: case 0xc5: case 0xc6:
                int bl = f == 0xc4 ? d[pos++] : f == 0xc5 ? ReadU16(d, ref pos) : (int)ReadU32(d, ref pos);
                w.WriteStringValue(Convert.ToBase64String(ReadRaw(d, ref pos, bl)));
                return;
            case 0xca:
                w.WriteNumberValue(BitConverter.Int32BitsToSingle((int)ReadU32(d, ref pos)));
                return;
            case 0xcb:
                w.WriteNumberValue(BitConverter.Int64BitsToDouble((long)ReadU64(d, ref pos)));
                return;
            case 0xcc: w.WriteNumberValue(d[pos++]); return;
            case 0xcd: w.WriteNumberValue(ReadU16(d, ref pos)); return;
            case 0xce: w.WriteNumberValue(ReadU32(d, ref pos)); return;
            case 0xcf: w.WriteNumberValue(ReadU64(d, ref pos)); return;
            case 0xd0: w.WriteNumberValue((sbyte)d[pos++]); return;
            case 0xd1: w.WriteNumberValue((short)ReadU16(d, ref pos)); return;
            case 0xd2: w.WriteNumberValue((int)ReadU32(d, ref pos)); return;
            case 0xd3: w.WriteNumberValue((long)ReadU64(d, ref pos)); return;
            case 0xd9: case 0xda: case 0xdb:
                int sl = f == 0xd9 ? d[pos++] : f == 0xda ? ReadU16(d, ref pos) : (int)ReadU32(d, ref pos);
                WriteString(w, ReadRaw(d, ref pos, sl));
                return;
            case 0xdc: case 0xdd:
                int al = f == 0xdc ? ReadU16(d, ref pos) : (int)ReadU32(d, ref pos);
                w.WriteStartArray();
                for (int i = 0; i < al; i++) Decode(w, d, ref pos);
                w.WriteEndArray();
                return;
            case 0xde: case 0xdf:
                int ml = f == 0xde ? ReadU16(d, ref pos) : (int)ReadU32(d, ref pos);
                w.WriteStartObject();
                for (int i = 0; i < ml; i++)
                {
                    w.WritePropertyName(ReadString(d, ref pos));
                    Decode(w, d, ref pos);
                }
                w.WriteEndObject();
                return;
            default:
                throw new IOException("msgpack format byte not in subset: 0x" + f.ToString("X2"));
        }
    }

    private static byte[] ReadRaw(byte[] d, ref int pos, int n)
    {
        var b = new byte[n];
        Array.Copy(d, pos, b, 0, n);
        pos += n;
        return b;
    }

    private static string ReadString(byte[] d, ref int pos)
    {
        byte f = d[pos++];
        int n;
        if (f <= 0xbf && f >= 0xa0) n = f & 0x1f;
        else if (f == 0xd9) n = d[pos++];
        else if (f == 0xda) n = ReadU16(d, ref pos);
        else if (f == 0xdb) n = (int)ReadU32(d, ref pos);
        else throw new IOException("msgpack map key is not str (format 0x" + f.ToString("X2") + ")");
        return Encoding.UTF8.GetString(ReadRaw(d, ref pos, n));
    }

    private static void WriteString(Utf8JsonWriter w, byte[] utf8)
        => w.WriteStringValue(Encoding.UTF8.GetString(utf8));

    private static int ReadU16(byte[] d, ref int pos)
    { int v = (d[pos] << 8) | d[pos + 1]; pos += 2; return v; }

    private static uint ReadU32(byte[] d, ref int pos)
    { uint v = ((uint)d[pos] << 24) | ((uint)d[pos + 1] << 16) | ((uint)d[pos + 2] << 8) | d[pos + 3]; pos += 4; return v; }

    private static ulong ReadU64(byte[] d, ref int pos)
    {
        ulong v = 0;
        for (int i = 0; i < 8; i++) v = (v << 8) | d[pos + i];
        pos += 8;
        return v;
    }

    #endregion
}
