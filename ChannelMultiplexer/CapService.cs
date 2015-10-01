using System;
using System.Collections.Generic;
using System.IO;

namespace ChannelMultiplexer
{
	public class CapService
	{
		const string Identifier = "SymbioSim";

		#region TestData
		readonly Dictionary<string,CapResponse> _responseMap = new Dictionary<string,CapResponse> 
		{
			{"SymbioSim,ScanModules", 
				new ScanResponse
				{
					Type = ResponseType.ACK,
					ResponseString =
						"<TecanModules>" +
						"<Module InstrumentSerialNumber=\"Instrument1\" ModuleSerialNumber=\"1234567\" ModuleType=\"MTP\" ModuleNumber=\"0\" Protocol=\"TDCL2.0\" InstrumentFamily=\"SYMBIO\" Mode=\"Operation\" Simulated=\"True\" CommandChannel=\"True\" DataChannel=\"True\" DebugChannel=\"False\" ExternModule=\"False\" />" +
						"<Module InstrumentSerialNumber=\"Instrument1\" ModuleSerialNumber=\"135\" ModuleType=\"ABS\" ModuleNumber=\"1\" Protocol=\"TDCL2.0\" InstrumentFamily=\"SYMBIO\" Mode=\"Operation\" Simulated=\"True\" CommandChannel=\"True\" DataChannel=\"True\" DebugChannel=\"False\" ExternModule=\"False\" />" +
						"<Module InstrumentSerialNumber=\"Instrument1\" ModuleSerialNumber=\"345678\" ModuleType=\"LUM\" ModuleNumber=\"1\" Protocol=\"TDCL2.0\" InstrumentFamily=\"SYMBIO\" Mode=\"Operation\" Simulated=\"True\" CommandChannel=\"True\" DataChannel=\"True\" DebugChannel=\"False\" ExternModule=\"False\" />" +
						"<Module InstrumentSerialNumber=\"Instrument1\" ModuleSerialNumber=\"11111123\" ModuleType=\"FLUOR\" ModuleNumber=\"1\" Protocol=\"TDCL2.0\" InstrumentFamily=\"SYMBIO\" Mode=\"Operation\" Simulated=\"True\" CommandChannel=\"True\" DataChannel=\"True\" DebugChannel=\"False\" ExternModule=\"False\" />" +
						"<Module InstrumentSerialNumber=\"Instrument1\" ModuleSerialNumber=\"00021\" ModuleType=\"USBCAM\" ModuleNumber=\"1\" Protocol=\"TDCL2.0\" InstrumentFamily=\"SYMBIO\" Mode=\"Operation\" Simulated=\"True\" CommandChannel=\"True\" DataChannel=\"True\" DebugChannel=\"False\" ExternModule=\"True\" />" +
						"</TecanModules>;"
				}
 			},
			{"SymbioSim,CommandChannel,Instrument1,1234567", 
				new OpenChannelResponse 
				{
					Type = ResponseType.ACK,
					ModuleIdentifier = Identifier,
					InstrumentName = "Instrument1",
					SerialNumberModule = "1234567",
					ChannelName = "Sim_Command_Instrument1_1234567",
					Direction = TcpMultiplexer.Direction.InOut
				}
			},
			{"SymbioSim,DataChannel,Instrument1,1234567",
				new OpenChannelResponse 
				{
					Type = ResponseType.ACK,
					ModuleIdentifier = Identifier,
					InstrumentName = "Instrument1",
					SerialNumberModule = "1234567",
					ChannelName = "Sim_Data_Instrument1_1234567",
					Direction = TcpMultiplexer.Direction.Out
				}
			},
			{"SymbioSim,CommandChannel,Instrument1,135", 
				new OpenChannelResponse 
				{
					Type = ResponseType.ACK,
					ModuleIdentifier = Identifier,
					InstrumentName = "Instrument1",
					SerialNumberModule = "135",
					ChannelName = "Sim_Command_Instrument1_135",
					Direction = TcpMultiplexer.Direction.InOut
				}
			},
			{"SymbioSim,DataChannel,Instrument1,135", 
				new OpenChannelResponse 
				{
					Type = ResponseType.ACK,
					ModuleIdentifier = Identifier,
					InstrumentName = "Instrument1",
					SerialNumberModule = "135",
					ChannelName = "Sim_Data_Instrument1_135",
					Direction = TcpMultiplexer.Direction.Out
				}
			},
			{"SymbioSim,CommandChannel,Instrument1,345678", 
				new OpenChannelResponse 
				{
					Type = ResponseType.ACK,
					ModuleIdentifier = Identifier,
					InstrumentName = "Instrument1",
					SerialNumberModule = "345678",
					ChannelName = "Sim_Command_Instrument1_345678",
					Direction = TcpMultiplexer.Direction.InOut
				}
			},
			{"SymbioSim,DataChannel,Instrument1,345678", 
				new OpenChannelResponse 
				{
					Type = ResponseType.ACK,
					ModuleIdentifier = Identifier,
					InstrumentName = "Instrument1",
					SerialNumberModule = "345678",
					ChannelName = "Sim_Data_Instrument1_345678",
					Direction = TcpMultiplexer.Direction.In
				}
			},
			{"SymbioSim,CommandChannel,Instrument1,11111123",
				new OpenChannelResponse 
				{
					Type = ResponseType.ACK,
					ModuleIdentifier = Identifier,
					InstrumentName = "Instrument1",
					SerialNumberModule = "11111123",
					ChannelName = "Sim_Command_Instrument1_11111123",
					Direction = TcpMultiplexer.Direction.InOut
				}
			},
			{"SymbioSim,DataChannel,Instrument1,11111123",
				new OpenChannelResponse 
				{
					Type = ResponseType.ACK,
					ModuleIdentifier = Identifier,
					InstrumentName = "Instrument1",
					SerialNumberModule = "11111123",
					ChannelName = "Sim_Data_Instrument1_11111123",
					Direction = TcpMultiplexer.Direction.Out
				}
			},
			{"SymbioSim,CommandChannel,Instrument1,00021", 
				new OpenChannelResponse 
				{
					Type = ResponseType.ACK,
					ModuleIdentifier = Identifier,
					InstrumentName = "Instrument1",
					SerialNumberModule = "00021",
					ChannelName = "Sim_Command_Instrument1_00021",
					Direction = TcpMultiplexer.Direction.InOut
				}
			},
			{"SymbioSim,DataChannel,Instrument1,00021", 
				new OpenChannelResponse 
				{
					Type = ResponseType.ACK,
					ModuleIdentifier = Identifier,
					InstrumentName = "Instrument1",
					SerialNumberModule = "00021",
					ChannelName = "Sim_Data_Instrument1_00021",
					Direction = TcpMultiplexer.Direction.Out
				}
			}
		};
		#endregion TestData

		readonly TcpMultiplexer _multiplexer;
		readonly List<Stream> _streams = new List<Stream> ();

		public CapService (TcpMultiplexer multiplexer)
		{
			if (multiplexer == null)
				throw new ArgumentNullException ("multiplexer");

			_multiplexer = multiplexer;
		}

		public string CreateResponse( CapCommand command )
		{
			dynamic cmd = command;
			return CreateResponse (cmd);
		}

		private string CreateResponse( ScanCommand command )
		{
			return _responseMap [command.CommandString].ToString();
		}

		private string CreateResponse( OpenChannelCommand command )
		{
			var response = _responseMap [command.CommandString] as OpenChannelResponse;

			if (response == null)
				throw new InvalidOperationException ("Invalid response type");

			var stream = _multiplexer.CreateStream (response.ChannelName, response.Direction);

			_streams.Add(stream );

			return response.ToString ();
		}

		private string CreateResponse( CloseCommand command )
		{
			return string.Empty;
		}

		private string CreateResponse( UnkownCommand command )
		{
			return string.Empty;
		}
	}
}

