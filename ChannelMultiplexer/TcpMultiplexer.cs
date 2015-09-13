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
	public class TcpMultiplexer
	{
		const int BufferSize = 0xFFFF;
		const int AngleBrackets = 6;
		const string HeaderPattern = "^<<<*.*>>>";

		readonly NetworkStream _netStream;

		readonly Dictionary<string, Tuple<Stream, byte[]>> _readableStreamMap 	= new Dictionary<string, Tuple<Stream, byte[]>> ();
		readonly Dictionary<string, Tuple<Stream, byte[]>> _writableStreamMap 	= new Dictionary<string, Tuple<Stream, byte[]>> ();

		readonly BlockingCollection<Tuple<Stream, byte[]>> _readableStreams = new BlockingCollection<Tuple<Stream, byte[]>> ();
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

		public Stream CreateReadableStream( string name )
		{
			if (string.IsNullOrWhiteSpace (name)) 
				throw new ArgumentException (name);

			if (_readableStreamMap.ContainsKey (name)) 
				throw new InvalidOperationException ("Key " + name + " already exists");

			var buffer = new byte[BufferSize];
			var ms = new MemoryStream (buffer, false);

			_readableStreamMap [name] = Tuple.Create ((Stream)ms, buffer);
			_readableStreams.Add( _readableStreamMap[name] );

			return ms;
		}

		public Stream CreateWriteableStream( string name )
		{
			if (string.IsNullOrWhiteSpace (name)) 
				throw new ArgumentException (name);

			if (_readableStreamMap.ContainsKey (name)) 
				throw new InvalidOperationException ("Key " + name + " already exists");

			var buffer = new byte[BufferSize];
			var ms = new MemoryStream (buffer, true);

			_readableStreamMap [name] = Tuple.Create ((Stream)ms, buffer);
			_readableStreams.Add( _readableStreamMap[name] );

			return ms;
		}


		public void Close( string name )
		{
			if (string.IsNullOrWhiteSpace (name)) 
				throw new ArgumentException (name);

			if (_readableStreamMap.ContainsKey (name)) 
				_readableStreamMap.Remove (name);
		}

		// Reads data from a certain stream, adds its own header "<<<AUniqueName>>>" and
		// sends it over the NetworkStream to its counter part.
		// Finally the readable stream is back inserted into the readable stream collection. 
		private void ConsumeReadableStreams()
		{
			while (!_tokenSource.Token.IsCancellationRequested) 
			{
				try 
				{
					var tuple = _readableStreams.Take();

					tuple.Item1
						.ReadAsync( tuple.Item2, 0, BufferSize, _tokenSource.Token )
						.ContinueWith( t => 
							{
								if (t.IsCompleted)
								{
									var count = t.Result;
									var encoding = new ASCIIEncoding();
									var text = encoding.GetString( tuple.Item2, 0, count );
									var header = "<<<" + _readableStreamMap.FirstOrDefault( x=> x.Value == tuple).Key + ">>>";
									// TODO: check for null key
									var packet = Encoding.ASCII.GetBytes( header + text );

									_netStream.Write( packet, 0, packet.Count() );

									_readableStreams.Add( tuple );
								}});
				}
				catch (OperationCanceledException e)
				{
					// TODO
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
							if (t.IsCompleted)
							{
								var count = t.Result;
								var encoding = new ASCIIEncoding();
								var text = encoding.GetString( buffer, 0, count );
								var match = Regex.Match( text, HeaderPattern );

								if (match.Success)
								{
									var streamName = match.Value.Remove(0,3).TakeWhile( c => c != '>').ToString(); // extract stream name

									if (!_readableStreamMap.ContainsKey(streamName))
										throw new InvalidDataException( "Received data for an unkown stream" );

									var payload = buffer.Skip(streamName.Count() + AngleBrackets).ToArray();

									_readableStreamMap[streamName].Item1.WriteAsync( payload, 0, payload.Count() );
									_readbleNetworkStream.Add( netStream );
								}
							}
						});
				}
				catch (OperationCanceledException e)
				{
					// TODO: log it
					Console.WriteLine ( e );
				}
				catch (InvalidDataException ide) 
				{
					// TODO: log it
					Console.WriteLine ( ide );
				}
			}
		}
	}
}

