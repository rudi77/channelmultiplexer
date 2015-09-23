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
				var mp = new TcpMultiplexer (ns);
				var writeableStream = mp.CreateWriteableStream ("channel1");
				var writeableStream2 = mp.CreateWriteableStream ("channel2");

				Task.Factory.StartNew(() => WriteToStream (writeableStream, "channel1", token, "Hello from Teisendorf"));
				Task.Factory.StartNew(() => WriteToStream (writeableStream2, "channel2", token, "Hello from Maria Alm"));
			}
		}

		private void WriteToStream( Stream stream, string name, CancellationToken token, string text )
		{				
			using (var streamWriter = new StreamWriter (stream))
			{
				while (!token.IsCancellationRequested) 
				{
					Logger.InfoOut ("{0} send {1}",name, text);

					streamWriter.WriteLine (text);
					streamWriter.Flush ();

					Thread.Sleep (2000);

					if (text == "exit")
						break;
				}
			}

		}
	}
}

