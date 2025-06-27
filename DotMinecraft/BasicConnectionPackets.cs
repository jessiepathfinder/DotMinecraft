using DotMinecraft.Schema;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace DotMinecraft
{
	[MinecraftStandardPacketBinding(0x00)] [StructLayout(LayoutKind.Sequential)]
	internal sealed class ClientHelloPacket : IMinecraftPacket{
		[VariableSize] private int protocolVersion;
		[LetDecoderDecideSize(16)] private string serverAddress;
		private ushort serverPort;
		[VariableSize] private int intent;
		public void Handle(MinecraftClientContext context){
			if (context.state != MinecraftProtocolState.handshake) throw new Exception("Unexpected client hello outside handshake state");
			context.state = MinecraftProtocolState.login;
			int intent = this.intent;
			if (intent != 2 & intent != 3) throw new Exception("Ping intent not supported for now");
			if (protocolVersion != 754) throw new Exception("Only Minecraft 1.16.5 is permitted");
			context.SetOverride(MinecraftListener.StaticHandler<MinecraftLogonPacket>, 0);
			Console.WriteLine("DONE handling hello");
		}
	}
	[StructLayout(LayoutKind.Sequential)]
	internal sealed class MinecraftLogonPacket : IMinecraftPacket
	{
		[LetDecoderDecideSize(16)]
		private string username;

		

		public void Handle(MinecraftClientContext context)
		{
			if (context.state != MinecraftProtocolState.login) throw new Exception("Unexpected client login outside handshake state");
			context.state = MinecraftProtocolState.encrypt;
			string username = this.username;
			Console.WriteLine("Minecraft username: {0}",username);
			context.username = username;
			MinecraftStreamWriter mcsw = context.writer;

			byte[] mcvt = new byte[32];
			RandomNumberGenerator.Fill(mcvt);
			context.minecraftVerifyToken = mcvt;


			Span<byte> mineCryptBuf = stackalloc byte[609];

			mineCryptBuf[0] = 223;
			mineCryptBuf[1] = 4;
			mineCryptBuf[2] = 1;

			if (MinecraftProtocolConstantSizeFastSerializer<MinecraftEncryptionStartPacket>.fastSerializeDelegate(mineCryptBuf.Slice(3), new((context.keypair ?? throw new Exception("Where is my RSA key? (should not reach here)")).ExportSubjectPublicKeyInfo(), mcvt)) != 606)
			{
				throw new Exception("Encryption request packet must be exactly 606 bytes long");
			}
			
			lock (mcsw.syncLock)
			{
				mcsw.Write(mineCryptBuf);
				mcsw.Flush();
			}

			context.SetOverride(MinecraftListener.StaticHandler<MinecraftTeleportConfirm>, 0);
		}
	}
	[StructLayout(LayoutKind.Sequential)]
	internal sealed class MinecraftTeleportConfirm : IMinecraftPacket
	{
		[VariableSize] private int teleportId;

		public void Handle(MinecraftClientContext minecraftContext)
		{
			
		}
	}
	[StructLayout(LayoutKind.Sequential)] [MinecraftStandardPacketBinding(0x2C)]
	internal sealed class MinecraftAnimatePacket : IMinecraftPacket
	{
		[VariableSize] private int thing;

		public void Handle(MinecraftClientContext minecraftContext)
		{

		}
	}
	[StructLayout(LayoutKind.Sequential)]
	public sealed class MinecraftEncryptionStartPacket : IMinecraftSerializable{
		[LetDecoderDecideSize(20)]
		private readonly string serverId = "                    ";
		[LetDecoderDecideSize(550)]
		private readonly byte[] publicKey;
		[LetDecoderDecideSize(32)]
		private readonly byte[] verifyToken;


		public MinecraftEncryptionStartPacket(byte[] publicKey, byte[] verifyToken)
		{
			this.publicKey = publicKey;
			this.verifyToken = verifyToken;
		}
	}
	[MinecraftStandardPacketBinding(0x01)][StructLayout(LayoutKind.Sequential)]
	internal sealed class MinecraftEncryptionResponse : IMinecraftPacket{
		
		[LetDecoderDecideSize(512)]
		private byte[] sharedSecret;
		[LetDecoderDecideSize(512)]
		private byte[] verifyToken;


		public void Handle(MinecraftClientContext context)
		{
			if (context.state != MinecraftProtocolState.encrypt) throw new Exception("Unexpected client encryption response outside encrypt state");
			Console.WriteLine("Encryption successfully enabled!");
			byte[] decrypted = (context.keypair ?? throw new Exception("Encryption is already enabled for this connection")).Decrypt(sharedSecret, RSAEncryptionPadding.Pkcs1);
			if (decrypted.Length != 16) throw new Exception("Shared secret must be exactly 16 bytes long");
			context.notifyEncryptionEnable = decrypted;

			return;
		}
	}
	[StructLayout(LayoutKind.Sequential)] [MinecraftPacketPrefix(2)]
	public sealed class MinecraftConnectionSuccessSimple : IMinecraftSerializable{
		private readonly UUID uuid;
		[LetDecoderDecideSize(16)]
		private readonly string username;

		//[VariableSize]
		//private readonly int properties = 0;
		//Skins will be implemented later!
		
		public MinecraftConnectionSuccessSimple(UUID uuid, string username)
		{
			this.uuid = uuid;
			this.username = username;
		}
	}
	[StructLayout(LayoutKind.Sequential)]
	[MinecraftPacketPrefix(0)]
	public sealed class MinecraftDisconnect : IMinecraftSerializable
	{
		[LetDecoderDecideSize(65536)]
		private readonly string reason;

		public MinecraftDisconnect(string reason)
		{
			this.reason = reason;
		}
	}

	[MinecraftStandardPacketBinding(0x05)]
	[StructLayout(LayoutKind.Sequential)]
	internal sealed class MinecraftClientSettings : IMinecraftPacket{
		[LetDecoderDecideSize(16)] private string locale;
		private byte renderDistance;
		[VariableSize] private int chatMode;

		private bool chatColors;
		private byte displaySkinParts;
		[VariableSize] int mainHand;
		public void Handle(MinecraftClientContext minecraftContext)
		{
			
		}
	}
	[MinecraftStandardPacketBinding(11)][StructLayout(LayoutKind.Sequential)]
	internal sealed class PluginMessage : IMinecraftPacket{
		public string identifier;
		public byte[] bytes;
		public void Handle(MinecraftClientContext minecraftContext)
		{
			
		}
	}
	[StructLayout(LayoutKind.Sequential)][MinecraftPacketPrefix(0x34)]
	internal sealed class MinecraftPlayerTeleport : IMinecraftSerializable{
		private readonly double x;
		private readonly double y;
		private readonly double z;
		private readonly float yaw;
		private readonly float pitch;
		private readonly byte flags = 0;
		[VariableSize]
		private readonly int teleportId;

		public MinecraftPlayerTeleport(double x, double y, double z, float yaw, float pitch,byte flags, int teleportId)
		{
			this.x = x;
			this.y = y;
			this.z = z;
			this.yaw = yaw;
			this.pitch = pitch;
			this.teleportId = teleportId;
		}
	}
	[StructLayout(LayoutKind.Sequential)][MinecraftStandardPacketBinding(0x13)]
	internal sealed class ServerboundTeleportPacket : IMinecraftPacket{
		private double x;
		private double y;
		private double z;
		private float yaw;
		private float pitch;
		private bool onGround;

		public void Handle(MinecraftClientContext minecraftContext)
		{			
			minecraftContext.position = new Position(x, y, z);
		}
	}
	[StructLayout(LayoutKind.Sequential)]
	[MinecraftStandardPacketBinding(0x15)]
	internal sealed class MinecraftPlayerMovement : IMinecraftPacket
	{

		private bool onGround;

		public void Handle(MinecraftClientContext minecraftContext)
		{
			
		}
	}
	[StructLayout(LayoutKind.Sequential)]
	[MinecraftStandardPacketBinding(0x10)]
	internal sealed class MinecraftServerboundKeepalive : IMinecraftPacket
	{

		private long l;

		public void Handle(MinecraftClientContext minecraftContext)
		{

		}
	}
	[StructLayout(LayoutKind.Sequential)]
	[MinecraftStandardPacketBinding(0x1c)]
	internal sealed class EntityAction : IMinecraftPacket
	{

		[VariableSize]
		private int a;
		[VariableSize]
		private int b;
		[VariableSize]
		private int c;

		public void Handle(MinecraftClientContext minecraftContext)
		{

		}
	}
	[StructLayout(LayoutKind.Sequential)]
	[MinecraftStandardPacketBinding(0x14)]
	internal sealed class MinecraftPlayerRotation : IMinecraftPacket
	{
		private float yaw;
		private float pitch;
		private bool onGround;

		public void Handle(MinecraftClientContext minecraftContext)
		{

		}
	}
	[StructLayout(LayoutKind.Sequential)]
	[MinecraftStandardPacketBinding(0x1A)]
	internal sealed class MinecraftPlayerAbilities : IMinecraftPacket
	{
		private bool flags;

		public void Handle(MinecraftClientContext minecraftContext)
		{

		}
	}
	[StructLayout(LayoutKind.Sequential)]
	[MinecraftStandardPacketBinding(0x12)]
	internal sealed class MinecraftPlayerPosition : IMinecraftPacket
	{
		private double x;
		private double y;
		private double z;
		private bool onGround;

		public void Handle(MinecraftClientContext minecraftContext)
		{
			minecraftContext.position = new Position(x, y, z);
		}
	}
	[StructLayout(LayoutKind.Sequential)][MinecraftPacketPrefix(0x1D)]
	internal sealed class MinecraftEventPacket : IMinecraftSerializable
	{
		private readonly byte e;
		private readonly float extraData;

		public MinecraftEventPacket(byte e, float extraData)
		{
			this.e = e;
			this.extraData = extraData;
		}
	}
	[StructLayout(LayoutKind.Sequential)] [MinecraftPacketPrefix(0x1C)]
	internal sealed class ChunkUnloadPacket : IMinecraftSerializable{
		private readonly int x;
		private readonly int z;

		public ChunkUnloadPacket(int x, int z)
		{
			this.x = x;
			this.z = z;
		}
	}
	[StructLayout(LayoutKind.Sequential)]
	[MinecraftPacketPrefix(0x40)]
	internal sealed class MinecraftUpdateViewPosition : IMinecraftSerializable
	{
		[VariableSize]
		private readonly int x;
		[VariableSize]
		private readonly int z;

		public MinecraftUpdateViewPosition(int x, int z)
		{
			this.x = x;
			this.z = z;
		}
	}
}
