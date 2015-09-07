using System;
using System.IO;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Threading;
using System.Text;

namespace ChannelMultiplexer
{
	public class TcpMultiplexer
	{
		class Channel : IDisposable
		{
			const int BufferSize = 0xFFFF;

			readonly NetworkStream _netStream;
			readonly string _header;
			readonly byte[] _buffer = new byte[BufferSize];
			readonly CancellationTokenSource _source = new CancellationTokenSource ();

			public Channel (NetworkStream netStream, string header)
			{
				if (netStream == null)
					throw new ArgumentNullException( "netStream" );

				if (string.IsNullOrWhiteSpace( header ))
					throw new ArgumentException( "header" );

				Stream = new MemoryStream();
				_netStream = netStream;
				_header = header;

				Task.Factory.StartNew( Read );
			}

			public Stream Stream { get; private set; }

			void Read()
			{
				while (!_source.Token.IsCancellationRequested) 
				{
					var count = Stream.Read (_buffer, 0, BufferSize);

					// TODO: check length

					var encoding = new ASCIIEncoding();
					var text = encoding.GetString( _buffer, 0, count );

					Console.WriteLine ( "Read: " + text );
				}
			}

			#region IDisposable implementation
			bool _isDisposed;
			public void Dispose()
			{
				GC.SuppressFinalize (this);

				if (!_isDisposed) 
				{
					_source.Cancel ();
					_isDisposed = true;
				}
			}
			#endregion
		}

		readonly Dictionary<string, Channel> _streamMap = new Dictionary<string, Channel>();
		readonly NetworkStream _netStream;

		public TcpMultiplexer ( NetworkStream netStream )
		{
			if (netStream == null)
				throw new ArgumentNullException ("netStream");

			_netStream = netStream;

//			var socket = new Socket (AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
//			_netStream = new NetworkStream (socket);
		}

		public Stream Open( string name )
		{
			if (string.IsNullOrWhiteSpace (name)) 
			{
				throw new ArgumentException (name);
			}

			if (_streamMap.ContainsKey (name)) 
			{
				throw new InvalidOperationException ("Key " + name + " already exists");
			}

			_streamMap [name] = new Channel (null, name);

			return _streamMap [name].Stream;
		}

		public void Close( string name )
		{
			if (string.IsNullOrWhiteSpace (name)) 
			{
				throw new ArgumentException (name);
			}

			if (_streamMap.ContainsKey (name)) 
			{
				_streamMap.Remove (name);
			}
		}
	}
}

