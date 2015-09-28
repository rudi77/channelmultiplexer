using System;
using System.IO;
using System.Threading.Tasks;
using System.Text;

namespace ChannelMultiplexer
{
	/// <summary>
	/// Cap reader. Converts raw bytes into a CAP command.
	/// </summary>
	/// <remarks>
	/// Encoding is currently not taken into account. We assume an ASCII encoded string
	/// </remarks>
	public class CapReader
	{
		const string ModuleIdentifier = "SymbioSim";
		readonly Stream _stream;

		public CapReader (Stream stream)
		{
			if (stream == null)
				throw new ArgumentNullException ("stream");

			_stream = stream;
		}

		public CapCommand ReadCommand()
		{
			char readChar;
			var sb = new StringBuilder ();

			do
			{
				var readInt = _stream.ReadByte ();

				if (readInt == -1)
					return null;

				readChar = (char)readInt;
				if (readChar != ';')
					sb.Append (readChar);
			} while (readChar != ';');

			var request = sb.ToString ();

			Logger.InfoOut ("CapReader read {0};", request);

			return Create( request );
		}

		public Task<CapCommand> ReadCommandAsync()
		{
			return Task<CapCommand>.Factory.StartNew (ReadCommand);
		}

		private CapCommand Create( string request )
		{
			string[] messageParts = request.Split (',');

			if (messageParts [0] == ModuleIdentifier)
			{
				var requestType = messageParts[1];

				Func<TcpMultiplexer.Direction, OpenChannel> createChannel = direction => new OpenChannel {
					CommandString = request,
					Direction = TcpMultiplexer.Direction.InOut,
					SerialNumberInstrument = messageParts [2],
					SerialNumberModule = messageParts [3]
				};

				switch (requestType)
				{
				case "ScanModules":
					return new Scan { CommandString = request };
				case "CommandChannel":
					return createChannel (TcpMultiplexer.Direction.InOut);
				case "DataChannel":
					return createChannel (TcpMultiplexer.Direction.Out);
				case "DebugChannel":
					return createChannel (TcpMultiplexer.Direction.Out);
				default:
					return null;
				}
			}
			else if (messageParts [0] == "Close")
			{
				return new Close { CommandString = request, ChannelName = messageParts [1] };
			}
			else
			{
				return new UnkownCommand { CommandString = request };
			}
		}
	}
}

