using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DotMinecraft
{
	public static class OutputBufferedStream
	{
		internal static Stream Create1(Stream stream)
		{
			if (MinecraftPacketMemoryPool.isWindows) return new WindowsOutputBufferedStream(stream);

			//Set buffer size > LOH threshold so that the buffer gets stored on LOH
			return new BufferedStream(stream, 131072);
		}
		private sealed class WindowsOutputBufferedStream : Stream{
			private const int bufferSize = 65536;
			private const UIntPtr howmuch = bufferSize;
			private const int rwProtect = 0x04;
			private const int allocMode = 0x00003000;


			private const int bufferBypassThreshold = 256;

			[DllImport("kernel32.dll", SetLastError = true)]
			[SuppressGCTransition]
			private static extern IntPtr VirtualAlloc(IntPtr lpAddress, UIntPtr dwSize, int flAllocationType, int flProtect);

			private static readonly ConcurrentBag<IntPtr> pool = new();



			private static IntPtr VirtualAllocSafe()
			{
				if (pool.TryTake(out IntPtr ptr))
				{
					return ptr;
				}
				ptr = VirtualAlloc(0, howmuch, allocMode, rwProtect);
				if (ptr == 0) throw new Exception("VirtualAlloc failed (should not reach here)");
				return ptr;
			}



			[DllImport("kernel32.dll", SetLastError = true)]
			[SuppressGCTransition]
			private static extern bool DiscardVirtualMemory(IntPtr lpAddress, UIntPtr dwSize);

			public override void Flush()
			{
				IntPtr b = bufferBase;
				IntPtr h = bufferHead;
				UIntPtr bs = (UIntPtr)(h - b);
				if (bs == 0) return;
				underlying.Write(IntPtrToSpan(b, (int)bs));
				underlying.Flush();
				if (!DiscardVirtualMemory(b, bs)) throw new Exception("DiscardVirtualMemory failed (should not reach here)");
				bufferHead = b;
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
			private static unsafe Span<byte> IntPtrToSpan(IntPtr i, int size)
			{
				return new Span<byte>(i.ToPointer(), size);
			}
			public override void Write(ReadOnlySpan<byte> buffer)
			{
				int len = buffer.Length;
				if (len == 0) return;
				IntPtr bufferBase = this.bufferBase;
				IntPtr bufferHead = this.bufferHead;
				bool bypass = len > bufferBypassThreshold;
				Stream underlying = this.underlying;
				if (bufferHead == bufferBase & bypass){
					underlying.Write(buffer);
					return;
				}
				UIntPtr bufferRemains = (howmuch - (UIntPtr)(bufferHead - bufferBase));
				int intBufferRemains = (int)bufferRemains;
				if(len > intBufferRemains){
					if(intBufferRemains > 0) buffer.Slice(0, intBufferRemains).CopyTo(IntPtrToSpan(bufferHead, intBufferRemains));
					underlying.Write(IntPtrToSpan(bufferBase, bufferSize));

					//HACK: DiscardVirtualMemory causes the OS to uncommit ALL the pages
					//and sets up a page fault handler that lazily re-allocates them
					//on first write
					if(!DiscardVirtualMemory(bufferBase, howmuch - 1)) throw new Exception("DiscardVirtualMemory failed (should not reach here)");
					ReadOnlySpan<byte> sliced = buffer.Slice(intBufferRemains);
					int residual = len - intBufferRemains;
					
					if (residual > bufferBypassThreshold){
						this.bufferHead = bufferBase;
						underlying.Write(sliced);
					} else{
						this.bufferHead = bufferBase + residual;
						sliced.CopyTo(IntPtrToSpan(bufferBase, residual));
					}
				} else{
					buffer.CopyTo(IntPtrToSpan(bufferHead, len));
					this.bufferHead = bufferHead + len;
				}

			}

			private readonly Stream underlying;
			private IntPtr bufferBase;
			private IntPtr bufferHead;

			public override bool CanRead => false;

			public override bool CanSeek => false;

			public override bool CanWrite => true;

			public override long Length => throw new NotImplementedException();

			public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
			private volatile int doubleDisposeProtection;
			protected override void Dispose(bool disposing)
			{
				if (Interlocked.Exchange(ref doubleDisposeProtection, 1) == 0)
				{
					IntPtr b = this.bufferBase;
					IntPtr h = bufferHead;
					UIntPtr bs = (UIntPtr)(h - b);
					if (bs > 0)
					{
						Stream underlying = this.underlying;
						underlying.Write(IntPtrToSpan(b, (int)bs));
						underlying.Flush();
						if (!DiscardVirtualMemory(bufferBase, (UIntPtr)(h - b))) throw new Exception("DiscardVirtualMemory failed (should not reach here)");
					}
					pool.Add(b);
				}
			}
			public WindowsOutputBufferedStream(Stream underlying)
			{
				this.underlying = underlying;
				IntPtr thing = VirtualAllocSafe();
				bufferBase = thing;
				bufferHead = thing;

			}



		}
	}
}
