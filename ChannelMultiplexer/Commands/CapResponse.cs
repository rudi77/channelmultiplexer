using System;

namespace ChannelMultiplexer
{
	public enum ResponseType
	{
		ACK,
		NACK
	}

	public abstract class CapResponse
	{
		public string ModuleIdentifier { get; set; }

		public string ResponseString { get; set; }

		public ResponseType Type { get; set; }
	}

	// TODO: Should contain a list of available modules
	public class ScanResponse : CapResponse
	{
		public override string ToString()
		{
			return string.Format("{0},{1};", Type, ResponseString);
		}
	}

	public class OpenChannelResponse : CapResponse
	{
		public string InstrumentName { get; set; }

		public string SerialNumberModule { get; set; }

		public string ChannelName { get; set; }

		public TcpMultiplexer.Direction Direction { get; set; }

		public override string ToString()
		{
			// e.g."ACK,Instrument1,1234567,Sim_Command_Instrument1_1234567;"
			return string.Format ("{0},{1},{2},{3};", Type, InstrumentName, SerialNumberModule, ChannelName);
		}
	}
}

