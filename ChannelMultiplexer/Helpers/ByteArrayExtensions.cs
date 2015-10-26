using System.Text;

namespace ChannelMultiplexer
{
	/// <summary>
	/// <a href="http://msdn.microsoft.com/en-us/library/bb383977.aspx">Extensions</a> for <c>byte[]</c> objects.
	/// </summary>
	public static class ByteArrayExtensions
	{
		/// <summary>
		/// Generates and returns a string which represents the byte[]. It is converted to a hex-string to HEX-ASCII lines with 16bytes per line.
		/// This method is supposed to help displaying the content of the array, assuming that the first byte is located at the given
		/// virtual address.
		/// </summary>
		/// <param name="buffer">The array, containing the data to convert to a human readable string.</param>
		/// <param name="virtualOffset">A virtual address of the first byte in the array.</param>
		/// <returns>A multi lined string representing a special part of the array in hex form, containing the addresses of the bytes.</returns>
		public static string ToString(this byte[] buffer, int virtualOffset)
		{
			int offset = 0;
			int count = buffer.Length;

			return ToString(buffer, offset, count, virtualOffset);
		}

		/// <summary>
		/// Generates and returns a string which represents the byte[]. It is converted to a hex-string to HEX-ASCII lines with 16bytes per line.
		/// This method is supposed to help displaying the content of a specific reagion in the array.
		/// </summary>
		/// <param name="buffer">The array, containing the data to convert to a human readable string.</param>
		/// <param name="offset">The index of the first byte to include to convert.</param>
		/// <param name="count">The amount of bytes to include to convert.</param>
		/// <returns>A multi lined string representing a special part of the array in hex form, containing the addresses of the bytes.</returns>
		public static string ToString(this byte[] buffer, int offset, int count)
		{
			return ToString(buffer, offset, count, offset);


			//string[] arrAnswer = new string[2 + count / 32 + 1];

			//arrAnswer[0] = " 0  1  2  3  4  5  6  7   8  9 10 11 12 13 14 15  16 17 18 19 20 21 22 23  24 25 26 27 28 29 30 31";
			//arrAnswer[1] = "---------------------------------------------------------------------------------------------------";
			//for (int b = offset; b < offset + count; b++)
			//{
			//  int iLine = (int)((b - offset) / 32);
			//  arrAnswer[2 + iLine] += String.Format("{0:X2} ", buffer[b]);
			//  if (((b - offset) % 8) == 7)
			//  {
			//    arrAnswer[2 + iLine] += " ";
			//  }
			//}

			//StringBuilder sb = new StringBuilder();
			//foreach (string str in arrAnswer)
			//{
			//  if (sb.Length > 0)
			//    sb.Append("\n");

			//  sb.Append(str);
			//}

			//return sb.ToString();
		}

		/// <summary>
		/// Generates and returns a string which represents the byte[]. It is converted to a continous hex-string.
		/// This method is supposed to help displaying the content of a specific reagion in the array.
		/// </summary>
		/// <param name="buffer">The array, containing the data to convert to a human readable string.</param>
		/// <param name="offset">The index of the first byte to include to convert.</param>
		/// <param name="count">The amount of bytes to include to convert.</param>
		/// <returns>A multi lined string representing a special part of the array in hex form, not containing any address hints.</returns>
		public static string ToShortString(this byte[] buffer, int offset, int count)
		{
			return ToString(buffer, offset, count, -1);

			//StringBuilder sb = new StringBuilder();

			//for (int i = offset; i < offset + count; i++)
			//  sb.Append(String.Format("{0:X2}", buffer[i]));

			//return sb.ToString();
		}

		private static string ToString(byte[] buffer, int offset, int count, int virtualOffset)
		{
			//the current position in the line
			int inLineIndex = 0;
			//the current address at the start of the line
			int hexAddress = 0;
			//the string builder holding the HEX values (and the ASCII interpretation at the end of the line)
			StringBuilder sbHex = new StringBuilder();
			//the ASCII interpretation of the current HEX line
			StringBuilder sbAscii = new StringBuilder();

			//if a virtual offset is set
			if (virtualOffset >= 0)
			{
				//calculate the 16byte aligning
				inLineIndex = virtualOffset % 16;
				hexAddress = virtualOffset - inLineIndex;

				//prepare the header
				string startLine = "offset (h)  00 01 02 03 04 05 06 07 08 09 0A 0B 0C 0D 0E 0F\n\n";
				sbHex.Append(startLine);

				//add spaces to the line to get the correct address
				if (inLineIndex > 0)
				{
					for (int i = 0; i < inLineIndex; i++)
					{
						sbHex.Append("   ");
						sbAscii.Append(" ");
					}
				}
			}

			//get the data in 16byte lines.
			for (int index = offset; index < offset + count; index++)
			{
				byte b = buffer[index];
				inLineIndex++;

				//on first byte in line
				if (inLineIndex == 1)
				{
					//move to next line, if not the first line
					if(index != offset)
						sbHex.Append("\n");

					//if virtual offset is set, add the current address
					if (virtualOffset >= 0)
						sbHex.Append(string.Format("{0:X8}    ", hexAddress));
				}

				//add the data byte to the HEX string
				sbHex.Append(string.Format("{0:X2} ", b));

				//add the current byte (as char, if possible) to the ASCII string
				if (b < 0x20 || b > 0x7E)
					sbAscii.Append('.');
				else
					sbAscii.Append((char)b);

				//when the line ends
				if (inLineIndex >= 16)
				{
					//reset the counter and increase the address
					inLineIndex = 0;
					hexAddress += 0x10;

					//add the ASCII interpretation of the HEX line to the string
					sbHex.Append("   ");
					sbHex.Append(sbAscii.ToString());

					//clear the ASCII interpretation to make place for the next line
					sbAscii.Clear();
				}
			}

			//if there are data left
			if (inLineIndex > 0)
			{
				//fill up the rest of the line with spaces
				for (int i = inLineIndex; i < 16; i++)
				{
					sbHex.Append("   ");
				}

				//add the ASCII interpretation of the HEX line to the string
				sbHex.Append("   ");
				sbHex.Append(sbAscii.ToString());
			}

			//return the whole string
			return sbHex.ToString();
		}
	}
}

