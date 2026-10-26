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
		var ctx = new HeadlessSynchronizationContext();
		SynchronizationContext.SetSynchronizationContext(ctx);
		PlatformInterop.DoEvents = () => ctx.PumpOnce(false);

		var surface = new HeadlessSurface(ctx, Config.WindowX, Config.WindowY);
		var console = new EmueraConsole(surface);
		surface.Attach(console);

		var ewp = new EwpHost(surface, Program.EwpPort);
		ewp.Start();

		try
		{
			var init = console.Initialize();
			while (!init.IsCompleted)
				ctx.PumpOnce(true);
			init.GetAwaiter().GetResult();

			while (!surface.QuitRequested)
				ctx.PumpOnce(true);
			return 0;
		}
		catch (Exception ex)
		{
			try { Console.Error.WriteLine("[ewp-headless] " + ex); } catch { }
			return 1;
		}
		finally
		{
			ewp.NotifyProcessExit();
		}
	}
}
