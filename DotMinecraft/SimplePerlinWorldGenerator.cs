using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Threading.Tasks;

namespace DotMinecraft
{
	public sealed class SimplePerlinWorldGenerator : IParallelMinecraftWorldGenerator
	{
		private static readonly Vector512<float> octaves = Vector512.Create(new float[] { 1.0f, 2.0f, 4.0f, 8.0f, 16.0f, 32.0f, 64.0f, 128.0f, 256.0f, 512.0f, 1024.0f, 2048.0f, 4096.0f, 8192.0f, 16384.0f, 32768.0f, 65536.0f});
		public static float Perlin16Octaves(float x, float y, Vector512<int> seed){
			Vector512<float> v512 = octaves;

			Vector512<float> x1 = Avx512F.Multiply(v512, Vector512.CreateScalar(x));
			Vector512<float> y1 = Avx512F.Multiply(v512, Vector512.CreateScalar(y));
			return Reduce(Avx512F.Divide(Perlin(x1,y1,seed), v512));

		}
		public static float Reduce(Vector512<float> v){
			Vector256<float> v256 = Avx.Add(Vector512.GetLower(v), Vector512.GetUpper(v));
			Vector128<float> v128 = Avx.Add(Vector256.GetLower(v256), Vector256.GetUpper(v256));
			Vector64<float> v64 = Vector64.Add(Vector128.GetLower(v128), Vector128.GetUpper(v128));
			return v64[0] + v64[1];
		}


		public static Vector512<float> Perlin(Vector512<float> x, Vector512<float> y, Vector512<int> seed)
		{
			// Floor values to get integer grid cell coordinates
			Vector512<float> xf = Floor(x);
			Vector512<float> yf = Floor(y);

			Vector512<int> xi = Avx512F.ConvertToVector512Int32(xf);
			Vector512<int> yi = Avx512F.ConvertToVector512Int32(yf);

			// Relative positions inside the grid cell
			Vector512<float> relx = Avx512F.Subtract(x, xf);
			Vector512<float> rely = Avx512F.Subtract(y, yf);

			// Interpolation weights (smoothed)
			Vector512<float> u = Smootherstep(relx);
			Vector512<float> v = Smootherstep(rely);

			// Grid corner coordinates
			Vector512<int> onei = Vector512.CreateScalar<int>(1);
			Vector512<int> xi1 = Avx512F.Add(xi, onei);
			Vector512<int> yi1 = Avx512F.Add(yi, onei);

			// Compute dot products at each corner
			Vector512<float> one = Vector512.CreateScalar(1.0f);
			Vector512<float> rx1 = Vector512.Subtract(relx, one);
			Vector512<float> ry1 = Vector512.Subtract(rely, one);

			Vector512<float> bottomLeft = DotGridVector(xi, yi, seed, relx, rely);
			Vector512<float> bottomRight = DotGridVector(xi1, yi, seed, rx1, rely);
			Vector512<float> topLeft = DotGridVector(xi, yi1, seed, relx, ry1);
			Vector512<float> topRight = DotGridVector(xi1, yi1, seed, rx1, ry1);

			// Interpolate horizontally
			Vector512<float> ix0 = Lerp(u, bottomLeft, bottomRight);
			Vector512<float> ix1 = Lerp(u, topLeft, topRight);

			// Interpolate vertically
			return Lerp(v, ix0, ix1);
		}

