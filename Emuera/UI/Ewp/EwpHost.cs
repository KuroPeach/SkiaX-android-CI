using MinorShift.Emuera.UI;
using MinorShift.Emuera.GameView;
using MinorShift.Emuera.Runtime;
using MinorShift.Emuera.Runtime.Script;
using MinorShift.Emuera.Runtime.Script.Statements;
using MinorShift.Emuera.Runtime.Utils;
using MinorShift.Emuera.Runtime.Utils.EvilMask;
using MinorShift.Emuera.Runtime.Config;
using MinorShift.Emuera.UI.Game;
using MinorShift.Emuera.UI.Game.Image;
using MinorShift.Emuera.UI.Game.Rendering;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;

// eraWire B1/B2/B3: EWP/0 host (JSON-Lines default or MessagePack via --ewp-msgpack; stdio or single TCP client).
// Spec: $EMUERA_WS/emuera/eraWire/specs/EWP-v0-协议草案.md
// Kernel hooks: CliLineAdded/CliWaitInputEntered/CliTerminalStateEntered (CliDebug set)
// plus CliLinesRemoved (B2 kernel ledger #1) for legacy.clear, sprite/island 5 events (B2 #2),
// and CliFramePainted (B3 #3, RefreshStrings 有效通过点) for pixel.frame.
// B3 additions: pixel stream (offscreen SKSurface -> console.OnPaint -> PNG on encode thread),
// codec abstraction (EwpCodec.cs), pixel negotiation via hello.ok mode.pixel.
// Remaining: channel/node (waits D-track macros); colormatrix/followScroll (v1).
namespace MinorShift.Emuera.UI.Ewp;

/// <summary>Static output layer: text queue + pixel queue + codec + single writer thread (B3).
/// 文本事件走 8192 有界队列（满则阻塞生产者＝信用窗背压）；像素事件走独立小队列
/// （满则丢新帧计数，EWP §6.3 像素流单独计数——与文本背压刻意不同：像素流可丢，行不可丢）。</summary>
internal static class EwpOut
{
    private const int QueueCapacity = 8192;
    private const int PixelQueueCapacity = 8;
    /// <summary>信用窗口（EWP §6.3）：未 ack 帧数达到此值时 writer 暂停发送。</summary>
    private const int CreditWindow = 8;
    /// <summary>ack 超时兜底：FE 不 ack 时最多等这么久再放行，避免死锁（闸门 4 要求无死锁）。</summary>
    private const int AckTimeoutMs = 5000;

    private sealed class Payload
    {
        public string Json;           // 文本事件（完整 JSON 对象，无换行）
        public EwpPixelPayload Pixel; // 像素事件（与 Json 互斥）
    }

    private static BlockingCollection<Payload> queue;
    private static BlockingCollection<Payload> pixelQueue;
    private static Stream outStream;
    private static Thread writerThread;
    /// <summary>Shutdown 期放行信用窗等待，确保 exit 事件能落盘（否则写手可能卡在 WaitForCredit）。</summary>
    private static volatile bool closing;
    private static long frameSeq;
    private static long ackCount;
    private static readonly object ackGate = new();
    private static int pendingFrames;
    private static int ackTimeouts;
    private static long pixelWritten;
    private static long pixelDropped;

    /// <summary>L0.5 编码器（B3）：JSON-Lines（默认）或 MessagePack（--ewp-msgpack）。</summary>
    public static IEwpCodec Codec { get; private set; } = new EwpJsonCodec();

    public static int AckTimeouts => Volatile.Read(ref ackTimeouts);
    public static long FramesWritten => Interlocked.Read(ref frameSeq);
    public static long Acks => Interlocked.Read(ref ackCount);
    public static int PendingFrames => Volatile.Read(ref pendingFrames);
    public static long PixelWritten => Interlocked.Read(ref pixelWritten);
    public static long PixelDropped => Interlocked.Read(ref pixelDropped);

    /// <summary>deferWriter=true（TCP 模式）：只建队列，写手线程等 AttachStream 再启动，
    /// 期间事件在队列中缓冲（boot 期 ~1.4k 行，远低于 8192 容量）。</summary>
    public static void InitIO(bool deferWriter = false, bool msgpack = false)
    {
        closing = false;
        Codec = msgpack ? new EwpMsgpackCodec() : new EwpJsonCodec();
        var utf8 = new UTF8Encoding(false);
        try { Console.InputEncoding = utf8; } catch { }
        try { Console.OutputEncoding = utf8; } catch { }
        queue = new BlockingCollection<Payload>(new ConcurrentQueue<Payload>(), QueueCapacity);
        pixelQueue = new BlockingCollection<Payload>(new ConcurrentQueue<Payload>(), PixelQueueCapacity);
        if (deferWriter)
            return;
        Stream s = null;
        try { s = Console.OpenStandardOutput(); } catch { }
        outStream = s;
        if (s == null)
            return;
        writerThread = new Thread(Loop) { IsBackground = true, Name = "EwpOut writer" };
        writerThread.Start();
    }

    /// <summary>TCP 客户端接入后挂载输出流并启动写手（只允许一次）。</summary>
    public static void AttachStream(Stream stream)
    {
        if (outStream != null || stream == null)
            return;
        outStream = stream;
        writerThread = new Thread(Loop) { IsBackground = true, Name = "EwpOut writer" };
        writerThread.Start();
    }

    /// <summary>Enqueue one text event. Blocks (pausing the producer thread) when the
    /// consumer is slow and the queue is full - this is the credit-window backpressure.</summary>
    public static void Emit(string json)
    {
        var q = queue;
        if (q == null)
            return;
        try { q.Add(new Payload { Json = json }); } catch { }
    }

    /// <summary>像素事件：编码完成后投递写出队列（丢新计数；丢旧保新在 EwpPixelStreamer 捕获级）。</summary>
    public static void EmitPixel(EwpPixelPayload px)
    {
        var q = pixelQueue;
        if (q == null)
            return;
        px.Dropped += (int)Interlocked.Read(ref pixelDropped);
        var p = new Payload { Pixel = px };
        if (!q.TryAdd(p))
        {
            Interlocked.Increment(ref pixelDropped);
            if (!q.TryAdd(p))
            {
                Interlocked.Increment(ref pixelDropped);
                return;
            }
        }
    }

    public static void NoteAck()
    {
        Interlocked.Increment(ref ackCount);
        // 不允许 pending 掉到负（多余 ack 忽略）
        while (true)
        {
            int cur = Volatile.Read(ref pendingFrames);
            if (cur <= 0) break;
            if (Interlocked.CompareExchange(ref pendingFrames, cur - 1, cur) == cur) break;
        }
        lock (ackGate) { Monitor.PulseAll(ackGate); }
    }

    public static void Shutdown()
    {
        var q = queue;
        if (q == null)
            return;
        closing = true;
        lock (ackGate)
        {
            // 唤醒可能卡在信用窗的写手，让它排空 exit 后再退出
            Monitor.PulseAll(ackGate);
        }
        try { q.CompleteAdding(); } catch { }
        try { pixelQueue?.CompleteAdding(); } catch { }
        try { writerThread?.Join(3000); } catch { }
    }

    private static void Loop()
    {
        var s = outStream;
        var sb = new StringBuilder(64);
        var colls = new[] { queue, pixelQueue };
        try
        {
            while (true)
            {
                Payload p = null;
                try { BlockingCollection<Payload>.TryTakeFromAny(colls, out p, TimeSpan.FromMilliseconds(250)); }
                catch (ArgumentException) { break; }          // 两队列均已 CompleteAdding
                catch (ObjectDisposedException) { break; }
                if (p == null)
                {
                    if (queue.IsCompleted && pixelQueue.IsCompleted) break;
                    continue;
                }
                if (p.Pixel != null)
                {
                    // pixel.frame 是独立帧型（自带 frameId/独立计数），不进文本批、不计入 frame.commit.events
                    Codec.WritePixel(s, p.Pixel);
                    Interlocked.Increment(ref pixelWritten);
                    s.Flush();
                    continue;
                }
                Codec.WriteText(s, p.Json);
                long count = 1;
                // drain whatever else is already queued (batch), then close the frame
                while (queue.TryTake(out var more, 0))
                {
                    if (more.Pixel != null)
                    {
                        Codec.WritePixel(s, more.Pixel);
                        Interlocked.Increment(ref pixelWritten);
                        continue;
                    }
                    Codec.WriteText(s, more.Json);
                    count++;
                }
                sb.Clear();
                sb.Append("{\"t\":\"frame.commit\",\"frameId\":").Append(++frameSeq)
                    .Append(",\"events\":").Append(count)
                    .Append(",\"pending\":").Append(Volatile.Read(ref pendingFrames) + 1)
                    .Append(",\"ackTimeouts\":").Append(Volatile.Read(ref ackTimeouts))
                    .Append('}');
                Codec.WriteText(s, sb.ToString());
                s.Flush();
                // 信用窗口：本帧未 ack 前最多积压 CreditWindow 帧
                Interlocked.Increment(ref pendingFrames);
                WaitForCredit();
            }
        }
        catch { }
        finally { try { s?.Flush(); } catch { } }
    }

    /// <summary>等待信用返还；超时兜底放行（记 ackTimeouts，不丢事件）。</summary>
    private static void WaitForCredit()
    {
        lock (ackGate)
        {
            long deadline = Environment.TickCount64 + AckTimeoutMs;
            while (!closing && Volatile.Read(ref pendingFrames) >= CreditWindow)
            {
                long left = deadline - Environment.TickCount64;
                if (left <= 0)
                {
                    Interlocked.Increment(ref ackTimeouts);
                    // 放行：把窗口清零，让后续帧继续流出（宁可失控也不死锁）。
                    // 非致命 error 事件尽量外显（队列已满则丢弃——计数器才是真相）。
                    queue?.TryAdd(new Payload { Json = "{\"t\":\"error\",\"code\":\"ack_timeout\",\"fatal\":false,\"detail\":\"credit window timed out; releasing\"}" }, 0);
                    Volatile.Write(ref pendingFrames, 0);
                    break;
                }
                Monitor.Wait(ackGate, (int)Math.Min(left, 250));
            }
        }
    }
}

