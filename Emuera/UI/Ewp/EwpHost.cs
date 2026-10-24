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

// eraWire B1/B2: EWP/0 text-level host (JSON-Lines framing; stdio or single TCP client).
// Spec: $EMUERA_WS/emuera/eraWire/specs/EWP-v0-协议草案.md
// Kernel hooks: CliLineAdded/CliWaitInputEntered/CliTerminalStateEntered (CliDebug set)
// plus CliLinesRemoved (B2 kernel ledger #1, +2 lines) for legacy.clear.
// B2 additions in this file: hello.metrics (grid fidelity baseline), FE viewport
// report via hello.ok -> window.ClientSize (ClientWidth/Height are live window reads,
// so this needs no kernel change), ack_timeout observability, structured snapshot,
// TCP transport (--ewp-port; events buffer in the queue until the client attaches).
// Remaining B2 items: sprite/overlay semantic events (needs kernel hooks),
// MessagePack framing (deferred with pixel stream), channel/node (waits D-track macros).
namespace MinorShift.Emuera.UI.Ewp;

/// <summary>Static stdio layer: utf-8 pipes, bounded queue, writer thread.</summary>
internal static class EwpOut
{
    private const int QueueCapacity = 8192;
    /// <summary>信用窗口（EWP §6.3）：未 ack 帧数达到此值时 writer 暂停发送。</summary>
    private const int CreditWindow = 8;
    /// <summary>ack 超时兜底：FE 不 ack 时最多等这么久再放行，避免死锁（闸门 4 要求无死锁）。</summary>
    private const int AckTimeoutMs = 5000;
    private static BlockingCollection<string> queue;
    private static StreamWriter writer;
    private static Thread writerThread;
    private static long frameSeq;
    private static long ackCount;
    private static readonly object ackGate = new();
    private static int pendingFrames;
    private static int ackTimeouts;

    public static int AckTimeouts => Volatile.Read(ref ackTimeouts);
    public static long FramesWritten => Interlocked.Read(ref frameSeq);
    public static long Acks => Interlocked.Read(ref ackCount);
    public static int PendingFrames => Volatile.Read(ref pendingFrames);

    /// <summary>deferWriter=true（TCP 模式）：只建队列，写手线程等 AttachStream 再启动，
    /// 期间事件在队列中缓冲（boot 期 ~1.4k 行，远低于 8192 容量）。</summary>
    public static void InitIO(bool deferWriter = false)
    {
        var utf8 = new UTF8Encoding(false);
        try { Console.InputEncoding = utf8; } catch { }
        try { Console.OutputEncoding = utf8; } catch { }
        queue = new BlockingCollection<string>(new ConcurrentQueue<string>(), QueueCapacity);
        if (deferWriter)
            return;
        StreamWriter w = null;
        try { w = new StreamWriter(Console.OpenStandardOutput(), utf8) { AutoFlush = true }; }
        catch { /* no stdout handle: everything below becomes no-op */ }
        writer = w;
        if (w == null)
            return;
        writerThread = new Thread(Loop) { IsBackground = true, Name = "EwpOut writer" };
        writerThread.Start();
    }

    /// <summary>TCP 客户端接入后挂载输出流并启动写手（只允许一次）。</summary>
    public static void AttachStream(Stream stream)
    {
        if (writer != null || stream == null)
            return;
        writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
        writerThread = new Thread(Loop) { IsBackground = true, Name = "EwpOut writer" };
        writerThread.Start();
    }

    /// <summary>Enqueue one event line. Blocks (pausing the producer thread) when the
    /// consumer is slow and the queue is full - this is the credit-window backpressure.</summary>
    public static void Emit(string json)
    {
        var q = queue;
        if (q == null)
            return;
        try { q.Add(json); } catch { }
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
        try { writerThread?.Join(3000); } catch { }
    }

