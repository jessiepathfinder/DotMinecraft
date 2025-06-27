using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using DotMinecraft.Schema;

namespace DotMinecraft.Schema{
	[AttributeUsage(AttributeTargets.Field)]
	public sealed class VariableSize : Attribute{
		
	}
	[AttributeUsage(AttributeTargets.Field)]
	public sealed class LetDecoderDecideSize : Attribute
	{
		public readonly int sizeLimit;

		public LetDecoderDecideSize(int sizeLimit)
		{
			this.sizeLimit = sizeLimit;
		}
	}

	[AttributeUsage(AttributeTargets.Field)]
	public sealed class NonMinecraftField : Attribute
	{

	}
	[AttributeUsage(AttributeTargets.Field)]
	public sealed class VariableSizeArray : Attribute
	{
		public readonly string methodName;
		public VariableSizeArray(string methodName)
		{
			this.methodName = methodName ?? throw new ArgumentNullException(nameof(methodName));
		}
	}
	public interface IMinecraftDeserializable{
	
	}
	public interface IMinecraftPacket : IMinecraftDeserializable{
		public void Handle(MinecraftClientContext minecraftContext);
	}
	[StructLayout(LayoutKind.Explicit)]
	public readonly struct UUID{
		[FieldOffset(0)]
		public readonly ulong minor;
		[FieldOffset(8)]
		public readonly ulong major;

		public UUID(ulong minor, ulong major)
		{
			this.minor = minor;
			this.major = major;
		}
	}
}

namespace DotMinecraft
{
	public static class MinecraftProtocolDeserializer
	{
		public static T ReadNextPacket<T>(MinecraftProtocolDecoder minecraftProtocolDecoder) where T : IMinecraftDeserializable, new() {
			T t = new T();
			ReadNextPacketImpl(minecraftProtocolDecoder, typeof(T), t);
			return t;
		}
		private static ConstructorInfo GetConstructorImpl(Type type) {
			if (!type.IsAssignableTo(typeof(IMinecraftDeserializable)))
			{
				throw new Exception("Deserialization to a non-Minecraft type");
			}
			return type.GetConstructor(Array.Empty<Type>()) ?? throw new Exception("Minecraft type does not have constructor");
		}
		public static object ReadNextPacket(MinecraftProtocolDecoder minecraftProtocolDecoder, Type type)
		{
			return ReadNextPacketImpl(minecraftProtocolDecoder, type, GetConstructorImpl(type));
		}
		private static object ReadNextPacketImpl(MinecraftProtocolDecoder minecraftProtocolDecoder, Type type, ConstructorInfo constructorInfo)
		{