internal sealed class EwpHost
{
    private readonly IConsoleSurface win;
    private readonly int tcpPort;
    private Thread transportThread;
    private volatile bool running = true;

    private long lineIdSeq;
    private long reqIdSeq;
    /// <summary>快照用行账本：lineId/文本/频道 owner/到达时的频道 rev；CLEARLINE 尾部裁剪。</summary>
    private sealed class LiveLine
    {
        public long Id;
        public string Text;
        /// <summary>R-3b-1：预构建的 spans JSON 数组（null＝该行不含 spans）。</summary>
        public string Spans;
        /// <summary>W0：保留行对象用于 snapshot 重建 spans（重捕获行内像素并签发新 rev）。
        /// 仅 UI 线程访问；行清理（CLEARLINE/ClearDisplay）时随 liveLines 一并释放。</summary>
        public ConsoleDisplayLine Line;
        public string Owner = "";
        public long OwnerRev;
    }
    /// <summary>仍存活的行（lineId/文本/归属；仅 UI 线程访问，频道元数据在 channelGate 下）。</summary>
    private readonly List<LiveLine> liveLines = new();
    /// <summary>活着的 sprite 图层（与引擎同语义：同深度可叠多层、按深度清）。snapshot 数据源。</summary>
    private readonly List<(long Depth, string Tex, int X, int Y, int W, int H, int Op, bool Follow, long Rev)> liveSprites = new();
    /// <summary>R-3a：运行期图层不可变快照（UI 线程；FE 声明 sprite.export 后才启用）。</summary>
    private readonly EwpSpriteExport spriteExport = new();
    /// <summary>当前 island 的原始 HTML（null＝无 island）。v0 单实例（A1-④）。</summary>
    private string liveIslandHtml;
    /// <summary>hello.ok 里 FE 上报的视口（窗体句柄未建时暂存，Subscribe 时经 UI 线程应用）。</summary>
    private int pendingVpW, pendingVpH;
    // currentReqId/currentGeneration are written on the UI thread (OnWaitInput) and
    // read on the UI thread (dispatched input.submit) - no cross-thread races.
    private long currentReqId = -1;
    private int currentGeneration = -1;
    /// <summary>B3 离屏像素流（hello.ok mode.pixel=true 才启用；帧信号=CliFramePainted）。</summary>
    private readonly EwpPixelStreamer pixel;

    /// <summary>B②/S1 频道状态（ChannelHooks 名挂钩维护；id → kind/rev/open/segments）。
    /// 脚本线程写（IntoFunction）、UI 线程读（snapshot/state）——lock 保护。</summary>
    private sealed class ChannelSegment
    {
        public long Rev;
        public bool Committed;
    }
    private sealed class ChannelState
    {
        public string Kind;
        public long Rev;
        public bool Open;
        public readonly List<ChannelSegment> Segments = new();
    }
    private readonly object channelGate = new();
    private readonly Dictionary<string, ChannelState> channels = new();
    /// <summary>真实 begin 次序（栈底→栈顶）；空栈时新行走主日志。</summary>
    private readonly List<string> openStack = new();
    private long channelMismatch;
    /// <summary>FE 的 hello.ok.caps 是否包含 channel；缺省 caps 按旧 FE 常开处理。</summary>
    private volatile bool channelEnabled = true;
    /// <summary>FE 的 hello.ok.caps 是否包含 input.rect（I3-b）：缺省 false ⇒ input.request 的
    /// buttons[] 不带 rect 键（⛔ 发 null 或空对象，FE 侧统一按「无 rect 回退文本/值」处理）。
    /// 引擎 hello.caps 自报暂不含 input.rect：声明即承诺，等 I3-c 真机门禁通过后再加。</summary>
    private volatile bool rectEnabled;
    /// <summary>FE 的 hello.ok.caps 是否包含 legacy.spans（R-3b-1）：缺省 false ⇒
    /// legacy.line/snapshot.full 不含 spans 键（旧 FE 逐字节不变）。</summary>
    private volatile bool spansEnabled;
    private long spansEmitted;
    private long spansSkipped;
    // R-3b-3（W1）div.children：有界递归／载荷上限（旧 FE 忽略 children 键）。
    private const int MaxDivChildSpans = 512;
    private const int MaxDivNesting = 1;
    private long spansChildNodes;
    private long spansChildTruncated;
    /// <summary>最近一次 EmitMetrics 读到的 render 尺寸（rect 的坐标画布）。
    /// Subscribe 期窗口可能未完成布局 ⇒ 读到 0（最小化实测 render=0x0）；首个 input.request
    /// 时窗口必已就绪，故在那里补发一次。</summary>
    private int metricsRenderW;
    private int metricsRenderH;
    private bool metricsRenderResent;
    /// <summary>rect 自检失败累计（I3-c 生产护栏）：几何自检不过的按钮不发 rect，只计数。
    /// <c>sys.state.rectSelfCheckFailed</c> 可观测；门禁断言 == 0。</summary>
    private long rectSelfCheckFailed;

    public EwpHost(IConsoleSurface window, int tcpPort = 0)
    {
        win = window;
        this.tcpPort = tcpPort;
        pixel = new EwpPixelStreamer(window, () => GlobalStatic.Console, EwpOut.EmitPixel);
    }

    // Program wires the dialog delegates after EwpOut.InitIO, so the queue already
    // exists when these fire - no pre-Start buffering is needed.
    public static void EmitDialogEvent(string kind, string text, string title)
    {
        EwpOut.Emit("{\"t\":\"dialog.request\",\"kind\":" + J(kind) + ",\"title\":" + J(title ?? "")
            + ",\"text\":" + J(text ?? "") + "}");
    }

    public void Start()
    {
        ChannelHooks.Handler = OnChannelMacro;
        var sb = new StringBuilder(320);
        sb.Append("{\"t\":\"hello\",\"proto\":\"EWP/0.3\",\"impl\":\"ewp-b4\",\"engine\":{")
            .Append("\"id\":\"LazyLoading-skia\",\"semver\":").Append(J(SafeVersion()))
            .Append("},\"game\":{")
            .Append("\"id\":").Append(J(SafeGameId()))
            .Append(",\"profile\":\"skia-superset\"},")
            .Append("\"caps\":[\"legacy.lines\",\"input.scope\",\"snapshot\",\"frame.commit\",\"view.ack\",\"viewport.report\",\"meta.metrics\",\"pixel.frame\",\"channel\",\"input.rect\",\"sprite.export\",\"legacy.spans\"],")
            .Append("\"mode\":{\"semantic\":true,\"pixel\":false}}");
        EwpOut.Emit(sb.ToString());
        EwpOut.Emit("{\"t\":\"meta.state\",\"state\":\"boot\"}");
        transportThread = new Thread(tcpPort > 0 ? AcceptLoop : StdinLoop)
        { IsBackground = true, Name = tcpPort > 0 ? "EwpHost tcp" : "EwpHost stdin" };
        transportThread.Start();
    }

    /// <summary>metrics 在字体系统就绪后（Subscribe 时）以 meta.metrics 补发——
    /// Start 期测宽会在 FontFactory 初始化前触碰 SKFont，污染字体状态触发引擎
    /// 「不適正なフォント→強制終了」守卫（2026-10-24 pixtest 实锤）。cellW 与
    /// PRINTC 同走 StringMeasure.GetDisplayLength（半角空格×2）。</summary>
    private void EmitMetrics()
    {
        int cellW = 0;
        try { cellW = StringMeasure.GetDisplayLength(" ", Config.DefaultFont) * 2; } catch { }
        // I3-b：renderW/renderH 与 sys.state.client 同源（console.ClientWidth/Height
        // ＝ MainWindow.RenderWidth/RenderHeight，即 rect 的坐标画布＝render 逻辑像素）。
        // 这里重复一次是为了让 FE 在网格基准旁直接拿到画布尺寸；⛔ 与 hello.ok.viewport
        // （FE 期望的 ClientSize，含滚动条/边框）不是一个量。
        int renderW = 0, renderH = 0;
        try { renderW = win.RenderWidth; renderH = win.RenderHeight; } catch { }
        metricsRenderW = renderW;
        metricsRenderH = renderH;
        // R-3b-0（2026-10-28）：把引擎的绘制原点/基线/默认色同源给 FE。
        // - originX＝ConsoleStyledString/ConsoleImagePart/ConsoleRectangleShapePart 的 +3px 位移
        //   （ConsoleDivPart.DrawTo 不加，FE 侧必须按 span 类型区分）。
        // - baselineOffset＝|font ascent|（Skia Ascent 为负；引擎 DrawTo 里 Offset(0,-Ascent)）。
        // - foreColor/BackColor＝引擎实际默认色（FE 旧值 0.86 与 #C0C0C0 不一致）。
        int originX = Config.DrawingParam_ShapePositionShift;
        float baselineOffset = Config.FontSize;
        try { baselineOffset = -Config.DefaultFont.Metrics.Ascent; } catch { }
        var foreColor = HtmlManager.GetColorToStringArgb(Config.ForeColor);
        var backColor = HtmlManager.GetColorToStringArgb(Config.BackColor);
        EwpOut.Emit("{\"t\":\"meta.metrics\",\"metrics\":{\"cellW\":" + cellW
            + ",\"cellH\":" + Config.FontSize
            + ",\"lineH\":" + Config.LineHeight
            + ",\"font\":" + J(Config.FontName)
            + ",\"fontSize\":" + Config.FontSize
            + ",\"renderW\":" + renderW
            + ",\"renderH\":" + renderH
            + ",\"originX\":" + originX
            + ",\"baselineOffset\":" + D(baselineOffset)
            + ",\"foreColor\":" + J(foreColor)
            + ",\"bgColor\":" + J(backColor) + "}}");
    }

