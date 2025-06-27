using DotMinecraft.Schema;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DotMinecraft
{
	public readonly struct MinecraftClientMailboxThread
	{
		private sealed class ExtendedConcurrentQueue : ConcurrentQueue<ReadOnlyMemory<byte>>{
			public volatile int disposeMarker;
			public bool enableFlushing;
		}
		private readonly ExtendedConcurrentQueue messageQueue;
		private readonly SemaphoreSlim semaphoreSlim;
		private readonly MinecraftStreamWriter minecraftStreamWriter;
		public MinecraftClientMailboxThread(MinecraftStreamWriter minecraftStreamWriter){
			this.minecraftStreamWriter = minecraftStreamWriter;
			messageQueue = new ();
			semaphoreSlim = new(0);
			Thread thread = new Thread(ThreadBody, 131072);
			thread.IsBackground = true;
			thread.Name = "Minecraft client mailbox thread";
			thread.Start();
		}
		public void CancelIfNotNull(){
			ExtendedConcurrentQueue? mq = messageQueue;
			if (mq is null) return;
			if (Interlocked.Exchange(ref mq.disposeMarker, 1) == 1) return;
			semaphoreSlim.Release();
		}
		public void RequireValid(){
			if (messageQueue is null) throw new Exception("Unexpected invalid mailbox");
		}
		public void RequireInvalid()
		{
			if (messageQueue is { }) throw new Exception("Unexpected valid mailbox");
		}
		public void SendDataAsync(ReadOnlyMemory<byte> data){
			if (data.Length == 0) return;
			messageQueue.Enqueue(data);
			try{
				semaphoreSlim.Release();
			} catch(ObjectDisposedException){
				messageQueue.TryDequeue(out _);
			}
		}
		public void SerializeCompressAsyncAndSendDataAsync<T>(T data) where T : IMinecraftSerializable
		{
			ThreadPool.QueueUserWorkItem<T>(SerializeCompressAndSendDataAsync<T>, data, true);
		}
		public void SerializeCompressAndSendDataAsync<T>(T data) where T : IMinecraftSerializable {
			SerializeCompressAndSendDataAsync(data, false);
		}

		public void SetFlushingPolicy(bool doflush){
			if(!Monitor.IsEntered(minecraftStreamWriter.syncLock)){
				throw new Exception("Stream writer lock is required in order to change flushing policy");
			}
			messageQueue.enableFlushing = doflush;
		}
		public void Flush()
		{
			minecraftStreamWriter.Flush();
		}

		public void SerializeCompressAndSendDataAsync<T>(T data,bool allowOpportunilisticSyncWrites) where T : IMinecraftSerializable{
			int wcs = MinecraftProtocolConstantSizeFastSerializer<T>.worstCaseSize + 5;

			int wcsplus = wcs + 10;

			byte[]? bytes1 = wcsplus > 4096 ? new byte[(wcsplus * 2) - 1] : null;

			Span<byte> buffer = bytes1 is null ? stackalloc byte[wcsplus] : bytes1;
			Memory<byte> memory = bytes1 is null ? default : bytes1;

			Span<byte> innerBuffer = buffer.Slice(10);
			int pidsize = StaticVariableSizeEncoders.WriteVarInt(innerBuffer, (typeof(T).GetCustomAttribute<MinecraftPacketPrefix>() ?? throw new Exception("Packet does not have id prefix")).id);

			int truelen = MinecraftProtocolConstantSizeFastSerializer<T>.fastSerializeDelegate(innerBuffer.Slice(pidsize), data) + pidsize;
			int la;
			int lb;



			if(truelen > 256){
				(byte[] newbuf,int boff) = (bytes1 is null) ? (new byte[truelen + 9],0) : (bytes1,truelen + 10);
				long newsz;
				try{
					using MemoryStream ms = new MemoryStream(newbuf, boff + 10, truelen - 1, true, false);
					using (ZLibStream zls = new ZLibStream(ms, CompressionLevel.SmallestSize, true))
					{
						zls.Write(innerBuffer.Slice(0,truelen));
					}
					newsz = ms.Position;

				} catch (NotSupportedException)
				{
					//[HACK] non-resizable MemoryStream throws if we write more than capacity
					//so we create a memory stream with capacity = length - 1 and try to write
					//our compressed data there
					goto nocompress;
				}
				la = (int)newsz;
				memory = newbuf.AsMemory(boff, la + 10);
				buffer = memory.Span;
				
				lb = truelen;
				goto doflush;
			}

		nocompress:
			la = truelen;
			lb = 0;

		doflush:
			Span<byte> nenc = stackalloc byte[5];
			int lc = StaticVariableSizeEncoders.WriteVarInt(nenc,lb);

			int bo = 10 - lc;

			nenc.Slice(0, lc).CopyTo(buffer.Slice(bo));
			la += lc;
			lc = StaticVariableSizeEncoders.WriteVarInt(nenc, la);
			bo -= lc;
			buffer = buffer.Slice(bo, (la + lc));
			

			nenc.Slice(0, lc).CopyTo(buffer);
			if (allowOpportunilisticSyncWrites){
				object mon = minecraftStreamWriter.syncLock;
				if(Monitor.IsEntered(mon)){
					minecraftStreamWriter.Write(buffer);
					if (messageQueue.enableFlushing) minecraftStreamWriter.Flush();
					return;
				}
				if(Monitor.TryEnter(mon)){
					try{
						minecraftStreamWriter.Write(buffer);
						if (messageQueue.enableFlushing) minecraftStreamWriter.Flush();
					} finally{
						Monitor.Exit(mon);
					}
					return;
				}
			}
			memory = (memory.Length > 0) ? memory.Slice(bo, la + lc) : buffer.ToArray();
			SendDataAsync(memory);
		}


		private void ThreadBody(){
			try{
				while (true)
				{
					semaphoreSlim.Wait();
					if (messageQueue.disposeMarker == 1) return;
					if (!messageQueue.TryDequeue(out ReadOnlyMemory<byte> bytes))
					{
						throw new Exception("Unexpected end of message queue (should not reach here)");
					}
					lock (minecraftStreamWriter.syncLock){
						minecraftStreamWriter.Write(bytes.Span);
						while (semaphoreSlim.Wait(0)){
							if (messageQueue.disposeMarker == 1) return;
							if (!messageQueue.TryDequeue(out ReadOnlyMemory<byte> bytes1))
							{
								throw new Exception("Unexpected end of message queue (should not reach here)");
							}
							minecraftStreamWriter.Write(bytes1.Span);
						}
						if(messageQueue.enableFlushing) minecraftStreamWriter.Flush();
					}
				}
			} catch (Exception e){
				Console.Error.WriteLine("Unexpected exception in client mailbox thread: " + e);
			} finally{
				semaphoreSlim.Dispose();
			}
		}
	}
}