			object obj = constructorInfo.Invoke(null);
			ReadNextPacketImpl(minecraftProtocolDecoder, type, obj);
			return obj;
		}
		private static void GeneralReadUnmanaged<T>(MinecraftProtocolDecoder minecraftProtocolDecoder, object arr) where T : unmanaged {
			Span<T> span = ((T[])arr).AsSpan();
			minecraftProtocolDecoder.ReadAtLeastOrThrow(MemoryMarshal.AsBytes(span));
			if (typeof(T) == typeof(byte)) return;
			int sl = span.Length;
			for (int i = 0; i < sl; ++i) {
				MemoryMarshal.AsBytes(span.Slice(i, 1)).Reverse();
			}
		}
		private static readonly MethodInfo gruMethod = typeof(MinecraftProtocolDeserializer).GetMethod("GeneralReadUnmanaged", BindingFlags.Static | BindingFlags.NonPublic, new Type[] { typeof(MinecraftProtocolDecoder), typeof(object) }) ?? throw new Exception("Unable to get GeneralReadUnmanaged method (should not reach here)");
		private static readonly ConcurrentDictionary<Type, FieldInfo[]> sortedFieldInfos = new();
		private static void ReadNextPacketImpl(MinecraftProtocolDecoder minecraftProtocolDecoder, Type type, object obj){
			MethodInfo gruMethod = MinecraftProtocolDeserializer.gruMethod;

			ConcurrentDictionary<Type, FieldInfo[]> sortedFieldInfos = MinecraftProtocolDeserializer.sortedFieldInfos;
			if (sortedFieldInfos.TryGetValue(type, out FieldInfo[]? fieldInfos)){
				if (fieldInfos is null) throw new Exception("Unexpected null field info array (should not reach here)");
			} else{
				fieldInfos = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
				fieldInfos.AsSpan().Sort((FieldInfo x, FieldInfo y) => {
					int a = Marshal.OffsetOf(type, x.Name).ToInt32();
					int b = Marshal.OffsetOf(type, y.Name).ToInt32();
					return a - b;
				});
				sortedFieldInfos.TryAdd(type, fieldInfos);
			}

			
			for (int i = 0, stop = fieldInfos.Length; i < stop; ++i){
				FieldInfo field = fieldInfos[i];
				if(field.IsStatic){
					continue;
				}
				if (field.IsInitOnly)
				{
					continue;
				}
				if (field.GetCustomAttribute<NonMinecraftField>() is { })
				{
					continue;
				}
				Type fieldType = field.FieldType;
				object value;
					
				
				if (fieldType == typeof(int)){
					if (field.GetCustomAttribute(typeof(VariableSize)) is null){
						value = minecraftProtocolDecoder.ReadUnmanagedBigEndian<int>();
					} else{
						value = minecraftProtocolDecoder.ReadVarInt();
					}
				}
				else if (fieldType == typeof(long))
				{
					if (field.GetCustomAttribute(typeof(VariableSize)) is null)
					{
						value = minecraftProtocolDecoder.ReadUnmanagedBigEndian<long>();
					}
					else
					{
						value = minecraftProtocolDecoder.ReadVarLong();
					}
				}
				else if (fieldType == typeof(byte))
				{
					value = minecraftProtocolDecoder.ReadUnsignedByte();
				}
				else if (fieldType == typeof(bool))
				{
					value = minecraftProtocolDecoder.ReadUnsignedByte() != 0;
				}
				else if (fieldType == typeof(sbyte))
				{
					value = minecraftProtocolDecoder.ReadByte();
				}
				else if (fieldType == typeof(short))
				{
					value = minecraftProtocolDecoder.ReadUnmanagedBigEndian<short>();
				}
				else if (fieldType == typeof(ushort))
				{
					value = minecraftProtocolDecoder.ReadUnmanagedBigEndian<ushort>();
				}
				else if (fieldType == typeof(uint))
				{
					value = minecraftProtocolDecoder.ReadUnmanagedBigEndian<uint>();
				}
				else if (fieldType == typeof(ulong))
				{
					value = minecraftProtocolDecoder.ReadUnmanagedBigEndian<ulong>();
				}
				else if (fieldType == typeof(double))
				{
					value = minecraftProtocolDecoder.ReadUnmanagedBigEndian<double>();
				}
				else if (fieldType == typeof(float))
				{
					value = minecraftProtocolDecoder.ReadUnmanagedBigEndian<float>();
				}
				else if(fieldType == typeof(string)){
					LetDecoderDecideSize? letDecoderDecideSize = field.GetCustomAttribute<LetDecoderDecideSize>();
					int sl = letDecoderDecideSize is null ? MinecraftProtocolDecoder.MAX_RECEIVE_STRING_SIZE : letDecoderDecideSize.sizeLimit;
					value = minecraftProtocolDecoder.ReadString();
				}
				else if (fieldType == typeof(byte[]))
				{
					LetDecoderDecideSize? letDecoderDecideSize = field.GetCustomAttribute<LetDecoderDecideSize>();
					int sl = letDecoderDecideSize is null ? MinecraftProtocolDecoder.MAX_RECEIVE_STRING_SIZE : letDecoderDecideSize.sizeLimit;
					value = minecraftProtocolDecoder.ReadRawString();
				}
				else if (fieldType == typeof(UUID))
				{
					ulong u1 = minecraftProtocolDecoder.ReadUnmanagedBigEndian<ulong>();
					ulong u2 = minecraftProtocolDecoder.ReadUnmanagedBigEndian<ulong>();
					value = new UUID(u1, u2);
				}
				else
				{
					if(fieldType.IsArray){
						Type underlyingType = fieldType.GetElementType() ?? throw new Exception("Array type has no element type (should not reach here)");
						

						
						string variableSizeMethodName = (field.GetCustomAttribute<VariableSizeArray>() ?? throw new Exception("Array element missing variable-size handler")).methodName;
						int size = (int)((type.GetMethod(variableSizeMethodName, BindingFlags.Public, Array.Empty<Type>()) ?? throw new Exception("Array element size method not defined")).Invoke(obj, null) ?? throw new Exception("Array element size method returned null value"));
						
						if (size == 0){
							value = Array.CreateInstance(underlyingType, size);
							goto donothing1;
						}
						if (underlyingType.IsPrimitive)
						{
							if(field.GetCustomAttribute(typeof(LetDecoderDecideSize)) is LetDecoderDecideSize letDecoderDecideSize){
								if (underlyingType != typeof(byte)) throw new Exception("LetDecoderDecideSize is only valid for byte arrays and strings");
								value = minecraftProtocolDecoder.ReadRawString(letDecoderDecideSize.sizeLimit);
							} else if (field.GetCustomAttribute(typeof(VariableSize)) is null)
							{
								value = Array.CreateInstance(underlyingType, size);
								int sizemul = Marshal.SizeOf(underlyingType);
								Array.CreateInstance(underlyingType, size);
								gruMethod.MakeGenericMethod(underlyingType).Invoke(null, new object[]{minecraftProtocolDecoder, value});
							} else if (underlyingType == typeof(int))
							{
								
								int[] intarr = new int[size];
								for (int z = 0; z < size; ++z)
								{
									intarr[z] = minecraftProtocolDecoder.ReadVarInt();
								}
								value = intarr;
							}
							else if (underlyingType == typeof(long))
							{
								long[] intarr = new long[size];
								for (int z = 0; z < size; ++z)
								{
									intarr[z] = minecraftProtocolDecoder.ReadVarLong();
								}
								value = intarr;
							}
							else
							{
								throw new Exception("Attempted to read variable-size type that is not int or long");
							}

						} else{
							ConstructorInfo constructorInfo = GetConstructorImpl(underlyingType);
							value = Array.CreateInstance(underlyingType, size);
							object[] thearr = (object[])value;
							for (int z = 0; z < size; ++z)
							{
								thearr[z] = ReadNextPacketImpl(minecraftProtocolDecoder, underlyingType, constructorInfo);
							}
						}
					donothing1:;

					}
					value = ReadNextPacket(minecraftProtocolDecoder,fieldType);
				}
				field.SetValue(obj, value);
			}
		}
	}
}
