using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DotMinecraft
{
	public enum GameMode : byte{
		Survival, Creative, Adventure, Spectator
	}
	public static partial class DefaultStartPacketBuilder
	{
		private const int size = 30848;
		private const int centerStart = 8;
		private const int centerSize = 30826;


		private const int EIDOffset = 1;
		private const int hardcoreOffset = 5;
		private const int gamemodeOffset = 6;
		private const int prevGamemodeOffset = 7;

		private const int hashedSeedOffset = 0x00007873;

		//NOTE: this is permitted for as long as render distance < 128 chunks
		//Because then the render distance takes only a single-byte varint to encode
		private const int maxPlayersOffset = 0x0000787C;
		private const int renderDistanceOffset = 0x0000787B;
		private const int reduceDebugInfoOffset = 0x0000787C;
		private const int enableRespawnScreenOffset = 0x0000787D;
		private const int isDebugOffset = 0x0000787E;
		private const int isFlatOffset = 0x0000787F;

		private const byte one = 1;
		private const byte zero = 0;
		
		public static void WriteCompressedDefaultStartPacket(MinecraftClientMailboxThread mailbox, int eid, bool hardcore, GameMode gameMode, ulong hashedSeed, int renderDistance, bool reduceDebugInfo, bool enableRespawnScreen, bool isDebug, bool isFlat){
			if (renderDistance < 0 | renderDistance > 127) throw new Exception("render distance out of range!");
			Span<byte> dsp = stackalloc byte[size];
			dsp[0] = 0x24;

			Span<byte> ss1 = dsp.Slice(EIDOffset, 4);
			MemoryMarshal.Cast<byte, int>(ss1)[0] = eid;
			ss1.Reverse();
			dsp[hardcoreOffset] = hardcore ? one : zero;
			byte bgm = (byte)gameMode;
			dsp[gamemodeOffset] = bgm;

			//NOTE: We do not track prevGamemode information (yet)
			dsp[prevGamemodeOffset] = 0xff;

			//NOTE: endianess does not matter since hashedSeed is a random variable used exclusively by the client
			//MemoryMarshal.Cast<byte, ulong>(dsp.Slice(hashedSeedOffset, 8))[0] = hashedSeed;
			
			center.CopyTo(dsp.Slice(centerStart));
			
			dsp[maxPlayersOffset] = 10;
			dsp[renderDistanceOffset] = (byte)renderDistance;
			dsp[reduceDebugInfoOffset] = reduceDebugInfo ? one : zero;
			dsp[enableRespawnScreenOffset] = enableRespawnScreen ? one : zero;
			dsp[isDebugOffset] = isDebug ? one : zero;
			dsp[isFlatOffset] = isFlat ? one : zero;
			
			


			byte[] trybuffer = new byte[8192];
			trybuffer[2] = 0x80;
			trybuffer[3] = 0xF1;
			trybuffer[4] = 0x01;
			int zp;
			using (MemoryStream ms = new MemoryStream(trybuffer, 5, 8187, true, false)){
				using(ZLibStream zls = new ZLibStream(ms, CompressionLevel.Optimal, true)){
					zls.Write(dsp);
					zls.Flush();
				}
				zp = (int)ms.Position;
				if (zp < 125) throw new Exception("Compressed start packet too small (should not reach here)!");
				zp += 3;
				trybuffer[0] = (byte)(((zp & 0x7f) | 0x80));
				trybuffer[1] = (byte)((zp >>> 7));
			}
			mailbox.SendDataAsync(trybuffer.AsMemory(0, zp + 2));
		}

	}
}
