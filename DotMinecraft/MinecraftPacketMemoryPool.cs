using System;
using System.Buffers;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DotMinecraft
{
	public abstract class MinecraftPacketMemoryPool
	{
		public const int MaximumPacketSize = 8388608;
		public static readonly bool isWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;
		internal static readonly MinecraftPacketMemoryPool instance = Create();

		public static MinecraftPacketMemoryPool Create(){
			if (isWindows) return new WindowsImplementation();
			return new NonWindowsImplementation();
		}
		internal static void StreamCopy(Stream a, Stream b){
			MinecraftPacketMemoryPool i = instance;
			(byte[] buf, GCHandle c, IntPtr d) = i.Borrow(MaximumPacketSize);
			try{
				while(true){
					int r = a.Read(buf, 0, MaximumPacketSize);
					if (r == 0) break;
					b.Write(buf, 0, r);
				}
			} finally{
				i.Return(buf, c, d, MaximumPacketSize);
			}
		}

		private sealed class NonWindowsImplementation : MinecraftPacketMemoryPool
		{
			private readonly ArrayPool<byte> ap = ArrayPool<byte>.Create(MaximumPacketSize, 16);
			public override (byte[], GCHandle, nint) Borrow(int howmuch)
			{
				if (howmuch > MaximumPacketSize) throw new Exception("Maximum packet size violation");
				return (ap.Rent(howmuch), default, default);
			}

			public override void Return(byte[] b, GCHandle h, nint i, int howmuch)
			{
				ap.Return(b);
			}
		}
		private sealed class WindowsImplementation : MinecraftPacketMemoryPool
		{
			[DllImport("kernel32.dll", SetLastError = true)]
			[SuppressGCTransition]
			private static extern bool DiscardVirtualMemory(IntPtr lpAddress, UIntPtr dwSize);


			private static readonly ConcurrentStack<(byte[], GCHandle, IntPtr)> concurrentStack = new ConcurrentStack<(byte[], GCHandle, IntPtr s)>();

			private static IntPtr DivRoundUp(IntPtr ptr){
				IntPtr ptr1 = (ptr / 4096) * 4096;
				if (ptr1 != ptr) ptr1 += 4096;
				return ptr1;
			}
			
			public override (byte[],GCHandle,IntPtr) Borrow(int howmuch)
			{
				if (howmuch > MaximumPacketSize) throw new Exception("Maximum packet size violation");
				if(concurrentStack.TryPop(out (byte[] array, GCHandle gch, IntPtr s) x)){
					return (x.array, x.gch, x.s);
				} else{
					byte[] bytes = new byte[MaximumPacketSize];
					GCHandle gch = GCHandle.Alloc(bytes, GCHandleType.Pinned);
					IntPtr s = gch.AddrOfPinnedObject();
					IntPtr startFree = DivRoundUp(s + howmuch);
					IntPtr endFree = ((s + MaximumPacketSize) / 4096) * 4096;
					if (endFree > startFree)
					{
						if (!DiscardVirtualMemory(startFree, (UIntPtr)(endFree - startFree)))
						{
							throw new Exception("Memory trimming failed (should not reach here)");
						}
					}


					return (bytes, gch, s);
				}
			}

			public override void Return(byte[] b, GCHandle h, IntPtr s, int howmuch)
			{
				IntPtr startFree = DivRoundUp(s);
				IntPtr endFree = DivRoundUp(s + howmuch);
				if (endFree > startFree)
				{
					if (!DiscardVirtualMemory(startFree, (UIntPtr)(endFree - startFree)))
					{
						throw new Exception("Memory trimming failed (should not reach here)");
					}
				}
				concurrentStack.Push((b, h, s));
			}
		}
		public abstract (byte[], GCHandle, IntPtr) Borrow(int howmuch);
		public abstract void Return(byte[] b, GCHandle h, IntPtr i, int howmuch);
	}
}
