using MinorShift.Emuera.GameView;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

// eraWire R-IMG-1a：CBG 静态快照导出。
// 语义：CBG（GCREATE/GDRAWSPRITE/CBGSETG 合成结果）＋SETBGIMAGE 烘焙层 ＝ 一张不可语义化的
// 运行期位图。EWP 不重实现 G 系（R-IMG-0 §2.1 Tier C 边界），只在 CBG 变更后按帧边界导出
// 「最终合成快照」：PaintBackgroundOnly 与 OnPaint 同源绘制 → PNG → bg.set/bg.data。
// 频率：背景变化少，捕获只在变更帧做；内容未变（像素逐字节相同）则不发事件。
// 编码在独立线程（1280×720 PNG ~20-40ms，⛔ 占 UI 线程）；单槽丢旧保新。
namespace MinorShift.Emuera.UI.Ewp;

/// <summary>bg 事件载荷：文本型（bg.set/bg.clear/bg.fail）与二进制型（bg.data）二选一。</summary>
internal sealed class EwpBgPayload
{
    /// <summary>非 null ⇒ 文本事件（与 Png 互斥）。</summary>
    public string Json;
    public long Rev;
    public int W, H;
    public byte[] Png;
}

/// <summary>CBG 合成快照导出器。捕获/比较在逻辑 UI 线程；编码在编码线程；线程边界只交接 byte[]。</summary>
internal sealed class EwpBgExport
{
    /// <summary>保留最近若干张已编码快照，供 FE 在 snapshot 重建时按 rev 重取。</summary>
    private const int MaxSnapshots = 4;

    private readonly Action<EwpBgPayload> emit;

    private readonly object gate = new();
    /// <summary>已编码快照：键＝rev，值＝PNG＋该 rev 的**原始尺寸**。
    /// ⛔ 不能只存 byte[]：窗口 resize 后 currentW/H 已变，按它回报会让 FE 用错尺寸解旧图。</summary>
    private readonly Dictionary<long, Snapshot> encoded = new();
    private readonly Queue<long> encodedOrder = new();

    private readonly BlockingCollection<Job> encodeQ = new(new ConcurrentQueue<Job>(), 1);
    private Thread encodeThread;

    /// <summary>画布位图的门（⛔ 复用 gate：绘制期间不能持 gate，否则 Resend/Stats 会被全屏绘制阻塞）。</summary>
    private readonly object canvasGate = new();
    private SKBitmap canvasBitmap;
    private int canvasW, canvasH;
    private volatile bool disposed;
    private byte[] lastPixels;

    private volatile bool enabled;
    private long revSeq;
    private long currentRev;
    private int currentW, currentH;
    private bool hasContent;

    private long captures;
    private long encodes;
    private long dropped;
    private long skippedIdentical;
    private double captureMsLast;
    private double encodeMsLast;

    public EwpBgExport(Action<EwpBgPayload> emitToOut)
    {
        emit = emitToOut;
    }

    public bool Enabled
    {
        get => enabled;
        set => enabled = value;
    }

    public bool HasContent => hasContent;
    public long CurrentRev => Interlocked.Read(ref currentRev);
    public int CurrentWidth => currentW;
    public int CurrentHeight => currentH;

    internal readonly record struct BgExportStats(
        bool Enabled, long Rev, int W, int H, bool HasContent,
        long Captures, long Encodes, long Dropped, long SkippedIdentical,
        double CaptureMsLast, double EncodeMsLast);

    public BgExportStats Stats()
    {
        lock (gate)
        {
            return new BgExportStats(enabled, currentRev, currentW, currentH, hasContent,
                captures, encodes, dropped, skippedIdentical, captureMsLast, encodeMsLast);
        }
    }

    /// <summary>UI 线程：重画背景层并（像素有变时）投递编码。返回本次是否产生新 rev。
    /// resend=true ⇒ 即使像素未变也把当前快照重发一次（snapshot/读档重建用）。</summary>
    public bool Capture(EmueraConsole console, int w, int h, bool resend = false)
    {
        if (!enabled || console == null || w <= 0 || h <= 0)
            return false;

        if (!console.HasBackgroundLayers)
        {
            // 无背景层 ⇒ bg.clear（⛔ 发纯底色快照冒充）
            lastPixels = null;
            bool changed;
            long rev;
            lock (gate)
            {
                changed = hasContent;
                // 本来就没有背景、也没要求重发 ⇒ 不推进 rev（否则每次状态对账都刷新 rev，FE 白重取）
                if (!changed && !resend)
                {
                    skippedIdentical++;
                    return false;
                }
                hasContent = false;
                rev = ++revSeq;
                currentRev = rev;
                currentW = w;
                currentH = h;
                encoded.Clear();
                encodedOrder.Clear();
            }
            emit(new EwpBgPayload
            {
                Json = "{\"t\":\"bg.clear\",\"rev\":" + rev + "}",
                Rev = rev,
            });
            return changed;
        }

        var sw = Stopwatch.StartNew();
        byte[] pixels;
        lock (canvasGate)
        {
            if (disposed)
                return false;
            try
            {
                if (canvasBitmap == null || canvasW != w || canvasH != h)
                {
                    canvasBitmap?.Dispose();
                    canvasBitmap = new SKBitmap(new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Premul));
                    canvasW = w;
                    canvasH = h;
                    lastPixels = null;
                }
                using (var canvas = new SKCanvas(canvasBitmap))
                {
                    canvas.Clear(SKColors.Transparent);
                    console.PaintBackgroundOnly(canvas, w, h);
                    canvas.Flush();
                }
                pixels = canvasBitmap.GetPixelSpan().ToArray();
            }
            catch
            {
                return false;
            }
        }
        sw.Stop();

