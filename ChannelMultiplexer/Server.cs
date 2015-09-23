using System;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ChannelMultiplexer
{
	public class Server
	{
		const int Port = 9050;
		readonly IPEndPoint _ipep = new IPEndPoint(IPAddress.Any, Port);
		readonly Socket _newsock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

		public void Start( CancellationToken token )
		{
			_newsock.Bind(_ipep);
			_newsock.Listen(10);

			Console.WriteLine("Waiting for a client...");

			var client = _newsock.Accept();
			var newclient = (IPEndPoint)client.RemoteEndPoint;

			Console.WriteLine("Connected with {0} at port {1}", newclient.Address, newclient.Port);

			using (var ns = new NetworkStream (client))
			{
				var multiplexer = new TcpMultiplexer (ns);
				var readerStream = multiplexer.CreateReadableStream ("channel1");
				var readerStream2 = multiplexer.CreateReadableStream ("channel2");

				var t1 = Task.Factory.StartNew(() => ReadFromStream( readerStream, "channel1", token ));
				var t2 = Task.Factory.StartNew(() => ReadFromStream( readerStream2, "channel2", token ));

				t1.Wait (token);
				t2.Wait (token);

				Console.WriteLine ("Disconnected from {0}", newclient.Address);
				ns.Close ();
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

					Logger.InfoOut ( name + " << " + output );

					if (output == "exit") 
						break;
				}
				catch (IOException)
				{
					break;
				}
			}
		}
	}
}

