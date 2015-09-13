using System;
using System.Net;
using System.Net.Sockets;
using System.IO;

namespace ChannelMultiplexer
{
	public class Server
	{
		const int Port = 9050;
		readonly IPEndPoint _ipep = new IPEndPoint(IPAddress.Any, Port);
		readonly Socket _newsock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

		public Server ()
		{}

		public void Start()
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

				while (true) 
				{
					try 
					{
						data = sr.ReadLine ();
					} catch (IOException)
					{
						break;
					}
				}

				Console.WriteLine ("Disconnected from {0}", newclient.Address);
				ns.Close ();
			}
		}
	}
}

