using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Text;

namespace ChannelMultiplexer
{
	public class TcpMultiplexerReader
	{
		readonly NetworkStream _stream;
		readonly byte[] _preamble = { 0xFF, 0x01, 0xFF, 01 };

		public TcpMultiplexerReader ( NetworkStream stream, ILog logger )
		{
			Guard.ArgumentNotNull (stream, "stream");
			Guard.ArgumentNotNull( logger, "logger" );
			_stream = stream;
			_logger = logger;
		}

		// Reads a TcpMultiplexer frame and returns its payload
		public TcpMultiplexerFrame Read()
		{
			// 1.) read preamble
			var preamble = new byte[4];
			var bytesRead = 0;
			while (bytesRead < _preamble.Length)
			{
				bytesRead += _stream.Read (preamble, 0, _preamble.Length - bytesRead);
			}

			_logger.Debug ("TcpMultiplexerReader Preamble {0}", BitConverter.ToString (preamble).Replace ("-", " "));

			if (preamble [0] != _preamble [0] || preamble [1] != _preamble [1] ||
				preamble [2] != _preamble [2] || preamble [3] != _preamble [3])
			{
				throw new InvalidOperationException (
					string.Format("Invalid TcpMultiplexerFrame preamble: {0}", BitConverter.ToString (preamble).Replace ("-", " ")));
			}

			// 2.) read channel name length field
			var channelNameLength = _stream.ReadByte();
			if (channelNameLength <= 0)
				throw new InvalidOperationException ("Invalid TcpMultiplexerFrame could not read ChannelNameLength field" );

			_logger.Debug ("TcpMultiplexerReader ChannelNameLength {0}", channelNameLength );

			// 3.) read channel name
			var channelNameBuffer = new byte[channelNameLength];
			bytesRead = 0;
			while (bytesRead < channelNameLength)
			{
				bytesRead += _stream.Read (channelNameBuffer, 0, channelNameBuffer.Length - bytesRead);
			}

			var channelName = Encoding.ASCII.GetString (channelNameBuffer);

			_logger.Debug ("TcpMultiplexerReader ChannelName {0}", channelName );

			// 4.) read payload length (four bytes)
			var payloadLength = 0;
			payloadLength |= _stream.ReadByte () << 24;
			payloadLength |= _stream.ReadByte () << 16;
			payloadLength |= _stream.ReadByte () << 8;
			payloadLength |= _stream.ReadByte ();

			_logger.Debug ("TcpMultiplexerReader PayloadLength {0}", payloadLength );

			// 5.) read payload
			var payloadBuffer = new byte[payloadLength];
			bytesRead = 0;
			while (bytesRead < payloadLength)
			{
				bytesRead += _stream.Read (payloadBuffer, 0, payloadBuffer.Length - bytesRead);
			}

			var frame = new TcpMultiplexerFrame {
				Preamble = preamble,
				ChannelNameLength = (byte)channelNameLength,
				ChannelName = channelName,
				PayloadLength = payloadLength,
				Payload = payloadBuffer
			};

			_logger.Debug ("TcpMultiplexerReader {0}", frame);

			return frame;
		}

		public Task<TcpMultiplexerFrame> ReadAsync()
		{
			return Task.Factory.StartNew ( () => Read() );
		}
	}
}