    private static string SafeVersion()
    {
        try { return PlatformInterop.GetProductVersion() ?? ""; } catch { return ""; }
    }

    private static string SafeGameId()
    {
        try
        {
            var dir = Directory.GetCurrentDirectory();
            var name = Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            return string.IsNullOrEmpty(name) ? dir : name;
        }
        catch { return ""; }
    }

    public void NotifyProcessExit()
    {
        running = false;
        ChannelHooks.Handler = null;
        // 先发 exit 再清像素流：像素流 Dispose 若抛异常也不能把 exit 吞掉。
        EwpOut.Emit("{\"t\":\"exit\"}");
        try { pixel.Dispose(); } catch { }
        EwpOut.Shutdown();
    }

    /// <summary>频道宏（脚本线程，EwpChannel 转发）：begin=create(首见)+begin{rev}+新段；
    /// end=commit{rev}（与配对 begin 同 rev 定稿）；clear=clear{id}（清段不清频道，
    /// rev/open/行 owner 保持）。未配对 end/clear 静默忽略并计 channelMismatch（state 可观测）。</summary>
    private void OnChannelMacro(string macro, string id, string kind)
    {
        // H4：FE 未声明 channel 时只停发 wire 事件，不改变 ERB 宏本身的空展开行为。
        if (!channelEnabled)
            return;
        switch (macro)
        {
            case "begin":
                if (string.IsNullOrEmpty(kind))
                    kind = "log";
                lock (channelGate)
                {
                    if (string.IsNullOrEmpty(id))
                    {
                        channelMismatch++;
                        break;
                    }
                    if (!channels.TryGetValue(id, out var st))
                    {
                        st = new ChannelState { Kind = kind };
                        channels[id] = st;
                        EwpOut.Emit("{\"t\":\"channel.create\",\"id\":" + J(id)
                            + ",\"kind\":" + J(kind) + ",\"tier\":\"grid-locked\"}");
                    }
                    // 同 id 重复 begin＝移栈顶＋rev++，不形成嵌套。
                    openStack.Remove(id);
                    openStack.Add(id);
                    st.Rev++;
                    st.Open = true;
                    st.Segments.Add(new ChannelSegment { Rev = st.Rev, Committed = false });
                    EwpOut.Emit("{\"t\":\"channel.begin\",\"id\":" + J(id) + ",\"rev\":" + st.Rev + "}");
                }
                break;
            case "end":
                lock (channelGate)
                {
                    if (channels.TryGetValue(id, out var st) && st.Open)
                    {
                        st.Open = false;
                        openStack.Remove(id);
                        var seg = st.Segments.Find(s => s.Rev == st.Rev);
                        if (seg != null)
                            seg.Committed = true;
                        EwpOut.Emit("{\"t\":\"channel.commit\",\"id\":" + J(id) + ",\"rev\":" + st.Rev + "}");
                    }
                    else
                        channelMismatch++;
                }
                break;
            case "clear":
                lock (channelGate)
                {
                    if (channels.TryGetValue(id, out var st))
                    {
                        // 清内容不清频道：段清空，rev/open 与行 owner 保持。
                        st.Segments.Clear();
                        EwpOut.Emit("{\"t\":\"channel.clear\",\"id\":" + J(id) + "}");
                    }
                    else
                        channelMismatch++;
                }
                break;
        }
    }

    private EmueraConsole GameConsole => GlobalStatic.Console;

    private void StdinLoop()
    {
        // console.Initialize completes on MainWindow.Shown; wait for handle + console.
        while (running && (!win.IsHandleCreated || GameConsole == null))
            Thread.Sleep(50);
        if (running)
            Subscribe();
        EwpOut.Emit("{\"t\":\"ready\"}");
        IEwpCommandReader reader = null;
        try { reader = EwpOut.Codec.CreateReader(Console.OpenStandardInput()); }
        catch { }
        CommandLoop(reader);
    }

    /// <summary>TCP 模式：单客户端；接入即挂输出流（期间事件在 EwpOut 队列缓冲），断开＝会话终结。</summary>
    private void AcceptLoop()
    {
        TcpListener listener = null;
        try
        {
            listener = new TcpListener(System.Net.IPAddress.Loopback, tcpPort);
            listener.Start(1);
            using var client = listener.AcceptTcpClient();
            listener.Stop();
            var stream = client.GetStream();
            EwpOut.AttachStream(stream);
            while (running && (!win.IsHandleCreated || GameConsole == null))
                Thread.Sleep(50);
            if (running)
                Subscribe();
            EwpOut.Emit("{\"t\":\"ready\"}");
            using var reader = EwpOut.Codec.CreateReader(stream);
            CommandLoop(reader);
        }
        catch { DispatchQuit(); }
        finally { try { listener?.Stop(); } catch { } }
    }

    private void CommandLoop(IEwpCommandReader reader)
    {
        while (running)
        {
            string line;
            try { line = reader != null ? reader.Read() : null; }
            catch { break; }
            if (line == null)
            {
                // consumer closed the transport -> graceful quit
                DispatchQuit();
                break;
            }
            if (line.Length == 0)
                continue;
            HandleLine(line);
        }
    }

    private void Subscribe()
    {
        var console = GameConsole;
        if (console == null)
            return;
        ApplyPendingViewport();
        console.CliLineAdded += OnLineAdded;
        console.CliWaitInputEntered += OnWaitInput;
        console.CliTerminalStateEntered += OnTerminalState;
        console.CliLinesRemoved += OnLinesRemoved;
        console.CliDisplayCleared += OnDisplayCleared;
        console.CliImageLayerSet += OnImageLayerSet;
        console.CliImageLayerCleared += OnImageLayerCleared;
        console.CliImageLayersCleared += OnImageLayersCleared;
        console.CliIslandPrinted += OnIslandPrinted;
        console.CliIslandCleared += OnIslandCleared;
        console.CliFramePainted += OnFramePainted;
        EmitMetrics();
    }

    /// <summary>RefreshStrings 有效通过点（UI 线程，B3 像素流帧信号；仅 mode.pixel=true 时实做捕获）。</summary>
    private void OnFramePainted() => pixel.OnFramePainted();

    /// <summary>应用 FE 视口（UI 线程）。⚠️ 最小化窗口上 ClientSize 不会触发子控件锚定布局
    /// （2026-10-25 fixture 实测 render=0x0 而 client=1000x640，mainPicBox 恒 0）——
    /// 先强制 PerformLayout；仍未落位则微扰一次尺寸（两次尺寸变更必跑布局链）。</summary>
    private void ApplyViewport(int w, int h)
    {
        win.Invoke(new Action(() =>
        {
            win.ClientSize = new Size(w, h);
            win.PerformLayout();
            if (win.RenderWidth <= 0 || win.RenderHeight <= 0)
            {
                win.ClientSize = new Size(w - 1, h - 1);
                win.ClientSize = new Size(w, h);
            }
            // N1-b：viewport 可能把 render 画布从「初始 Config 尺寸」改到实际 client 尺寸；
            // 原逻辑只在初始 render<=0 时补发 metrics，headless（初始 render=Config 尺寸>0）
            // 会永远停在旧值。这里按实际变化补发一次（桌面同样受益，FE 更早拿到正确网格）。
            if (win.RenderWidth != metricsRenderW || win.RenderHeight != metricsRenderH)
                EmitMetrics();
        }));
    }

    /// <summary>应用 hello.ok 暂存的 FE 视口（UI 线程执行；ClientWidth/Height 是窗口活属性，改窗体即生效）。</summary>
    private void ApplyPendingViewport()
    {
        int w = Volatile.Read(ref pendingVpW), h = Volatile.Read(ref pendingVpH);
        if (w <= 0 || h <= 0)
            return;
        Volatile.Write(ref pendingVpW, 0);
        try { ApplyViewport(w, h); } catch { }
    }

    private void OnLineAdded(ConsoleDisplayLine line)
    {
        var id = ++lineIdSeq;
        var raw = line.ToString();
        var text = ClipboardProcessor.StripHTML(raw);
        // R-3b-1：仅在 FE 声明 legacy.spans 且 text 未被 StripHTML 改写时构建 spans；
        // 保证 span 的 start/end 与 wire 上的 text 同源（CBIgnoreTags 非常态配置时跳过并计数）。
        string spans = null;
        if (spansEnabled)
        {
            if (string.Equals(text, raw, StringComparison.Ordinal))
            {
                spans = BuildSpans(line);
                if (spans != null)
                    Interlocked.Increment(ref spansEmitted);
            }
            else
            {
                Interlocked.Increment(ref spansSkipped);
            }
        }
        var owner = "";
        long ownerRev = 0;
        lock (channelGate)
        {
            if (openStack.Count > 0)
            {
                owner = openStack[openStack.Count - 1];
                if (channels.TryGetValue(owner, out var st))
                {
                    ownerRev = st.Rev;
                    // clear 后仍 open 的频道：下一次内容到达时重建当前 rev 的未提交段。
                    if (!st.Segments.Exists(s => s.Rev == ownerRev))
                        st.Segments.Add(new ChannelSegment { Rev = ownerRev, Committed = false });
                }
                else
                    owner = "";
            }
        }
        // W0：仅含运行期行内像素（rt）的行保留行对象，供 snapshot 重建 spans；
        // 普通行（绝大多数）不持有 ConsoleDisplayLine，避免无意扩大长会话 retained 面。
        // 行清理（CLEARLINE/ClearDisplay）随 liveLines 释放；引擎自动 MaxLog 裁剪不发
        // CliLinesRemoved 属既有账本问题（文本/spans 本已保留），W1/T6 再单独处理。
        bool needLine = spans != null && spans.Contains("\"rt\":true");
        liveLines.Add(new LiveLine { Id = id, Text = text, Spans = spans,
            Line = needLine ? line : null, Owner = owner, OwnerRev = ownerRev });
        var json = "{\"t\":\"legacy.line\",\"lineId\":" + id
            + ",\"gen\":" + currentGeneration
            + ",\"text\":" + J(text);
        if (line.Align != DisplayLineAlignment.LEFT)
            json += ",\"align\":\"" + (line.Align == DisplayLineAlignment.CENTER ? "center" : "right") + "\"";
        if (spans != null)
            json += ",\"spans\":" + spans;
        json += "}";
        EwpOut.Emit(json);
    }