        bool identical = lastPixels != null && pixels.Length == lastPixels.Length
            && pixels.AsSpan().SequenceEqual(lastPixels);
        if (identical && !resend)
        {
            lock (gate)
            {
                skippedIdentical++;
                captureMsLast = sw.Elapsed.TotalMilliseconds;
            }
            return false;
        }
        lastPixels = pixels;

        long newRev;
        lock (gate)
        {
            newRev = ++revSeq;
            currentRev = newRev;
            currentW = w;
            currentH = h;
            hasContent = true;
            captures++;
            captureMsLast = sw.Elapsed.TotalMilliseconds;
        }

        // bg.set 先于 bg.data，且二者走同一队列（⛔ 分队列会让 FE 先收到 data）
        emit(new EwpBgPayload
        {
            Json = "{\"t\":\"bg.set\",\"rev\":" + newRev + ",\"w\":" + w + ",\"h\":" + h + ",\"depth\":0}",
            Rev = newRev,
        });
        if (disposed)
            return true;
        EnsureEncodeThread();
        var job = new Job { Rev = newRev, W = w, H = h, Pixels = pixels };
        try
        {
            if (!encodeQ.TryAdd(job))
            {
                // 单槽丢旧保新：编码跟不上时宁可少一张中间态
                if (encodeQ.TryTake(out _))
                    Interlocked.Increment(ref dropped);
                encodeQ.TryAdd(job);
            }
        }
        catch (Exception)
        {
            // Dispose 已 CompleteAdding：收尾期不再投递（bg.set 已发，FE 可靠 bg.req 兜底）
            Interlocked.Increment(ref dropped);
        }
        return true;
    }

    /// <summary>FE 请求重取（bg.req）：rev 命中缓存 ⇒ 按**该 rev 的原始尺寸**重发 data；
    /// 未命中但仍是当前 rev ⇒ pending（编码线程还没编完，data 稍后会自然到达，⛔ 回 not_found 逼 FE 重试）；
    /// 更旧 ⇒ expired；其余 ⇒ not_found。</summary>
    public void Resend(long rev)
    {
        Snapshot snap = default;
        string code;
        lock (gate)
        {
            if (encoded.TryGetValue(rev, out snap))
            {
                code = null;
            }
            else if (hasContent && rev == currentRev)
            {
                code = "pending";
            }
            else if (rev > 0 && rev < currentRev)
            {
                code = "expired";
            }
            else
            {
                code = "not_found";
            }
        }
        if (snap.Png == null)
        {
            emit(new EwpBgPayload
            {
                Json = "{\"t\":\"bg.fail\",\"rev\":" + rev + ",\"code\":\"" + code + "\"}",
                Rev = rev,
            });
            return;
        }
        emit(new EwpBgPayload { Rev = rev, W = snap.W, H = snap.H, Png = snap.Png });
    }

    /// <summary>会话级清场（sys.title / ClearDisplay）：释放像素缓存，rev 序列不重置。</summary>
    public void Clear()
    {
        lock (gate)
        {
            lastPixels = null;
            hasContent = false;
            encoded.Clear();
            encodedOrder.Clear();
        }
    }

    private sealed class Job
    {
        public long Rev;
        public int W, H;
        public byte[] Pixels;
    }

    /// <summary>已编码快照。W/H 必须随 PNG 一起存（resize 后 currentW/H 已不是这张图的尺寸）。</summary>
    private readonly struct Snapshot
    {
        public readonly byte[] Png;
        public readonly int W, H;
        public Snapshot(byte[] png, int w, int h)
        {
            Png = png;
            W = w;
            H = h;
        }
    }

    private void EnsureEncodeThread()
    {
        if (disposed || encodeThread != null)
            return;
        lock (gate)
        {
            if (disposed || encodeThread != null)
                return;
            encodeThread = new Thread(EncodeLoop) { IsBackground = true, Name = "EwpBg encode" };
            encodeThread.Start();
        }
    }

    private void EncodeLoop()
    {
        try
        {
            foreach (var job in encodeQ.GetConsumingEnumerable())
            {
                var sw = Stopwatch.StartNew();
                byte[] png = null;
                try
                {
                    var info = new SKImageInfo(job.W, job.H, SKColorType.Bgra8888, SKAlphaType.Premul);
                    using var img = SKImage.FromPixelCopy(info, job.Pixels);
                    using var data = img.Encode(SKEncodedImageFormat.Png, 100);
                    png = data.ToArray();
                }
                catch { }
                sw.Stop();
                if (png == null || png.Length == 0)
                {
                    Interlocked.Increment(ref dropped);
                    emit(new EwpBgPayload
                    {
                        Json = "{\"t\":\"bg.fail\",\"rev\":" + job.Rev + ",\"code\":\"encode_error\"}",
                        Rev = job.Rev,
                    });
                    continue;
                }
                lock (gate)
                {
                    encodes++;
                    encodeMsLast = sw.Elapsed.TotalMilliseconds;
                    encoded[job.Rev] = new Snapshot(png, job.W, job.H);
                    encodedOrder.Enqueue(job.Rev);
                    while (encodedOrder.Count > MaxSnapshots)
                        encoded.Remove(encodedOrder.Dequeue());
                }
                emit(new EwpBgPayload { Rev = job.Rev, W = job.W, H = job.H, Png = png });
            }
        }
        catch { }
    }

    public void Dispose()
    {
        disposed = true;
        try { encodeQ.CompleteAdding(); } catch { }
        // 等编码线程退出：否则它可能正在用 job.Pixels 编码，而 canvasBitmap 已被本线程释放
        var t = encodeThread;
        if (t != null && t.IsAlive)
        {
            try { t.Join(2000); } catch { }
        }
        lock (canvasGate)
        {
            canvasBitmap?.Dispose();
            canvasBitmap = null;
        }
    }
}
