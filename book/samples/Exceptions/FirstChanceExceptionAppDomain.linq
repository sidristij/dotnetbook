<Query Kind="Program">
  <Output>DataGrids</Output>
  <Namespace>System.Runtime.ExceptionServices</Namespace>
  <Namespace>System.Collections.Concurrent</Namespace>
</Query>

static void  Main()
{
    using (ApplicationLogger.Go(AppDomain.CurrentDomain))
    {
        throw new Exception("Hello!");
    }
}

public class ApplicationLogger : MarshalByRefObject
{
	ConcurrentQueue<Exception> queue = new ConcurrentQueue<Exception>();
	CancellationTokenSource cancellation;
	ManualResetEvent @event;

	public void LogFCE(Exception message)
	{
		queue.Enqueue(message);
	}

	private void StartThread()
	{
		cancellation = new CancellationTokenSource();
		@event = new ManualResetEvent(false);
		var thread = new Thread(() =>
		{
			while (!cancellation.IsCancellationRequested)
			{
				if (queue.TryDequeue(out var exception))
				{
					Console.WriteLine(exception.Message);
				}
				Thread.Yield();
			}
			@event.Set();
		});
		thread.Start();
	}

	private void Wait()
	{
		@event.WaitOne();
	}

	public static IDisposable Go(AppDomain observable)
	{
		var dom = AppDomain.CreateDomain("ApplicationLogger", null, new AppDomainSetup
		{
			ApplicationBase = AppDomain.CurrentDomain.BaseDirectory,
		});

		var proxy = (ApplicationLogger)dom.CreateInstanceAndUnwrap(typeof(ApplicationLogger).Assembly.FullName, typeof(ApplicationLogger).FullName);
		proxy.StartThread();

		var subscription = new EventHandler<FirstChanceExceptionEventArgs>((_, args) =>
		{
			proxy.LogFCE(args.Exception);
		});
		observable.FirstChanceException += subscription;

		return new Subscription(() =>
		{
			observable.FirstChanceException -= subscription;
			proxy.Wait();
		});
	}
}

private class Subscription : IDisposable
{
	Action act;
	public Subscription(Action act)
	{
		this.act = act;
	}

	public void Dispose()
	{
		act();
	}
}