using System;
using System.Diagnostics;

namespace ChannelMultiplexer
{
	public static class Logger
	{
		[Conditional("LOG_DEBUG")]
		public static void DebugOut(string msg)
		{
			Console.WriteLine( "DEBUG " + msg);
		}

		[Conditional("LOG_DEBUG")]
		public static void DebugOut(string fmt, params object[] parms)
		{
			DebugOut(string.Format(fmt, parms));
		}

		public static void InfoOut( string msg )
		{
			Console.WriteLine ( "INFO " + msg );
		}

		public static void InfoOut( string fmt, params object[] parms )
		{
			InfoOut(string.Format(fmt, parms ));
		}

		public static void ErrOut( string msg )
		{
			Console.WriteLine ( "ERR " + msg );
		}

		public static void ErrOut( string fmt, params object[] parms )
		{
			ErrOut(string.Format(fmt, parms ));
		}
	}
}

