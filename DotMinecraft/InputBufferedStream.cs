using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DotMinecraft
{

	public static class InputBufferedStream
	{
		
		
		internal static Stream Create1(Stream stream)
		{
			if (MinecraftPacketMemoryPool.isWindows) return new WindowsInputBufferedStream(stream);

			//Set buffer size > LOH threshold so that the buffer gets stored on LOH
			return new BufferedStream(stream, 131072);
		}

		private sealed class WindowsInputBufferedStream : Stream
		{
			private IntPtr bufferBase;
			private IntPtr bufferPointer;
			private IntPtr decommitPointer;
			private IntPtr bufferHead;

			
			private static readonly ConcurrentBag<IntPtr> pool = new();


			

			//NOTE: the entire 16 MiB memory pressure is not placed on the system
			//this is due to windows not allocating RAM immediately after VirtualAlloc
			//and insteads sets up a page fault handler that lazy allocates them on first write
			private const int bufferSize = 16777216;
			private const UIntPtr howmuch = bufferSize;
			private const IntPtr howmuch1 = bufferSize;

			private const int rwProtect = 0x04;
			private const int allocMode = 0x00003000;


			private const int bufferBypassThreshold = 256;

			[DllImport("kernel32.dll", SetLastError = true)]
			[SuppressGCTransition]
			private static extern IntPtr VirtualAlloc(IntPtr lpAddress, UIntPtr dwSize, int flAllocationType, int flProtect);



			private static IntPtr VirtualAllocSafe(){
				if(pool.TryTake(out IntPtr ptr)){
					return ptr;
				}
				ptr = VirtualAlloc(0, howmuch, allocMode, rwProtect);
				if (ptr == 0) throw new Exception("VirtualAlloc failed (should not reach here)");
				if ((ptr & 4095) != 0) throw new Exception("VirtualAlloc memory not page-aligned (should not reach here)");
				return ptr;
			}

			[DllImport("kernel32.dll", SetLastError = true)]
			[SuppressGCTransition]
			private static extern bool DiscardVirtualMemory(IntPtr lpAddress, UIntPtr dwSize);

			private readonly Stream underlying;

			public WindowsInputBufferedStream(Stream underlying)
			{
				this.underlying = underlying;
				IntPtr thePtr = VirtualAllocSafe();
				bufferBase = thePtr;
				bufferPointer = thePtr;
				decommitPointer = thePtr;
				bufferHead = thePtr;
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
			//Uncommit "tail pages" in order to save memory
			private static IntPtr DecommitRoundDown(IntPtr start, IntPtr end){
				if (start == end) return start;
				IntPtr end1 = ((end >>> 12) << 12);
				if(end1 > start){
					DiscardVirtualMemory(start, (UIntPtr)(end1 - start));
					return end1;
				}

				return start;
				
			}
			private static unsafe Span<byte> IntPtrToSpan(IntPtr i, int size){
				return new Span<byte>(i.ToPointer(), size);
			}

			public override int Read(Span<byte> buffer)
			{
				int length = buffer.Length;
				if (length == 0) return 0;
				IntPtr bufferBase = this.bufferBase;
				IntPtr bufferEnd = bufferBase + howmuch1;
				IntPtr bufferPointer = this.bufferPointer;
				Stream underlying = this.underlying;

				IntPtr bufferHead = this.bufferHead;
				int bufferHealth = (int)(bufferHead - bufferPointer);
				IntPtr decommitPointer = this.decommitPointer;
				if(bufferHealth == 0){
					if(length > bufferBypassThreshold){
						return underlying.Read(buffer);
					}
					int rs = (int)(bufferEnd - bufferHead);
					if (length >= rs)
					{
						
						DecommitRoundDown(decommitPointer, bufferBase + howmuch1);
						bufferPointer = bufferBase;
						bufferHead = bufferBase;
						decommitPointer = bufferBase;
						rs = bufferSize;
					}
					bufferHealth = underlying.Read(IntPtrToSpan(bufferHead, rs));
					if (bufferHealth == 0) return 0;
					bufferHead += bufferHealth;
					this.bufferHead = bufferHead;
				}

				int r1 = Math.Min(bufferHealth, length);
				IntPtrToSpan(bufferPointer, r1).CopyTo(buffer);
				bufferPointer += r1;

				IntPtr decommitPointer1 = DecommitRoundDown(decommitPointer, bufferPointer);
				if (bufferPointer == bufferEnd)
				{
					this.bufferPointer = bufferBase;
					this.decommitPointer = bufferBase;
					this.bufferHead = bufferBase;
				}
				else
				{
					this.decommitPointer = decommitPointer1;
					this.bufferPointer = bufferPointer;
				}
				return r1;
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
			private volatile int doubleDisposeProtection;
			protected override void Dispose(bool disposing){
				if(Interlocked.Exchange(ref doubleDisposeProtection,1) == 0){
					DecommitRoundDown(decommitPointer, bufferBase + howmuch1);
					pool.Add(bufferBase);
				}
			}
		}
	}
}
