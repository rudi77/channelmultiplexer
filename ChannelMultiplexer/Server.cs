using System;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Tecan.At.Dragonfly.Communication.Generic.Simulator;

namespace ChannelMultiplexer
{
	public class Server
	{
		const string CapName = @"\\.\pipe\SymbioSimCapControllerPipe";
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
				var mp = new TcpMultiplexer ();
				var capService = new CapService (mp);

				var rwStream = mp.CreateStream (CapName, TcpMultiplexer.Direction.InOut);

				var t1 = Task.Factory.StartNew (() => ReadAndAck (rwStream, CapName, token, capService));
				t1.Wait (token);

				Console.WriteLine ("Disconnected from {0}", newclient.Address);
				ns.Close ();
			}
		}

		private void ReadAndAck( Stream stream, string name, CancellationToken token, CapService capService )
		{
			var stringReader = new CapReader (stream);
			var stringWriter = new StreamWriter (stream);

			while (!token.IsCancellationRequested) 
			{
				try 
				{
					var command = stringReader.ReadCommand();
					Logger.InfoOut ( command.ToString() );

					var response = capService.CreateResponse( command );

					Logger.InfoOut( "Server {0} sending" + "\n>> ", response ); 
					stringWriter.WriteLine( response );
					stringWriter.Flush();
				}
				catch (Exception e)
				{
					Logger.ErrOut (e.ToString ());
					break;
				}
			}
		}

		private void ReadFromStream( Stream inStream, string channel, CancellationToken token )
		{
			var stringReader = new StreamReader (inStream);

			while (!token.IsCancellationRequested) 
			{
				try 
				{
					var output = stringReader.ReadLine();

					if (string.IsNullOrEmpty(output)) continue;

					Logger.InfoOut ( "Server " + channel + " receive << " + output );

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

