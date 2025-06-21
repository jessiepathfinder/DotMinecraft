using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DotMinecraft
{
	public static class MinecraftNBTSerializationEngine
	{
		private static void WriteBigEndian<T>(Span<byte> span, T val) where T : unmanaged {
			Span<byte> bytes = MemoryMarshal.AsBytes(new Span<T>(ref val));
			for(int i = 0, stop = bytes.Length, sm1 = stop - 1; i < stop;++i){
				span[i] = bytes[sm1 - i];
			}
		}
		private static void SerializeCompoundImpl(Stream stream, IEnumerable<KeyValuePair<string, object>> dict, string compoundName, Span<byte> mallocSpan, bool skipFirstByte){

			Encoding UTF8 = Encoding.UTF8;

			{
				int kl3 = (compoundName.Length * 3) + 3;
				Span<byte> malloc = kl3 > 4096 ? new byte[kl3] : mallocSpan;
				int slice1 = UTF8.GetBytes(compoundName, malloc.Slice(3));
				WriteBigEndian(malloc.Slice(1, 2), (ushort)slice1);
				if(skipFirstByte){
					stream.Write(malloc.Slice(1, slice1 + 2));
				} else{
					malloc[0] = 10;
					stream.Write(malloc.Slice(0, slice1 + 3));
				}
				
			}


			
			foreach (KeyValuePair<string, object> kvp in dict){
				
				string key = kvp.Key;
				object value = kvp.Value;
				if (value is IReadOnlyDictionary<string, object> rod)
				{
					SerializeCompoundImpl(stream, rod, key, mallocSpan, false);
				}
				int kl3 = (key.Length * 3) + 11;
				Span<byte> malloc = kl3 > 4096 ? new byte[kl3] : mallocSpan;
				int slice1 = UTF8.GetBytes(key, malloc.Slice(3));

				WriteBigEndian(malloc.Slice(1, 2), (ushort)slice1);
				slice1 += 3;

				
				if(value is sbyte a){
					malloc[0] = 1;
					WriteBigEndian(malloc.Slice(slice1),a);
					stream.Write(malloc.Slice(0, slice1 + 1));


				} else if(value is short b){
					malloc[0] = 2;
					WriteBigEndian(malloc.Slice(slice1), b);
					stream.Write(malloc.Slice(0, slice1 + 2));
				} else if(value is int c){
					malloc[0] = 3;
					WriteBigEndian(malloc.Slice(slice1), c);
					stream.Write(malloc.Slice(0, slice1 + 4));
				} else if(value is long d){
					malloc[0] = 4;
					WriteBigEndian(malloc.Slice(slice1), d);
					stream.Write(malloc.Slice(0, slice1 + 8));
				}
				else if (value is float e)
				{
					malloc[0] = 5;
					WriteBigEndian(malloc.Slice(slice1), e);
					stream.Write(malloc.Slice(0, slice1 + 4));
				}
				else if (value is double f)
				{
					malloc[0] = 6;
					WriteBigEndian(malloc.Slice(slice1), f);
					stream.Write(malloc.Slice(0, slice1 + 8));
				}
				else if(value is byte[] g){
					malloc[0] = 7;
					int gl = g.Length;
					WriteBigEndian(malloc.Slice(slice1), g.Length);
					stream.Write(malloc.Slice(0, slice1 + 4));
					stream.Write(g, 0, gl);
				}
				else if(value is string str){
					mallocSpan[0] = 8;
					stream.Write(malloc.Slice(0, slice1));
					int s3 = (str.Length * 3) + 2;

					if(malloc.Length < s3){
						malloc = new byte[s3];
					}
					int t = UTF8.GetBytes(str, malloc.Slice(2));
					WriteBigEndian(malloc, (ushort)t);
					stream.Write(malloc.Slice(0, t + 2));
				}
				//List data type partially supported
				else if(value is IReadOnlyList<IReadOnlyDictionary<string,object>> readOnlyDictionaries){
					malloc[0] = 9;
					malloc[slice1] = 10;
					int len = readOnlyDictionaries.Count;
					WriteBigEndian(malloc.Slice(slice1 + 1), len);
					stream.Write(malloc.Slice(0, slice1 + 5));
					for(int i = 0; i < len; ++i){
						SerializeCompoundImpl(stream, readOnlyDictionaries[i], "", mallocSpan, true);
					}
				}
				else if(value is int[] intarr){
					int l = intarr.Length;
					malloc[0] = 11;
					WriteBigEndian(malloc.Slice(0, slice1), l);
					stream.Write(malloc.Slice(0, slice1 + 4));
					Span<int> target;
					if (l > 1024){
						target = new int[l];
					} else{
						target = MemoryMarshal.Cast<byte, int>(malloc).Slice(0, l);
					}
					intarr.CopyTo(target);
					l *= 4;
					for(int i = 0; i < l; i += 4){
						malloc.Slice(i, 4).Reverse();
					}
				}
				else if (value is long[] longarr)
				{
					int l = longarr.Length;
					malloc[0] = 12;
					WriteBigEndian(malloc.Slice(0, slice1), l);
					stream.Write(malloc.Slice(0, slice1 + 4));
					Span<long> target;
					if (l > 512)
					{
						target = new long[l];
					}
					else
					{
						target = MemoryMarshal.Cast<byte, long>(malloc).Slice(0, l);
					}
					longarr.CopyTo(target);
					l *= 8;
					for (int i = 0; i < l; i += 8)
					{
						malloc.Slice(i, 8).Reverse();
					}
				} else{
					throw new Exception("Attempted to serialize invalid data type");
				}

			}
			mallocSpan[0] = 0;
			stream.Write(mallocSpan.Slice(0, 1));
		}
		public static void SerializeCompound(Stream stream, IEnumerable<KeyValuePair<string, object>> dict, string compoundName){
			Span<byte> mallocSpan = stackalloc byte[4096];
			SerializeCompoundImpl(stream, dict, compoundName, mallocSpan, false);
		}
	}
}
