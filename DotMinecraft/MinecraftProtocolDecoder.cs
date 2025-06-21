using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Encodings;
using System.Text.Unicode;

namespace DotMinecraft
{
	public sealed class NoEncoding : Encoding
	{
		private NoEncoding() { }
		public static NoEncoding instance = new NoEncoding();
		public override int GetByteCount(char[] chars, int index, int count)
		{
			throw new NotImplementedException();
		}

		public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex)
		{
			throw new NotImplementedException();
		}

		public override int GetCharCount(byte[] bytes, int index, int count)
		{
			throw new NotImplementedException();
		}

		public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex)
		{
			throw new NotImplementedException();
		}

		public override int GetMaxByteCount(int charCount)
		{
			return charCount;
		}

		public override int GetMaxCharCount(int byteCount)
		{
			return byteCount;
		}
	}
	public readonly struct MinecraftProtocolDecoder
	{
		public MinecraftProtocolDecoder(BinaryReader underlying) {
			this.underlying = underlying ?? throw new ArgumentNullException(nameof(underlying));
		}
		private const int SEGMENT_BITS = 0x7F;
		private const int CONTINUE_BIT = 0x80;
		private const long SEGMENT_BITS_LONG = 0x7F;
		private const long CONTINUE_BIT_LONG = 0x80;
		public const int MAX_RECEIVE_STRING_SIZE = 262144;
		public static void ReadAtLeastOrThrow(BinaryReader binaryReader, Span<byte> buffer){
			int bufsize = buffer.Length;
			if(bufsize == 0){
				return;
			}
			while(true){
				int count = binaryReader.Read(buffer);
				bufsize -= count;
				if (bufsize == 0) return;
				if (count < 1){
					throw new EndOfStreamException();
				}
				buffer = buffer.Slice(count);
			}

		}
		
		private readonly BinaryReader underlying;
		public void ReadAtLeastOrThrow(Span<byte> buffer){
			ReadAtLeastOrThrow(underlying, buffer);
		}
		public bool ReadBoolean(){
			return underlying.ReadBoolean();
		}
		public sbyte ReadByte(){
			return underlying.ReadSByte();
		}
		public byte ReadUnsignedByte(){
			return underlying.ReadByte();
		}
		public T ReadUnmanagedBigEndian<T>() where T : unmanaged{
			T response = default;
			Span<byte> byteSpan = MemoryMarshal.AsBytes(new Span<T>(ref response));
			ReadAtLeastOrThrow(underlying, byteSpan);
			byteSpan.Reverse();
			return response;
		}
		public int ReadVarInt() {
			int value = 0;
			int position = 0;
			byte currentByte;

			while (true)
			{
				currentByte = underlying.ReadByte();
				value |= (currentByte & SEGMENT_BITS) << position;

				if ((currentByte & CONTINUE_BIT) == 0) break;

				position += 7;

				if (position >= 32) throw new Exception("VarInt is too big");
			}

			return value;
		}
		public (int,int) ReadVarIntExtended()
		{
			int value = 0;
			int position = 0;
			byte currentByte;

			while (true)
			{
				currentByte = underlying.ReadByte();
				value |= (currentByte & SEGMENT_BITS) << position;

				if ((currentByte & CONTINUE_BIT) == 0) break;

				position += 7;

				if (position >= 32) throw new Exception("VarInt is too big");
			}

			return (value,(position / 7) + 1);
		}
		public long ReadVarLong()
		{
			long value = 0;
			int position = 0;
			byte currentByte;

			while (true)
			{
				currentByte = underlying.ReadByte();
				value |= (currentByte & SEGMENT_BITS_LONG) << position;

				if ((currentByte & CONTINUE_BIT_LONG) == 0) break;

				position += 7;

				if (position >= 64) throw new Exception("VarLong is too big");
			}

			return value;
		}
		public byte[] ReadRawString(){
			return ReadRawStringImpl(MAX_RECEIVE_STRING_SIZE);
		}
		public byte[] ReadRawString(int sizeLimit){
			if (sizeLimit > MAX_RECEIVE_STRING_SIZE)
			{
				throw new Exception("Excessively large size limit for string!");
			}
			return ReadRawStringImpl(sizeLimit);
		}
		private byte[] ReadRawStringImpl(int sizeLimit)
		{
			
			int size = ReadVarInt();
			if (size > sizeLimit)
			{
				throw new Exception("Unsafe client behavior: received excessively large string!");
			}

			byte[] result = new byte[size];
			ReadAtLeastOrThrow(underlying, result);
			return result;
		}
		public string ReadString(){
			return ReadStringImpl(MAX_RECEIVE_STRING_SIZE);
		}
		public string ReadString(int sizeLimit){
			if (sizeLimit > MAX_RECEIVE_STRING_SIZE)
			{
				sizeLimit = MAX_RECEIVE_STRING_SIZE;
			}
			return ReadString(sizeLimit);
		}
		public string ReadStringImpl(int sizeLimit)
		{
			int size = ReadVarInt();
			if (size > sizeLimit)
			{
				throw new Exception("Unsafe client behavior: received excessively large string!");
			}
			MinecraftPacketMemoryPool pool = MinecraftPacketMemoryPool.instance;
			(byte[]? a, GCHandle b, IntPtr c) = size > 4096 ? pool.Borrow(size) : (null, default, default);
			Span<byte> buf = a is null ? stackalloc byte[size] : a;
			ReadAtLeastOrThrow(underlying, buf);
			string str = Encoding.UTF8.GetString(buf);
			if (a is { })
			{
				pool.Return(a, b, c, size);
			}

			return str;
		}
	}
	
}