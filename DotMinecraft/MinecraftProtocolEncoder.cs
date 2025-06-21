using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DotMinecraft
{
	public sealed class MinecraftProtocolEncoder{
		private readonly Stream stream;

		public MinecraftProtocolEncoder(Stream stream)
		{
			this.stream = stream ?? throw new ArgumentNullException(nameof(stream));
		}

		public void WriteByte(byte b) => stream.WriteByte(b);
		public void WriteBool(bool b) => stream.WriteByte(b ? (byte)1 : (byte)0);
		public void WriteUnmanagedBigEndian<T>(T t) where T : unmanaged{
			Span<byte> span = MemoryMarshal.AsBytes(new Span<T>(ref t));
			span.Reverse();
			stream.Write(span);
		}
		public void WriteDirect(ReadOnlySpan<byte> span) => stream.Write(span);
		private const int SEGMENT_BITS = 0x7F;
		private const int CONTINUE_BIT = 0x80;
		private const long SEGMENT_BITS_LONG = 0x7F;
		private const long CONTINUE_BIT_LONG = 0x80;
		public void WriteVarint(int value){
			int i = 0;
			Span<byte> span = stackalloc byte[5];
			while (true)
			{
				if ((value & ~SEGMENT_BITS) == 0)
				{
					span[i++] = (byte)value;
					break;
				}

				span[i++] = (byte)((value & SEGMENT_BITS) | CONTINUE_BIT);

				// Note: >>> means that the sign bit is shifted with the rest of the number rather than being left alone
				value >>>= 7;
			}
			stream.Write(span.Slice(0, i));
		}
		public void WriteVarlong(long value)
		{
			int i = 0;
			Span<byte> span = stackalloc byte[10];
			while (true)
			{
				if ((value & ~SEGMENT_BITS_LONG) == 0)
				{
					span[i++] = (byte)value;
					break;
				}

				span[i++] = (byte)((value & SEGMENT_BITS_LONG) | CONTINUE_BIT_LONG);

				// Note: >>> means that the sign bit is shifted with the rest of the number rather than being left alone
				value >>>= 7;
			}
			stream.Write(span.Slice(0, i));
		}
	}
}
