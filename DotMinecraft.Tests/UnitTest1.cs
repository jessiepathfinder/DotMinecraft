using System.Runtime.InteropServices;
using System.Security.Cryptography;

using DotMinecraft.Schema;

namespace DotMinecraft.Tests
{

	[TestClass]
	public class UnitTest1
	{
		[TestMethod]
		public void TestPacketAllocator()
		{
			MinecraftPacketMemoryPool pool = MinecraftPacketMemoryPool.Create();
			(byte[], GCHandle, IntPtr,int d)[] arr = new (byte[], GCHandle, IntPtr,int d)[16];
			for(int i = 0; i < 65536; ++i){
				int rota = i % 16;
				if (i > 15){
					(byte[] a, GCHandle b, IntPtr c,int d) = arr[rota];
					pool.Return(a,b,c,d);
				}
				int rsz = RandomNumberGenerator.GetInt32(1, MinecraftPacketMemoryPool.MaximumPacketSize);
				{
					(byte[] a, GCHandle b, IntPtr c) = pool.Borrow(rsz);
					arr[rota] = (a, b, c, rsz);
				}
			}
		}
		[StructLayout(LayoutKind.Sequential)]
		private sealed class StaticSerializeClass : IMinecraftSerializable, IMinecraftDeserializable{
			[NonMinecraftField]
			private readonly string thestr = "Fortnite sucks! Play Minecraft Instead!";
			[LetDecoderDecideSize(256)]
			public byte[]? bar;

			public StaticSerializeClass(byte[] bar)
			{
				this.bar = bar ?? throw new ArgumentNullException(nameof(bar));
			}
			public StaticSerializeClass(){
				
			}
		}
		[TestMethod]
		public void TestStaticSerialize1()
		{
			byte[]? bar = new byte[256];
			for(int i = 0; i < 256; ++i){
				bar[i] = (byte)i;
			}
			Assert.AreEqual(261, MinecraftProtocolConstantSizeFastSerializer<StaticSerializeClass>.worstCaseSize);
			Span<byte> span = stackalloc byte[258];
			Assert.AreEqual(258, MinecraftProtocolConstantSizeFastSerializer<StaticSerializeClass>.fastSerializeDelegate(span, new StaticSerializeClass(bar)));
			bar = null;
			bar = MinecraftProtocolDeserializer.ReadNextPacket<StaticSerializeClass>(new MinecraftProtocolDecoder(new BinaryReader(new MemoryStream(span.ToArray(), 0, 258, false, false)))).bar ?? throw new Exception("Where is my byte array???");
			for (int i = 0; i < 256; ++i)
			{
				Assert.AreEqual(i, bar[i]);
			}
		}

		private const int SEGMENT_BITS = 0x7F;
		private const int CONTINUE_BIT = 0x80;
		public static int WriteVarInt1(int value)
		{
			while (true)
			{
				if ((value & ~SEGMENT_BITS) == 0)
				{
					Console.WriteLine((byte)value);
					break;
				}

				Console.WriteLine((byte)((value & SEGMENT_BITS) | CONTINUE_BIT));

				// Note: >>> means that the sign bit is shifted with the rest of the number rather than being left alone
				value >>>= 7;
			}
			return 0;
		}
		[StructLayout(LayoutKind.Sequential)]
		public sealed class MinecraftEncryptionStartPacket1 : IMinecraftDeserializable
		{
			[LetDecoderDecideSize(20)]
			public string serverId;
			[LetDecoderDecideSize(550)]
			public byte[] publicKey;
			[LetDecoderDecideSize(32)]
			public byte[] verifyToken;


		}
		[TestMethod]
		public void TestRSAKeysize()
		{
			//WriteVarInt1(583);
			Span<byte> span = stackalloc byte[582];
			byte[] rk = RSA.Create(4096).ExportRSAPublicKey();
			Assert.AreEqual(526, rk.Length);
			Assert.AreEqual(582, MinecraftProtocolConstantSizeFastSerializer<MinecraftEncryptionStartPacket>.fastSerializeDelegate(span, new MinecraftEncryptionStartPacket(rk, new byte[32])));
			MinecraftEncryptionStartPacket1 p1 = MinecraftProtocolDeserializer.ReadNextPacket<MinecraftEncryptionStartPacket1>(new MinecraftProtocolDecoder(new BinaryReader(new MemoryStream(span.ToArray(), 0, 582, false, false))));
			Assert.AreEqual("                    ", p1.serverId);
			byte[] rk1 = p1.publicKey;
			Assert.AreEqual(526, rk1.Length);
			for(int i = 0; i < 526; ++i){
				Assert.AreEqual(rk[i], rk1[i]);
			}
		}
		[TestMethod]
		public void TestRSAKeysize1()
		{
			Span<byte> span = stackalloc byte[1024];
			byte[] key = RSA.Create(4096).ExportSubjectPublicKeyInfo();
			Console.WriteLine(key.Length);

			int len = MinecraftProtocolConstantSizeFastSerializer<MinecraftEncryptionStartPacket>.fastSerializeDelegate(span, new MinecraftEncryptionStartPacket(key, new byte[32]));
			Console.WriteLine(len);
			WriteVarInt1(len);
		}
	}
}