		private static Vector512<float> Lerp(Vector512<float> a, Vector512<float> b, Vector512<float> c){
			return Avx512F.FusedMultiplyAdd(a, Avx512F.Subtract(c,b), b);
		}
		private static Vector512<float> Smootherstep(Vector512<float> x)
		{
			// Compute powers
			var x2 = Avx512F.Multiply(x, x);               // x^2
			var x3 = Avx512F.Multiply(x2, x);              // x^3

			// 6.0f * x
			var six = Vector512.CreateScalar(6.0f);
			var sixX = Avx512F.Multiply(six, x);           // 6x

			// 6x - 15.0f
			var fifteen = Vector512.CreateScalar(15.0f);
			var inner1 = Avx512F.Subtract(sixX, fifteen);  // 6x - 15

			// x * (6x - 15)
			var inner2 = Avx512F.Multiply(x, inner1);      // x*(6x - 15)

			// +10.0f
			var ten = Vector512.CreateScalar(10.0f);
			var inner3 = Avx512F.Add(inner2, ten);         // x*(6x - 15) + 10

			// Final multiplication
			var result = Avx512F.Multiply(x3, inner3);     // x^3 * (...)

			return result;
		}
		private static Vector512<float> Floor(Vector512<float> v){
			return Vector512.Create(Avx512F.Floor(Vector512.GetLower(v)), Avx512F.Floor(Vector512.GetUpper(v)));
		}
		private static Vector512<float> Ceil(Vector512<float> v)
		{
			return Vector512.Create(Avx512F.Ceiling(Vector512.GetLower(v)), Avx512F.Ceiling(Vector512.GetUpper(v)));
		}
		private static (Vector512<float>,Vector512<float>) RandomVectorSlow(Vector512<float> x){
			x = Avx512F.Multiply(x, Vector512.CreateScalar(2.0f * float.Pi));
			Span<float> span1 = stackalloc float[32];
			for(int i = 0; i < 16; ++i){
				float x1 = x[i];
				span1[i] = MathF.Sin(x1);
				span1[i + 16] = MathF.Cos(x1);
			}
			Span<Vector512<float>> span2 = MemoryMarshal.Cast<float, Vector512<float>>(span1);
			return (span2[0],span2[1]);
		}
		private static Vector512<float> DotGridVector(Vector512<int> x, Vector512<int> y, Vector512<int> seed, Vector512<float> relx, Vector512<float> rely)
		{
			Vector512<int> v512 = Avx512F.Xor(seed, x);
			v512 = XorShiftEnch(v512);
			v512 = Avx512F.Xor(v512,y);
			v512 = XorShiftEnch(v512);



			(Vector512<float> x1, Vector512<float> y1) = RandomVectorSlow(FromRandInts(v512));

			return Avx512F.FusedMultiplyAdd(x1, relx, Avx512F.Multiply(y1, rely));
		}
		private static Vector512<float> FromRandInts(Vector512<int> i){
			return Avx512F.Subtract(Vector512.As<int,float>(Avx512F.And(Avx512F.Or(i, Vector512.CreateScalar(0x3F800000)), Vector512.CreateScalar(0x3FFFFFFC))), Vector512.CreateScalar<float>(1.0f));
		}
		private static Vector512<int> XorShift(Vector512<int> x){
			x = Avx512F.Xor(x, Avx512F.ShiftLeftLogical(x, 13));
			x = Avx512F.Xor(x, Avx512F.ShiftRightLogical(x, 17));
			return Avx512F.Xor(x, Avx512F.ShiftLeftLogical(x, 5));
		}
		private static Vector512<int> XorShiftEnch(Vector512<int> x){
			x = XorShift(x);

			//NOTE: we enhance the randomness of XorShift with a random logical shift operation.
			x = Avx512F.ShiftRightArithmeticVariable(x, Vector512.As<int, uint>(Avx512F.ShiftRightLogical(Avx512F.ShiftLeftLogical(x, 27), 27)));
			return XorShift(x);
		}
		private static readonly Vector512<int> hardcoded_test_seed = MemoryMarshal.Cast<byte, Vector512<int>>(new byte[] { 0x3a, 0x6e, 0x92, 0x9f, 0x0f, 0x45, 0xa8, 0xcc, 0xd5, 0x51, 0x6f, 0xd4, 0x39, 0xd1, 0x98, 0x01, 0xbf, 0x0f, 0xf7, 0x7b, 0x03, 0x59, 0xb2, 0xad, 0xfa, 0x27, 0x14, 0x25, 0x96, 0x4e, 0xc6, 0x3e, 0x21, 0x8f, 0xca, 0x3d, 0x12, 0x08, 0xf8, 0xff, 0xd2, 0x2e, 0x99, 0xe2, 0x37, 0x28, 0x13, 0x2c, 0x2b, 0x4f, 0xfb, 0xb8, 0x17, 0xd5, 0x26, 0xdf, 0x69, 0x25, 0xb4, 0xeb, 0xf1, 0x24, 0xf8, 0x79 })[0];
		private static void GenerateImpl(Vector512<int> seed, IMinecraftWorldView minecraftWorldView, Coordinate2d coordinate2D){
			int xbound = coordinate2D.x * 16;
			int zbound = coordinate2D.z * 16;

			FlatMinecraftBlock dirt = new FlatMinecraftBlock(10, null);

			for(int x = 0; x < 16; ++x) {
				float xb1 = (x + xbound) / 16.0f;
				for (int z = 0; z < 16; ++z){
					int height = Math.Clamp(64 + (int)Math.Round(Perlin16Octaves(xb1, (z + zbound) / 16.0f, seed) * 32.0f),0,255);
					for(int y = 0; y < height; ++y){
						minecraftWorldView.WriteBlock(new PreciseCoordinate(x, y, z), dirt);
					}
					for (int y = height; y < 256; ++y)
					{
						minecraftWorldView.GetLightData(new PreciseCoordinate(x, y, z),0) = 240;
					}
				}
			}
			
		}
		public ChunkData? GenerateHighestParallelStage(Coordinate2d coordinate2D)
		{
			ChunkData chunkData = new ChunkData(255, new ushort[65536], new byte[65536], new());
			GenerateImpl(hardcoded_test_seed,chunkData, coordinate2D);
			return chunkData;
		}

		public bool QueryDeferralPolicy(Coordinate2d coordinate2D, byte generationStage)
		{
			return false;
		}

		public void AdvanceGenerationStage(IMinecraftWorldView minecraftWorldView, Coordinate2d coordinate2D, ref byte generationStage)
		{
			if (generationStage > 0) throw new Exception("Simple perlin noise generator only supports single generation stage");
			GenerateImpl(hardcoded_test_seed, minecraftWorldView, coordinate2D);
			generationStage = 255;
		}

		public int GetSurroundingMaturityRequirementSize(byte generationStage)
		{
			return 0;
		}
	}
}
