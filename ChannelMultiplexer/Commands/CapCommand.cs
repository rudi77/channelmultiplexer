namespace ChannelMultiplexer
{
	public abstract class CapCommand
	{
		public string CommandString { get; set; }

		public override string ToString()
		{
			return string.Format("[CapCommand: CommandString={0}]", CommandString);
		}
	}

	// Scan modules
	public class ScanCommand : CapCommand
	{}

	// Open a channel, one way or two way
	public class OpenChannelCommand : CapCommand
	{
		public string SerialNumberInstrument { get; set; }

		public string SerialNumberModule { get; set; }

		public TcpMultiplexer.Direction Direction { get; set; }
	}

	// Close a channel
	public class CloseCommand : CapCommand
	{
		public string ChannelName { get; set; }
	}

	public class UnkownCommand : CapCommand
	{}
}