    /// <summary>R-3b-1：构建 legacy.line.spans（可选；仅 FE 声明 legacy.spans 且 text 未被
    /// StripHTML 改写时调用）。start/end 为 text 的 Unicode 码点偏移；div 整体跳过，
    /// 由 FE F-div 继续负责。发 x/advance 与解析色，文本的 x 含引擎对齐位移。</summary>
    private string BuildSpans(ConsoleDisplayLine line)
    {
        var spans = new List<string>();
        int cp = 0;
        bool anyNonText = false, anyStyled = false;
        foreach (var button in line.Buttons)
        {
            if (button?.StrArray == null)
                continue;
            foreach (var node in button.StrArray)
            {
                if (node == null)
                    continue;
                string s = node.ToString() ?? "";
                int len = CodePointLength(s);
                int start = cp, end = cp + len;
                cp = end;

                if (node is ConsoleStyledString css)
                {
                    var st = css.StringStyle;
                    bool def = !st.ColorChanged && st.Color == Config.ForeColor
                        && !st.BackgroundColor.HasValue && st.ButtonColor == Config.FocusColor
                        && st.FontStyle == FontStyle.Regular
                        && (string.IsNullOrEmpty(st.Fontname) || string.Equals(st.Fontname, Config.FontName, StringComparison.OrdinalIgnoreCase))
                        && !css.FontSize.HasValue && !css.VerticalAlign.HasValue;
                    if (!def)
                        anyStyled = true;
                    if (len == 0)
                        continue;
                    var sb = new StringBuilder(128);
                    sb.Append("{\"start\":").Append(start).Append(",\"end\":").Append(end)
                        .Append(",\"kind\":\"text\",\"x\":").Append(css.PointX)
                        .Append(",\"advance\":").Append(css.Width)
                        .Append(",\"color\":").Append(J(HtmlManager.GetColorToStringArgb(st.Color)));
                    if (st.BackgroundColor.HasValue)
                        sb.Append(",\"bg\":").Append(J(HtmlManager.GetColorToStringArgb(st.BackgroundColor.Value)));
                    if ((st.FontStyle & FontStyle.Bold) != 0) sb.Append(",\"bold\":true");
                    if ((st.FontStyle & FontStyle.Italic) != 0) sb.Append(",\"italic\":true");
                    if ((st.FontStyle & FontStyle.Underline) != 0) sb.Append(",\"underline\":true");
                    if ((st.FontStyle & FontStyle.Strikeout) != 0) sb.Append(",\"strike\":true");
                    if (!string.IsNullOrEmpty(st.Fontname) && !string.Equals(st.Fontname, Config.FontName, StringComparison.OrdinalIgnoreCase))
                        sb.Append(",\"font\":").Append(J(st.Fontname));
                    if (css.FontSize.HasValue)
                        sb.Append(",\"size\":").Append(D(css.FontSize.Value));
                    if (css.VerticalAlign.HasValue)
                        sb.Append(",\"valign\":").Append(J(css.VerticalAlign.Value.ToString().ToLowerInvariant()));
                    sb.Append('}');
                    spans.Add(sb.ToString());
                    continue;
                }

                anyNonText = true;
                if (node is ConsoleImagePart img)
                {
                    var dr = img.DestRect;
                    int dx, dy;
                    string display = img.Display switch
                    {
                        DisplayMode.AbsoluteLeftTop => "absolute-lefttop",
                        DisplayMode.AbsoluteLeftBottom => "absolute-leftbottom",
                        DisplayMode.Absolute => "absolute",
                        _ => "relative"
                    };
                    if (img.Display == DisplayMode.Relative)
                    {
                        dx = img.PointX + dr.X;
                        dy = dr.Y;
                    }
                    else if (img.Display == DisplayMode.AbsoluteLeftBottom)
                    {
                        dx = img.PositionX;
                        dy = (GameConsole?.ClientHeight ?? 0) + img.PositionY;
                    }
                    else
                    {
                        dx = img.PositionX;
                        dy = img.PositionY;
                    }
                    var sb = new StringBuilder(160);
                    sb.Append("{\"start\":").Append(start).Append(",\"end\":").Append(end)
                        .Append(",\"kind\":\"img\",\"x\":").Append(img.PointX)
                        .Append(",\"advance\":").Append(img.Width)
                        .Append(",\"draw\":{\"x\":").Append(dx).Append(",\"y\":").Append(dy)
                        .Append(",\"w\":").Append(dr.Width).Append(",\"h\":").Append(dr.Height).Append('}')
                        .Append(",\"src\":").Append(J(img.ResourceName ?? ""));
                    if (!string.IsNullOrEmpty(img.ButtonResourceName))
                        sb.Append(",\"srcb\":").Append(J(img.ButtonResourceName));
                    if (!string.IsNullOrEmpty(img.MappingGraphName))
                        sb.Append(",\"srcm\":").Append(J(img.MappingGraphName));
                    if (display != "relative")
                        sb.Append(",\"display\":").Append(J(display));
                    if (img.MissingImage)
                        sb.Append(",\"missing\":true");
                    if (img.HasColorMatrix)
                        sb.Append(",\"cm\":true");
                    // R-3b-2b：运行期 G 名（SpriteG）在 span 构建时冻结像素快照；
                    // FE 用 (src,rev) 发 sprite.req，旧 rev 永不返回新图。literal（SpriteF）走 2a。
                    if (spriteExport.Enabled && img.Image is SpriteG && dr.Width > 0 && dr.Height > 0)
                    {
                        long rtRev = spriteExport.CaptureInline(img.Image, dr.Width, dr.Height, img.ColorMatrix);
                        if (rtRev > 0)
                            sb.Append(",\"rt\":true,\"rev\":").Append(rtRev);
                    }
                    sb.Append('}');
                    spans.Add(sb.ToString());
                    continue;
                }
                if (node is ConsoleShapePart shape)
                {
                    var sb = new StringBuilder(128);
                    sb.Append("{\"start\":").Append(start).Append(",\"end\":").Append(end)
                        .Append(",\"kind\":\"shape\",\"shape\":").Append(J(shape.ShapeKind))
                        .Append(",\"x\":").Append(shape.PointX)
                        .Append(",\"advance\":").Append(shape.Width);
                    if (shape.HasDrawRect)
                    {
                        var r = shape.DrawRect;
                        // shape rect 的 rect.X 已烘入 originX（ConsoleShapePart.SetWidth），
                        // 归一到「行内坐标不含 originX」，FE 再统一加 meta.metrics.originX。
                        int dx = shape.PointX + r.X - Config.DrawingParam_ShapePositionShift;
                        sb.Append(",\"draw\":{\"x\":").Append(dx).Append(",\"y\":").Append(r.Y)
                            .Append(",\"w\":").Append(r.Width).Append(",\"h\":").Append(r.Height).Append('}')
                            .Append(",\"color\":").Append(J(HtmlManager.GetColorToStringArgb(shape.ShapeColor)));
                    }
                    else if (shape.ShapeKind == "error")
                    {
                        sb.Append(",\"color\":").Append(J(HtmlManager.GetColorToStringArgb(shape.ShapeColor)));
                    }
                    sb.Append('}');
                    spans.Add(sb.ToString());
                    continue;
                }
                if (node is ConsoleDivPart div)
                {
                    // R-3b-3（W1）：div 整块附带**有界** children（text/img/shape/div 同形），
                    // 坐标相对 div 内容框；递归深度与载荷上限固定，截断时标 truncated。
                    int budget = MaxDivChildSpans;
                    var sb = new StringBuilder(256);
                    sb.Append("{\"start\":").Append(start).Append(",\"end\":").Append(end)
                        .Append(",\"kind\":\"div\",\"x\":").Append(div.PointX);
                    int picHeight = GameConsole?.ClientHeight ?? 0;
                    var content = div.GetContentRect(picHeight, 0);
                    var children = BuildDivChildrenCore(div, start, 0, content.X, content.Y,
                        picHeight, ref budget, ref anyStyled, out bool truncated);
                    if (children.Count > 0)
                    {
                        sb.Append(",\"children\":[").Append(string.Join(",", children)).Append(']');
                        if (truncated)
                            sb.Append(",\"truncated\":true");
                        // 计入整棵 div 子树（含嵌套；budget 为共享配额）。
                        Interlocked.Add(ref spansChildNodes, MaxDivChildSpans - budget);
                    }
                    if (truncated)
                        Interlocked.Increment(ref spansChildTruncated);
                    sb.Append('}');
                    spans.Add(sb.ToString());
                    continue;
                }
                // 未知节点：保守当作不可绘制跨度（只推进码点，不画）。
                spans.Add("{\"start\":" + start + ",\"end\":" + end + ",\"kind\":\"unsupported\"}");
            }
        }
        if (!anyNonText && !anyStyled && line.Align == DisplayLineAlignment.LEFT)
            return null;
        if (spans.Count == 0)
            return null;
        return "[" + string.Join(",", spans) + "]";
    }

