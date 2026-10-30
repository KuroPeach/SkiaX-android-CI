using MinorShift.Emuera.GameView;
using MinorShift.Emuera.Runtime.Config;
using MinorShift.Emuera.UI.Ewp;
using System;
using System.Threading;

namespace MinorShift.Emuera.UI.Headless;

/// <summary>
/// 无窗 EWP 宿主：不创建 MainWindow、不进 Application.Run。
/// 用 <see cref="HeadlessSynchronizationContext"/> 泵驱动 console.Initialize 的 await 续体与
/// EWP 传输线程的 Invoke，直到 sys.quit / Close。
/// </summary>
internal static class HeadlessHost
{
	public static int Run()
	{
		EwpDiagnostics.Trace("HeadlessHost.Run enter");
		var ctx = new HeadlessSynchronizationContext();
		SynchronizationContext.SetSynchronizationContext(ctx);
		PlatformInterop.DoEvents = () => ctx.PumpOnce(false);

		var surface = new HeadlessSurface(ctx, Config.WindowX, Config.WindowY);
		var console = new EmueraConsole(surface);
		surface.Attach(console);

		var ewp = new EwpHost(surface, Program.EwpPort);
		if (Program.EwpPort > 0)
		{
			// G3（2026-10-30）：TCP 模式必须先握手再启动游戏——否则首帧 legacy.line /
			// input.request 早于 AcceptLoop 的 Subscribe（钩子未挂、无补发），FE 永远
			// 拿不到 reqId。console 提前注入，让 Subscribe 在 Initialize 之前完成。
			ewp.AttachConsole(console);
		}
		ewp.Start();

		try
		{
			if (Program.EwpPort > 0)
			{
				long deadline = Environment.TickCount64 + 30000;
				while (!ewp.GameStartReady && Environment.TickCount64 < deadline)
					ctx.PumpOnce(true);   // ≤50ms 一轮；同时服务 hello.ok 里的 win.Invoke
				if (!ewp.GameStartReady)
					Console.Error.WriteLine("[ewp-headless] FE handshake timeout (30s); starting game anyway");
			}
			var init = console.Initialize();
			while (!init.IsCompleted)
				ctx.PumpOnce(true);
			init.GetAwaiter().GetResult();

			while (!surface.QuitRequested)
				ctx.PumpOnce(true);
			EwpDiagnostics.Trace("headless loop exit quit=" + surface.QuitRequested);
			return 0;
		}
		catch (Exception ex)
		{
			try { Console.Error.WriteLine("[ewp-headless] " + ex); } catch { }
			return 1;
		}
		finally
		{
			EwpDiagnostics.Trace("headless finally enter");
			ewp.NotifyProcessExit();
			EwpDiagnostics.Trace("headless finally Notify returned");
		}
	}
}
