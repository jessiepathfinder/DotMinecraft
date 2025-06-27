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
using RandomNumberGenerator = System.Security.Cryptography.RandomNumberGenerator;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Transactions;
using System.IO.Pipes;

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
		public static Coordinate2d operator -(Coordinate2d a, Coordinate2d b)
		{
			return new Coordinate2d(a.x - b.x, a.z - b.z);
		}
		public Coordinate2d ShiftRoundDown(int i){
			return new Coordinate2d(x >> i, z >> i);
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
			RandomNumberGenerator.Fill(MemoryMarshal.AsBytes(new Span<Vector128<byte>>(ref v)));
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
            RandomNumberGenerator.Fill(MemoryMarshal.AsBytes(new Span<Vector128<byte>>(ref v)));
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

	public static class FractalCompressor{
		public static int CompressInPlace(Span<byte> span){
			int prevSize = span.Length;
			Span<byte> a = stackalloc byte[prevSize - 1];

			
			while (Snappy.TryCompress(span.Slice(0, prevSize), a.Slice(0, prevSize - 1), out int bytesWritten1))
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
				l = Snappy.Decompress(a_.Slice(0,l), b_);
				if (l < 131072)
				{
					Span<byte> tmp = b_;
					b_ = a_;
					a_ = tmp;
					(a, b) = (b, a);
					goto start;
				}
				return b;
			}
			return a;
		}
		public static byte[] UncompressLightMatrix(ReadOnlySpan<byte> ros)
		{
			int rl = ros.Length;
			if (rl > 65536) throw new InvalidOperationException("Light matrix bigger than 65536 bytes");
			byte[] a = new byte[65536];
			Span<byte> a_ = a;
			if (rl == 65536)
			{
				ros.CopyTo(a_);
				return a;
			}

		startthing:
			int l = Snappy.Decompress(ros, a_);
			if (l < 65536)
			{
				byte[] b = new byte[65536];
				Span<byte> b_ = b;
			start:
				l = Snappy.Decompress(a_.Slice(0, l), b_);
				if (l < 65536)
				{
					Span<byte> tmp = b_;
					b_ = a_;
					a_ = tmp;
					(a, b) = (b, a);
					goto start;
				}
				return b;
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
		public ReadOnlyMemory<byte> DestructiveSerialize(){
			Span<byte> s1 = MemoryMarshal.AsBytes(blockMatrix.AsSpan());
			int sl1 = FractalCompressor.CompressInPlace(s1);
			Span<byte> s2 = MemoryMarshal.AsBytes(lightMatrix.AsSpan());
			int sl2 = FractalCompressor.CompressInPlace(s2);
			MemoryStream memoryStream = new MemoryStream(sl1 + sl2 + 11);
			memoryStream.WriteByte(generationStage);
			MinecraftProtocolEncoder minecraftProtocolEncoder = new MinecraftProtocolEncoder(memoryStream);
			minecraftProtocolEncoder.WriteVarint(sl1);
			memoryStream.Write(s1.Slice(0, sl1));
			minecraftProtocolEncoder.WriteVarint(sl2);
			memoryStream.Write(s2.Slice(0, sl2));
			return memoryStream.GetBuffer().AsMemory(0, (int)memoryStream.Position);
		}
	}
	public interface IMinecraftWorldGenerator{
		public bool QueryDeferralPolicy(Coordinate2d coordinate2D, byte generationStage);
		public void AdvanceGenerationStage(IMinecraftWorldView minecraftWorldView, Coordinate2d coordinate2D, ref byte generationStage);
		public int GetSurroundingMaturityRequirementSize(byte generationStage);
	}
	public interface IParallelMinecraftWorldGenerator : IMinecraftWorldGenerator{
		public ChunkData? GenerateHighestParallelStage(Coordinate2d coordinate2D);
	}
	public sealed class SimpleSuperflatGenerator : IParallelMinecraftWorldGenerator
	{
		private SimpleSuperflatGenerator(){
			
		}
		public static readonly SimpleSuperflatGenerator instance = new SimpleSuperflatGenerator();
		public void AdvanceGenerationStage(IMinecraftWorldView minecraftWorldView, Coordinate2d coordinate2D, ref byte generationStage)
		{
			if (generationStage != 0) throw new Exception("Simple Superflat does not support multi-stage world generation");
			GenerateImpl(minecraftWorldView);
			generationStage = 255;
		}
		private static void GenerateImpl(IMinecraftWorldView minecraftWorldView){
			FlatMinecraftBlock bedrock = new FlatMinecraftBlock(33, null);
			FlatMinecraftBlock stone = new FlatMinecraftBlock(1, null);
			FlatMinecraftBlock dirt = new FlatMinecraftBlock(10, null);
			FlatMinecraftBlock grass = new FlatMinecraftBlock(9, null);

			for (int x = 0; x < 16; ++x)
			{
				for (int z = 0; z < 16; ++z)
				{
					minecraftWorldView.WriteBlock(new PreciseCoordinate(x, 0, z), bedrock);
					for (int y = 1; y < 58; ++y)
					{
						minecraftWorldView.WriteBlock(new PreciseCoordinate(x, y, z), stone);
					}
					for (int y = 58; y < 63; ++y)
					{
						minecraftWorldView.WriteBlock(new PreciseCoordinate(x, y, z), dirt);
					}
					minecraftWorldView.WriteBlock(new PreciseCoordinate(x, 63, z), grass);


					for (int y = 64; y < 255; ++y)
					{
						minecraftWorldView.GetLightData(new PreciseCoordinate(x, y, z), 0) = 240;
					}

				}
			}
		}

		public int GetSurroundingMaturityRequirementSize(byte generationStage)
		{
			return 0;
		}

		public bool QueryDeferralPolicy(Coordinate2d coordinate2D, byte generationStage)
		{
			return false;
		}

		public ChunkData? GenerateHighestParallelStage(Coordinate2d coordinate2D)
		{
			ChunkData chunkData = new ChunkData(255, new ushort[65536], new byte[65536], new());
			GenerateImpl(chunkData);
			return chunkData;
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
	public sealed class MinecraftWorldManager : IMinecraftWorldView
	{
		private static readonly byte[] emptyRegion = new byte[] { 0x63, 0x60, 0x18, 0xD9, 0x00, 0x00 };

		private static readonly ManualResetEventSlim savingLoopTerminated = new();


		private readonly ReaderWriterLockSlim globalLock = new(LockRecursionPolicy.SupportsRecursion);

		private volatile int isStateCorrupted;
		private volatile int startDestructionProcess;
		private const int SEGMENT_BITS = 0x7F;
		private const int CONTINUE_BIT = 0x80;
		private static int ReadVarInt1(Stream stream)
		{
			int value = 0;
			int position = 0;
			int currentByte;

			while (true)
			{
				currentByte = stream.ReadByte();
				if (currentByte < 0) throw new EndOfStreamException("Unexpected end of file!");

				value |= (currentByte & SEGMENT_BITS) << position;

				if ((currentByte & CONTINUE_BIT) == 0) break;

				position += 7;

				if (position >= 32) throw new Exception("VarInt is too big");
			}

			return value;
		}
		private readonly SemaphoreSlim saveLoadThreadSemaphore = new SemaphoreSlim(0);
		private void SaveLoadThread(){
			SemaphoreSlim saveLoadThreadSemaphore = this.saveLoadThreadSemaphore;
			while (true){
				saveLoadThreadSemaphore.Wait();
				//Give the queue some time to pile up before we take a snapshot of 
				//all the registered chunks
				Thread.Sleep(RandomNumberGenerator.GetInt32(100,200));
				int tc = 1;
				while (saveLoadThreadSemaphore.Wait(0)) ++tc;

				Dictionary<Coordinate2d, TaskCompletionSource<ReadOnlyMemory<byte>>> readQueue = new Dictionary<Coordinate2d, TaskCompletionSource<ReadOnlyMemory<byte>>>();

				
				try
				{
					KeyValuePair<Coordinate2d, RegisteredChunk>[] kvps = registeredChunks.ToArray();
					Dictionary<Coordinate2d, bool> keyValuePairs = new Dictionary<Coordinate2d, bool>();
					Dictionary<Coordinate2d, bool> keyValuePairs1 = new Dictionary<Coordinate2d, bool>();
					Dictionary<Coordinate2d, RegisteredChunk> writeQueue = new Dictionary<Coordinate2d, RegisteredChunk>();
					int stop = kvps.Length;
					for (int i = 0, i1 = 0; i < stop & i1 < tc; ++i)
					{
						KeyValuePair<Coordinate2d, RegisteredChunk> kvp = kvps[i];
						Coordinate2d c2d = kvp.Key;
						RegisteredChunk rc = kvp.Value;
						ReadOnlyMemory<byte> rom = rc.save;
						bool isWrite = rom.Length > 0;
						if (isWrite)
						{
							writeQueue.Add(c2d, rc);
							keyValuePairs[c2d.ShiftRoundDown(4)] = true;
						}
						else
						{
							TaskCompletionSource<ReadOnlyMemory<byte>>? tsc = Interlocked.Exchange(ref rc.pendingLoadRequest, null);
							if (tsc is null) continue;
							readQueue.Add(c2d, tsc);
							c2d = c2d.ShiftRoundDown(4);
							if (keyValuePairs1.TryAdd(c2d, false)) keyValuePairs.TryAdd(c2d, false);
						}
						++i1;
					}
					string prefix = regionNamePrefix;
					int nrt = keyValuePairs.Count;
					Thread[] threads = new Thread[nrt];
					int q = 0;
					if (isStateCorrupted == 1) return;
					foreach (KeyValuePair<Coordinate2d, bool> cc in keyValuePairs)
					{
						Coordinate2d c = cc.Key;
						bool isWrite = cc.Value;
						Thread thr = new Thread(() => {
							int xb = c.x << 4;
							int zb = c.z << 4;
							Console.WriteLine("start ephemeral thr: " + c.x + ", " + c.z);
							Span<byte> drain = stackalloc byte[256];
							FileStream? fileStream = null;
							try
							{
								MemoryStream? memoryStream;
								string name = prefix + c.x + "." + c.z + ".mca2";
								if (isWrite)
								{
									fileStream = new FileStream(name, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 65536, FileOptions.SequentialScan);
									if (fileStream.Length == 0)
									{
										try
										{
											//Critical region reduces corruption risks
										}
										finally
										{
											fileStream.Write(emptyRegion);
											fileStream.Flush(true);
										}
										fileStream.Seek(0, SeekOrigin.Begin);
									}
									memoryStream = new MemoryStream();
								}
								else
								{
									try
									{
										fileStream = new FileStream(name, FileMode.Open, FileAccess.Read, FileShare.None, 65536, FileOptions.SequentialScan);
									}
									catch (FileNotFoundException)
									{
										for (int x = 0; x < 16; ++x)
										{
											for (int z = 0; z < 16; ++z)
											{
												if (readQueue.TryGetValue(new Coordinate2d(x + xb, z + zb), out TaskCompletionSource<ReadOnlyMemory<byte>>? tsc))
												{
													if (tsc is null) throw new Exception("Unexpected null task completion source (should not reach here)");
													tsc.SetResult(default);
												}
											}
										}
										return;
									}
									memoryStream = null;
								}

								MinecraftProtocolEncoder minecraftProtocolEncoder = memoryStream is null ? default : new MinecraftProtocolEncoder(memoryStream);
								using (DeflateStream deflateStream = new DeflateStream(fileStream, CompressionMode.Decompress, true))
								{
									for (int x = 0; x < 16; ++x)
									{
										for (int z = 0; z < 16; ++z)
										{
											

											int i = ReadVarInt1(deflateStream);
											
											//ZigZag encoding can enhance locality when deflating
											//Which can make the compression a lot tighter
											Coordinate2d c2d1 = new Coordinate2d(x + xb, ((x & 1) == 0 ? z : (15 - z)) + zb);
											if (memoryStream is { } && writeQueue.TryGetValue(c2d1, out RegisteredChunk? registeredChunk))
											{
												if (registeredChunk is null) throw new Exception("Unexpected null registered chunk (should not reach here)");
												ReadOnlySpan<byte> sav = registeredChunk.save.Span;
												registeredChunk.save = default;
												registeredChunk.Set();
												minecraftProtocolEncoder.WriteVarint(sav.Length);
												memoryStream.Write(sav);

												
												//NOTE: DeflateStream does not support seeking
												//so we must drain the stream
												int toDrain = i;
												while (toDrain > 0)
												{
													int drained = deflateStream.Read(drain.Slice(0, Math.Min(toDrain, 256)));
													if (drained == 0) throw new EndOfStreamException();
													toDrain -= drained;
												}

												continue;
											}
											if (isWrite)
											{
												minecraftProtocolEncoder.WriteVarint(i);
											}
											if (readQueue.TryGetValue(c2d1, out TaskCompletionSource<ReadOnlyMemory<byte>>? tsc))
											{
												if (tsc is null) throw new Exception("Unexpected null task completion source (should not reach here)");
												if (i == 0)
												{
													tsc.SetResult(default);
													continue;
												}
												byte[] buf = new byte[i];
												deflateStream.ReadExactly(buf, 0, i);
												tsc.SetResult(buf);
												memoryStream?.Write(buf, 0, i);
											}
											else if (memoryStream is null)
											{
												//NOTE: DeflateStream does not support seeking
												//so we must drain the stream
												int toDrain = i;
												while (toDrain > 0)
												{
													int drained = deflateStream.Read(drain.Slice(0, Math.Min(toDrain, 256)));
													if (drained == 0) throw new EndOfStreamException();
													toDrain -= drained;
												}
											} else if(i > 0){
												
												int msp = (int)memoryStream.Position;
												int sek = msp + i;
												memoryStream.SetLength(sek);
												memoryStream.Seek(sek, SeekOrigin.Begin);
												deflateStream.ReadExactly(memoryStream.GetBuffer(), msp, i);
											}
										}
									}
								}
								
								if (memoryStream is { })
								{
									fileStream.Seek(0, SeekOrigin.Begin);
									ReadOnlySpan<byte> directSave = memoryStream.GetBuffer().AsSpan(0, (int)memoryStream.Position);
									try{
										//Critical region reduces corruption risks
									} finally{
										using (DeflateStream deflateStream1 = new DeflateStream(fileStream, CompressionLevel.SmallestSize, true))
										{
											deflateStream1.Write(directSave);
											deflateStream1.Flush();
										}
										long fsl = fileStream.Length;
										long fsp = fileStream.Position;
										if (fsl > fsp) fileStream.SetLength(fsp);
										fileStream.Flush(true);
									}

								}
							}
							catch (Exception e)
							{
								for (int x = 0; x < 16; ++x)
								{
									for (int z = 0; z < 16; ++z)
									{
										if (readQueue.TryGetValue(new Coordinate2d(x + xb, z + zb), out TaskCompletionSource<ReadOnlyMemory<byte>>? tsc))
										{
											if (tsc is null) throw new Exception("Unexpected null task completion source (should not reach here)");
											tsc.TrySetException(e);
										}
									}
								}
								isStateCorrupted = 1;
								throw;
							}
							finally
							{
								fileStream?.Dispose();
								Console.WriteLine("end ephemeral thr: " + c.x + ", " + c.z);
							}
						}, 131072);
						//Intelligent priority avoids stalling world loading!
						thr.Priority = keyValuePairs1.ContainsKey(c) ? ThreadPriority.Highest : ThreadPriority.BelowNormal;
						thr.Name = "DotMinecraft ephemeral world loading thread";
						thr.Start();
						threads[q++] = thr;
					}
					for(int p = 0; p < nrt; ++p){
						threads[p].Join();
					}
				} catch(Exception e){
					isStateCorrupted = 1;
					foreach(TaskCompletionSource<ReadOnlyMemory<byte>> tsc in readQueue.Values){
						tsc.SetException(e);
					}
					throw;
				}
				if (startDestructionProcess == 1) break;
			}
			savingLoopTerminated.Set();


		}


		private readonly IMinecraftWorldGenerator minecraftWorldGenerator;
		private readonly string datadir;
		private readonly string regionNamePrefix;
		public MinecraftWorldManager(IMinecraftWorldGenerator minecraftWorldGenerator, string datadir){
			this.minecraftWorldGenerator = minecraftWorldGenerator ?? throw new ArgumentNullException(nameof(minecraftWorldGenerator));
			datadir = Path.GetFullPath(datadir) + Path.DirectorySeparatorChar;
			this.datadir = datadir;
			this.regionNamePrefix = datadir + "region.";

			Thread t = new Thread(SaveLoadThread, 131072);
			t.Name = "DotMinecraft world loading manager thread";
			t.Priority = ThreadPriority.Highest;
			t.Start();
			t = new Thread(GarbageCollectionLoop, 131072);
			t.Name = "DotMinecraft chunk garbage collector thread";
			t.Priority = ThreadPriority.Highest;
			t.IsBackground = true;
			t.Start();
		}
		private void GarbageCollectionLoop(){
			ConcurrentDictionary<Coordinate2d, RegisteredChunk> cd = registeredChunks;
			ConcurrentDictionary<MinecraftClientContext, bool> cc = registeredClients;
			ReaderWriterLockSlim readerWriterLock = globalLock;
			while (true){
				Thread.Sleep(RandomNumberGenerator.GetInt32(100, 200));
				var ar1 = cc.ToArray();
				for(int i = 0, limit = ar1.Length; i < limit; ++i){
					MinecraftClientContext mcc = ar1[i].Key;
					if(mcc.deactivating == 1){
						if(cc.TryRemove(mcc, out _))
							continue;
						throw new Exception("Deactivating unregistered client (should not reach here)");
					}
					mcc.ProcessTick(this);
				}
				var ar2 = cd.ToArray();
				try{
					readerWriterLock.EnterWriteLock();
					for (int i = 0, limit = ar2.Length; i < limit; ++i)
					{
						RegisteredChunk registeredChunk = ar2[i].Value;
						if (registeredChunk.inUse)
						{
							
							registeredChunk.inUse = false;
							continue;
						}
						SaveChunk(registeredChunk, false);
					}
				} finally{
					if (readerWriterLock.IsWriteLockHeld) readerWriterLock.ExitWriteLock();
				}
			}
		}

		private sealed class RegisteredChunk : ManualResetEventSlim{
			public Task<(ReadOnlyMemory<byte>, BlockUpdateConsequencesQueue)>? pendingReadRequest;
			public volatile TaskCompletionSource<ReadOnlyMemory<byte>>? pendingLoadRequest;
			public ChunkData? chunkData;
			public ChunkData? deserialized;
			public ReadOnlyMemory<byte> save;
			public BlockUpdateConsequencesQueue blockUpdateConsequencesHead = new(default);
			public bool inUse;
			public bool hintNotGeneratedYet;
			public RegisteredChunk() : base(true){
				
			}
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


		public Task<(ReadOnlyMemory<byte>,BlockUpdateConsequencesQueue)> LoadAndSerializeChunkPacketAsync(Coordinate2d coordinate2D){
			RegisteredChunk registeredChunk = GetRegisteredChunk(coordinate2D);
			Task<(ReadOnlyMemory<byte>,BlockUpdateConsequencesQueue)>? tsk = registeredChunk.pendingReadRequest;
			if (tsk is { }) return tsk;
			TaskCompletionSource<(ReadOnlyMemory<byte>, BlockUpdateConsequencesQueue)> tsc = new TaskCompletionSource<(ReadOnlyMemory<byte>,BlockUpdateConsequencesQueue)>();
			tsk = tsc.Task;
			var tsk1 = Interlocked.CompareExchange(ref registeredChunk.pendingReadRequest, tsk, null);
			if (tsk1 is null){
				Thread thread = new Thread(() => ClientLoadRequestFulfillerEphemeralThread(coordinate2D, tsc, registeredChunk), 262144);
				thread.Name = "DotMinecraft chunk load request handler ephemeral thread";
				thread.IsBackground = true;
				thread.Priority = ThreadPriority.AboveNormal;
				thread.Start();
				return tsk;
			} else{
				return tsk1;
			}


		}


		private ChunkData? TryLoadChunkNoLock(RegisteredChunk registeredChunk)
		{
			ChunkData? alreadyLoaded = registeredChunk.chunkData;
			if (alreadyLoaded is { }) return alreadyLoaded;
			alreadyLoaded = registeredChunk.deserialized;
			if (alreadyLoaded is { })
			{
				registeredChunk.chunkData = alreadyLoaded;
				registeredChunk.deserialized = null;
				return alreadyLoaded;
			}
			alreadyLoaded = LoadChunkImpl((registeredChunk, saveLoadThreadSemaphore));
			registeredChunk.hintNotGeneratedYet = alreadyLoaded is null;
			registeredChunk.chunkData = alreadyLoaded;
			return alreadyLoaded;
		}
		private void PrefetchChunkAsync(Coordinate2d coordinate2D){
			Thread thr = new Thread(() => PrefetchChunk(coordinate2D), 131072);
			thr.Name = "DotMinecraft chunk prefetcher ephemeral thread";
			thr.IsBackground = true;
			thr.Start();
		}
		private void PrefetchChunk(Coordinate2d coordinate2D){
			RegisteredChunk registeredChunk = GetRegisteredChunk(coordinate2D);
			lock(registeredChunk){
				registeredChunk.Wait();
				if (registeredChunk.chunkData is { } | registeredChunk.deserialized is { }) return;
				ChunkData? cd = LoadChunkImpl((registeredChunk, saveLoadThreadSemaphore));

				if(cd is null && minecraftWorldGenerator is IParallelMinecraftWorldGenerator parallelMinecraftWorldGenerator){
					cd = parallelMinecraftWorldGenerator.GenerateHighestParallelStage(coordinate2D);
				}
				registeredChunk.hintNotGeneratedYet = cd is null;
				registeredChunk.deserialized = cd;
			}
		}
		private static ChunkData? LoadChunkImpl((RegisteredChunk registeredChunk, SemaphoreSlim semaphoreSlim) x){
			//We need to wait, because the registered chunk has a pending save request
			//It is unsafe to overlap
			TaskCompletionSource<ReadOnlyMemory<byte>> tsc = new TaskCompletionSource<ReadOnlyMemory<byte>>();
			x.registeredChunk.Wait();
			
			if (Interlocked.CompareExchange(ref x.registeredChunk.pendingLoadRequest, tsc, null) is { }){
				throw new Exception("Duplicate load requests submitted for the same chunk (should not reach here)");
			}

			x.semaphoreSlim.Release();
			ReadOnlyMemory<byte> ros1 = tsc.Task.Result;
			ReadOnlySpan<byte> res = ros1.Span;

			if (res.Length == 0) return null;
			(int s, int s1) = StaticVariableSizeEncoders.ReadVarInt(res.Slice(1));
			s1 += 1;
			ushort[] blockMatrix = FractalCompressor.UncompressBlockMatrix(res.Slice(s1, s));
			s1 += s;
			(s, int s2) = StaticVariableSizeEncoders.ReadVarInt(res.Slice(s1));
			byte[] lightMatrix = FractalCompressor.UncompressLightMatrix(res.Slice(s1 + s2,s));
			return new ChunkData(res[0], blockMatrix, lightMatrix, new());
			

		}
		internal void MarkChunkInUse(Coordinate2d c2d){
			GetRegisteredChunk(c2d).inUse = true;
		}

		private ChunkData EnsureFullyGenerated1(Coordinate2d c2d, RegisteredChunk registeredChunk){
			try
			{
				return RepeatedlyAdvanceGeneratorStage1(c2d, registeredChunk, 255, false);
			} catch{
				isStateCorrupted = 1;
				throw;
			}
		}

		private ChunkData RepeatedlyAdvanceGeneratorStage1(Coordinate2d c2d, RegisteredChunk registeredChunk, byte target, bool throwIfExceeded){
			bool lockRequired = false;
			try{
				lockRequired = !Monitor.IsEntered(registeredChunk);
				if (lockRequired) Monitor.Enter(registeredChunk);
				ChunkData? chunkData = registeredChunk.hintNotGeneratedYet ? null : TryLoadChunkNoLock(registeredChunk);
				byte genStage;
				if (chunkData is null)
				{
					chunkData = new ChunkData(0, new ushort[65536], new byte[65536], new());
					registeredChunk.chunkData = chunkData;
					registeredChunk.hintNotGeneratedYet = false;
					if (target == 0) return chunkData;
					genStage = 0;
				}
				else
				{
					if (target == 0) return chunkData;
					genStage = chunkData.generationStage;
				}
				if (genStage >= target)
				{
					if (throwIfExceeded & genStage != target)
					{
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
					if (smr == 0)
					{
						//OPTIMIZATION: allow direct chunk access if surrounding generation requirement is 0.
						mcwv = chunkData;
					}
					else
					{
						if (smr < 1) throw new Exception("Generator reported negative surrounding generation requirement");
						for (int x1 = -smr; x1 <= smr; ++x1)
						{
							for (int x2 = -smr; x2 <= smr; ++x2)
							{
								if (x1 == 0 & x2 == 0) continue;
								Coordinate2d c2 = new Coordinate2d(c2d.x + x1, c2d.z + x2);
								RepeatedlyAdvanceGeneratorStage1(c2, GetRegisteredChunkFast2(c2), genStage, true);
							}
						}

						//Defer policy is imposed if the generation phase involves multi-chunk structures
						//(e.g villages)
						bool hasDeferPolicy = minecraftWorldGenerator.QueryDeferralPolicy(c2d, genStage);

						if (hasDeferPolicy)
						{
							byte genStage1 = (byte)(genStage + 1);
							bool didit = false;
							for (int x1 = -smr; x1 <= smr; ++x1)
							{
								for (int x2 = -smr; x2 <= smr; ++x2)
								{
									if (x1 == 0 & x2 == 0) continue;
									Coordinate2d c2 = new Coordinate2d(c2d.x + x1, c2d.z + x2);
									if (!minecraftWorldGenerator.QueryDeferralPolicy(c2, genStage1))
									{
										if (didit) throw new Exception("Multiple non-deferred chunks within range of a deferred chunk (perhaps broken generator?)");
										RepeatedlyAdvanceGeneratorStage1(c2, GetRegisteredChunkFast2(c2), genStage1, true);
										didit = true;
									}

								}
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
					//ALSO OPTIMIZATION: Stage-0 chunks have ZERO surrounding maturity requirements
					mcwv = chunkData;
				}

				mcwg.AdvanceGenerationStage(mcwv, c2d, ref chunkData.generationStage);
				genStage = chunkData.generationStage;
				if (genStage < target) goto startFun;
				return chunkData;
			} finally{
				if (lockRequired && Monitor.IsEntered(registeredChunk)) Monitor.Exit(registeredChunk);
			}
			
		}
		//private static readonly object dataVersion = 2586;

		//private readonly object badIdeaLock = new();
		private void ClientLoadRequestFulfillerEphemeralThread(Coordinate2d coordinate2D, TaskCompletionSource<(ReadOnlyMemory<byte>,BlockUpdateConsequencesQueue)> tsc, RegisteredChunk registeredChunk)
		{
			
			try
			{

				using NoGCRegionHandle noGCRegionHandle = new NoGCRegionHandle();
				PrefetchChunkAsync(coordinate2D);
				Span<byte> ls = stackalloc byte[32800];
				Span<byte> ls1 = stackalloc byte[32800];


				ReaderWriterLockSlim globalLock = this.globalLock;
				List<Dictionary<string, object>> tileEntities = new();

				int offsetX = coordinate2D.x * 16;
				int offsetZ = coordinate2D.z * 16;
				ReadOnlySpan<IReadOnlyDictionary<string, string>?> s = MinecraftBlockFlattener.unflatteningPopulation.Span;
				ReadOnlySpan<string> s1 = MinecraftBlockFlattener.blockNameMappings.Span;
				

				

				MemoryStream ms1 = new MemoryStream(20);
				ms1.WriteByte(0x23);
				MinecraftProtocolEncoder e2 = new MinecraftProtocolEncoder(ms1);
				e2.WriteVarint(coordinate2D.x);
				e2.WriteVarint(coordinate2D.z);
				e2.WriteByte(1);
				int skyLightMask = 0;
				int blockLightMask = 0;
				int skyLightIndex = 0;
				int blockLightIndex = 0;
				MemoryStream ms = new MemoryStream(139264);



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
				
				

				try
				{
					globalLock.EnterReadLock();
					bool firstOptimisticAttempt = true;
					ChunkData? chunkData = registeredChunk.chunkData;
					if(chunkData is null || chunkData.generationStage != 255){
						
						globalLock.ExitReadLock();
						if (firstOptimisticAttempt)
						{
							firstOptimisticAttempt = false;
						}
						globalLock.EnterWriteLock();
						chunkData = EnsureFullyGenerated1(coordinate2D, registeredChunk);

						//Atomic lock downgrade from write to read
						//Without unlocking
						globalLock.EnterReadLock();
						globalLock.ExitWriteLock();
					}

					List<IReadOnlyDictionary<string, object>> ti = new();
					
					for(int y1 = 0; y1 < 256; y1 += 16){
						int bc = 0;
						int i3 = 0;
						bool hadSkyLight = false;
						bool hadBlockLight = false;
						int blb = blockLightIndex * 2050;
						int slb = skyLightIndex * 2050;
						Span<byte> blockLight1 = ls.Slice(blb + 2, 2048);
						Span<byte> skyLight1 = ls1.Slice(slb + 2, 2048);
						//Optimization: MemoryStream direct buffer mapping
						int msl = ((int)ms.Position) + 5;
						ms.SetLength(msl + 8192);
						Span<byte> spanView = ms.GetBuffer().AsSpan(msl, 8192);
						Span<ulong> ulongs = MemoryMarshal.Cast<byte, ulong>(spanView);


						ulong t = 0;
						for (int y2 = 0; y2 < 16; ++y2){
							int y = y1 + y2;
							

							for (int z = 0; z < 16; ++z){
								for(int x = 0; x < 16; ++x){
									
									byte light = chunkData.GetLightData(new PreciseCoordinate(x, y, z), 255);
									if(light != 0){
										int blockLight = light & 15;
										hadBlockLight |= blockLight > 0;
                                        int skyLight = light >> 4;
										hadSkyLight |= skyLight > 0;

										if((i3 & 1) == 0){
											blockLight <<= 4;
											skyLight <<= 4;
										}
										blockLight1[i3 / 2] |= (byte)blockLight;
										skyLight1[i3 / 2] |= (byte)skyLight;
									}
									
									FlatMinecraftBlock flatMinecraftBlock = chunkData.ReadBlock(new PreciseCoordinate(x, y, z), 255);
									int mod = i3 - ((i3 / 4) * 4);
									t |= ((ulong)flatMinecraftBlock.id)  << (mod * 15);

									
									if ((i3 & 3) == 3){
										ulongs[i3 / 4] = t;
										t = 0;
									}
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
						if (y1 == 240) globalLock.ExitReadLock();
						if (hadBlockLight)
						{
							ls[blb] = 0x80;
							ls[blb + 1] = 0x10;
							blockLightMask |= 1;
							blockLightMask <<= 1;
							++blockLightIndex;
						}
						if (hadSkyLight)
						{
							ls1[slb] = 0x80;
							ls1[slb + 1] = 0x10;
							skyLightMask |= 1;
							skyLightMask <<= 1;
							++skyLightIndex;
						}
						for (int i = 0; i < 8192; i += 8)
						{
							spanView.Slice(i, 8).Reverse();
						}
						minecraftProtocolEncoder.WriteUnmanagedBigEndian((short)bc);
						minecraftProtocolEncoder.WriteByte(15);
						minecraftProtocolEncoder.WriteVarint(1024);
						ms.Seek(8192, SeekOrigin.Current);
					}

				} finally{
					if(globalLock.IsReadLockHeld){
						globalLock.ExitReadLock();
					}
					if(globalLock.IsWriteLockHeld){
						globalLock.ExitWriteLock();
					}
				}
				e2.WriteVarint(skyLightMask);
				e2.WriteVarint(blockLightMask);
				e2.WriteByte(0);
				e2.WriteByte(0);
				ms1.Write(ls.Slice(0, blockLightIndex * 2050));
				ms1.Write(ls1.Slice(0, skyLightIndex * 2050));
				
				
				minecraftProtocolEncoder.WriteVarint(0);
				byte[] buf = ms.GetBuffer();
				int mss = ((int)ms.Position);
				MemoryStream memoryStream = new MemoryStream(256);
				memoryStream.SetLength(10);
				memoryStream.Seek(10, SeekOrigin.Begin);
				
				using (ZLibStream zls = new ZLibStream(memoryStream, CompressionLevel.Optimal, true))
				{
					zls.Write(buf.AsSpan(0, mss));
					zls.Flush();
				}
				byte[] buf1 = memoryStream.GetBuffer();
				Span<byte> venc = stackalloc byte[5];
				int vs1 = StaticVariableSizeEncoders.WriteVarInt(venc, mss);
				int mvs1 = 10 - vs1;
				venc.Slice(0, vs1).CopyTo(buf1.AsSpan(mvs1));
				int msp = ((int)memoryStream.Position) - 10;
				int ss2 = (vs1 + msp);
				int vs2 = StaticVariableSizeEncoders.WriteVarInt(venc, ss2);
				int mvs2 = mvs1 - vs2;
				venc.Slice(0, vs2).CopyTo(buf1.AsSpan(mvs2));
				
				int ss6 = (int)ms1.Position;
				MemoryStream ms3 = new MemoryStream();
				using(ZLibStream zls = new ZLibStream(ms3, CompressionLevel.Optimal, true)){
					zls.Write(ms1.GetBuffer(), 0, ss6);
				}
				int ss4 = (int)ms3.Position;
				int ss5 = StaticVariableSizeEncoders.WriteVarInt(venc, ss6);
				int h1 = (int)memoryStream.Position;

				MinecraftProtocolEncoder e3 = new MinecraftProtocolEncoder(memoryStream);
				e3.WriteVarint(ss4 + ss5);
				memoryStream.Write(venc.Slice(0, ss5));
				memoryStream.Write(ms3.GetBuffer(),0,ss4);

				int h2 = (int)memoryStream.Position;
				

				tsc.SetResult((memoryStream.GetBuffer().AsMemory(mvs2, ss2 + vs2 + (h2 - h1)),registeredChunk.blockUpdateConsequencesHead));
				registeredChunk.pendingReadRequest = null;
				GC.KeepAlive(noGCRegionHandle);
			} catch(Exception e){
				Console.Error.WriteLine("Unexpected exception while processing chunk load request: " + e);
				tsc.TrySetException(e);

			}
		}
		private void SaveChunk(RegisteredChunk registeredChunk, bool throwIfAlreadyLoaded){
			ChunkData? cd;
			//NOTE: We only need lock to detach the chunk from the Minecraft world
			lock (registeredChunk){
				cd = registeredChunk.chunkData;
				if (cd is null){
					if(throwIfAlreadyLoaded) throw new Exception("Attempted to save unloaded chunk (should not reach here)");

					registeredChunk.deserialized = null;
					return;
				}
				registeredChunk.chunkData = null;
				registeredChunk.deserialized = null;
				BlockUpdateConsequencesQueue? bucq = registeredChunk.blockUpdateConsequencesHead;
				if (bucq is { })
				{
					if (bucq.packet.Length > 0)
					{
						BlockUpdateConsequencesQueue nb = new(default);
						bucq.next = nb;
						registeredChunk.blockUpdateConsequencesHead = nb;
					}
				}
				//This causes threads who are attempting to load the chunk to block
				//It is unsafe to load a chunk as it is being saved to disk
				registeredChunk.Reset();
			}
			ThreadPool.QueueUserWorkItem(SaveChunk1, (cd,registeredChunk,saveLoadThreadSemaphore), false);
		}
		private static void SaveChunk1((ChunkData chunkData, RegisteredChunk registeredChunk, SemaphoreSlim semaphoreSlim) x){
			x.registeredChunk.save = x.chunkData.DestructiveSerialize();
			x.semaphoreSlim.Release();
		}


		public FlatMinecraftBlock ReadBlock(PreciseCoordinate preciseCoordinate, byte minimumGenerationLevel)
		{
			if (!(globalLock.IsReadLockHeld || globalLock.IsWriteLockHeld)) throw new Exception("lock required");
			(Coordinate2d chunk, PreciseCoordinate relative) = preciseCoordinate.ToChunkRelativeCoordinate();
			ChunkData cd = RepeatedlyAdvanceGeneratorStage1(chunk, GetRegisteredChunkFast(chunk), minimumGenerationLevel, false);
			return cd.ReadBlock(relative, minimumGenerationLevel);
		}

		public void WriteBlock(PreciseCoordinate preciseCoordinate, FlatMinecraftBlock block)
		{
			if (!globalLock.IsWriteLockHeld) throw new Exception("lock required");
			(Coordinate2d chunk, PreciseCoordinate relative) = preciseCoordinate.ToChunkRelativeCoordinate();
			ChunkData cd = RepeatedlyAdvanceGeneratorStage1(chunk, GetRegisteredChunkFast2(chunk), 0, false);
			cd.WriteBlock(relative, block);
		}
		private readonly Dictionary<Coordinate2d, RegisteredChunk> registeredChunks1 = new();

		private RegisteredChunk GetRegisteredChunkFast(Coordinate2d coordinate2D){
			if(registeredChunks1.TryGetValue(coordinate2D,out RegisteredChunk? registeredChunk)){
				if (registeredChunk is null) throw new Exception("Unexpected null registered chunk (should not reach here)");
				return registeredChunk;
			}
			return GetRegisteredChunk(coordinate2D);
		}
		private RegisteredChunk GetRegisteredChunkFast2(Coordinate2d coordinate2D)
		{
			if (registeredChunks1.TryGetValue(coordinate2D, out RegisteredChunk? registeredChunk))
			{
				if (registeredChunk is null) throw new Exception("Unexpected null registered chunk (should not reach here)");
				return registeredChunk;
			}
			registeredChunk = GetRegisteredChunk(coordinate2D);
			registeredChunks1.Add(coordinate2D, registeredChunk);
			return registeredChunk;
		}

		public ref byte GetLightData(PreciseCoordinate preciseCoordinate, byte minimumGenerationLevel)
		{

			(Coordinate2d chunk, PreciseCoordinate relative) = preciseCoordinate.ToChunkRelativeCoordinate();
			ChunkData cd = RepeatedlyAdvanceGeneratorStage1(chunk, GetRegisteredChunkFast(chunk), minimumGenerationLevel, false);
			return ref cd.GetLightData(relative, minimumGenerationLevel);
		}

		private static readonly ConcurrentDictionary<MinecraftClientContext, bool> registeredClients = new(ReferenceEqualityComparer.Instance);
		internal void RegisterClient(MinecraftClientContext minecraftClientContext){
			registeredClients.TryAdd(minecraftClientContext, false);
		}
	}
}
