using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;
using System.Threading.Tasks;

namespace ChannelMultiplexer
{
	public class Client
	{
		readonly IPEndPoint _ipep 	= new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9050);
		readonly Socket _server 	= new Socket(AddressFamily.InterNetwork,SocketType.Stream, ProtocolType.Tcp);

		public void Start( CancellationToken token )
		{
			try
			{
				_server.Connect( _ipep );
			}
			catch (SocketException e) {
				Console.WriteLine (e);
			}


			Thread.Sleep (1000);

			using (var ns = new NetworkStream (_server))
			{
				var mp = new TcpMultiplexer (ns, "MPClient");
				var rwStream = mp.CreateStream ("rwStream", TcpMultiplexer.Direction.InOut);
				var t2 = Task.Factory.StartNew(() => WriteToStream (rwStream, "rwStream", token, "Hello from Maria Alm"));
				var t3 = Task.Factory.StartNew(() => ReadFromStream( rwStream, "rwStream", token ));

				var writeableStream = mp.CreateStream ("channel1", TcpMultiplexer.Direction.Out);
				var t1 = Task.Factory.StartNew(() => WriteToStream (writeableStream, "channel1", token, "Hello from Teisendorf"));

				t1.Wait (token);
				t2.Wait (token);
				t3.Wait(token);
			}
		}

		private void WriteToStream( Stream stream, string name, CancellationToken token, string text )
		{				
			using (var streamWriter = new StreamWriter (stream))
			{
				while (!token.IsCancellationRequested) 
				{
					Logger.InfoOut ("Client {0} send {1}",name, text);

					streamWriter.WriteLine (text);
					streamWriter.Flush ();

					Thread.Sleep (2000);

					if (text == "exit")
						break;
				}
			}
		}

		private void ReadFromStream( Stream stream, string name, CancellationToken token )
		{
			var stringReader = new StreamReader (stream);

			while (!token.IsCancellationRequested) 
			{
				try 
				{
					var output = stringReader.ReadLine();

					if (string.IsNullOrEmpty(output)) continue;

					Logger.InfoOut ( "Client " + name + " << " + output );

					if (output == "exit") 
						break;
				}
				catch (IOException e)
				{
					Logger.ErrOut ("Client ReadFromStream Error");
					Logger.ErrOut (e.ToString ());

					break;
				}
			}
		}
	}
}

