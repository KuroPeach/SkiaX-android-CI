using System;
using System.Collections.Concurrent;
using System.Threading;

namespace MinorShift.Emuera.UI.Headless;

/// <summary>
/// 单线程队列泵：替代 WinForms 的 Application.Run 消息循环。
/// 所有 UI 线程工作（ErbLoader/Initialize 的 await 续体、EWP 传输线程的 Invoke）
/// 都排到本上下文，由宿主线程 PumpOnce 串行执行。
/// </summary>
internal sealed class HeadlessSynchronizationContext : SynchronizationContext
{
	private readonly ConcurrentQueue<(SendOrPostCallback Callback, object State)> queue = new();
	private readonly AutoResetEvent signal = new(false);
	private readonly int pumpThreadId;
	private int pumping;

	public HeadlessSynchronizationContext()
	{
		pumpThreadId = Environment.CurrentManagedThreadId;
	}

	public bool IsPumpThread => Environment.CurrentManagedThreadId == pumpThreadId;

	public override void Post(SendOrPostCallback d, object state)
	{
		queue.Enqueue((d, state));
		signal.Set();
	}

	public override void Send(SendOrPostCallback d, object state)
	{
		if (IsPumpThread)
		{
			d(state);
			return;
		}

		Exception error = null;
		using var done = new ManualResetEventSlim(false);
		queue.Enqueue((s =>
		{
			try { d(s); }
			catch (Exception ex) { error = ex; }
			finally { done.Set(); }
		}, state));
		signal.Set();
		done.Wait();
		if (error != null)
			throw new AggregateException(error);
	}

	/// <summary>执行队列中的全部回调；block=true 时在空队列上最多等 timeoutMs。
	/// 重入（DoEvents 在泵线程内被调用）直接返回 false，由外层泵继续排空。</summary>
	public bool PumpOnce(bool block, int timeoutMs = 50)
	{
		if (Interlocked.Exchange(ref pumping, 1) == 1)
			return false;
		try
		{
			if (block && queue.IsEmpty)
				signal.WaitOne(timeoutMs);
			bool ran = false;
			while (queue.TryDequeue(out var item))
			{
				ran = true;
				item.Callback(item.State);
			}
			return ran;
		}
		finally
		{
			Interlocked.Exchange(ref pumping, 0);
		}
	}

	public void RequestStop() => signal.Set();
}
