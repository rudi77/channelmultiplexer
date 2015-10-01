using System;
using System.IO;

namespace ChannelMultiplexer
{
	/// <summary>
	/// Read write stream.
	/// </summary>
	public class ReadWriteStream : Stream
	{
		// A default buffer size
		const int BufSize = 1024;

		readonly string _name;

		// A client, like a network stream writes to the read stream.
		readonly ProducerConsumerStream _inStream;

		// A client, like a network stream reads from the write stream.
		readonly ProducerConsumerStream _outStream;

		public ReadWriteStream ( string name )
		{
			if (string.IsNullOrWhiteSpace (name))
				throw new ArgumentException ("SelectiveStream name argument be a non-empty string");

			_name = name;
			_inStream = new ProducerConsumerStream (BufSize);
			_outStream = new ProducerConsumerStream (BufSize);
		}

		public ReadWriteStream ( string name, int bufSize ) : this(name)
		{
			if (bufSize < 1)
				throw new ArgumentException ("Buffer size must be greater 0");

			_inStream = new ProducerConsumerStream (bufSize);
			_outStream = new ProducerConsumerStream (bufSize);
		}

		public string Name
		{
			get { return _name; }
		}

		public Stream InStream
		{
			get { return _inStream; }
		}

		public Stream OutStream 
		{ 
			get { return _outStream; } 
		}

		#region implemented abstract members of Stream

		public override void Flush()
		{
			Logger.DebugOut ("Flush outStream");
			_outStream.Flush ();
		}

		public override long Seek( long offset, SeekOrigin origin )
		{
			throw new NotImplementedException ();
		}

		public override void SetLength( long value )
		{
			throw new NotImplementedException ();
		}
			
		public override int Read( byte[] buffer, int offset, int count )
		{
//			#if LOG_DEBUG
//			var res = _inStream.Read (buffer, offset, count);
//			Logger.DebugOut ("Read from inStream, buffer {0}, offset {1}, count {2}, read {3}", buffer, offset, count, res);
//			return res;
//			#else
			return _inStream.Read (buffer, offset, count);
//			#endif
		}

		public override void Write( byte[] buffer, int offset, int count )
		{
//			Logger.DebugOut ("Write to outStream, buffer {0}, offset {1}, count {2}", buffer, offset, count);
			_outStream.Write (buffer, offset, count);
		}

		public override bool CanRead {
			get {
				return true;
			}
		}

		public override bool CanSeek {
			get {
				return false;
			}
		}

		public override bool CanWrite {
			get {
				return true;
			}
		}

		public override long Length {
			get {
				throw new NotImplementedException ();
			}
		}

		public override long Position {
			get {
				throw new NotImplementedException ();
			}
			set {
				throw new NotImplementedException ();
			}
		}

		#endregion
	}
}

