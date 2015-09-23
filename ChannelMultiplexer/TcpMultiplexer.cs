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
	using ReadableStreamMap = Dictionary<string, ProducerConsumerStream>;
	using WritableStreamMap = Dictionary<string, ProducerConsumerStream>;


	public class TcpMultiplexer
	{
		const int BufferSize = 0x100;
		const int AngleBrackets = 6;
		const string HeaderPattern = "^<<<*.*>>>";

		readonly NetworkStream _netStream;

		readonly ReadableStreamMap _readableStreamMap = new ReadableStreamMap();
		readonly WritableStreamMap _writableStreamMap = new WritableStreamMap();

		readonly BlockingCollection<ProducerConsumerStream> _readableStreams = new BlockingCollection<ProducerConsumerStream> ();
		readonly BlockingCollection<NetworkStream> _readbleNetworkStream 	= new BlockingCollection<NetworkStream> ();

		readonly CancellationTokenSource _tokenSource = new CancellationTokenSource();
		readonly List<Task> _activeTasks = new List<Task> ();

		public TcpMultiplexer ( NetworkStream netStream )
		{
			if (netStream == null)
				throw new ArgumentNullException ("netStream");

			_netStream = netStream;
			_readbleNetworkStream.Add (_netStream);

			Task.Factory.StartNew( ConsumeReadableStreams, _tokenSource.Token );
			Task.Factory.StartNew (ConsumeNetworkStream, _tokenSource.Token);
		}

		// Returns a stream which is readable by the client and
		// writeabel by the multiplexer
		public Stream CreateReadableStream( string name )
		{
			if (string.IsNullOrWhiteSpace (name)) 
				throw new ArgumentException (name);

			if (_readableStreamMap.ContainsKey (name)) 
				throw new InvalidOperationException ("Key " + name + " already exists");

			_writableStreamMap [name] = new ProducerConsumerStream (BufferSize);

			return _writableStreamMap [name];
		}

		public Stream CreateWriteableStream( string name )
		{
			if (string.IsNullOrWhiteSpace (name)) 
				throw new ArgumentException (name);

			if (_readableStreamMap.ContainsKey (name)) 
				throw new InvalidOperationException ("Key " + name + " already exists");

			_readableStreamMap [name] = new ProducerConsumerStream (BufferSize);
			_readableStreams.Add( _readableStreamMap[name] );

			return _readableStreamMap [name];
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
						if (!t.IsFaulted)
						{
							var count = t.Result;
							var encoding = new ASCIIEncoding();
							var text = encoding.GetString( buffer, 0, count );
							var header = "<<<" + _readableStreamMap.FirstOrDefault( x=> x.Value == ms).Key + ">>>";

							Logger.InfoOut( "Sending Packet {0}{1}", header, text);

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

								Logger.InfoOut( "Recv {0}", text );

								var match = Regex.Match( text, HeaderPattern );

								if (match.Success)
								{
									var channelNameArray = match.Value.Remove(0,3).TakeWhile( c => c != '>'); // extract stream name
									var channelName = new string(channelNameArray.ToArray());

									Logger.DebugOut( "ChannelName: {0}", channelName );

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

