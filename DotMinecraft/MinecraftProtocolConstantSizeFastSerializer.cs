using DotMinecraft.Schema;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Text;
using System.Threading.Tasks;

namespace DotMinecraft
{
	namespace Schema{
		[AttributeUsage(AttributeTargets.Class)]
		public sealed class MinecraftPacketPrefix : Attribute{
			public readonly int id;

			public MinecraftPacketPrefix(int id)
			{
				this.id = id;
			}
		}
	}
	public interface IMinecraftSerializable{
		
	}
	public static class StaticVariableSizeEncoders
	{
		private const int SEGMENT_BITS = 0x7F;
		private const int CONTINUE_BIT = 0x80;
		private const long SEGMENT_BITS_LONG = 0x7F;
		private const long CONTINUE_BIT_LONG = 0x80;
		public static (int val, int index) ReadVarInt(ReadOnlySpan<byte> span)
		{
			int value = 0;
			int position = 0;
			int i = 0;
			byte currentByte;

			while (true)
			{
				currentByte = span[i++];
				value |= (currentByte & SEGMENT_BITS) << position;

				if ((currentByte & CONTINUE_BIT) == 0) break;

				position += 7;

				if (position >= 32) throw new Exception("VarInt is too big");
			}

			return (value,i);
		}
		public static (long val, int index) ReadVarLong(ReadOnlySpan<byte> span)
		{
			long value = 0;
			int position = 0;
			int i = 0;
			byte currentByte;

			while (true)
			{
				currentByte = span[i++];
				value |= (currentByte & SEGMENT_BITS_LONG) << position;

				if ((currentByte & CONTINUE_BIT_LONG) == 0) break;

				position += 7;

				if (position >= 64) throw new Exception("VarLong is too big");
			}

			return (value, i);
		}
		public static int WriteVarInt(Span<byte> span, int value)
		{
			int i = 0;
			while (true)
			{
				if ((value & ~SEGMENT_BITS) == 0)
				{
					span[i++] = (byte)value;
					break;
				}

				span[i++] = (byte)((value & SEGMENT_BITS) | CONTINUE_BIT);

				// Note: >>> means that the sign bit is shifted with the rest of the number rather than being left alone
				value >>>= 7;
			}
			return i;
		}
		public static int WriteVarLong(Span<byte> span, long value)
		{
			int i = 0;
			while (true)
			{
				if ((value & ~SEGMENT_BITS_LONG) == 0)
				{
					span[i++] = (byte)value;
					break;
				}

				span[i++] = (byte)((value & SEGMENT_BITS_LONG) | CONTINUE_BIT_LONG);

				// Note: >>> means that the sign bit is shifted with the rest of the number rather than being left alone
				value >>>= 7;
			}
			return i;
		}
		public static void WriteUnmanagedBigEndian<T>(Span<byte> span, T value) where T : unmanaged{
			Span<byte> span2 = MemoryMarshal.AsBytes(new Span<T>(ref value));
			int len = span2.Length;
			int s = len - 1;
			for(int i = 0; i < len; ++i){
				span[i] = span2[s - i];
			}
		}
	}
	public static class MinecraftProtocolConstantSizeFastSerializer<T> where T : IMinecraftSerializable
	{
		public delegate int FastSerializeDelegate(Span<byte> buffer, T data);


