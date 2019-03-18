<Query Kind="Program">
  <Output>DataGrids</Output>
  <Namespace>System.Runtime.ExceptionServices</Namespace>
  <Namespace>System.Collections.Concurrent</Namespace>
</Query>

void Main()
{
	using (ApplicationLogger.Go(AppDomain.CurrentDomain))
	{
		throw new Exception("Hello!");
	}

public class ApplicationLogger : MarshalByRefObject
{
	ConcurrentQueue<FirstChanceExceptionEventArgs> queue = new ConcurrentQueue<FirstChanceExceptionEventArgs>();
	CancellationTokenSource cancellation;
	ManualResetEvent @event;

	public void LogFCE(FirstChanceExceptionEventArgs message)
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
				if (queue.TryDequeue(out var args))
				{
					Console.WriteLine(args.Exception.Message);
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
			ApplicationBase = AppDomain.CurrentDomain.BaseDirectory
		});
		var proxy = (ApplicationLogger)dom.CreateInstanceAndUnwrap(typeof(ApplicationLogger).Assembly.FullName, typeof(ApplicationLogger).FullName);
		proxy.StartThread();
		
		var subscription = new EventHandler<FirstChanceExceptionEventArgs>((_, args) =>
		{
			proxy.LogFCE(args);
		});
		observable.FirstChanceException += subscription;

		return new Subscription(() => { 
				observable.FirstChanceException -= subscription; 
				proxy.Wait(); 
			});
	}

	private class Subscription : IDisposable 
	{
		Action act;
		public Subscription (Action act) {
			this.act = act;
		}
		
		public void Dispose()
		{
			act();
		}
	}
}