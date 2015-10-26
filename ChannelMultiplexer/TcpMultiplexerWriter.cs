using System;
using System.Net.Sockets;
using Tecan.At.Dragonfly.Framework;
using System.Threading.Tasks;

using Tecan.At.Dragonfly.Framework;

{
	/// <summary>
	/// Tcp multiplexer writer. Writes a byte buffer to the network
	/// stream. Appending a header || FF 01 FF 01  "ChannelNameLength" "ChannelName" PayloadLength | Payload ||
	/// </summary>
	/// <remarks>
	/// Header Field Description:
	/// FF 01 FF 01 - preamble indicates the beginning of a new TcpMultiplexer frame
	/// ChannelNameLength - 1 Byte long, the length of the channel name which comes next
	/// ChannelName - the name of the channel to be addressed
	/// PayloadLength - 4 Bytes long, the length of the payload.
	/// </remarks>
	public class TcpMultiplexerWriter
	{
		readonly NetworkStream _stream;
		readonly byte[] _preamble = { 0xFF, 0x01, 0xFF, 01 };
		readonly ILog _logger;

		public TcpMultiplexerWriter ( NetworkStream stream, ILog logger )
		{
			Guard.ArgumentNotNull (stream, "stream");
			Guard.ArgumentNotNull (logger, "logger");
			_stream = stream;
			_logger = logger;
		}

		public void Write( byte[] payload, string channelName )
		{
			var frame = new TcpMultiplexerFrame {
				Preamble = _preamble,
				ChannelNameLength = (byte)channelName.Length,
				ChannelName = channelName,
				PayloadLength = payload.Length,
				Payload = payload,
			};

			var buffer = frame.ToBytes ();
			_stream.Write (buffer, 0, buffer.Length);
			_stream.Flush ();
			_logger.Debug ("TcpMultiplexerWriter {0}", frame);
		}

		public Task WriteAsync( byte[] payload, string channelName )
		{
			var frame = new TcpMultiplexerFrame {
				Preamble = _preamble,
				ChannelNameLength = (byte)channelName.Length,
				ChannelName = channelName,
				PayloadLength = payload.Length,
				Payload = payload,
			};

			var buffer = frame.ToBytes ();
			return _stream.WriteAsync (buffer, 0, buffer.Length).ContinueWith( t => 
				{
					_stream.FlushAsync();
					_logger.Debug ("TcpMultiplexerWriter {0}", frame);
				});
		}
	}
}

