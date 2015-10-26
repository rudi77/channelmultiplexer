using System.Text;
using System;
using System.Diagnostics;

namespace ChannelMultiplexer
{
	public class TcpMultiplexerFrame
	{
		public byte[] Preamble { get; set; }

		public byte ChannelNameLength { get; set; }

		public string ChannelName { get; set; }

		public int PayloadLength { get; set; }

		public byte[] Payload { get; set; }

		public override string ToString()
		{
			return string.Format ("[TcpMultiplexerFrame: |{0} | {1} | {2} | {3} | {4}]", 
				BitConverter.ToString (Preamble).Replace ("-", " "), 
				ChannelNameLength, 
				ChannelName, 
				PayloadLength, 
				BitConverter.ToString (Payload).Replace ("-", " "));
		}

		public byte[] ToBytes()
		{
			// The total frame legnth
			var totalLength = Preamble.Length + 1 + (int)ChannelNameLength + 4 + PayloadLength;
			var frame = new byte[totalLength];
			var idx = 0;

			// add preamble
			for (var i = 0; i < Preamble.Length; i++)
			{
				frame [idx++] = Preamble [i];
			}

			// add channel name length
			frame[idx++] = ChannelNameLength;

			// add channel name
			var nameAsBytes = Encoding.ASCII.GetBytes (ChannelName);

			Debug.Assert ((int)ChannelNameLength == nameAsBytes.Length);

			for (var i = 0; i < nameAsBytes.Length; i++)
			{
				frame [idx++] = nameAsBytes [i];
			}

			// add payload length (little endian)
			//			var payloadLengthAsBytes = BitConverter.GetBytes( PayloadLength );
			frame [idx++] = (byte)((PayloadLength >> 24) & 0xFF);
			frame [idx++] = (byte)((PayloadLength >> 16) & 0xFF);
			frame [idx++] = (byte)((PayloadLength >> 8 ) & 0xFF);
			frame [idx++] = (byte)((PayloadLength      ) & 0xFF);

			Debug.Assert (PayloadLength == Payload.Length);

			// add payload
			for (var i = 0; i < Payload.Length; i++)
			{
				frame [idx++] = Payload [i];
			}

			return frame;
		}
	}
}

