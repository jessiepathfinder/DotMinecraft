using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DotMinecraft
{
	//Why a lot of programmers use green threads and asynchronous io: because they are lightweight - they don't carry the 1MB burden known as the stack
	//AutoStackParkStream makes heavyweight threads lightweight by trimming them down while waiting for synchronous IO
	//This combines the best of both worlds sync and async. With sync the compiler can perform more optimizations and we can use stackalloc spans. With async we can
	//efficiently handle an extremely large number of clients.
	public static class AutoStackParkStream
	{
		public static Stream Create(Stream stream){
			if(stream is null) throw new ArgumentNullException(nameof(stream));
			if(MinecraftPacketMemoryPool.isWindows){
				return new WindowsStackParkStream(stream);
			}
			return stream;
		}
		private sealed class WindowsStackParkStream : Stream{



			[DllImport("kernel32.dll", SetLastError = true)]
			[SuppressGCTransition]
			private static extern IntPtr DiscardVirtualMemory(IntPtr lpAddress, UIntPtr dwSize);





			private static void ParkCurrentThread(){
				IntPtr stackLimit = threadStackLimit;
				if(stackLimit == 0){
					stackLimit = GetStackLimit();
					IntPtr b = (stackLimit >>> 12) << 12;
					if(b != stackLimit){
						stackLimit = b + 4096;
					}
					threadStackLimit = stackLimit;
				}

				IntPtr endDealloc = ((GuessStackHead() >>> 12) - 2) << 12;
				IntPtr howMuchDealloc = endDealloc - stackLimit;
				if(howMuchDealloc > 0){
					DiscardVirtualMemory(stackLimit, (UIntPtr)howMuchDealloc);
				}
			}

			private static unsafe IntPtr GuessStackHead(){
				int i = 0;
				return (IntPtr) (&i);
			}

			[ThreadStatic] private static IntPtr threadStackLimit;

			[DllImport("ntdll.dll")]
			private static extern int NtQueryInformationThread(
			IntPtr ThreadHandle,
			int ThreadInformationClass,
			out THREAD_BASIC_INFORMATION ThreadInformation,
			int ThreadInformationLength,
			IntPtr ReturnLength);

			[StructLayout(LayoutKind.Sequential)]
			private struct THREAD_BASIC_INFORMATION
			{
				public IntPtr ExitStatus;
				public IntPtr TebBaseAddress;
				public CLIENT_ID ClientId;
				public IntPtr AffinityMask;
				public int Priority;
				public int BasePriority;
			}

			[StructLayout(LayoutKind.Sequential)]
			private struct CLIENT_ID
			{
				public IntPtr UniqueProcess;
				public IntPtr UniqueThread;
			}

			[StructLayout(LayoutKind.Sequential)]
			private struct NT_TIB
			{
				public IntPtr ExceptionList;
				public IntPtr StackBase;
				public IntPtr StackLimit;
				// ... other fields omitted
			}

			private static IntPtr GetStackLimit()
			{
				THREAD_BASIC_INFORMATION tbi;


				int status = NtQueryInformationThread(GetCurrentThread(), 0, out tbi, Marshal.SizeOf<THREAD_BASIC_INFORMATION>(), IntPtr.Zero);
				if (status != 0)
					throw new Exception("NtQueryInformationThread failed");

				NT_TIB tib = Marshal.PtrToStructure<NT_TIB>(tbi.TebBaseAddress);
				return tib.StackLimit;
			}

			[DllImport("kernel32.dll")]
			private static extern IntPtr GetCurrentThread();


			private readonly Stream underlying;

			public WindowsStackParkStream(Stream underlying)
			{
				this.underlying = underlying;
			}

			public override bool CanRead => underlying.CanRead;

			public override bool CanSeek => underlying.CanSeek;

			public override bool CanWrite => underlying.CanWrite;

			public override long Length => underlying.Length;

			public override long Position { get => underlying.Position; set => underlying.Position = value; }

			public override void Flush()
			{
				ParkCurrentThread();
				underlying.Flush();
			}

			public override int Read(byte[] buffer, int offset, int count)
			{
				ParkCurrentThread();
				return underlying.Read(buffer, offset, count);
			}
			public override int Read(Span<byte> buffer)
			{
				ParkCurrentThread();
				return underlying.Read(buffer);
			}

			public override long Seek(long offset, SeekOrigin origin)
			{
				return underlying.Seek(offset, origin);
			}

			public override void SetLength(long value)
			{
				underlying.SetLength(value);
			}

			public override void Write(byte[] buffer, int offset, int count)
			{
				ParkCurrentThread();
				underlying.Write(buffer, offset, count);

			}
			public override void Write(ReadOnlySpan<byte> buffer)
			{
				ParkCurrentThread();
				underlying.Write(buffer);
			}
		}
	}
}