    private static int CodePointLength(string s)
    {
        int n = 0;
        for (int i = 0; i < s.Length; i++)
        {
            if (char.IsHighSurrogate(s[i]) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
                i++;
            n++;
        }
        return n;
    }

    /// <summary>R-3b-3（W1）：div 子节点 spans（坐标相对 div 内容框；start/end 仍对应
    /// legacy.line.text 的 Unicode 码点区间，供 FE 从原始行取同一段文本）。
    /// budget＝整棵 div 子树共享的载荷配额；超限即停并置 truncated（⛔ 静默丢节点）。</summary>
    private List<string> BuildDivChildrenCore(ConsoleDivPart div, int divStart, int depth,
        int contentXAbs, int contentYAbs, int picHeight,
        ref int budget, ref bool anyStyled, out bool truncated)
    {
        truncated = false;
        var list = new List<string>();
        var children = div.Children;
        if (children == null || children.Length == 0)
            return list;
        int cp = divStart + div.AltHeadTagLength;
        int lineY = 0;
        foreach (var childLine in children)
        {
            if (childLine?.Buttons != null)
            {
                foreach (var button in childLine.Buttons)
                {
                    if (button?.StrArray == null)
                        continue;
                    foreach (var node in button.StrArray)
                    {
                        if (node == null)
                            continue;
                        string s = node.ToString() ?? "";
                        int len = CodePointLength(s);
                        int start = cp, end = cp + len;
                        cp = end;
                        if (budget <= 0)
                        {
                            truncated = true;
                            return list;
                        }
                        budget--;
                        string json = BuildDivChildSpan(node, start, end, lineY, contentXAbs, contentYAbs,
                            depth, picHeight, ref budget, ref anyStyled, out bool childTrunc);
                        if (childTrunc)
                            truncated = true;
                        if (json != null)
                            list.Add(json);
                    }
                }
            }
            cp += 2;  // ConsoleDivPart.ToString：每条子行后追加 "\r\n"
            lineY += Config.LineHeight;
        }
        return list;
    }

    private string BuildDivChildSpan(AConsoleDisplayNode node, int start, int end, int lineY,
        int contentXAbs, int contentYAbs, int depth, int picHeight,
        ref int budget, ref bool anyStyled, out bool truncated)
    {
        truncated = false;
        if (node is ConsoleStyledString css)
        {
            if (end <= start)
                return null;
            var st = css.StringStyle;
            bool def = !st.ColorChanged && st.Color == Config.ForeColor
                && !st.BackgroundColor.HasValue && st.ButtonColor == Config.FocusColor
                && st.FontStyle == FontStyle.Regular
                && (string.IsNullOrEmpty(st.Fontname) || string.Equals(st.Fontname, Config.FontName, StringComparison.OrdinalIgnoreCase))
                && !css.FontSize.HasValue && !css.VerticalAlign.HasValue;
            if (!def)
                anyStyled = true;
            var sb = new StringBuilder(128);
            sb.Append("{\"start\":").Append(start).Append(",\"end\":").Append(end)
                .Append(",\"kind\":\"text\",\"x\":").Append(css.PointX - contentXAbs)
                .Append(",\"y\":").Append(lineY)
                .Append(",\"advance\":").Append(css.Width)
                .Append(",\"color\":").Append(J(HtmlManager.GetColorToStringArgb(st.Color)));
            if (st.BackgroundColor.HasValue)
                sb.Append(",\"bg\":").Append(J(HtmlManager.GetColorToStringArgb(st.BackgroundColor.Value)));
            if ((st.FontStyle & FontStyle.Bold) != 0) sb.Append(",\"bold\":true");
            if ((st.FontStyle & FontStyle.Italic) != 0) sb.Append(",\"italic\":true");
            if ((st.FontStyle & FontStyle.Underline) != 0) sb.Append(",\"underline\":true");
            if ((st.FontStyle & FontStyle.Strikeout) != 0) sb.Append(",\"strike\":true");
            if (!string.IsNullOrEmpty(st.Fontname) && !string.Equals(st.Fontname, Config.FontName, StringComparison.OrdinalIgnoreCase))
                sb.Append(",\"font\":").Append(J(st.Fontname));
            if (css.FontSize.HasValue)
                sb.Append(",\"size\":").Append(D(css.FontSize.Value));
            if (css.VerticalAlign.HasValue)
                sb.Append(",\"valign\":").Append(J(css.VerticalAlign.Value.ToString().ToLowerInvariant()));
            sb.Append('}');
            return sb.ToString();
        }
        if (node is ConsoleImagePart img)
        {
            var dr = img.DestRect;
            int dx, dy;
            string display = img.Display switch
            {
                DisplayMode.AbsoluteLeftTop => "absolute-lefttop",
                DisplayMode.AbsoluteLeftBottom => "absolute-leftbottom",
                DisplayMode.Absolute => "absolute",
                _ => "relative"
            };
            if (img.Display == DisplayMode.Relative)
            {
                dx = img.PointX + dr.X - contentXAbs;
                dy = dr.Y;
            }
            else if (img.Display == DisplayMode.AbsoluteLeftBottom)
            {
                dx = img.PositionX;
                dy = picHeight + img.PositionY;
            }
            else
            {
                dx = img.PositionX;
                dy = img.PositionY;
            }
            var sb = new StringBuilder(192);
            sb.Append("{\"start\":").Append(start).Append(",\"end\":").Append(end)
                .Append(",\"kind\":\"img\",\"x\":").Append(img.PointX - contentXAbs)
                .Append(",\"y\":").Append(lineY)
                .Append(",\"advance\":").Append(img.Width)
                .Append(",\"draw\":{\"x\":").Append(dx).Append(",\"y\":").Append(dy)
                .Append(",\"w\":").Append(dr.Width).Append(",\"h\":").Append(dr.Height).Append('}')
                .Append(",\"src\":").Append(J(img.ResourceName ?? ""));
            if (!string.IsNullOrEmpty(img.ButtonResourceName))
                sb.Append(",\"srcb\":").Append(J(img.ButtonResourceName));
            if (!string.IsNullOrEmpty(img.MappingGraphName))
                sb.Append(",\"srcm\":").Append(J(img.MappingGraphName));
            if (display != "relative")
                sb.Append(",\"display\":").Append(J(display));
            if (img.MissingImage)
                sb.Append(",\"missing\":true");
            if (img.HasColorMatrix)
                sb.Append(",\"cm\":true");
            if (spriteExport.Enabled && img.Image is SpriteG && dr.Width > 0 && dr.Height > 0)
            {
                long rtRev = spriteExport.CaptureInline(img.Image, dr.Width, dr.Height, img.ColorMatrix);
                if (rtRev > 0)
                    sb.Append(",\"rt\":true,\"rev\":").Append(rtRev);
            }
            sb.Append('}');
            return sb.ToString();
        }
        if (node is ConsoleShapePart shape)
        {
            var sb = new StringBuilder(160);
            sb.Append("{\"start\":").Append(start).Append(",\"end\":").Append(end)
                .Append(",\"kind\":\"shape\",\"shape\":").Append(J(shape.ShapeKind))
                .Append(",\"x\":").Append(shape.PointX - contentXAbs)
                .Append(",\"y\":").Append(lineY)
                .Append(",\"advance\":").Append(shape.Width);
            if (shape.HasDrawRect)
            {
                var r = shape.DrawRect;
                int dx = shape.PointX + r.X - Config.DrawingParam_ShapePositionShift - contentXAbs;
                sb.Append(",\"draw\":{\"x\":").Append(dx).Append(",\"y\":").Append(r.Y)
                    .Append(",\"w\":").Append(r.Width).Append(",\"h\":").Append(r.Height).Append('}')
                    .Append(",\"color\":").Append(J(HtmlManager.GetColorToStringArgb(shape.ShapeColor)));
            }
            else if (shape.ShapeKind == "error")
            {
                sb.Append(",\"color\":").Append(J(HtmlManager.GetColorToStringArgb(shape.ShapeColor)));
            }
            sb.Append('}');
            return sb.ToString();
        }
        if (node is ConsoleDivPart nd)
        {
            var nContent = nd.GetContentRect(picHeight, 0);
            int bl = InsetOf(nd.DivBorder, Utils.Direction.Left), br = InsetOf(nd.DivBorder, Utils.Direction.Right);
            int bt = InsetOf(nd.DivBorder, Utils.Direction.Top), bb = InsetOf(nd.DivBorder, Utils.Direction.Bottom);
            int pl = InsetOf(nd.DivPadding, Utils.Direction.Left), pr = InsetOf(nd.DivPadding, Utils.Direction.Right);
            int pt = InsetOf(nd.DivPadding, Utils.Direction.Top), pb = InsetOf(nd.DivPadding, Utils.Direction.Bottom);
            var sb = new StringBuilder(320);
            sb.Append("{\"start\":").Append(start).Append(",\"end\":").Append(end)
                .Append(",\"kind\":\"div\",\"x\":").Append(nContent.X - contentXAbs)
                .Append(",\"y\":").Append(lineY)
                .Append(",\"box\":{\"x\":").Append(nContent.X - contentXAbs - (bl + pl))
                .Append(",\"y\":").Append(lineY + nContent.Y - (bt + pt))
                .Append(",\"w\":").Append(nContent.Width + bl + br + pl + pr)
                .Append(",\"h\":").Append(nContent.Height + bt + bb + pt + pb)
                .Append(",\"border\":{\"top\":").Append(bt).Append(",\"right\":").Append(br)
                .Append(",\"bottom\":").Append(bb).Append(",\"left\":").Append(bl).Append('}')
                .Append(",\"padding\":{\"top\":").Append(pt).Append(",\"right\":").Append(pr)
                .Append(",\"bottom\":").Append(pb).Append(",\"left\":").Append(pl).Append('}')
                .Append(",\"color\":").Append(J(HtmlManager.GetColorToStringArgb(nd.DivBackgroundColor)));
            if (nd.DivBorderColors != null)
            {
                sb.Append(",\"bcolor\":[");
                for (int i = 0; i < 4; i++)
                {
                    if (i > 0)
                        sb.Append(',');
                    sb.Append(J(HtmlManager.GetColorToStringArgb(nd.DivBorderColors[i])));
                }
                sb.Append(']');
            }
            sb.Append('}');
            if (nd.Children != null && nd.Children.Length > 0)
            {
                if (depth < MaxDivNesting)
                {
                    int nestedContentYAbs = contentYAbs + lineY + nContent.Y;
                    var nested = BuildDivChildrenCore(nd, start, depth + 1, nContent.X, nestedContentYAbs,
                        picHeight, ref budget, ref anyStyled, out bool nt);
                    if (nt)
                        truncated = true;
                    if (nested.Count > 0)
                        sb.Append(",\"children\":[").Append(string.Join(",", nested)).Append(']');
                    else if (nt)
                        sb.Append(",\"truncated\":\"depth\"");
                }
                else
                {
                    truncated = true;
                    sb.Append(",\"truncated\":\"depth\"");
                }
            }
            sb.Append('}');
            return sb.ToString();
        }
        return "{\"start\":" + start + ",\"end\":" + end + ",\"kind\":\"unsupported\",\"y\":" + lineY + "}";
    }

    private static int InsetOf(int[] arr, int dir)
        => arr != null && dir >= 0 && dir < arr.Length ? arr[dir] : 0;

    /// <summary>CLEARLINE：按尾部裁剪并广播 legacy.clear（EWP §4.1，fromLineId + count）。</summary>
    private void OnLinesRemoved(int count)
    {
        if (count <= 0 || liveLines.Count == 0)
            return;
        int removed = Math.Min(count, liveLines.Count);
        long fromId = liveLines[liveLines.Count - removed].Id;
        liveLines.RemoveRange(liveLines.Count - removed, removed);
        EwpOut.Emit("{\"t\":\"legacy.clear\",\"fromLineId\":" + fromId
            + ",\"count\":" + removed + "}");
    }

    /// <summary>ClearDisplay 全量清场：清快照行/频道/段/栈，向 FE 发一条清空通知。
    /// v0.2 不发 channel.destroy；重连以无旧状态的 snapshot 为准。</summary>
    private void OnDisplayCleared()
    {
        int count = liveLines.Count;
        long fromId = count > 0 ? liveLines[0].Id : -1;
        liveLines.Clear();
        spriteExport.Clear();
        // EG-1b/EG-1d：ClearDisplay＝整屏换场（启动/GotoTitle/读档前），图层与 island 账本
        // 必须同源清空；否则新会话快照会带出已 Dispose 的旧 sprite（DrawTo 跳过但账本残留）。
        bool hadSprites = liveSprites.Count > 0;
        liveSprites.Clear();
        bool hadIsland = liveIslandHtml != null;
        liveIslandHtml = null;
        lock (channelGate)
        {
            channels.Clear();
            openStack.Clear();
            channelMismatch = 0;
        }
        if (hadSprites)
            EwpOut.Emit("{\"t\":\"sprite.clear\",\"all\":true}");
        if (hadIsland)
            EwpOut.Emit("{\"t\":\"overlay.island.clear\"}");
        if (count > 0)
            EwpOut.Emit("{\"t\":\"legacy.clear\",\"fromLineId\":" + fromId + ",\"count\":-1}");
    }

    private void OnWaitInput()
    {
        var console = GameConsole;
        if (console == null || console.inputReq == null)
            return;
        var req = console.inputReq;
        // I3-b：Subscribe 期窗口若未完成布局，meta.metrics 的 renderW/H 会读到 0（最小化实测）。
        // 首个 input.request 时窗口必已就绪 ⇒ 补发一次，让 FE 在拿到 rect 之前就有画布尺寸。
        if (!metricsRenderResent && metricsRenderW <= 0)
        {
            int probeW = 0, probeH = 0;
            try { probeW = win.RenderWidth; probeH = win.RenderHeight; } catch { }
            if (probeW > 0 && probeH > 0)
            {
                metricsRenderResent = true;
                EmitMetrics();
            }
        }
        // I3-b：统一走几何版本（同一遍历，多算几个 int，开销可忽略），
        // 是否把 rect 写进 wire 由 FE 请求的 input.rect cap 决定。
        var buttons = console.GetSelectableButtonRects();
        var reqId = ++reqIdSeq;
        currentReqId = reqId;
        currentGeneration = console.LastButtonGeneration;
        var sb = new StringBuilder(256);
        sb.Append("{\"t\":\"input.request\",\"reqId\":").Append(reqId)
            .Append(",\"type\":").Append(J(InputTypeName(req.InputType)))
            .Append(",\"generation\":").Append(currentGeneration);
        if (req.NeedValue)
            sb.Append(",\"needValue\":true");
        if (req.OneInput)
            sb.Append(",\"oneInput\":true");
        if (req.Timelimit > 0)
            sb.Append(",\"timeoutMs\":").Append(req.Timelimit);
        if (req.HasDefValue)
            sb.Append(",\"default\":").Append(J(req.DefStrValue));
        sb.Append(",\"buttons\":[");
        for (int i = 0; i < buttons.Count; i++)
        {
            if (i > 0)
                sb.Append(',');
            var button = buttons[i];
            sb.Append("{\"index\":").Append(i)
                .Append(",\"text\":").Append(J(button.Text))
                .Append(",\"value\":").Append(J(button.Value))
                .Append(",\"isInteger\":").Append(button.IsInteger ? "true" : "false");
            if (rectEnabled && button.HasRect)
            {
                // I3-c 生产护栏：先跑连通性自检（中心＋共同区四角共 5 点须全部命中同一按钮）。
                // 不过就降级不发 rect（FE 回退文本/值），只计数——⛔ 宁可没有 rect，不可点错。
                // I3-d：div 按钮（HitTestOracle=false）跳过自检——其 hit-test 与绘制在绝对定位下分叉
                // （设计卡 §3.3），rect 由 GetContentRect 的绘制矩形推出，拿 hit-test 当 oracle 必误判。
                bool selfCheckOk = !button.HitTestOracle
                    || console.RectSelfCheckPoints(button.Button, button.Rect) == 0;
                if (selfCheckOk)
                {
                    sb.Append(",\"rect\":{\"x\":").Append(button.Rect.X)
                        .Append(",\"y\":").Append(button.Rect.Y)
                        .Append(",\"w\":").Append(button.Rect.Width)
                        .Append(",\"h\":").Append(button.Rect.Height)
                        .Append(",\"coordinateSpace\":\"viewport-global\"}");
                }
                else
                {
                    Interlocked.Increment(ref rectSelfCheckFailed);
                }
            }
            sb.Append('}');
        }
        sb.Append("]}");
        EwpOut.Emit(sb.ToString());
    }

    private void OnTerminalState()
    {
        EwpOut.Emit("{\"t\":\"meta.state\",\"state\":" + J(GameConsole?.CliStateName ?? "Unknown") + "}");
    }

    /// <summary>SETIMAGELAYER(L)→sprite.set（EWP §4.3；colormatrix 留 v1＝A1-③）。
    /// R-3a：先用实际 ImageLayer 对象同步冻结快照，再发事件；rev 只在启用导出时出现。</summary>
    private void OnImageLayerSet(ImageLayer layer)
    {
        long rev = spriteExport.Capture(layer);
        string tex = layer.Image?.Name ?? "";
        liveSprites.Add((layer.Depth, tex, layer.X, layer.Y, layer.Width, layer.Height,
            layer.Opacity, layer.FollowScroll, rev));
        var sb = new StringBuilder(160);
        sb.Append("{\"t\":\"sprite.set\",\"depth\":").Append(layer.Depth)
            .Append(",\"texture\":").Append(J(tex))
            .Append(",\"x\":").Append(layer.X)
            .Append(",\"y\":").Append(layer.Y)
            .Append(",\"w\":").Append(layer.Width)
            .Append(",\"h\":").Append(layer.Height)
            .Append(",\"opacity\":").Append(layer.Opacity)
            .Append(",\"follow\":").Append(layer.FollowScroll ? "true" : "false");
        if (rev > 0)
            sb.Append(",\"rev\":").Append(rev);
        sb.Append('}');
        EwpOut.Emit(sb.ToString());
    }

    private void OnImageLayerCleared(long depth)
    {
        liveSprites.RemoveAll(s => s.Depth == depth);
        EwpOut.Emit("{\"t\":\"sprite.clear\",\"depth\":" + depth + "}");
    }

    private void OnImageLayersCleared()
    {
        liveSprites.Clear();
        EwpOut.Emit("{\"t\":\"sprite.clear\",\"all\":true}");
    }

    /// <summary>HTML_PRINT_ISLAND→overlay.island（v0 载荷＝原始 HTML 透传，A1-④ 单实例）。</summary>
    private void OnIslandPrinted(string html)
    {
        liveIslandHtml = html;
        EwpOut.Emit("{\"t\":\"overlay.island\",\"html\":" + J(html) + "}");
    }

    private void OnIslandCleared()
    {
        liveIslandHtml = null;
        EwpOut.Emit("{\"t\":\"overlay.island.clear\"}");
    }

    private static string InputTypeName(InputType t) => t switch
    {
        InputType.EnterKey => "EnterKey",
        InputType.AnyKey => "AnyKey",
        InputType.IntValue => "IntValue",
        InputType.StrValue => "StrValue",
        InputType.Void => "Void",
        InputType.AnyValue => "AnyValue",
        InputType.IntButton => "IntButton",
        InputType.StrButton => "StrButton",
        InputType.PrimitiveMouseKey => "PrimitiveMouseKey",
        _ => "Unknown",
    };

    private static string J(string s) => JsonSerializer.Serialize(s ?? "");

    /// <summary>EG-1b：统计用浮点 JSON 数值（invariant，最多 3 位小数）。</summary>
    private static string D(double v) => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

    private void HandleLine(string line)
    {
        JsonDocument doc;
        try { doc = JsonDocument.Parse(line); }
        catch
        {
            EmitResult(false, "protocol_violation", "invalid JSON line");
            return;
        }
        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                EmitResult(false, "protocol_violation", "command must be a JSON object");
                return;
            }
            string t = root.TryGetProperty("t", out var tv) && tv.ValueKind == JsonValueKind.String ? tv.GetString() : null;
            switch (t)
            {
                case "sprite.req":
                    Dispatch(() => DoSpriteRequest(root.Clone()));
                    break;
                case "input.submit":
                    Dispatch(() => DoInputSubmit(root.Clone()));
                    break;
                case "view.ack":
                    EwpOut.NoteAck();
                    break;
                case "hello.ok":
                    DoHelloOk(root);
                    break;
                // EWP/0.2 正名与 0.1 旧名共用同一入口；hello.proto 已翻为 0.2。
                case "state":
                case "sys.state":
                    Dispatch(() => DoState());
                    break;
                case "snapshot.req":
                case "sys.snapshot":
                    Dispatch(() => DoSnapshot());
                    break;
                case "sys.quit":
                    DispatchQuit();
                    break;
                // EG-1d：sys.title 落 console.GotoTitle（ClearDisplay ⇒ 精灵快照缓存清空）。
                case "sys.title":
                    Dispatch(() => DoTitle());
                    break;
                default:
                    EmitResult(false, "protocol_violation", "unknown or missing \"t\"");
                    break;
            }
        }
    }

    private void Dispatch(Action action)
    {
        try
        {
            if (win.IsDisposed)
                return;
            win.Invoke(new Action(() =>
            {
                var console = GameConsole;
                if (console == null)
                {
                    EmitResult(false, "internal", "console not ready");
                    return;
                }
                try { action(); }
                catch (Exception ex) { EmitResult(false, "internal", ex.GetType().Name + ": " + ex.Message); }
            }));
        }
        catch (Exception ex)
        {
            EmitResult(false, "internal", "dispatch failed: " + ex.Message);
        }
    }

    private void DoInputSubmit(JsonElement root)
    {
        var console = GameConsole;
        if (!console.IsWaitInputState)
        {
            EmitResult(false, "not_waiting", "console is not in input-wait state");
            return;
        }
        long reqId = -1;
        if (root.TryGetProperty("reqId", out var r) && r.ValueKind == JsonValueKind.Number && r.TryGetInt64(out var rid))
            reqId = rid;
        if (reqId != currentReqId)
        {
            // stale click/request must never reach the game (EWP 6.2; acceptance gate 3)
            EmitResult(false, "stale_input", "reqId " + reqId + " != current " + currentReqId);
            return;
        }
        string value = root.TryGetProperty("value", out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : "";
        string via = root.TryGetProperty("via", out var vi) && vi.ValueKind == JsonValueKind.String ? vi.GetString() : "text";
        switch (via)
        {
            case "button":
                console.PressEnterKey(false, value ?? "", true);
                break;
            case "enter":
                console.PressEnterKey(false, "", false);
                break;
            case "skip":
                console.PressEnterKey(true, "", false);
                break;
            default:
                console.PressEnterKey(false, value ?? "", false);
                break;
        }
        EmitResult(true, null, null);
    }

    /// <summary>FE 视口上报＋像素模式协商（传输线程）：句柄已建则立即经 UI 线程应用，否则暂存到 Subscribe。
    /// mode.pixel=true ⇒ 启用离屏像素流（EWP §3/§4.4；Enable 幂等且线程安全——
    /// 早于 Subscribe 启用也无妨，CliFramePainted 未挂前捕获不会发生）。</summary>
    private void DoHelloOk(JsonElement root)
    {
        // H4：caps 缺失＝旧 FE，保持 channel 常开；显式数组按是否含 channel 降级。
        // I3-b：同一次遍历顺带解析 input.rect（旧 FE 不请求 ⇒ rectEnabled=false，不发 rect）。
        // caps 存在但不是数组视为旧格式，按常开兼容。
        if (!root.TryGetProperty("caps", out var caps) || caps.ValueKind != JsonValueKind.Array)
        {
            channelEnabled = true;
            rectEnabled = false;
            spriteExport.Enabled = false;
            spansEnabled = false;
        }
        else
        {
            var hasChannel = false;
            var hasRect = false;
            var hasSpriteExport = false;
            var hasSpans = false;
            foreach (var cap in caps.EnumerateArray())
            {
                if (cap.ValueKind != JsonValueKind.String)
                    continue;
                var capName = cap.GetString();
                if (capName == "channel")
                    hasChannel = true;
                else if (capName == "input.rect")
                    hasRect = true;
                else if (capName == "sprite.export")
                    hasSpriteExport = true;
                else if (capName == "legacy.spans")
                    hasSpans = true;
            }
            channelEnabled = hasChannel;
            rectEnabled = hasRect;
            spriteExport.Enabled = hasSpriteExport;
            spansEnabled = hasSpans;
        }
        if (!channelEnabled)
        {
            // 降级期间不保留旧频道状态；后续 state/snapshot 必须表现为 channel 关闭。
            lock (channelGate)
            {
                channels.Clear();
                channelMismatch = 0;
            }
        }

        int w = 0, h = 0;
        if (root.TryGetProperty("viewport", out var vp) && vp.ValueKind == JsonValueKind.Object)
        {
            if (vp.TryGetProperty("width", out var we) && we.ValueKind == JsonValueKind.Number && we.TryGetInt32(out w)) { }
            if (vp.TryGetProperty("height", out var he) && he.ValueKind == JsonValueKind.Number && he.TryGetInt32(out h)) { }
        }
        if (w > 0 && h > 0)
        {
            if (win.IsHandleCreated)
            {
                try { ApplyViewport(w, h); } catch { }
            }
            else
            {
                Volatile.Write(ref pendingVpW, w);
                Volatile.Write(ref pendingVpH, h);
            }
            EmitResult(true, null, "viewport " + w + "x" + h);
        }
        else
            EmitResult(true, null, null);
        if (root.TryGetProperty("mode", out var mode) && mode.ValueKind == JsonValueKind.Object
            && mode.TryGetProperty("pixel", out var pe) && pe.ValueKind == JsonValueKind.True)
        {
            // 防呆（静默失效陷阱）：TEXTRENDERER 界面无 Skia 画布（mainPicBox 恒 0 尺寸，
            // OnPaint WINAPI 分支也不画 Skia 内容）——显性报错而非零帧静默（2026-10-25 fixture 实锤）
            if (Config.TextDrawingMode == TextDrawingMode.WINAPI)
                EwpOut.Emit("{\"t\":\"error\",\"code\":\"pixel_unavailable\",\"fatal\":false,"
                    + "\"detail\":\"pixel.frame requires SKIASHARP drawing interface (config DRAWING INTERFACE)\"}");
            else
                pixel.Enable();
        }
    }

    /// <summary>R-3a：FE 请求精确快照。只读不可变快照缓存，不查 activeSprites、不现画。
    /// 成功回 sprite.data；失败回 sprite.data.fail；⛔ 不返回 result。</summary>
    private void DoSpriteRequest(JsonElement root)
    {
        // EG-1b：旧 FE（caps 未声明 sprite.export）明确忽略——⛔不回 data/fail，只计数。
        if (!spriteExport.Enabled)
        {
            spriteExport.NoteIgnoredRequest();
            return;
        }
        string texture = null;
        long rev = 0;
        if (root.TryGetProperty("texture", out var texEl) && texEl.ValueKind == JsonValueKind.String)
            texture = texEl.GetString();
        if (root.TryGetProperty("rev", out var revEl) && revEl.ValueKind == JsonValueKind.Number)
            revEl.TryGetInt64(out rev);
        if (string.IsNullOrEmpty(texture) || rev <= 0)
        {
            EmitSpriteFail(texture ?? "", rev, "bad_request",
                string.IsNullOrEmpty(texture) ? "texture" : "rev");
            return;
        }

        string key = texture.ToUpperInvariant();
        if (!spriteExport.TryEncode(key, rev, out var png, out int w, out int h, out var code, out var detail))
        {
            EmitSpriteFail(key, rev, code ?? "not_exportable", detail);
            return;
        }

        var sb = new StringBuilder(256 + png.Length * 2);
        sb.Append("{\"t\":\"sprite.data\",\"texture\":").Append(J(key))
            .Append(",\"rev\":").Append(rev)
            .Append(",\"w\":").Append(w)
            .Append(",\"h\":").Append(h)
            .Append(",\"fmt\":\"png\",\"data\":\"").Append(Convert.ToBase64String(png)).Append("\"}");
        EwpOut.Emit(sb.ToString());
    }

    /// <summary>EG-1d：返回标题（console.GotoTitle 封装）。ClearDisplay 会清空精灵快照缓存，
    /// 旧 rev 之后必须报 not_found（不是 expired）。</summary>
    private void DoTitle()
    {
        GameConsole.GotoTitle();
        EmitResult(true, null, "title");
    }

    private static void EmitSpriteFail(string texture, long rev, string code, string detail)
    {
        var sb = new StringBuilder(128);
        sb.Append("{\"t\":\"sprite.data.fail\",\"texture\":").Append(J(texture ?? ""))
            .Append(",\"rev\":").Append(rev)
            .Append(",\"code\":").Append(J(code ?? "not_exportable"));
        if (!string.IsNullOrEmpty(detail))
            sb.Append(",\"detail\":").Append(J(detail));
        sb.Append('}');
        EwpOut.Emit(sb.ToString());
    }

    private void DoState()
    {
        var console = GameConsole;
        int channelCount;
        long mismatch;
        lock (channelGate)
        {
            channelCount = channelEnabled ? channels.Count : 0;
            mismatch = channelMismatch;
        }
        var spriteStats = spriteExport.Stats();
        EwpOut.Emit("{\"t\":\"result\",\"ok\":true,\"state\":" + J(console.CliStateName)
            + ",\"waitInput\":" + (console.IsWaitInputState ? "true" : "false")
            + ",\"reqId\":" + currentReqId
            + ",\"generation\":" + currentGeneration
            + ",\"lines\":" + liveLines.Count
            + ",\"client\":[" + console.ClientWidth + "," + console.ClientHeight + "]"
            + ",\"winClient\":[" + win.ClientSize.Width + "," + win.ClientSize.Height + "]"
            + ",\"winState\":\"" + win.WindowStateName + "\""
            + ",\"frames\":" + EwpOut.FramesWritten
            + ",\"acks\":" + EwpOut.Acks
            + ",\"pending\":" + EwpOut.PendingFrames
            + ",\"ackTimeouts\":" + EwpOut.AckTimeouts
            + ",\"pixel\":{\"enabled\":" + (pixel.Enabled ? "true" : "false")
            + ",\"frames\":" + pixel.Frames
            + ",\"dropped\":" + pixel.Dropped
            + ",\"written\":" + EwpOut.PixelWritten
            + ",\"writeDropped\":" + EwpOut.PixelDropped + "}"
            + ",\"channels\":" + channelCount
            + ",\"channelMismatch\":" + mismatch
            + ",\"channelEnabled\":" + (channelEnabled ? "true" : "false")
            + ",\"rectSelfCheckFailed\":" + Interlocked.Read(ref rectSelfCheckFailed)
            + ",\"rectDiag\":" + EmitRectDiag(console)
            + ",\"spansEnabled\":" + (spansEnabled ? "true" : "false")
            + ",\"spansEmitted\":" + Interlocked.Read(ref spansEmitted)
            + ",\"spansSkipped\":" + Interlocked.Read(ref spansSkipped)
            + ",\"spansChildNodes\":" + Interlocked.Read(ref spansChildNodes)
            + ",\"spansChildTruncated\":" + Interlocked.Read(ref spansChildTruncated)
            + ",\"spriteExport\":{\"enabled\":" + (spriteStats.Enabled ? "true" : "false")
            + ",\"snapshots\":" + spriteStats.Snapshots
            + ",\"bytes\":" + spriteStats.Bytes
            + ",\"requests\":" + spriteStats.Requests
            + ",\"failures\":" + spriteStats.Failures
            + ",\"lastRev\":" + spriteStats.LastRev
            + ",\"requestsIgnored\":" + spriteStats.RequestsIgnored
            + ",\"captureMsLast\":" + D(spriteStats.CaptureMsLast)
            + ",\"captureMsP50\":" + D(spriteStats.CaptureMsP50)
            + ",\"captureMsP95\":" + D(spriteStats.CaptureMsP95)
            + ",\"encodeMsLast\":" + D(spriteStats.EncodeMsLast)
            + ",\"encodeMsP50\":" + D(spriteStats.EncodeMsP50)
            + ",\"encodeMsP95\":" + D(spriteStats.EncodeMsP95) + "}}");
    }

    /// <summary>W2a：rect 导出诊断（console.LastRectDiag——最近一次 GetSelectableButtonRects 的
    /// 失败原因分布与几何快照）。只读观测，⛔ 门禁语义仍用累计的 rectSelfCheckFailed。</summary>
    private static string EmitRectDiag(EmueraConsole? console)
    {
        if (console == null)
            return "null";
        var d = console.LastRectDiag;
        return "{\"noWindow\":" + d.NoWindow
            + ",\"noStrArray\":" + d.NoStrArray
            + ",\"widthNonPositive\":" + d.WidthNonPositive
            + ",\"renderInvalid\":" + d.RenderInvalid
            + ",\"normalRect\":" + d.NormalRect
            + ",\"divPosUnusable\":" + d.DivPosUnusable
            + ",\"divNoParent\":" + d.DivNoParent
            + ",\"divParentNotInList\":" + d.DivParentNotInList
            + ",\"divEmptyContent\":" + d.DivEmptyContent
            + ",\"divRect\":" + d.DivRect
            + ",\"renderW\":" + d.RenderW
            + ",\"renderH\":" + d.RenderH
            + ",\"bottomLine\":" + d.BottomLine
            + ",\"scrollBottom\":" + d.ScrollBottom
            + ",\"lineCount\":" + d.LineCount + "}";
    }

    /// <summary>结构化快照（B2/S1）：行数组＋世代＋挂起输入＋行 owner 映射/频道段/open 栈；
    /// 断线重连按此原子重建。</summary>
    private void DoSnapshot()
    {
        var console = GameConsole;
        var sb = new StringBuilder(4096);
        sb.Append("{\"t\":\"snapshot.full\",\"lineCount\":").Append(liveLines.Count)
            .Append(",\"generation\":").Append(currentGeneration)
            .Append(",\"pendingInput\":");
        if (console.IsWaitInputState && console.inputReq != null)
            sb.Append("{\"reqId\":").Append(currentReqId)
                .Append(",\"type\":").Append(J(InputTypeName(console.inputReq.InputType))).Append('}');
        else
            sb.Append("null");
        sb.Append(",\"lines\":[");
        for (int i = 0; i < liveLines.Count; i++)
        {
            if (i > 0) sb.Append(',');
            // W0：retained 行若含运行期行内像素（rt），快照必须重建 spans 并重捕获新 rev；
            // 不得复用可能已被 LRU 淘汰的旧 rev（旧 rev 仍按不可变语义返回旧图）。
            // 非 rt 行（文本/几何）保持原 spans，避免无意义重建与文本漂移。
            var ll = liveLines[i];
            if (spansEnabled && ll.Line != null && ll.Spans != null && ll.Spans.Contains("\"rt\":true"))
                ll.Spans = BuildSpans(ll.Line);
            sb.Append("{\"lineId\":").Append(ll.Id)
                .Append(",\"text\":").Append(J(ll.Text));
            if (ll.Spans != null)
                sb.Append(",\"spans\":").Append(ll.Spans);
            sb.Append('}');
        }
        sb.Append("],\"sprites\":[");
        for (int i = 0; i < liveSprites.Count; i++)
        {
            if (i > 0) sb.Append(',');
            var s = liveSprites[i];
            sb.Append("{\"depth\":").Append(s.Depth).Append(",\"texture\":").Append(J(s.Tex))
                .Append(",\"x\":").Append(s.X).Append(",\"y\":").Append(s.Y)
                .Append(",\"w\":").Append(s.W).Append(",\"h\":").Append(s.H)
                .Append(",\"opacity\":").Append(s.Op)
                .Append(",\"follow\":").Append(s.Follow ? "true" : "false");
            if (s.Rev > 0)
                sb.Append(",\"rev\":").Append(s.Rev);
            sb.Append('}');
        }
        sb.Append("],\"island\":").Append(liveIslandHtml != null ? J(liveIslandHtml) : "null");
        lock (channelGate)
        {
            // 所有存活行的 owner 映射（含 channel.clear 后隐藏行）；主日志行不出现在映射里。
            var lineChannels = new Dictionary<string, List<long>>();
            if (channelEnabled)
            {
                foreach (var line in liveLines)
                {
                    if (string.IsNullOrEmpty(line.Owner) || !channels.ContainsKey(line.Owner))
                        continue;
                    if (!lineChannels.TryGetValue(line.Owner, out var ids))
                    {
                        ids = new List<long>();
                        lineChannels[line.Owner] = ids;
                    }
                    ids.Add(line.Id);
                }
            }
            sb.Append(",\"lineChannels\":{");
            int lci = 0;
            foreach (var kv in lineChannels)
            {
                if (lci++ > 0) sb.Append(',');
                sb.Append(J(kv.Key)).Append(":[");
                for (int i = 0; i < kv.Value.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append(kv.Value[i]);
                }
                sb.Append(']');
            }
            sb.Append("},\"openStack\":[");
            if (channelEnabled)
            {
                for (int i = 0; i < openStack.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append(J(openStack[i]));
                }
            }
            sb.Append("],\"channels\":[");
            if (channelEnabled)
            {
                int ci = 0;
                foreach (var kv in channels)
                {
                    if (ci++ > 0) sb.Append(',');
                    sb.Append("{\"id\":").Append(J(kv.Key))
                        .Append(",\"kind\":").Append(J(kv.Value.Kind))
                        .Append(",\"rev\":").Append(kv.Value.Rev)
                        .Append(",\"open\":").Append(kv.Value.Open ? "true" : "false")
                        .Append(",\"segments\":[");
                    int si = 0;
                    foreach (var seg in kv.Value.Segments)
                    {
                        if (si++ > 0) sb.Append(',');
                        sb.Append("{\"rev\":").Append(seg.Rev)
                            .Append(",\"committed\":").Append(seg.Committed ? "true" : "false")
                            .Append(",\"lineIds\":[");
                        int li = 0;
                        foreach (var line in liveLines)
                        {
                            if (line.Owner != kv.Key || line.OwnerRev != seg.Rev)
                                continue;
                            if (li++ > 0) sb.Append(',');
                            sb.Append(line.Id);
                        }
                        sb.Append("]}");
                    }
                    sb.Append("]}");
                }
            }
            sb.Append("]}");
        }
        EwpOut.Emit(sb.ToString());
    }

    private void DispatchQuit()
    {
        try
        {
            if (win.IsDisposed)
                return;
            win.Invoke(new Action(() =>
            {
                try { GameConsole?.Quit(); win.Close(); }
                catch { }
            }));
        }
        catch { }
    }

    private static void EmitResult(bool ok, string code, string detail)
    {
        var sb = new StringBuilder(96);
        sb.Append("{\"t\":\"result\",\"ok\":").Append(ok ? "true" : "false");
        if (code != null)
            sb.Append(",\"code\":").Append(J(code));
        if (detail != null)
            sb.Append(",\"detail\":").Append(J(detail));
        sb.Append('}');
        EwpOut.Emit(sb.ToString());
    }
}