		private static class WriteUnmanagedWrapper {
			private static void WriteByte(Span<byte> buffer, int counter, byte b)
			{
				buffer[counter] = b;
			}
			public static readonly MethodInfo writeByteMethod = ((Delegate)WriteByte).Method;
			private static void WriteUnmanaged<Z>(Span<byte> buffer, int counter, Z b) where Z : unmanaged
			{
				Span<Z> s = MemoryMarshal.Cast<byte, Z>(buffer.Slice(counter)).Slice(0,1);
				s[0] = b;
				MemoryMarshal.AsBytes(s).Reverse();
			}
			private static void WriteUUID(Span<byte> buffer, int counter, UUID b)
			{
				MemoryMarshal.Cast<byte,UUID>(buffer)[0] = b;
				buffer.Slice(counter, 8).Reverse();
				buffer.Slice(counter + 8, 8).Reverse();
			}
			public static readonly MethodInfo writeUnmanagedMethod = typeof(WriteUnmanagedWrapper).GetMethod("WriteUnmanaged", BindingFlags.NonPublic | BindingFlags.Static) ?? throw new Exception("Unable to reflectively access WriteUnmanaged (should not reach here)");
			public static readonly MethodInfo writeUUIDMethod = ((Delegate)WriteUUID).Method;
			private static int GetSizeHack<Z>() where Z : unmanaged
			{
				return MemoryMarshal.AsBytes(stackalloc Z[1]).Length;
			}
			public static readonly MethodInfo sizeHackMethod = typeof(WriteUnmanagedWrapper).GetMethod("GetSizeHack", BindingFlags.NonPublic | BindingFlags.Static) ?? throw new Exception("Unable to reflectively access GetSizeHack (should not reach here)");
			private const int SEGMENT_BITS = 0x7F;
			private const int CONTINUE_BIT = 0x80;
			private const long SEGMENT_BITS_LONG = 0x7F;
			private const long CONTINUE_BIT_LONG = 0x80;

			private static int WriteVarInt(Span<byte> span, int value, int i)
			{
				while (true)
				{
					if ((value & ~SEGMENT_BITS) == 0)
					{
						span[i++] = (byte)value;
						break;
					}

					span[i++] = (byte)((value & SEGMENT_BITS) | CONTINUE_BIT);

					// Note: >>> means that the sign bit is shifted with the rest of the number rather than being left alone
					value >>>= 7;
				}
				return i;
			}
			private static int WriteVarLong(Span<byte> span, long value, int i)
			{
				while (true)
				{
					if ((value & ~SEGMENT_BITS_LONG) == 0)
					{
						span[i++] = (byte)value;
						break;
					}

					span[i++] = (byte)((value & SEGMENT_BITS_LONG) | CONTINUE_BIT_LONG);

					// Note: >>> means that the sign bit is shifted with the rest of the number rather than being left alone
					value >>>= 7;
				}
				return i;
			}
			public static readonly MethodInfo writeVarIntMethod = ((Delegate)WriteVarInt).Method;
			public static readonly MethodInfo writeVarLongMethod = ((Delegate)WriteVarLong).Method;
			private static int SerializeByteArray(byte[] bytes, Span<byte> span, int i)
			{
				bytes.CopyTo(span.Slice(i));
				return i + bytes.Length;
			}
			public static readonly MethodInfo serializeByteArrayMethod = ((Delegate)SerializeByteArray).GetMethodInfo();
			public static readonly Type[] strtypes = new Type[] { typeof(string) };
			public static readonly PropertyInfo UTF8Field = typeof(Encoding).GetProperty("UTF8", BindingFlags.Public | BindingFlags.Static) ?? throw new Exception("Cannot reflectively access UTF8 encoder (should not reach here)");
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			private static void WriteSpan(Span<byte> span, int index, byte value){
				span[index] = value;
			}
			public static readonly MethodInfo writeSpanMethod = ((Delegate)WriteSpan).Method;
			public static readonly MethodInfo getBytes = typeof(Encoding).GetMethod("GetBytes", BindingFlags.Public | BindingFlags.Instance, strtypes) ?? throw new Exception("Unable to reflectively access GetBytes (should not reach here)");
		}
		private static int TryGetWorstCaseSize(FieldInfo fieldInfo) {
			LetDecoderDecideSize? letDecoderDecideSize = fieldInfo.GetCustomAttribute<LetDecoderDecideSize>();
			if (letDecoderDecideSize is null) throw new Exception("Maximum size limits are mandatory in fast on-stack serialization");
			return letDecoderDecideSize.sizeLimit;
		}

		

