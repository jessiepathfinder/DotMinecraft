using Newtonsoft.Json.Linq;
using Snappier;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO.IsolatedStorage;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace DotMinecraft
{
	public readonly struct Coordinate2d{
		public readonly int x;
		public readonly int z;

		public Coordinate2d(int x, int z)
		{
			this.x = x;
			this.z = z;
		}
		public static Coordinate2d operator +(Coordinate2d a, Coordinate2d b)
		{
			return new Coordinate2d(a.x + b.x, a.z + b.z);
		}


		public bool Equals(Coordinate2d other)
		{
			return this == other;
		}

		public static bool operator ==(Coordinate2d a, Coordinate2d b)
		{
			return (a.x == b.x) & (a.z == b.z);
		}
		public static bool operator !=(Coordinate2d a, Coordinate2d b)
		{
			return (a.x != b.x) | (a.z != b.z);
		}

		private static readonly Vector128<byte> v;
		static Coordinate2d()
		{
			System.Security.Cryptography.RandomNumberGenerator.Fill(MemoryMarshal.AsBytes(new Span<Vector128<byte>>(ref v)));
		}
		public override int GetHashCode()
		{
			Span<int> s = stackalloc int[4];
			s[0] = x;
			s[1] = z;
			s[2] = x;
			s[3] = z;
			Vector128<byte> b = Aes.Encrypt(MemoryMarshal.Cast<int, Vector128<byte>>(s)[0], v);
			return MemoryMarshal.Cast<Vector128<byte>, int>(new Span<Vector128<byte>>(ref b))[0];
		}
	}
	public readonly struct PreciseCoordinate : IEquatable<PreciseCoordinate>
	{
		public readonly int x;
		public readonly int y;
		public readonly int z;


		public PreciseCoordinate(int x, int y, int z)
		{
			this.x = x;
			this.y = y;
			this.z = z;
		}
		public (Coordinate2d, PreciseCoordinate) ToChunkRelativeCoordinate(){
			return (new Coordinate2d(x >> 4, z >> 4), new PreciseCoordinate(x & 15, y, z & 15));
		}
		public override int GetHashCode()
		{
			Span<int> s = stackalloc int[4];
			s[0] = x;
			s[1] = y;
			s[2] = z;
			s[3] = 0;
			Vector128<byte> b = Aes.Encrypt(MemoryMarshal.Cast<int, Vector128<byte>>(s)[0], v);
			return MemoryMarshal.Cast<Vector128<byte>, int>(new Span<Vector128<byte>>(ref b))[0];
		}
		public static PreciseCoordinate operator +(PreciseCoordinate a, PreciseCoordinate b){
			return new PreciseCoordinate(a.x + b.x, a.y + b.y, a.z + b.z);
		}

		
		public bool Equals(PreciseCoordinate other)
		{
			return this == other;
		}

		public static bool operator ==(PreciseCoordinate a, PreciseCoordinate b){
			return (a.x == b.x) & (a.y == b.y) & (a.z == b.z);
		}
		public static bool operator !=(PreciseCoordinate a, PreciseCoordinate b)
		{
			return (a.x != b.x) | (a.y != b.y) | (a.z != b.z);
		}

		private static readonly Vector128<byte> v;
		static PreciseCoordinate(){
            System.Security.Cryptography.RandomNumberGenerator.Fill(MemoryMarshal.AsBytes(new Span<Vector128<byte>>(ref v)));
		}
	}

	public readonly struct ImpreciseCoordinate
	{
		public readonly float x;
		public readonly float y;
		public readonly float z;

		public ImpreciseCoordinate(float x, float y, float z)
		{
			this.x = x;
			this.y = y;
			this.z = z;
		}
	}

	public static class MatrixCompressor{
		public static int CompressInPlace(Span<byte> span){
			bool odd = false;
			int prevSize = span.Length;
			Span<byte> a = stackalloc byte[prevSize - 1];

			
			while (Snappy.TryDecompress(span.Slice(0, prevSize), a.Slice(0, prevSize - 1), out int bytesWritten1))
			{
				a.Slice(0, bytesWritten1).CopyTo(span);
				prevSize = bytesWritten1;
			}
			return prevSize;
		}
		public static ushort[] UncompressBlockMatrix(ReadOnlySpan<byte> ros){
			int rl = ros.Length;
			if (rl > 131072) throw new InvalidOperationException("Block matrix bigger than 131072 bytes");
			ushort[] a = new ushort[65536];
			Span<byte> a_ = MemoryMarshal.AsBytes(a.AsSpan());
			if(rl == 131072){
				ros.CopyTo(a_);
				return a;
			}

		startthing:
			int l = Snappy.Decompress(ros, a_);
			if(l < 131072){
				ushort[] b = new ushort[65536];
				Span<byte> b_ = MemoryMarshal.AsBytes(b.AsSpan());
			start:
				l = Snappy.Decompress(ros, a_);
				if (l < 131072)
				{
					Span<byte> tmp = b_;
					b_ = a_;
					a_ = tmp;
					(a, b) = (b, a);
					goto start;
				}
			}
			
			return a;
		}
		public static byte[] UncompressLightMatrix(ReadOnlySpan<byte> ros)
		{
			int rl = ros.Length;
			if (rl > 65536) throw new InvalidOperationException("Light matrix bigger than 65536 bytes");
			byte[] a = new byte[65536];
			Span<byte> a_ = a;
			if (rl == 131072)
			{
				ros.CopyTo(a_);
				return a;
			}

		startthing:
			int l = Snappy.Decompress(ros, a_);
			if (l < 131072)
			{
				byte[] b = new byte[65536];
				Span<byte> b_ = b;
			start:
				l = Snappy.Decompress(ros, a_);
				if (l < 131072)
				{
					Span<byte> tmp = b_;
					b_ = a_;
					a_ = tmp;
					(a, b) = (b, a);
					goto start;
				}
			}

			return a;
		}
	}
	public readonly struct FlatMinecraftBlock
	{
		public readonly ushort id;
		public readonly IReadOnlyDictionary<string, object>? extraData;

		public FlatMinecraftBlock(ushort state, IReadOnlyDictionary<string, object>? extraData)
		{
			this.id = state;
			this.extraData = extraData;
		}
	}
	public interface IMinecraftWorldView{
		public FlatMinecraftBlock ReadBlock(PreciseCoordinate preciseCoordinate, byte minimumGenerationLevel);
		public void WriteBlock(PreciseCoordinate preciseCoordinate, FlatMinecraftBlock block);
		public ref byte GetLightData(PreciseCoordinate preciseCoordinate, byte minumumGenerationLevel);
	}



	public sealed class ChunkData : IMinecraftWorldView{
		public byte generationStage;
		private readonly ushort[] blockMatrix;
		private readonly byte[] lightMatrix;

		private readonly Dictionary<PreciseCoordinate, IReadOnlyDictionary<string,object>> extendedBlockData = new();

		public ChunkData(byte generationStage, ushort[] blockMatrix, byte[] lightMatrix, Dictionary<PreciseCoordinate, IReadOnlyDictionary<string, object>> extendedBlockData)
		{
			if (blockMatrix.Length != 65536) throw new Exception("Block matrix must be exactly 65536 long!");
			if (lightMatrix.Length != 65536) throw new Exception("Light matrix must be exactly 65536 long!");
			this.generationStage = generationStage;
			this.blockMatrix = blockMatrix;
			this.lightMatrix = lightMatrix;
			this.extendedBlockData = extendedBlockData ?? throw new ArgumentNullException(nameof(extendedBlockData));
		}

		public ref byte GetLightData(PreciseCoordinate preciseCoordinate, byte minimumGenerationLevel)
		{
			if (generationStage < minimumGenerationLevel) throw new Exception("Attempted to modify light data excessively immature chunk");
			return ref lightMatrix[(preciseCoordinate.y * 256) + (preciseCoordinate.z * 16) + preciseCoordinate.x];
		}
		private static void EnsureValidCoordinate(PreciseCoordinate preciseCoordinate){
			if (preciseCoordinate.x < 0 | preciseCoordinate.x > 15 | preciseCoordinate.y < 0 | preciseCoordinate.y > 255 | preciseCoordinate.z < 0 | preciseCoordinate.z > 15)
				throw new Exception("Access blocks outsise of chunk");
		}
		public FlatMinecraftBlock ReadBlock(PreciseCoordinate preciseCoordinate, byte minimumGenerationLevel)
		{
			if (generationStage < minimumGenerationLevel) throw new Exception("Attempted to read excessively immature chunk");
			EnsureValidCoordinate(preciseCoordinate);
			if(!extendedBlockData.TryGetValue(preciseCoordinate, out IReadOnlyDictionary<string,object>? rod)){
				rod = null;
			}
			return new FlatMinecraftBlock(blockMatrix[(preciseCoordinate.y * 256) + (preciseCoordinate.z * 16) + preciseCoordinate.x], rod);
		}

		public void WriteBlock(PreciseCoordinate preciseCoordinate, FlatMinecraftBlock block)
		{
			EnsureValidCoordinate(preciseCoordinate);
			blockMatrix[(preciseCoordinate.y * 256) + (preciseCoordinate.z * 16) + preciseCoordinate.x] = block.id;
			if(block.extraData is null){
				extendedBlockData.Remove(preciseCoordinate);
			} else{
				extendedBlockData[preciseCoordinate] = block.extraData;
			}
		}
	}
	public interface IMinecraftWorldGenerator{
		public void AdvanceGenerationStage(IMinecraftWorldView minecraftWorldView, ref byte generationStage);
		public int GetSurroundingMaturityRequirementSize(byte generationStage);
	}
	public sealed class SimpleSuperflatGenerator : IMinecraftWorldGenerator
	{
		private SimpleSuperflatGenerator(){
			
		}
		public static readonly SimpleSuperflatGenerator instance = new SimpleSuperflatGenerator();
		public void AdvanceGenerationStage(IMinecraftWorldView minecraftWorldView, ref byte generationStage)
		{
			FlatMinecraftBlock bedrock = new FlatMinecraftBlock(33, null);
			FlatMinecraftBlock stone = new FlatMinecraftBlock(1, null);
			FlatMinecraftBlock dirt = new FlatMinecraftBlock(10, null);
			FlatMinecraftBlock grass = new FlatMinecraftBlock(9, null);

			for(int x = 0; x < 16; ++x){
				for(int z = 0; z < 16; ++z){
					minecraftWorldView.WriteBlock(new PreciseCoordinate(x,0,z), bedrock);
					for(int y = 1; y < 58; ++y){
						minecraftWorldView.WriteBlock(new PreciseCoordinate(x, y, z), stone);
					}
					for (int y = 58; y < 62; ++y)
					{
						minecraftWorldView.WriteBlock(new PreciseCoordinate(x, y, z), dirt);
					}
					minecraftWorldView.WriteBlock(new PreciseCoordinate(x, 63, z), grass);

					for(int y = 0; y < 64; ++y){
						minecraftWorldView.GetLightData(new PreciseCoordinate(x, y, z), 0) = 0;
					}
					for (int y = 64; y < 255; ++y)
					{
						minecraftWorldView.GetLightData(new PreciseCoordinate(x, y, z), 0) = 240;
					}

				}
			}

			generationStage = 255;
		}

		public int GetSurroundingMaturityRequirementSize(byte generationStage)
		{
			return 0;
		}
	}
	public sealed class ShiftedMinecraftWorldView : IMinecraftWorldView{
		private readonly PreciseCoordinate shift;
		private readonly IMinecraftWorldView minecraftWorldView;

		private ShiftedMinecraftWorldView(PreciseCoordinate shift, IMinecraftWorldView minecraftWorldView)
		{
			this.shift = shift;
			this.minecraftWorldView = minecraftWorldView;
		}
		public static IMinecraftWorldView Create(IMinecraftWorldView minecraftWorldView, PreciseCoordinate shift){
			if (minecraftWorldView is null) throw new ArgumentNullException(nameof(minecraftWorldView));
			if (shift.x == 0 & shift.y == 0 & shift.z == 0) return minecraftWorldView;
			if(minecraftWorldView is ShiftedMinecraftWorldView shiftedMinecraftWorldView){
				shift = shiftedMinecraftWorldView.shift + shift;
				if (shift.x == 0 & shift.y == 0 & shift.z == 0) return shiftedMinecraftWorldView.minecraftWorldView;
				return new ShiftedMinecraftWorldView(shift, shiftedMinecraftWorldView.minecraftWorldView);
			}
			return new ShiftedMinecraftWorldView(shift, minecraftWorldView);
		}

		public ref byte GetLightData(PreciseCoordinate preciseCoordinate, byte minumumGenerationLevel)
		{
			return ref minecraftWorldView.GetLightData(preciseCoordinate + shift, minumumGenerationLevel);
		}

		public FlatMinecraftBlock ReadBlock(PreciseCoordinate preciseCoordinate, byte minimumGenerationLevel)
		{
			return minecraftWorldView.ReadBlock(preciseCoordinate + shift, minimumGenerationLevel);
		}

		public void WriteBlock(PreciseCoordinate preciseCoordinate, FlatMinecraftBlock block)
		{
			minecraftWorldView.WriteBlock(preciseCoordinate + shift, block);
		}
	}
	public sealed class BoundedMinecraftWorldView : IMinecraftWorldView{
		private readonly IMinecraftWorldView underlying;
		private readonly int minx;
		private readonly int miny;
		private readonly int minz;
		private readonly int maxx;
		private readonly int maxy;
		private readonly int maxz;
		public BoundedMinecraftWorldView(IMinecraftWorldView underlying, PreciseCoordinate a, PreciseCoordinate b){
			this.underlying = underlying ?? throw new ArgumentNullException(nameof(underlying));
			minx = Math.Min(a.x,b.x);
			miny = Math.Min(a.y,b.y);
			minz = Math.Min(a.z,b.z);
			maxz = Math.Max(a.x,b.x);
			maxy = Math.Max(a.y,b.y);
			maxz = Math.Max(a.z,b.z);
		}
		private void EnsureWithinBounds(PreciseCoordinate preciseCoordinate){
			if (preciseCoordinate.x < minx | preciseCoordinate.y < miny | preciseCoordinate.z < minz | preciseCoordinate.x > maxx | preciseCoordinate.y > maxy | preciseCoordinate.z > maxz) throw new Exception("Accessed block out of bounds");
		}
		public ref byte GetLightData(PreciseCoordinate preciseCoordinate, byte minumumGenerationLevel)
		{
			EnsureWithinBounds(preciseCoordinate);
			return ref underlying.GetLightData(preciseCoordinate, minumumGenerationLevel);
		}

		public FlatMinecraftBlock ReadBlock(PreciseCoordinate preciseCoordinate, byte minimumGenerationLevel)
		{
			EnsureWithinBounds(preciseCoordinate);
			return underlying.ReadBlock(preciseCoordinate, minimumGenerationLevel);
		}

		public void WriteBlock(PreciseCoordinate preciseCoordinate, FlatMinecraftBlock block)
		{
			EnsureWithinBounds(preciseCoordinate);
			underlying.WriteBlock(preciseCoordinate, block);
		}
	}
	public sealed class WorldManager : IMinecraftWorldView
	{
		private readonly object globalObjectLock = new object();

		private readonly IMinecraftWorldGenerator minecraftWorldGenerator;
		public WorldManager(IMinecraftWorldGenerator minecraftWorldGenerator){
			this.minecraftWorldGenerator = minecraftWorldGenerator ?? throw new ArgumentNullException(nameof(minecraftWorldGenerator));
		}
		private sealed class RegisteredChunk{
			public Task<ReadOnlyMemory<byte>>? pendingReadRequest;
			public ChunkData? chunkData;
		}

		private readonly ConcurrentDictionary<Coordinate2d, RegisteredChunk> registeredChunks = new();
		private readonly ConcurrentQueue<Action> worldUpdateQueue = new();

		private RegisteredChunk GetRegisteredChunk(Coordinate2d coord){
			ConcurrentDictionary<Coordinate2d, RegisteredChunk> registeredChunks = this.registeredChunks;
			if(registeredChunks.TryGetValue(coord, out RegisteredChunk? chunk)){
				if (chunk is null) throw new Exception("Unexpected null registered chunk (should not reach here)");
				return chunk;
			}
			chunk = new RegisteredChunk();
			return registeredChunks.GetOrAdd(coord, chunk);
		}


		public Task<ReadOnlyMemory<byte>> LoadAndSerializeChunkPacketAsync(Coordinate2d coordinate2D){
			RegisteredChunk registeredChunk = GetRegisteredChunk(coordinate2D);
			Task<ReadOnlyMemory<byte>>? tsk = registeredChunk.pendingReadRequest;
			if (tsk is { }) return tsk;
			TaskCompletionSource<ReadOnlyMemory<byte>> tsc = new TaskCompletionSource<ReadOnlyMemory<byte>>();
			tsk = tsc.Task;
			var tsk1 = Interlocked.CompareExchange(ref registeredChunk.pendingReadRequest, tsk, null);
			if (tsk1 is null){
				Thread thread = new Thread(() => ClientLoadRequestFulfillerEphemeralThread(coordinate2D, tsc, registeredChunk), 262144);
				thread.IsBackground = true;
				thread.Priority = ThreadPriority.AboveNormal;
				thread.Start();
				return tsk;
			} else{
				return tsk1;
			}


		}

		private ChunkData? TryLoadChunk(Coordinate2d c2d){
			return null; //todo will complete later
		}
		private ChunkData EnsureFullyGenerated(Coordinate2d c2d){
			ChunkData? cd = TryLoadChunk(c2d);
			if(cd is null){
				return RepeatedlyAdvanceGeneratorStage1(c2d, 255, GetRegisteredChunk(c2d), false);
			}
			return cd;
		}
		private ChunkData RepeatedlyAdvanceGeneratorStage1(Coordinate2d c2d, byte target, RegisteredChunk registeredChunk, bool throwIfExceeded){
			ChunkData? chunkData = registeredChunk.chunkData;
			byte genStage;
			if (chunkData is null)
			{
				chunkData = new ChunkData(0, new ushort[65536], new byte[65536], new());
				registeredChunk.chunkData = chunkData;
				if (target == 0) return chunkData;
				genStage = 0;
			}
			else
			{
				if (target == 0) return chunkData;
				genStage = chunkData.generationStage;
			}
			if (genStage >= target){
				if(throwIfExceeded & genStage != target){
					throw new Exception("overly mature proto-chunk");
				}
				return chunkData;
			}
			int smr;
			IMinecraftWorldView mcwv;
			IMinecraftWorldGenerator mcwg = minecraftWorldGenerator;
		startFun:
			if (genStage > 0)
			{
				smr = mcwg.GetSurroundingMaturityRequirementSize(genStage);
				if(smr == 0){
					mcwv = chunkData;
				} else{
					if (smr < 1) throw new Exception("Generator reported negative surrounding generation requirement");
					for(int x1 = -smr; x1 <= smr; ++x1){
						for(int x2 = -smr; x2 <= smr; ++x2){
							if (x1 == 0 & x2 == 0) continue;
							Coordinate2d c2 = new Coordinate2d(c2d.x + x1, c2d.z + x2);
							RepeatedlyAdvanceGeneratorStage1(c2, genStage, GetRegisteredChunk(c2), true);
						}
					}
					int smr1 = (-16 * smr) + 1;
					int smr2 = (smr * 16) + 15;
					mcwv = ShiftedMinecraftWorldView.Create(this, new PreciseCoordinate(-16 * c2d.x, 0, -16 * c2d.z));
					mcwv = new BoundedMinecraftWorldView(mcwv, new PreciseCoordinate(smr1, 0, smr1), new PreciseCoordinate(smr2, 255, smr2));
				}
			}
			else
			{
				//OPTIMIZATION: allow direct chunk access if surrounding generation requirement is 0.
				mcwv = chunkData;
			}

			mcwg.AdvanceGenerationStage(mcwv, ref chunkData.generationStage);
			genStage = chunkData.generationStage;
			if (genStage < target) goto startFun;
			return chunkData;
		}
		//private static readonly object dataVersion = 2586;
		

		private void ClientLoadRequestFulfillerEphemeralThread(Coordinate2d coordinate2D, TaskCompletionSource<ReadOnlyMemory<byte>> tsc, RegisteredChunk registeredChunk)
		{
			try{
				List<Dictionary<string, object>> tileEntities = new();

				int offsetX = coordinate2D.x * 16;
				int offsetZ = coordinate2D.z * 16;
				ReadOnlySpan<IReadOnlyDictionary<string, string>?> s = MinecraftBlockFlattener.unflatteningPopulation.Span;
				ReadOnlySpan<string> s1 = MinecraftBlockFlattener.blockNameMappings.Span;
				/*
				lock(globalObjectLock){
					byte[] bs = SerializeChunkPacket(coordinate2D.x, coordinate2D.z, EnsureFullyGenerated(coordinate2D));
					MemoryStream bs1 = new MemoryStream(bs.Length + 6);
					new MinecraftProtocolEncoder(bs1).WriteVarint(bs.Length + 1);

					tsc.SetResult(bs1.GetBuffer().AsMemory(0,(int) bs1.Position));
				}
				*/
				
				MemoryStream ms = new MemoryStream(135312);
				ms.SetLength(6);
				ms.Seek(6, SeekOrigin.Begin);

				MinecraftProtocolEncoder minecraftProtocolEncoder = new MinecraftProtocolEncoder(ms);
				minecraftProtocolEncoder.WriteByte(0x20);
				minecraftProtocolEncoder.WriteUnmanagedBigEndian(coordinate2D.x);
				minecraftProtocolEncoder.WriteUnmanagedBigEndian(coordinate2D.z);
				minecraftProtocolEncoder.WriteBool(true);
				minecraftProtocolEncoder.WriteVarint(65535);

				//Empty compound NBT tag
				minecraftProtocolEncoder.WriteByte(10);
				minecraftProtocolEncoder.WriteByte(0);
				minecraftProtocolEncoder.WriteByte(0);
				minecraftProtocolEncoder.WriteByte(0);

				//plains everywhere for now
				minecraftProtocolEncoder.WriteVarint(1024);
				for (int i = 0; i < 1024; ++i) minecraftProtocolEncoder.WriteByte(1);

				//minecraftProtocolEncoder.WriteVarint(0);
				minecraftProtocolEncoder.WriteVarint(131152);

				Span<ulong> ulongs = stackalloc ulong[1024];
				Span<byte> spanView = MemoryMarshal.AsBytes(ulongs);

				try
				{
					Monitor.Enter(globalObjectLock);
					ChunkData chunkData = EnsureFullyGenerated(coordinate2D);
					List<IReadOnlyDictionary<string, object>> ti = new();
					
					for(int y1 = 0; y1 < 256; y1 += 16){
						int bc = 0;
						int i3 = 0;
						ulongs.Clear();
						for (int y2 = 0; y2 < 16; ++y2){
							int y = y1 + y2;
							
							for (int z = 0; z < 16; ++z){
								for(int x = 0; x < 16; ++x){
									FlatMinecraftBlock flatMinecraftBlock = chunkData.ReadBlock(new PreciseCoordinate(x, y, z), 255);
									int mod = i3 - ((i3 / 4) * 4);
									ulongs[i3 / 4] |= ((ulong)flatMinecraftBlock.id)  << (mod * 15);
									++i3;
									//spanView.Slice(2 * i3++, 2).Reverse();
									if (flatMinecraftBlock.id != 0) ++bc;
									IReadOnlyDictionary<string, object>? extraData = flatMinecraftBlock.extraData;
									if(extraData is { }){
										Dictionary<string, object> data = new Dictionary<string, object>(extraData.Count + 3)
										{
											{ "x", x + offsetX },
											{ "y", y },
											{ "z", z + offsetZ }
										};
										foreach (KeyValuePair<string, object> kvp in extraData) data.Add(kvp.Key, kvp.Value);
										tileEntities.Add(data);
									}

								}
							}							
						}
						if (y1 == 240) Monitor.Exit(globalObjectLock);

						for (int i = 0; i < 8192; i += 8)
						{
							spanView.Slice(i, 8).Reverse();
						}
						minecraftProtocolEncoder.WriteUnmanagedBigEndian((short)bc);
						minecraftProtocolEncoder.WriteByte(15);
						minecraftProtocolEncoder.WriteVarint(1024);
						minecraftProtocolEncoder.WriteDirect(spanView);
					}
				} finally{
					if(Monitor.IsEntered(globalObjectLock)) Monitor.Exit(globalObjectLock);
				}
				
				/*
				
				*/
				
				minecraftProtocolEncoder.WriteVarint(0);
				byte[] buf = ms.GetBuffer();
				int mss = ((int)ms.Position) - 6;
				byte[] buf1 = new byte[mss + 9];
				MemoryStream memoryStream = new MemoryStream(buf1, 10, mss - 1, true, false);
				memoryStream.SetLength(10);
				memoryStream.Seek(10, SeekOrigin.Begin);
				Span<byte> venc = stackalloc byte[5];
				try
				{
					//TODO: Remove this once we got uncompressed chunk right
					throw new NotSupportedException();
					using ZLibStream zls = new ZLibStream(memoryStream, CompressionLevel.Optimal, true);
					zls.Write(buf.AsSpan(6, mss));
					zls.Flush();
				} catch(NotSupportedException){
					//HACK: non-resizable MemoryStream throws NotSupportedException when we run out of space
					//We can exploit this behavior for conditional compression: compress only if new size is smaller than old size
					buf[5] = 0;
					int vs = StaticVariableSizeEncoders.WriteVarInt(venc, mss + 1);
					int mvs = 5 - vs;
					venc.Slice(0,vs).CopyTo(buf.AsSpan(mvs));
					tsc.SetResult(buf.AsMemory(mvs, vs + 1 + mss));
					return;
				}
				int vs1 = StaticVariableSizeEncoders.WriteVarInt(venc, mss);
				int mvs1 = 10 - vs1;
				venc.Slice(0, vs1).CopyTo(buf1.AsSpan(mvs1));
				int msp = ((int)memoryStream.Position) - 10;
				int ss2 = (vs1 + msp);
				int vs2 = StaticVariableSizeEncoders.WriteVarInt(venc, ss2);
				int mvs2 = mvs1 - vs2;
				venc.Slice(0, vs2).CopyTo(buf1.AsSpan(mvs2));
				tsc.SetResult(buf1.AsMemory(mvs2, msp + vs2));
				
			} catch(Exception e){
				Console.Error.WriteLine("Unexpected exception while processing chunk load request: " + e);
				tsc.SetException(e);

			}
		}

		public FlatMinecraftBlock ReadBlock(PreciseCoordinate preciseCoordinate, byte minimumGenerationLevel)
		{
			if (!Monitor.IsEntered(globalObjectLock)) throw new Exception("lock required");
			(Coordinate2d chunk, PreciseCoordinate relative) = preciseCoordinate.ToChunkRelativeCoordinate();
			ChunkData cd = RepeatedlyAdvanceGeneratorStage1(chunk, minimumGenerationLevel, GetRegisteredChunk(chunk), false);
			return cd.ReadBlock(relative, minimumGenerationLevel);
		}

		public void WriteBlock(PreciseCoordinate preciseCoordinate, FlatMinecraftBlock block)
		{
			if (!Monitor.IsEntered(globalObjectLock)) throw new Exception("lock required");
			(Coordinate2d chunk, PreciseCoordinate relative) = preciseCoordinate.ToChunkRelativeCoordinate();
			ChunkData cd = RepeatedlyAdvanceGeneratorStage1(chunk, 0, GetRegisteredChunk(chunk), false);
			cd.WriteBlock(relative, block);
		}

		public ref byte GetLightData(PreciseCoordinate preciseCoordinate, byte minimumGenerationLevel)
		{
			//Technically, the caller can still modify the returned reference even after they have released the lock
			//This check is intended to catch forgot-to-lock mistakes, and is not a strong security measure!
			if (!Monitor.IsEntered(globalObjectLock)) throw new Exception("lock required");
			(Coordinate2d chunk, PreciseCoordinate relative) = preciseCoordinate.ToChunkRelativeCoordinate();
			ChunkData cd = RepeatedlyAdvanceGeneratorStage1(chunk, minimumGenerationLevel, GetRegisteredChunk(chunk), false);
			return ref cd.GetLightData(relative, minimumGenerationLevel);
		}
	}
}
