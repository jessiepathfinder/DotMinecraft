using DotMinecraft.Schema;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Sockets;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace DotMinecraft
{
	public enum MinecraftProtocolState : byte{
		handshake, login, encrypt, play
	}
	public sealed class Position{
		public Position(double x, double y, double z){
			this.x = x;
			this.y = y;
			this.z = z;
		}
		public double x;
		public double y;
		public double z;
	}
	public sealed class BlockUpdateConsequencesQueue{
		public BlockUpdateConsequencesQueue(ReadOnlyMemory<byte> packet) {
			this.packet = packet;
		}
		public readonly ReadOnlyMemory<byte> packet;
		public BlockUpdateConsequencesQueue? next;
	}
	public sealed class MinecraftClientContext{
		
		
		
		public readonly MinecraftStreamWriter writer;
		public RSA? keypair;
		public MinecraftProtocolState state;
		public byte[]? notifyEncryptionEnable;
		public byte[]? minecraftVerifyToken;
		public string? username;
		public readonly UUID uuid;
		private MinecraftClientMailboxThread mailboxThread;
		public volatile Position position;
		public volatile int deactivating;

		public const int renderDistance = 12;

		private readonly Dictionary<Coordinate2d, bool> loadedChunks = new Dictionary<Coordinate2d, bool>();
		private readonly ConcurrentDictionary<Coordinate2d, BlockUpdateConsequencesQueue[]> completedLoadingRequests = new ConcurrentDictionary<Coordinate2d, BlockUpdateConsequencesQueue[]>();

		public MinecraftClientMailboxThread MailboxThread{
			get {
				mailboxThread.RequireValid();
				return mailboxThread;
			}
		}
		public void CreateMailbox(){
			MinecraftStreamWriter writer = this.writer;
			writer.RequireLock();
			mailboxThread.RequireInvalid();
			mailboxThread = new MinecraftClientMailboxThread(writer);
		}

		private readonly Dictionary<int, Action<MinecraftClientContext, MinecraftProtocolDecoder>> overrides = new();
		private readonly Action<MinecraftClientContext, MinecraftProtocolDecoder>[] overridesLow = new Action<MinecraftClientContext, MinecraftProtocolDecoder>[256];
		public Action<MinecraftClientContext, MinecraftProtocolDecoder>? TryGetOverrideImpl(int index, bool isLowIndex){
			if (isLowIndex)
			{
				return overridesLow[index + 128];
			}
			else if (overrides.TryGetValue(index, out var ovrd))
			{
				if (ovrd is null) throw new Exception("Unexpected null override (should not reach here)");
				return ovrd;
			}
			return null;
		}
		public void SetOverride(Action<MinecraftClientContext, MinecraftProtocolDecoder> action, int packetType){
			if(packetType > -129 & packetType < 128){
				overridesLow[packetType + 128] = action;
			} else{
				overrides[packetType] = action;
			}
		}
		public MinecraftClientContext(MinecraftStreamWriter writer)
		{
			this.writer = writer;
			RandomNumberGenerator.Fill(MemoryMarshal.AsBytes(new Span<UUID>(ref uuid)));
		}
		private void SendChunkToClientAsync(MinecraftWorldManager worldManager, Coordinate2d coordinate2D)
		{
			if (loadedChunks.TryAdd(coordinate2D, false))
			{
				SendChunkToClientAsync1(worldManager, coordinate2D);
			}
		}
		private async void SendChunkToClientAsync1(MinecraftWorldManager worldManager, Coordinate2d coordinate2D)
		{
			(ReadOnlyMemory<byte> rom, BlockUpdateConsequencesQueue blockUpdateConsequencesQueue) = await worldManager.LoadAndSerializeChunkPacketAsync(coordinate2D);
			mailboxThread.SendDataAsync(rom);
			if (!completedLoadingRequests.TryAdd(coordinate2D, new BlockUpdateConsequencesQueue[] { blockUpdateConsequencesQueue }))
			{
				throw new Exception("Unable to register chunk as loaded (should not reach here)");
			}
		}
		internal void ProcessTick(MinecraftWorldManager minecraftWorldManager){
			Position position = this.position;
			Coordinate2d chunkAlignedPosition = new Coordinate2d((int)Math.Floor(position.x), (int)Math.Floor(position.z)).ShiftRoundDown(4);
			int hrd = renderDistance / 2;
			int bx = chunkAlignedPosition.x - hrd;
			int bz = chunkAlignedPosition.z - hrd;
			MailboxThread.SerializeCompressAndSendDataAsync(new MinecraftUpdateViewPosition(chunkAlignedPosition.x, chunkAlignedPosition.z), false);

			Dictionary<Coordinate2d,bool> residentSet = new(renderDistance * renderDistance);
			for(int x = 0; x < renderDistance; ++x){
				for(int z = 0; z < renderDistance; ++z){
					Coordinate2d c2d = new Coordinate2d(x + bx, z + bz);
					SendChunkToClientAsync(minecraftWorldManager, c2d);
					residentSet.Add(c2d,false);
				}
			}
			foreach(Coordinate2d coordinate2D in residentSet.Keys){
				minecraftWorldManager.MarkChunkInUse(coordinate2D);
			}
			Dictionary<Coordinate2d, bool> loaded = loadedChunks;
			var clrs = completedLoadingRequests;
			var kvps = clrs.ToArray();
			MinecraftClientMailboxThread m = mailboxThread;

			for (int i = 0, stop = kvps.Length; i < stop; ++i ){
				KeyValuePair<Coordinate2d, BlockUpdateConsequencesQueue[]> kvp = kvps[i];
				Coordinate2d c2d = kvp.Key;
				if (residentSet.ContainsKey(c2d)){
					ref BlockUpdateConsequencesQueue b = ref kvp.Value[0];
					BlockUpdateConsequencesQueue c = b;
					while (true){
						ReadOnlyMemory<byte> rom = c.packet;
						if(rom.Length > 0){
							m.SendDataAsync(rom);
						}
						BlockUpdateConsequencesQueue? bn = c.next;
						if (bn is null) break;
						c = bn;
					}
					b = c;
				} else{
					m.SerializeCompressAndSendDataAsync(new ChunkUnloadPacket(c2d.x, c2d.z));
					if (!clrs.TryRemove(c2d, out BlockUpdateConsequencesQueue[]? b)) throw new Exception("Unable to unregister chunk (should not reach here)");
					if (!ReferenceEquals(b, kvp.Value)) throw new Exception("Unexpected different array (should not reach here)");
					if (!loaded.Remove(c2d, out bool _)) throw new Exception("Unable to mark chunk as unloaded by client (should not reach here)");
				}
			}

		}

	}
	public sealed class MinecraftStreamWriter : Stream{
		private Stream stream;
		public readonly object syncLock = new();
		public MinecraftStreamWriter(Stream stream)
		{
			this.stream = stream;
		}

		public void SetStream(Stream str){
			RequireLock();
			stream = str;
		}


		public override bool CanRead => false;

		public override bool CanSeek => false;

		public override bool CanWrite => true;

		public override long Length => throw new NotSupportedException();

		public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

		public void RequireLock(){
			if (!Monitor.IsEntered(syncLock)) throw new Exception("Lock must be acquired on current stream in order to write!");
		}

		public override void Flush()
		{
			RequireLock();
			stream.Flush();
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			throw new NotSupportedException();
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			throw new NotSupportedException();
		}

		public override void SetLength(long value)
		{
			throw new NotSupportedException();
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			RequireLock();
			stream.Write(buffer, offset, count);
		}
		public override void Write(ReadOnlySpan<byte> buffer)
		{
			RequireLock();
			stream.Write(buffer);
		}
	}
	public sealed class MinecraftListener
	{
		private static readonly MinecraftWorldManager worldManager = new MinecraftWorldManager(new SimplePerlinWorldGenerator(), "C:\\Users\\jessi\\source\\repos\\DotMinecraft\\DotMinecraft.Server\\bin\\Debug\\net8.0\\savetester\\");

		private static readonly SemaphoreSlim RSAKeygenSemaphore = new(0);
		private static readonly ConcurrentBag<RSA> rsaKeyPool = new ConcurrentBag<RSA>();
		
		private static void PopulateRSAKeypool(bool b){
			rsaKeyPool.Add(RSA.Create(4096));
			RSAKeygenSemaphore.Release();
		}

		private sealed class SpecialTruncateReadStream : Stream
		{
			public SpecialTruncateReadStream(Stream stream, int limit){
				_stream = stream;
				_limit = limit;
			}
			private readonly Stream _stream;
			private int _limit;
			public override bool CanRead => true;

			public override bool CanSeek => false;

			public override bool CanWrite => false;

			public override long Length => throw new NotSupportedException();

			public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

			public override void Flush()
			{
				
			}

			public override int Read(byte[] buffer, int offset, int count)
			{
				return Read(buffer.AsSpan(offset, count));
			}
			public override int Read(Span<byte> buffer)
			{
				int len = buffer.Length;
				int lim = _limit;
				if (len > lim) len = lim;
				if (len == 0) return 0;
				int r = _stream.Read(buffer);
				_limit = lim - r;
				return r;
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
		}
		private readonly TcpListener tcpListener;

		private readonly Action<MinecraftClientContext,MinecraftProtocolDecoder>?[] registeredPacketHandlersLower;
		private readonly Dictionary<int, Action<MinecraftClientContext, MinecraftProtocolDecoder>> registeredPacketHandlers = new Dictionary<int, Action<MinecraftClientContext, MinecraftProtocolDecoder>>();
		private void Handle2(MinecraftClientContext mcc, MinecraftProtocolDecoder minecraftProtocolDecoder){
			int packetType = minecraftProtocolDecoder.ReadVarInt();

			bool isLowIndex = (packetType > -129 & packetType < 128);

			Action<MinecraftClientContext, MinecraftProtocolDecoder>? ovrd = mcc.TryGetOverrideImpl(packetType,isLowIndex);
			if(ovrd is null){
				ovrd = isLowIndex ? (registeredPacketHandlersLower[packetType + 128] ?? throw new Exception("Client sent invalid packet type: " + packetType)) : registeredPacketHandlers[packetType];
			}
			ovrd(mcc,minecraftProtocolDecoder);
		}
		public static void StaticHandler<T>(MinecraftClientContext mcc, MinecraftProtocolDecoder dec) where T : IMinecraftPacket, new()
		{
			MinecraftProtocolDeserializer.ReadNextPacket<T>(dec).Handle(mcc);
		}
		public void RegisterPacketHandler<T>(int packetType) where T : IMinecraftPacket, new() {
			if (packetType > -129 & packetType < 128){
				ref var rph = ref registeredPacketHandlersLower[packetType + 128];
				if (rph is { }) throw new Exception("Packet type already defined");
				rph = StaticHandler<T>;
			} else{
				registeredPacketHandlers.Add(packetType, StaticHandler<T>);
			}
		}
		private void ClientHandlerThread(){
			ThreadPool.QueueUserWorkItem(PopulateRSAKeypool, false, false);
			Socket socket = tcpListener.AcceptSocket();
			SpawnClientHandlerThread();
			RSA? rsakey = null;
			MinecraftStreamWriter? mcsw = null;
			MinecraftClientMailboxThread mcmb = default;
			Stream? inputStream = null;
			Stream? outputStream = null;
			NetworkStream? networkStream = null;
			MinecraftClientContext? mcc = null;
			try
			{
				Console.WriteLine("New client connecting!");

				RSAKeygenSemaphore.Wait();
				if (!rsaKeyPool.TryTake(out rsakey)) rsakey = null;
				if (rsakey is null) throw new Exception("Unexpected null RSA key");

				

				MinecraftPacketMemoryPool pool = MinecraftPacketMemoryPool.instance;

				networkStream = new NetworkStream(socket, FileAccess.ReadWrite, true);

				Stream ns1 = AutoStackParkStream.Create(networkStream);
				///Stream ns1 = networkStream;

				inputStream = InputBufferedStream.Create1(ns1);
				MinecraftProtocolDecoder decoder = new MinecraftProtocolDecoder(new BinaryReader(inputStream, NoEncoding.instance,true));
				outputStream = OutputBufferedStream.Create1(ns1);
				//outputStream = ns1;
				//outputStream = networkStream;
				mcsw = new MinecraftStreamWriter(outputStream);

				object mcsw_lock = mcsw.syncLock;

				mcc = new MinecraftClientContext(mcsw);
				mcc.keypair = rsakey;
				bool isCompressionEnabled = false;

				while (true){
					int packetsize = decoder.ReadVarInt();
					if(packetsize > MinecraftPacketMemoryPool.MaximumPacketSize){
						throw new Exception("Client sent excessively large packet");
					}
					if (isCompressionEnabled){
						(int dataSize, int dataSizeSize) = decoder.ReadVarIntExtended();
						if(dataSize > MinecraftPacketMemoryPool.MaximumPacketSize){
							throw new Exception("Client sent excessively large packet");
						}
						if (dataSize > 0){
							(byte[] a, GCHandle b, IntPtr c) = pool.Borrow(dataSize);
							
							using(ZLibStream z = new ZLibStream(new SpecialTruncateReadStream(inputStream,packetsize - dataSizeSize), CompressionMode.Decompress,false)){
								z.ReadExactly(a.AsSpan(0, dataSize));
							}
							using(MemoryStream ms = new MemoryStream(a, 0, dataSize, false, false)){
								Handle2(mcc,new MinecraftProtocolDecoder(new BinaryReader(ms, NoEncoding.instance, false)));
							}

							pool.Return(a, b, c, dataSize);
							continue;
						}
						
					}
					Handle2(mcc,decoder);
					byte[]? nec = mcc.notifyEncryptionEnable;
					if(nec is { }){
						if(rsakey is null){
							throw new Exception("Attempted to enable encryption on already encrypted connection");
						}

						
						UUID v128 = MemoryMarshal.Cast<byte, UUID>(nec)[0];
						inputStream = new AesCfbInputStream(inputStream,v128,v128);
						decoder = new MinecraftProtocolDecoder(new BinaryReader(inputStream, NoEncoding.instance, false));
						AesCfbOutputStream es = new AesCfbOutputStream(outputStream, v128,v128);
						lock (mcsw_lock)
						{
							mcsw.SetStream(es);
						}
						mcc.keypair = null;
						RSA rsa1 = rsakey;
						rsakey = null;
						rsa1.Dispose();
						mcc.notifyEncryptionEnable = null;
						lock(mcsw_lock)
						{
							mcsw.Write(hardcodedStartCompression);
							mcc.CreateMailbox();

						}
						isCompressionEnabled = true;
						mcmb = mcc.MailboxThread;
						
						

						mcmb.SerializeCompressAndSendDataAsync(new MinecraftConnectionSuccessSimple(mcc.uuid, mcc.username ?? throw new Exception("Minecraft player does not have username")),false);
						DefaultStartPacketBuilder.WriteCompressedDefaultStartPacket(mcmb, 1, false, GameMode.Creative, 1234567890, MinecraftClientContext.renderDistance, false, true, false, true);

						HardcodedDeclarationsPacket.Send(mcmb);
						mcc.position = new Position(0.0,64.0,0.0);

						mcmb.SerializeCompressAndSendDataAsync(new MinecraftPlayerTeleport(0, 64, 0, 0.0f, 0.0f,0, 1));

						lock (mcsw.syncLock)
						{
							mcmb.SetFlushingPolicy(true);
							mcmb.Flush();
						}
						worldManager.RegisterClient(mcc);
						KeepaliveLoop(mcc);


						//mcmb.SerializeCompressAndSendDataAsync(new MinecraftEventPacket(2, 0.0f));



						//SendChunkToClientAsync(worldManager, mcmb, new Coordinate2d(0, 0));


						//SendChunkToClientAsync(worldManager, mcmb, new Coordinate2d(1, 0));


					}
				}
			}

			catch (Exception e){
				if (e is ClientDisconnectException){
					Console.WriteLine("Client disconnecting normally...");
					return;
				}
				Console.Error.WriteLine("Encountered exception in client handler thread: " + e.ToString());

			}
			finally{
				if(networkStream is { }){
					networkStream.Dispose();
					if (inputStream is { })
					{
						inputStream.Dispose();
						if (outputStream is { })
						{
							outputStream.Dispose();
							rsakey?.Dispose();
							if (mcsw is { })
							{
								mcsw.Dispose();
								mcmb.CancelIfNotNull();
								if(mcc is { }){
									mcc.deactivating = 1;
								}
							}
						}
					}
				}
			}

		}
		private static readonly byte[] hardcodedStartCompression = new byte[] { 0x03, 0x03, 0x80, 0x02 };
		private void SpawnClientHandlerThread(){
			Thread newthread = new Thread(ClientHandlerThread, 262144);
			newthread.Name = "Minecraft client handler thread";
			newthread.IsBackground = true;
			newthread.Start();
		}
		private static readonly MethodInfo staticHandler = typeof(MinecraftListener).GetMethod("StaticHandler", BindingFlags.Static | BindingFlags.Public) ?? throw new Exception("Unable to reflectively access static handler (should not reach here)");
		private static readonly byte[] hardcodedKeepalivePacket = new byte[] {10,0x00,0x1f,0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
		private static async void KeepaliveLoop(MinecraftClientContext b){
			MinecraftClientMailboxThread a = b.MailboxThread;
			while (b.deactivating == 0){
				await Task.Delay(RandomNumberGenerator.GetInt32(1000, 2000));
				a.SendDataAsync(hardcodedKeepalivePacket);
			}
		}
		public MinecraftListener(TcpListener tcpListener)
		{
			this.tcpListener = tcpListener ?? throw new ArgumentNullException(nameof(tcpListener));

			var rphl = new Action<MinecraftClientContext, MinecraftProtocolDecoder>?[256];
			ParameterExpression inputExpr1 = Expression.Parameter(typeof(MinecraftClientContext));
			ParameterExpression inputExpr2 = Expression.Parameter(typeof(MinecraftProtocolDecoder));
			ParameterExpression[] inputExpressions = new ParameterExpression[] { inputExpr1, inputExpr2};
			Type[] gts = new Type[1];
			MethodInfo staticHandler = MinecraftListener.staticHandler;
			foreach (Type type in typeof(MinecraftListener).Assembly.GetTypes()){
				MinecraftStandardPacketBinding? minecraftStandardPacketBinding = type.GetCustomAttribute<MinecraftStandardPacketBinding>();
				if (minecraftStandardPacketBinding is null) continue;
				gts[0] = type;
				rphl[minecraftStandardPacketBinding.pid + 128] = Expression.Lambda<Action<MinecraftClientContext, MinecraftProtocolDecoder>>(Expression.Call(staticHandler.MakeGenericMethod(gts), inputExpr1, inputExpr2), inputExpressions).Compile();
			}
			registeredPacketHandlersLower = rphl;

			SpawnClientHandlerThread();
		}
		
	}
	[AttributeUsage(AttributeTargets.Class)]
	internal sealed class MinecraftStandardPacketBinding : Attribute{
		public readonly int pid;

		public MinecraftStandardPacketBinding(sbyte pid)
		{
			this.pid = pid;
		}
	}


	public sealed class ClientDisconnectException : Exception
	{
		public ClientDisconnectException()
		{
			
		}


	}
}