		private static (FastSerializeDelegate serializer, int worstCaseSize) Build() {
			int worstCounter = 0;
			Type type = typeof(T);
			FieldInfo[] fieldInfos = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

			fieldInfos.AsSpan().Sort((FieldInfo x, FieldInfo y) => {
				int a = Marshal.OffsetOf(type, x.Name).ToInt32();
				int b = Marshal.OffsetOf(type, y.Name).ToInt32();
				return a - b;
			});



			ParameterExpression inputExpr = Expression.Parameter(typeof(T));
			ParameterExpression spanExpr = Expression.Parameter(typeof(Span<byte>));
			ParameterExpression counterExpression = Expression.Variable(typeof(int));
			ParameterExpression? UTF8Encoder = null;
			List<Expression> expressions = new List<Expression>();
			List<ParameterExpression> variables = new List<ParameterExpression>();
			variables.Add(counterExpression);

			MethodInfo writeByteMethod = WriteUnmanagedWrapper.writeByteMethod;
			MethodInfo writeUnmanagedMethod = WriteUnmanagedWrapper.writeUnmanagedMethod;
			MethodInfo serializeByteArrayMethod = WriteUnmanagedWrapper.serializeByteArrayMethod;
			MethodInfo writeVarIntMethod = WriteUnmanagedWrapper.writeVarIntMethod;
			MethodInfo writeVarLongMethod = WriteUnmanagedWrapper.writeVarLongMethod;
			MethodInfo sizeHackMethod = WriteUnmanagedWrapper.sizeHackMethod;
			MethodInfo writeSpanMethod = WriteUnmanagedWrapper.writeSpanMethod;
			MethodInfo writeUUIDMethod = WriteUnmanagedWrapper.writeUUIDMethod;
			Type[] strtypes = WriteUnmanagedWrapper.strtypes;
			MethodInfo getBytes = WriteUnmanagedWrapper.getBytes;
			ConstructorInfo exceptionConstructorInfo = typeof(Exception).GetConstructor(strtypes) ?? throw new Exception("Exception does not have constructor (should not reach here)");

			for (int i = 0, stop = fieldInfos.Length; i < stop; ++i) {
				FieldInfo fieldInfo = fieldInfos[i];
				if (fieldInfo.GetCustomAttribute<NonMinecraftField>() is { }) continue;

				Type ftype = fieldInfo.FieldType;
				Expression fieldExpr = Expression.Field(inputExpr, fieldInfo);
				if (fieldInfo.GetCustomAttribute<VariableSize>() is { })
				{
					if (ftype == typeof(int)) {
						worstCounter += 5;
						expressions.Add(Expression.Assign(counterExpression, Expression.Call(writeVarIntMethod, spanExpr, fieldExpr, counterExpression)));
					} else if (ftype == typeof(long)) {
						worstCounter += 10;
						expressions.Add(Expression.Assign(counterExpression, Expression.Call(writeVarLongMethod, spanExpr, fieldExpr, counterExpression)));
					} else {
						throw new Exception("Only int and long can be variable-size");
					}
				}
				else if (ftype == typeof(bool))
				{
					expressions.Add(Expression.Call(writeSpanMethod,spanExpr, Expression.PostIncrementAssign(counterExpression), Expression.Condition(fieldExpr, Expression.Constant((byte)1), Expression.Constant((byte)0))));
					++worstCounter;
				}
				else if (ftype == typeof(byte)) {

					expressions.Add(Expression.Call(writeSpanMethod, spanExpr, Expression.PostIncrementAssign(counterExpression), fieldExpr));
					++worstCounter;
				}
				else if (ftype == typeof(byte[])) {
					int wcs = TryGetWorstCaseSize(fieldInfo);
					worstCounter += wcs + 5;
					ParameterExpression fieldExpr1 = Expression.Variable(typeof(byte[]));
					variables.Add(fieldExpr1);
					ParameterExpression fieldExpr2 = Expression.Variable(typeof(int));
					variables.Add(fieldExpr2);
					expressions.Add(Expression.Assign(fieldExpr1, fieldExpr));
					expressions.Add(Expression.Assign(fieldExpr2, Expression.ArrayLength(fieldExpr1)));
					expressions.Add(Expression.IfThen(Expression.GreaterThan(fieldExpr2, Expression.Constant(wcs)), Expression.Throw(Expression.New(exceptionConstructorInfo, Expression.Constant("Attempted to serialize field bigger than maximum size limit")))));
					expressions.Add(Expression.Assign(counterExpression, Expression.Call(writeVarIntMethod, spanExpr, fieldExpr2, counterExpression)));
					expressions.Add(Expression.Assign(counterExpression, Expression.Call(serializeByteArrayMethod, fieldExpr1, spanExpr, counterExpression)));
				}
				else if (ftype == typeof(string))
				{
					if(UTF8Encoder is null){
						UTF8Encoder = Expression.Parameter(typeof(Encoding));
						expressions.Add(Expression.Assign(UTF8Encoder, Expression.Property(null, WriteUnmanagedWrapper.UTF8Field)));
						variables.Add(UTF8Encoder);
					}
					int wcs = TryGetWorstCaseSize(fieldInfo) * 3;
					worstCounter += wcs + 5;
					ParameterExpression fieldExpr1 = Expression.Variable(typeof(byte[]));
					variables.Add(fieldExpr1);
					ParameterExpression fieldExpr2 = Expression.Variable(typeof(int));
					variables.Add(fieldExpr2);
					expressions.Add(Expression.Assign(fieldExpr1, Expression.Call(UTF8Encoder, getBytes, fieldExpr)));
					expressions.Add(Expression.Assign(fieldExpr2, Expression.ArrayLength(fieldExpr1)));
					expressions.Add(Expression.IfThen(Expression.GreaterThan(fieldExpr2, Expression.Constant(wcs)), Expression.Throw(Expression.New(exceptionConstructorInfo, Expression.Constant("Attempted to serialize field bigger than maximum size limit")))));
					expressions.Add(Expression.Assign(counterExpression, Expression.Call(writeVarIntMethod, spanExpr, fieldExpr2, counterExpression)));
					expressions.Add(Expression.Assign(counterExpression, Expression.Call(serializeByteArrayMethod, fieldExpr1, spanExpr, counterExpression)));
				}

				else if (ftype.IsPrimitive || ftype == typeof(UUID)) {
					int size1 = (int)(sizeHackMethod.MakeGenericMethod(ftype).Invoke(null, null) ?? throw new Exception("Size hack method failed to return (should not reach here)"));
					worstCounter += size1;
					expressions.Add(Expression.Call(writeUnmanagedMethod.MakeGenericMethod(ftype), spanExpr, counterExpression, fieldExpr));
					expressions.Add(Expression.AddAssign(counterExpression, Expression.Constant(size1)));
				}
				else if (ftype == typeof(UUID))
				{
					worstCounter += 16;
					expressions.Add(Expression.Call(writeUUIDMethod, spanExpr, counterExpression, fieldExpr));
					expressions.Add(Expression.AddAssign(counterExpression, Expression.Constant(16)));
				}
				else {
					throw new Exception("Attempted to compile Minecraft serializer for non-serializable class");
				}
			}
			LabelTarget returnTarget = Expression.Label(typeof(int));
			expressions.Add(Expression.Return(returnTarget, counterExpression));
			expressions.Add(Expression.Label(returnTarget,counterExpression));


			return (Expression.Lambda<FastSerializeDelegate>(Expression.Block(variables, expressions), spanExpr, inputExpr).Compile(), worstCounter);

		}
		public static readonly FastSerializeDelegate fastSerializeDelegate;
		public static readonly int worstCaseSize;
		static MinecraftProtocolConstantSizeFastSerializer()
		{
			(fastSerializeDelegate, worstCaseSize) = Build();
		}
	}
}
