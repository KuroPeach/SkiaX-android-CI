using MinorShift.Emuera.Forms;
using MinorShift.Emuera.GameView;
using MinorShift.Emuera.Runtime;
using MinorShift.Emuera.Runtime.Script.Statements;
using MinorShift.Emuera.Runtime.Utils;
using MinorShift.Emuera.Runtime.Config;
using MinorShift.Emuera.UI.Game;
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
            while (Volatile.Read(ref pendingFrames) >= CreditWindow)
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
    private readonly MainWindow win;
    private readonly int tcpPort;
    private Thread transportThread;
    private volatile bool running = true;

    private long lineIdSeq;
    private long reqIdSeq;
    /// <summary>仍在缓冲里的行（lineId+文本；CLEARLINE 尾部裁剪；仅 UI/脚本线程访问）。结构化 snapshot 的数据源。</summary>
    private readonly List<(long Id, string Text)> liveLines = new();
    /// <summary>活着的 sprite 图层（与引擎同语义：同深度可叠多层、按深度清）。snapshot 数据源。</summary>
    private readonly List<(long Depth, string Tex, int X, int Y, int W, int H, int Op, bool Follow)> liveSprites = new();
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

    /// <summary>B② 频道状态（EwpChannel 名挂钩维护；id → kind/rev/open）。
    /// 脚本线程写（IntoFunction）、UI 线程读（snapshot/state）——lock 保护。</summary>
    private sealed class ChannelState
    {
        public string Kind;
        public long Rev;
        public bool Open;
    }
    private readonly object channelGate = new();
    private readonly Dictionary<string, ChannelState> channels = new();
    private long channelMismatch;

    public EwpHost(MainWindow window, int tcpPort = 0)
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
        EwpChannel.Handler = OnChannelMacro;
        var sb = new StringBuilder(320);
        sb.Append("{\"t\":\"hello\",\"proto\":\"EWP/0.1\",\"impl\":\"ewp-b4\",\"engine\":{")
            .Append("\"id\":\"LazyLoading-skia\",\"semver\":").Append(J(SafeVersion()))
            .Append("},\"game\":{")
            .Append("\"id\":").Append(J(SafeGameId()))
            .Append(",\"profile\":\"skia-superset\"},")
            .Append("\"caps\":[\"legacy.lines\",\"input.scope\",\"snapshot\",\"frame.commit\",\"view.ack\",\"viewport.report\",\"meta.metrics\",\"pixel.frame\",\"channel\"],")
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
        EwpOut.Emit("{\"t\":\"meta.metrics\",\"metrics\":{\"cellW\":" + cellW
            + ",\"cellH\":" + Config.FontSize
            + ",\"lineH\":" + Config.LineHeight
            + ",\"font\":" + J(Config.FontName)
            + ",\"fontSize\":" + Config.FontSize + "}}");
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
        EwpChannel.Handler = null;
        pixel.Dispose();
        EwpOut.Emit("{\"t\":\"exit\"}");
        EwpOut.Shutdown();
    }

    /// <summary>频道宏（脚本线程，EwpChannel 转发）：begin=create(首见)+begin{rev}；
    /// end=commit{rev}（与配对 begin 同 rev 定稿）；clear=clear{id}（清内容不清频道，
    /// rev 保持）。未配对 end/clear 静默忽略并计 channelMismatch（state 可观测）。</summary>
    private void OnChannelMacro(string macro, string id, string kind)
    {
        switch (macro)
        {
            case "begin":
                if (string.IsNullOrEmpty(kind))
                    kind = "log";
                lock (channelGate)
                {
                    if (!channels.TryGetValue(id, out var st))
                    {
                        st = new ChannelState { Kind = kind };
                        channels[id] = st;
                        EwpOut.Emit("{\"t\":\"channel.create\",\"id\":" + J(id)
                            + ",\"kind\":" + J(kind) + ",\"tier\":\"grid-locked\"}");
                    }
                    st.Rev++;
                    st.Open = true;
                    EwpOut.Emit("{\"t\":\"channel.begin\",\"id\":" + J(id) + ",\"rev\":" + st.Rev + "}");
                }
                break;
            case "end":
                lock (channelGate)
                {
                    if (channels.TryGetValue(id, out var st) && st.Open)
                    {
                        st.Open = false;
                        EwpOut.Emit("{\"t\":\"channel.commit\",\"id\":" + J(id) + ",\"rev\":" + st.Rev + "}");
                    }
                    else
                        channelMismatch++;
                }
                break;
            case "clear":
                lock (channelGate)
                {
                    if (channels.ContainsKey(id))
                        EwpOut.Emit("{\"t\":\"channel.clear\",\"id\":" + J(id) + "}");
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
        // text level; spans/line-level buttons deferred (spec freeze list).
        var id = ++lineIdSeq;
        var text = ClipboardProcessor.StripHTML(line.ToString());
        liveLines.Add((id, text));
        var json = "{\"t\":\"legacy.line\",\"lineId\":" + id
            + ",\"gen\":" + currentGeneration
            + ",\"text\":" + J(text) + "}";
        EwpOut.Emit(json);
    }

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

    private void OnWaitInput()
    {
        var console = GameConsole;
        if (console == null || console.inputReq == null)
            return;
        var req = console.inputReq;
        var buttons = console.GetSelectableButtons();
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
            sb.Append("{\"index\":").Append(i)
                .Append(",\"text\":").Append(J(buttons[i].Text))
                .Append(",\"value\":").Append(J(buttons[i].Value))
                .Append(",\"isInteger\":").Append(buttons[i].IsInteger ? "true" : "false")
                .Append('}');
        }
        sb.Append("]}");
        EwpOut.Emit(sb.ToString());
    }

    private void OnTerminalState()
    {
        EwpOut.Emit("{\"t\":\"meta.state\",\"state\":" + J(GameConsole?.CliStateName ?? "Unknown") + "}");
    }

    /// <summary>SETIMAGELAYER(L)→sprite.set（EWP §4.3；colormatrix 留 v1＝A1-③）。</summary>
    private void OnImageLayerSet(string tex, long depth, int x, int y, int w, int h, int op, bool follow)
    {
        liveSprites.Add((depth, tex, x, y, w, h, op, follow));
        EwpOut.Emit("{\"t\":\"sprite.set\",\"depth\":" + depth + ",\"texture\":" + J(tex)
            + ",\"x\":" + x + ",\"y\":" + y + ",\"w\":" + w + ",\"h\":" + h
            + ",\"opacity\":" + op + ",\"follow\":" + (follow ? "true" : "false") + "}");
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
                case "input.submit":
                    Dispatch(() => DoInputSubmit(root.Clone()));
                    break;
                case "view.ack":
                    EwpOut.NoteAck();
                    break;
                case "hello.ok":
                    DoHelloOk(root);
                    break;
                case "state":
                    Dispatch(() => DoState());
                    break;
                case "snapshot.req":
                    Dispatch(() => DoSnapshot());
                    break;
                case "sys.quit":
                    DispatchQuit();
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

    private void DoState()
    {
        var console = GameConsole;
        EwpOut.Emit("{\"t\":\"result\",\"ok\":true,\"state\":" + J(console.CliStateName)
            + ",\"waitInput\":" + (console.IsWaitInputState ? "true" : "false")
            + ",\"reqId\":" + currentReqId
            + ",\"generation\":" + currentGeneration
            + ",\"lines\":" + liveLines.Count
            + ",\"client\":[" + console.ClientWidth + "," + console.ClientHeight + "]"
            + ",\"winClient\":[" + win.ClientSize.Width + "," + win.ClientSize.Height + "]"
            + ",\"winState\":\"" + win.WindowState + "\""
            + ",\"frames\":" + EwpOut.FramesWritten
            + ",\"acks\":" + EwpOut.Acks
            + ",\"pending\":" + EwpOut.PendingFrames
            + ",\"ackTimeouts\":" + EwpOut.AckTimeouts
            + ",\"pixel\":{\"enabled\":" + (pixel.Enabled ? "true" : "false")
            + ",\"frames\":" + pixel.Frames
            + ",\"dropped\":" + pixel.Dropped
            + ",\"written\":" + EwpOut.PixelWritten
            + ",\"writeDropped\":" + EwpOut.PixelDropped + "}"
            + ",\"channels\":" + channels.Count
            + ",\"channelMismatch\":" + channelMismatch + "}");
    }

    /// <summary>结构化快照（B2）：行数组（lineId+text）＋世代＋挂起输入；断线重连按此幂等重建。</summary>
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
            sb.Append("{\"lineId\":").Append(liveLines[i].Id)
                .Append(",\"text\":").Append(J(liveLines[i].Text)).Append('}');
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
                .Append(",\"follow\":").Append(s.Follow ? "true" : "false").Append('}');
        }
        sb.Append("],\"island\":").Append(liveIslandHtml != null ? J(liveIslandHtml) : "null");
        sb.Append(",\"channels\":[");
        lock (channelGate)
        {
            int ci = 0;
            foreach (var kv in channels)
            {
                if (ci++ > 0) sb.Append(',');
                sb.Append("{\"id\":").Append(J(kv.Key))
                    .Append(",\"kind\":").Append(J(kv.Value.Kind))
                    .Append(",\"rev\":").Append(kv.Value.Rev)
                    .Append(",\"open\":").Append(kv.Value.Open ? "true" : "false").Append('}');
            }
        }
        sb.Append("]}");
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
