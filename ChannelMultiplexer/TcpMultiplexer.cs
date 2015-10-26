using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;
using System.Net;
using ChannelMultiplexer;

namespace ChannelMultiplexer
{
	using ReadableStreamMap = Dictionary<string, Stream>;
	using WritableStreamMap = Dictionary<string, Stream>;

	/// <summary>
	/// The possible directions of the created stream.
	/// </summary>
	public enum Direction
	{
		In,		// Readonly stream
		Out,	// Writeonly stream
		InOut	// Readable and writable stream
	}

	public interface IMultiplexing
	{
		Stream Create( string name, Direction direction );

		void Close( string name );

		bool Run();
		bool Run( NetworkStream stream );

		void Stop();
	}

	public class TcpMultiplexer : IMultiplexing
	{
		const int BufferSize = 0xFFFF;

		readonly object _rootLock = new object ();
		readonly ReadableStreamMap _readableStreamMap = new ReadableStreamMap();
		readonly WritableStreamMap _writableStreamMap = new WritableStreamMap();
		readonly BlockingCollection<Stream> _readableStreams = new BlockingCollection<Stream> ();
		readonly CancellationTokenSource _tokenSource = new CancellationTokenSource();

		NetworkStream 	_netStream;
		IPEndPoint 		_ipep;
		Socket 			_server;

		TcpMultiplexerWriter _tcpFrameWriter;
		TcpMultiplexerReader _tcpFrameReader;

		public Stream Create( string name, Direction direction )
		{
			if (string.IsNullOrWhiteSpace (name))
				throw new ArgumentException (name);

			Logger.InfoOut ("TcpMultiplexer create stream {0}, {1}", name, direction);

			switch (direction) 
			{
			case Direction.In: 		return CreateReadonlyStream (name);
			case Direction.Out:		return CreateWriteonlyStream (name);
			case Direction.InOut:	return CreateReadWriteStream (name);
			default:				throw new NotSupportedException ("Direction does not exist");
			}
		}

		public void Close( string name )
		{
			if (string.IsNullOrWhiteSpace (name)) 
				throw new ArgumentException (name);

			if (_readableStreamMap.ContainsKey (name)) 
				_readableStreamMap.Remove (name);

			if (_writableStreamMap.ContainsKey (name))
				_writableStreamMap.Remove (name);
		}

		/// <summary>
		/// Run this instance. Tries to create a TCP connection to the simulator.
		/// If this fails False will be returned otherwise true;
		/// </summary>
		public bool Run()
		{
			try
			{
				_ipep 	= new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9050);
				_server = new Socket(AddressFamily.InterNetwork,SocketType.Stream, ProtocolType.Tcp);
				_server.Connect( _ipep );
			}
			catch (SocketException e) {
				Logger.ErrOut (e.ToString());
				return false;
			}

			Run( new NetworkStream (_server) );

			return true;
		}

		public bool Run( NetworkStream stream )
		{
			if (stream == null)
				throw new ArgumentNullException ("stream");

			_netStream = stream;
			_tcpFrameWriter = new TcpMultiplexerWriter (_netStream, _logger);
			_tcpFrameReader = new TcpMultiplexerReader (_netStream, _logger);

			Task.Factory.StartNew( ConsumeReadableStreams, _tokenSource.Token );
			Task.Factory.StartNew (ConsumeNetworkStream, _tokenSource.Token);

			return true;
		}

		public void Stop()
		{
			_tokenSource.Cancel ();
			_netStream.Dispose ();

			// TODO: Clear all Maps, Arrays etc.
		}

		#region Private Methods
		// Returns a stream which is readable by the client and writable by the multiplexer
		Stream CreateReadonlyStream( string name )
		{
			if (_writableStreamMap.ContainsKey (name)) 
				throw new InvalidOperationException ("Key " + name + " already exists");

			_writableStreamMap [name] = new ProducerConsumerStream (BufferSize);

			Logger.DebugOut ("TcpMultiplexer create ReadonlyStream {0}", name);

			return _writableStreamMap [name];
		}

		Stream CreateWriteonlyStream( string name )
		{
			if (_readableStreamMap.ContainsKey (name)) 
				throw new InvalidOperationException ("Key " + name + " already exists");

			_readableStreamMap [name] = new ProducerConsumerStream (BufferSize);
			_readableStreams.Add( _readableStreamMap[name] );

			Logger.DebugOut ("TcpMultiplexer create WriteStream {0}", name);

			return _readableStreamMap [name];
		}

		Stream CreateReadWriteStream( string name )
		{
			if (_writableStreamMap.ContainsKey (name) || _readableStreamMap.ContainsKey (name))
				throw new InvalidOperationException ("Key " + name + " already exists");

			var rwStream = new ReadWriteStream (name, BufferSize);

			_readableStreamMap [name] = rwStream.OutStream;
			_readableStreams.Add( _readableStreamMap[name] );

			Logger.DebugOut ("TcpMultiplexer create ReadWriteStream {0}", name);

			_writableStreamMap [name] = rwStream.InStream;

			return rwStream;
		}

		// Reads data from a certain stream, adds its own header "<<<AUniqueName>>>" and
		// sends it over the NetworkStream to its counter part.
		// Finally the readable stream is back inserted into the readable stream collection. 
		void ConsumeReadableStreams()
		{
			var buffer = new byte[BufferSize];

			while (!_tokenSource.Token.IsCancellationRequested) 
			{
				try 
				{
					var ms = _readableStreams.Take();

					Logger.DebugOut( "TcpMultiplexer taken a readableStream" );

					ms.ReadAsync( buffer, 0, BufferSize, _tokenSource.Token ).ContinueWith( t => 
						{
							if (!t.IsFaulted)
							{
								var payload = buffer.Take( t.Result ).ToArray();

								Logger.DebugOut( "TcpMultiplexer writing to TCP: [PayloadLength:{0} | Payload: {1}]", 
									t.Result, 
									BitConverter.ToString(payload).Replace( "-", " ") );

								_tcpFrameWriter.WriteAsync( payload, _readableStreamMap.FirstOrDefault( x=> x.Value == ms).Key );

								_readableStreams.Add( ms );

								Logger.DebugOut( "TcpMultiplexer written to TCP: [PayloadLength:{0} | Payload: {1}]", 
									t.Result, 
									BitConverter.ToString(payload).Replace( "-", " ") );
							}
						});
				}
				catch (OperationCanceledException e)
				{
					Logger.ErrOut(e.ToString());
				}
			}
		}

		void ConsumeNetworkStream()
		{
			while (!_tokenSource.Token.IsCancellationRequested) 
			{
				try 
				{
					var frame = _tcpFrameReader.Read();

					Logger.DebugOut( "TcpMultiplexer read frame {0}", frame );

					// TODO: Check if there is a stream for frame.ChannelName

					_writableStreamMap[frame.ChannelName]
						.WriteAsync( frame.Payload, 0, frame.PayloadLength )
						.ContinueWith( t =>
							{
								_writableStreamMap[frame.ChannelName].Flush();
								Logger.DebugOut( "TcpMultiplexer read and written frame {0} to {1}", frame, frame.ChannelName );
							});
				}
				catch (OperationCanceledException e)
				{
					Logger.ErrOut( e.ToString() );
				}
				catch (InvalidDataException ide) 
				{
					Logger.ErrOut( ide.ToString() );
				}
			}
		}
		#endregion Private Methods
	}
}

