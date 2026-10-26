using MinorShift.Emuera.UI;
using MinorShift.Emuera.GameView;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;

// eraWire B3: 离屏像素流（EWP §4.4 pixel.frame；spec §8 落点＝console.OnPaint 离屏直调）。
// 管线：UI 线程（CliFramePainted，RefreshStrings 有效通过点）渲染离屏 raster surface →
// ReadPixels 拷贝（脱离 surface 生命周期）→ 编码线程 PNG → EwpOut.EmitPixel（写出队列丢新计数）。
// v0 整帧、无 dirty-rect（A1-⑥ 已冻）；过载策略＝捕获队列丢旧保新（EWP §6.3 像素流单独计数，
// 编码跟不上时宁可丢中间帧也绝不阻塞 UI/脚本线程——与文本信用窗背压刻意不同）。
namespace MinorShift.Emuera.UI.Ewp;

/// <summary>离屏像素流捕获器。UI 线程捕获；独立线程编码；线程边界只交接 SKBitmap 所有权。</summary>
internal sealed class EwpPixelStreamer : IDisposable
{
    private const int EncodeQueueCap = 4;

    private readonly IConsoleSurface win;
    private readonly Func<EmueraConsole> consoleOf;
    private readonly Action<EwpPixelPayload> emit;

    private readonly BlockingCollection<Capture> encodeQ;
    private Thread encodeThread;
    private readonly object startGate = new();

    private SKSurface surface;
    private int surfW, surfH;
    private volatile bool enabled;
    private bool zeroSizeWarned;
    private long paintSeq;
    private long dropped;

    public long Frames => Interlocked.Read(ref paintSeq);
    public long Dropped => Interlocked.Read(ref dropped);
    public bool Enabled => enabled;

    public EwpPixelStreamer(IConsoleSurface window, Func<EmueraConsole> consoleProvider, Action<EwpPixelPayload> emitToOut)
    {
        win = window;
        consoleOf = consoleProvider;
        emit = emitToOut;
        encodeQ = new BlockingCollection<Capture>(new ConcurrentQueue<Capture>(), EncodeQueueCap);
    }

    /// <summary>hello.ok mode.pixel=true 时启用（任意线程；只立旗与起线程，surface 惰性建于 UI 线程）。</summary>
    public void Enable()
    {
        lock (startGate)
        {
            enabled = true;
            if (encodeThread == null)
            {
                encodeThread = new Thread(EncodeLoop) { IsBackground = true, Name = "EwpPixel encode" };
                encodeThread.Start();
            }
        }
    }

    private sealed class Capture
    {
        public SKBitmap Bmp;
        public long FrameId;
    }

    /// <summary>CliFramePainted（UI 线程）：渲染整帧并投递编码。surface 尺寸＝RenderWidth×RenderHeight
    /// （OnPaint 的图层布局用 LocalClipBounds、行定位用 RenderHeight，两者必须同源同值）。</summary>
    public void OnFramePainted()
    {
        if (!enabled)
            return;
        var console = consoleOf();
        if (console == null)
            return;
        int w = win.RenderWidth, h = win.RenderHeight;
        if (w <= 0 || h <= 0)
        {
            // 一次性诊断（B3 调试期）：定位最小化窗口布局未落地的静默早退
            if (!zeroSizeWarned)
            {
                zeroSizeWarned = true;
                EwpOut.Emit("{\"t\":\"error\",\"code\":\"pixel_zero_size\",\"fatal\":false,\"detail\":\"render="
                    + w + "x" + h + " client=" + win.ClientSize.Width + "x" + win.ClientSize.Height
                    + " state=" + win.WindowStateName + "\"}");
            }
            return;
        }
        try
        {
            if (surface == null || surfW != w || surfH != h)
            {
                surface?.Dispose();
                surface = SKSurface.Create(new SKImageInfo(w, h));
                surfW = w; surfH = h;
                if (surface == null)
                    return;
            }
            console.OnPaint(surface.Canvas);
            surface.Flush();
            var info = new SKImageInfo(w, h);
            var bmp = new SKBitmap(info);
            if (!surface.ReadPixels(info, bmp.GetPixels(), info.RowBytes, 0, 0))
            {
                bmp.Dispose();
                return;
            }
            var cap = new Capture { Bmp = bmp, FrameId = Interlocked.Increment(ref paintSeq) };
            while (!encodeQ.TryAdd(cap))
            {
                Interlocked.Increment(ref dropped);
                Capture stale;
                if (!encodeQ.TryTake(out stale))
                {
                    // 队列同时被编码线程清空：直接再试一次加队，仍失败则丢本帧（计数）
                    if (!encodeQ.TryAdd(cap))
                    {
                        Interlocked.Increment(ref dropped);
                        cap.Bmp.Dispose();
                    }
                    return;
                }
                stale.Bmp.Dispose();
            }
        }
        catch
        {
            // 像素流是旁路增益：捕获异常绝不冒泡进 RefreshStrings（帧信号订阅点在内核 hot path）
        }
    }

    private void EncodeLoop()
    {
        try
        {
            foreach (var cap in encodeQ.GetConsumingEnumerable())
            {
                using (cap.Bmp)
                {
                    byte[] png = null;
                    var sw = Stopwatch.StartNew();
                    try
                    {
                        using var img = SKImage.FromBitmap(cap.Bmp);
                        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
                        png = data.ToArray();
                    }
                    catch { }
                    sw.Stop();
                    if (png == null || png.Length == 0)
                    {
                        Interlocked.Increment(ref dropped);
                        continue;
                    }
                    emit(new EwpPixelPayload
                    {
                        FrameId = cap.FrameId,
                        Width = cap.Bmp.Width,
                        Height = cap.Bmp.Height,
                        EncodeMs = (int)sw.ElapsedMilliseconds,
                        Dropped = (int)Interlocked.Read(ref dropped),
                        Png = png,
                    });
                }
            }
        }
        catch { }
    }

    public void Dispose()
    {
        try { encodeQ.CompleteAdding(); } catch { }
        lock (startGate)
        {
            try { surface?.Dispose(); } catch { }
            surface = null;
        }
        Capture stale;
        while (encodeQ.TryTake(out stale))
            stale.Bmp.Dispose();
    }
}