    private static void Loop()
    {
        var w = writer;
        var sb = new StringBuilder(64);
        try
        {
            foreach (var first in q_getConsuming())
            {
                w.WriteLine(first);
                long count = 1;
                // drain whatever else is already queued (batch), then close the frame
                while (q_tryTake(out var more))
                {
                    w.WriteLine(more);
                    count++;
                }
                sb.Clear();
                sb.Append("{\"t\":\"frame.commit\",\"frameId\":").Append(++frameSeq)
                    .Append(",\"events\":").Append(count)
                    .Append(",\"pending\":").Append(Volatile.Read(ref pendingFrames) + 1)
                    .Append(",\"ackTimeouts\":").Append(Volatile.Read(ref ackTimeouts))
                    .Append('}');
                w.WriteLine(sb.ToString());
                // 信用窗口：本帧未 ack 前最多积压 CreditWindow 帧
                Interlocked.Increment(ref pendingFrames);
                WaitForCredit();
            }
        }
        catch { }
    }

    private static System.Collections.Generic.IEnumerable<string> q_getConsuming()
    {
        return queue.GetConsumingEnumerable();
    }

    private static bool q_tryTake(out string item)
    {
        try { return queue.TryTake(out item, 0); }
        catch { item = null; return false; }
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
                    queue?.TryAdd("{\"t\":\"error\",\"code\":\"ack_timeout\",\"fatal\":false,\"detail\":\"credit window timed out; releasing\"}", 0);
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

    public EwpHost(MainWindow window, int tcpPort = 0)
    {
        win = window;
        this.tcpPort = tcpPort;
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
        var sb = new StringBuilder(320);
        sb.Append("{\"t\":\"hello\",\"proto\":\"EWP/0\",\"impl\":\"ewp-b2\",\"engine\":{")
            .Append("\"id\":\"LazyLoading-skia\",\"semver\":").Append(J(SafeVersion()))
            .Append("},\"game\":{")
            .Append("\"id\":").Append(J(SafeGameId()))
            .Append(",\"profile\":\"skia-superset\"},")
            .Append("\"caps\":[\"legacy.lines\",\"input.scope\",\"snapshot\",\"frame.commit\",\"view.ack\",\"viewport.report\",\"meta.metrics\"],")
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
        EwpOut.Emit("{\"t\":\"exit\"}");
        EwpOut.Shutdown();
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
        StreamReader reader;
        try { reader = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true); }
        catch { reader = null; }
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
            using var reader = new StreamReader(stream, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true);
            CommandLoop(reader);
        }
        catch { DispatchQuit(); }
        finally { try { listener?.Stop(); } catch { } }
    }

    private void CommandLoop(TextReader reader)
    {
        while (running)
        {
            string line;
            try { line = reader != null ? reader.ReadLine() : Console.In.ReadLine(); }
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
        EmitMetrics();
    }

    /// <summary>应用 hello.ok 暂存的 FE 视口（UI 线程执行；ClientWidth/Height 是窗口活属性，改窗体即生效）。</summary>
    private void ApplyPendingViewport()
    {
        int w = Volatile.Read(ref pendingVpW), h = Volatile.Read(ref pendingVpH);
        if (w <= 0 || h <= 0)
            return;
        Volatile.Write(ref pendingVpW, 0);
        try { win.Invoke(new Action(() => win.ClientSize = new Size(w, h))); } catch { }
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

    /// <summary>FE 视口上报（传输线程）：句柄已建则立即经 UI 线程应用，否则暂存到 Subscribe。</summary>
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
                try { win.Invoke(new Action(() => win.ClientSize = new Size(w, h))); } catch { }
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
            + ",\"frames\":" + EwpOut.FramesWritten
            + ",\"acks\":" + EwpOut.Acks
            + ",\"pending\":" + EwpOut.PendingFrames
            + ",\"ackTimeouts\":" + EwpOut.AckTimeouts + "}");
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
        sb.Append("}");
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
