using MinorShift.Emuera.Forms;
using MinorShift.Emuera.GameView;
using MinorShift.Emuera.Runtime;
using MinorShift.Emuera.Runtime.Script.Statements;
using MinorShift.Emuera.Runtime.Utils;
using MinorShift.Emuera.UI.Game;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;

// eraWire B1: EWP/0 stdio text-level host (JSON-Lines framing).
// Spec: $EMUERA_WS/emuera/eraWire/specs/EWP-v0-协议草案.md
// Kernel hooks are the same three console events CliDebug uses (zero kernel change):
//   CliLineAdded -> legacy.line   CliWaitInputEntered -> input.request
//   CliTerminalStateEntered -> meta.state
// Differences vs CliDebug (B1 rationale):
//   1. Dedicated writer thread + bounded queue: producers block when the pipe consumer
//      stalls (script pauses = EWP 6.3 backpressure), replacing the synchronous-write
//      pipe deadlock documented in knowledge/erabasic/cli-debug-automation.md 3.1.
//   2. Monotonic lineId per legacy.line (history virtualization key).
//   3. input.request carries reqId/generation; input.submit is validated against them
//      (stale clicks must never reach the game - EWP 6.2).
//   4. frame.commit is synthesized by the writer per drained batch (Neovim-flush model).
// B1 limitations (deferred to B2, see spec freeze list):
//   - line-level button spans absent (buttons ride input.request only)
//   - dialog.request is notify-only (ShowQuestion still auto-answers No like CliDebug)
//   - snapshot.full is plain log text (structured snapshot in B2)
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
    private static int droppedEvents;

    public static int AckTimeouts => Volatile.Read(ref ackTimeouts);
    public static int DroppedEvents => Volatile.Read(ref droppedEvents);
    public static long FramesWritten => Interlocked.Read(ref frameSeq);
    public static long Acks => Interlocked.Read(ref ackCount);
    public static int PendingFrames => Volatile.Read(ref pendingFrames);

    public static void InitIO()
    {
        var utf8 = new UTF8Encoding(false);
        try { Console.InputEncoding = utf8; } catch { }
        try { Console.OutputEncoding = utf8; } catch { }
        StreamWriter w = null;
        try { w = new StreamWriter(Console.OpenStandardOutput(), utf8) { AutoFlush = true }; }
        catch { /* no stdout handle: everything below becomes no-op */ }
        writer = w;
        if (w == null)
            return;
        queue = new BlockingCollection<string>(new ConcurrentQueue<string>(), QueueCapacity);
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
                    // 放行：把窗口清零，让后续帧继续流出（宁可失控也不死锁）
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
    private Thread stdinThread;
    private volatile bool running = true;

    private long lineIdSeq;
    private long reqIdSeq;
    /// <summary>当前仍在缓冲里的 lineId（CLEARLINE 时按尾部裁剪，仅脚本线程访问）。</summary>
    private readonly List<long> liveLineIds = new();
    // currentReqId/currentGeneration are written on the UI thread (OnWaitInput) and
    // read on the UI thread (dispatched input.submit) - no cross-thread races.
    private long currentReqId = -1;
    private int currentGeneration = -1;

    public EwpHost(MainWindow window)
    {
        win = window;
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
        var sb = new StringBuilder(256);
        sb.Append("{\"t\":\"hello\",\"proto\":\"EWP/0\",\"impl\":\"ewp-b1\",\"engine\":{")
            .Append("\"id\":\"LazyLoading-skia\",\"semver\":").Append(J(SafeVersion()))
            .Append("},\"game\":{")
            .Append("\"id\":").Append(J(SafeGameId()))
            .Append(",\"profile\":\"skia-superset\"},")
            .Append("\"caps\":[\"legacy.lines\",\"input.scope\",\"snapshot\",\"frame.commit\",\"view.ack\"],")
            .Append("\"mode\":{\"semantic\":true,\"pixel\":false}}");
        EwpOut.Emit(sb.ToString());
        EwpOut.Emit("{\"t\":\"meta.state\",\"state\":\"boot\"}");
        stdinThread = new Thread(StdinLoop) { IsBackground = true, Name = "EwpHost stdin" };
        stdinThread.Start();
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
        while (running)
        {
            string line;
            try { line = reader != null ? reader.ReadLine() : Console.In.ReadLine(); }
            catch { break; }
            if (line == null)
            {
                // consumer closed stdin -> graceful quit
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
        console.CliLineAdded += OnLineAdded;
        console.CliWaitInputEntered += OnWaitInput;
        console.CliTerminalStateEntered += OnTerminalState;
        console.CliLinesRemoved += OnLinesRemoved;
    }

    private void OnLineAdded(ConsoleDisplayLine line)
    {
        // B1: text level; spans/line-level buttons deferred (spec freeze list).
        var id = ++lineIdSeq;
        liveLineIds.Add(id);
        var json = "{\"t\":\"legacy.line\",\"lineId\":" + id
            + ",\"gen\":" + currentGeneration
            + ",\"text\":" + J(ClipboardProcessor.StripHTML(line.ToString())) + "}";
        EwpOut.Emit(json);
    }

    /// <summary>CLEARLINE：按尾部裁剪并广播 legacy.clear（EWP §4.1，fromLineId + count，count&lt;0 表到末尾）。</summary>
    private void OnLinesRemoved(int count)
    {
        if (count <= 0 || liveLineIds.Count == 0)
            return;
        int removed = Math.Min(count, liveLineIds.Count);
        long fromId = liveLineIds[liveLineIds.Count - removed];
        liveLineIds.RemoveRange(liveLineIds.Count - removed, removed);
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

    private void DoState()
    {
        var console = GameConsole;
        EwpOut.Emit("{\"t\":\"result\",\"ok\":true,\"state\":" + J(console.CliStateName)
            + ",\"waitInput\":" + (console.IsWaitInputState ? "true" : "false")
            + ",\"reqId\":" + currentReqId
            + ",\"generation\":" + currentGeneration
            + ",\"lines\":" + liveLineIds.Count
            + ",\"frames\":" + EwpOut.FramesWritten
            + ",\"acks\":" + EwpOut.Acks
            + ",\"pending\":" + EwpOut.PendingFrames
            + ",\"ackTimeouts\":" + EwpOut.AckTimeouts + "}");
    }

    private void DoSnapshot()
    {
        var console = GameConsole;
        // B1 snapshot = full log text (reconnect parity); structured snapshot in B2.
        EwpOut.Emit("{\"t\":\"snapshot.full\",\"text\":" + J(console.GetLog(false)) + "}");
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
