using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Threading;
using System.Text;
using System.Linq;
using System.Text.RegularExpressions;

namespace ChannelMultiplexer
{
	using ReadableStreamMap = Dictionary<string, Stream>;
	using WritableStreamMap = Dictionary<string, Stream>;


	public class TcpMultiplexer
	{
		const int BufferSize = 0x100;
		const int AngleBrackets = 6;
		const string HeaderPattern = "^<<<*.*>>>";

		/// <summary>
		/// The possible directions of the created stream.
		/// </summary>
		public enum Direction
		{
			In,		// Readonly stream
			Out,	// Writeonly stream
			InOut	// Readable and writable stream
		}

		readonly NetworkStream _netStream;

		readonly ReadableStreamMap _readableStreamMap = new ReadableStreamMap();
		readonly WritableStreamMap _writableStreamMap = new WritableStreamMap();

		readonly BlockingCollection<Stream> _readableStreams = new BlockingCollection<Stream> ();
		readonly BlockingCollection<NetworkStream> _readbleNetworkStream 	= new BlockingCollection<NetworkStream> ();

		readonly CancellationTokenSource _tokenSource = new CancellationTokenSource();
		readonly string _name;

		public TcpMultiplexer ( NetworkStream netStream, string name="" )
		{
			if (netStream == null)
				throw new ArgumentNullException ("netStream");

			_netStream = netStream;
			_readbleNetworkStream.Add (_netStream);

			_name = name;

			Task.Factory.StartNew( ConsumeReadableStreams, _tokenSource.Token );
			Task.Factory.StartNew (ConsumeNetworkStream, _tokenSource.Token);
		}

		public Stream CreateStream( string name, Direction direction )
		{
			if (string.IsNullOrWhiteSpace (name))
				throw new ArgumentException (name);

			switch (direction) 
			{
				case Direction.In: 		return CreateReadonlyStream (name);
				case Direction.Out:		return CreateWriteonlyStream (name);
				case Direction.InOut:	return CreateReadWriteStream (name);
				default:				throw new NotSupportedException ("Direction does not exist");
			}
		}

		// Returns a stream which is readable by the client and
		// writeabel by the multiplexer
		private Stream CreateReadonlyStream( string name )
		{
			if (_writableStreamMap.ContainsKey (name)) 
				throw new InvalidOperationException ("Key " + name + " already exists");

			_writableStreamMap [name] = new ProducerConsumerStream (BufferSize);

			return _writableStreamMap [name];
		}

		private Stream CreateWriteonlyStream( string name )
		{
			if (_readableStreamMap.ContainsKey (name)) 
				throw new InvalidOperationException ("Key " + name + " already exists");

			_readableStreamMap [name] = new ProducerConsumerStream (BufferSize);
			_readableStreams.Add( _readableStreamMap[name] );

			return _readableStreamMap [name];
		}

		private Stream CreateReadWriteStream( string name )
		{
			if (_writableStreamMap.ContainsKey (name) || _readableStreamMap.ContainsKey (name))
				throw new InvalidOperationException ("Key " + name + " already exists");

			var rwStream = new ReadWriteStream (name, BufferSize);

			_readableStreamMap [name] = rwStream.OutStream;
			_readableStreams.Add( _readableStreamMap[name] );

			_writableStreamMap [name] = rwStream.InStream;

			return rwStream;
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

		// Reads data from a certain stream, adds its own header "<<<AUniqueName>>>" and
		// sends it over the NetworkStream to its counter part.
		// Finally the readable stream is back inserted into the readable stream collection. 
		private void ConsumeReadableStreams()
		{
			var buffer = new byte[BufferSize];
			while (!_tokenSource.Token.IsCancellationRequested) 
			{
				try 
				{
					var ms = _readableStreams.Take();

					ms.ReadAsync( buffer, 0, BufferSize, _tokenSource.Token ).ContinueWith( t => 
					{
						Logger.DebugOut( "{0} Read from ReadableStream", _name );

						if (!t.IsFaulted)
						{
							var count = t.Result;
							var encoding = new ASCIIEncoding();
							var text = encoding.GetString( buffer, 0, count );
							var header = "<<<" + _readableStreamMap.FirstOrDefault( x=> x.Value == ms).Key + ">>>";

							Logger.InfoOut( "{0} sending Packet {1}{2}", _name, header, text);

							// TODO: check for null key
							var packet = Encoding.ASCII.GetBytes( header + text );

							_netStream.Write( packet, 0, packet.Count() );
							_netStream.Flush();

							_readableStreams.Add( ms );
						}
					});
				}
				catch (OperationCanceledException e)
				{
					Logger.ErrOut (e.ToString());
				}
			}
		}

		private void ConsumeNetworkStream()
		{
			var buffer = new byte[BufferSize];

			while (!_tokenSource.Token.IsCancellationRequested) 
			{
				try 
				{
					var netStream = _readbleNetworkStream.Take();

					netStream
						.ReadAsync( buffer, 0, BufferSize )
						.ContinueWith( t => {
							if (!t.IsFaulted)
							{
								var count = t.Result;
								var encoding = new ASCIIEncoding();
								var text = encoding.GetString( buffer, 0, count );

								Logger.InfoOut( "{0} recv {1}", _name, text );

								var match = Regex.Match( text, HeaderPattern );

								if (match.Success)
								{
									var channelNameArray = match.Value.Remove(0,3).TakeWhile( c => c != '>'); // extract stream name
									var channelName = new string(channelNameArray.ToArray());

									Logger.DebugOut( "{0} ChannelName: {1}", _name, channelName );

									if (!_writableStreamMap.ContainsKey(channelName))
										throw new InvalidDataException( "Received data for an unkown stream" );

									var payload = Encoding.ASCII.GetBytes(text.Remove(0, channelName.Count() + AngleBrackets));

									_writableStreamMap[channelName].WriteAsync( payload, 0, payload.Count() ).ContinueWith( ta =>
									{
										_writableStreamMap[channelName].Flush();
									});
									
									_readbleNetworkStream.Add( netStream );
								}
							}
						});
				}
				catch (OperationCanceledException e)
				{
					// TODO: log it
					Logger.ErrOut( e.ToString() );
				}
				catch (InvalidDataException ide) 
				{
					// TODO: log it
					Logger.ErrOut( ide.ToString() );
				}
			}
		}
	}
}

