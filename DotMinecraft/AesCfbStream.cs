using DotMinecraft.Schema;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace DotMinecraft
{
	public sealed class AesCfbOutputStream : Stream
	{
		private UUID state;
		private readonly Stream underlying;
		private readonly Aes aes;
		public AesCfbOutputStream(Stream underlying, UUID key, UUID iv)
		{
			state = iv;
			Aes aes = Aes.Create();
			aes.KeySize = 128;
			aes.Key = MemoryMarshal.AsBytes(new Span<UUID>(ref key)).ToArray();
			aes.BlockSize = 128;
			this.aes = aes; 
			this.underlying = underlying ?? throw new ArgumentNullException(nameof(underlying));
		}

		public override bool CanRead => false;

		public override bool CanSeek => false;

		public override bool CanWrite => true;

		public override long Length => throw new NotImplementedException();

		public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

		public override void Flush()
		{
			underlying.Flush();
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			throw new NotImplementedException();
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			throw new NotImplementedException();
		}

		public override void SetLength(long value)
		{
			throw new NotImplementedException();
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			Write(buffer.AsSpan(offset, count));
		}

		public override void Write(ReadOnlySpan<byte> buffer)
		{
			int len = buffer.Length;
			if (len == 0) return;

			//CLRJIT Optimization: constant-size alloc is better for performance
			Span<byte> bf = stackalloc byte[1024];
			
			Stream underlying = this.underlying;
			Span<byte> v = MemoryMarshal.AsBytes(new Span<UUID>(ref state));
			Span<byte> v1 = stackalloc byte[16];

			Aes aes = this.aes;

			for (int i = 0; i < len; ++i) {



				if (!aes.TryEncryptEcb(v, v1, PaddingMode.None, out _)) throw new Exception("Encryption failed (should not reach here)");
				byte e = (byte)(buffer[i] ^ (uint)v1[0]);
				for(int j = 0; j < 15; )
				{
					int oj = j;
					++j;
					v[oj] = v[j];
				}
				v[15] = e;
				

				int modbfc = i & 1023;
				bf[modbfc] = e;
				if(modbfc == 1023){
					underlying.Write(bf);
				}


			}
			int modbfc1 = len & 1023;
			if (modbfc1 > 0)
			{
				underlying.Write(bf.Slice(0,modbfc1));
			}
		}
	}
	public sealed class AesCfbInputStream : Stream
	{
		private UUID state;
		private readonly Stream underlying;
		private readonly Aes aes;
		public AesCfbInputStream(Stream underlying, UUID key, UUID iv)
		{
			state = iv;
			Aes aes = Aes.Create();
			aes.KeySize = 128;
			aes.Key = MemoryMarshal.AsBytes(new Span<UUID>(ref key)).ToArray();
			aes.BlockSize = 128;
			this.aes = aes;
			this.underlying = underlying ?? throw new ArgumentNullException(nameof(underlying));
		}

		public override bool CanRead => true;

		public override bool CanSeek => false;

		public override bool CanWrite => false;

		public override long Length => throw new NotImplementedException();

		public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

		public override void Flush()
		{
			
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			return Read(buffer.AsSpan(offset, count));
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			throw new NotImplementedException();
		}

		public override void SetLength(long value)
		{
			throw new NotImplementedException();
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			throw new NotImplementedException();
		}
		public override int Read(Span<byte> buffer){
			int len = buffer.Length;
			if (len == 0) return 0;

			Stream underlying = this.underlying;
			Span<byte> v = MemoryMarshal.AsBytes(new Span<UUID>(ref state));
			Span<byte> v1 = stackalloc byte[16];

			//CLRJIT Optimization: constant-size alloc is better for performance
			Span<byte> cache = stackalloc byte[4096];

			Aes aes = this.aes;

			if(len > 4096) len = 4096;
			int rb = underlying.Read(cache.Slice(0, len));
			if (rb > len) throw new Exception("Underlying stream returned too much data (should not reach here)");
			len = rb;
			for (int i = 0; i < len; ++i)
			{
				if (!aes.TryEncryptEcb(v, v1, PaddingMode.None, out _)) throw new Exception("Encryption failed (should not reach here)");
				byte e = cache[i];
				buffer[i] = (byte)(e ^ (uint)v1[0]);
				for (int j = 0; j < 15;)
				{
					int oj = j;
					++j;
					v[oj] = v[j];
				}
				v[15] = e;
			}
			return len;
		}
		protected override void Dispose(bool disposing)
		{
			underlying.Dispose();
		}
	}
}
