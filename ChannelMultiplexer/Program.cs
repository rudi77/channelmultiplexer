using System;
using System.Threading.Tasks;
using System.Threading;

namespace ChannelMultiplexer
{
	class MainClass
	{
		public static void Main( string[] args )
		{
			var tokenSource = new CancellationTokenSource ();

			var serverTask = Task.Factory.StartNew (() => {
				var server = new Server ();
				server.Start( tokenSource.Token );
			}, tokenSource.Token );


			Thread.Sleep (1000);

			var clientTask = Task.Factory.StartNew (() => {
				var client = new Client();
				client.Start( tokenSource.Token );
			}, tokenSource.Token);

			serverTask.Wait (tokenSource.Token);
			clientTask.Wait (tokenSource.Token);

			Console.WriteLine ("Press any key to exit...");
			Console.ReadKey ();

			tokenSource.Cancel ();
		}
	}
}
