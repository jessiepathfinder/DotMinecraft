using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DotMinecraft
{
	//Special thanks to StackOverflow: https://stackoverflow.com/a/34906004
	public sealed class JsonInternedStringConverter : JsonConverter
	{
		private JsonInternedStringConverter() { }
		public static readonly JsonInternedStringConverter instance = new();
		public override bool CanConvert(Type objectType)
		{
			return objectType == typeof(string);
		}

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
		{
			if (reader.TokenType == JsonToken.Null)
				return null;
			var s = reader.TokenType == JsonToken.String ? (string)reader.Value : (string)JToken.Load(reader); // Check is in case the value is a non-string literal such as an integer.
			return string.IsInterned(s) ?? s;
		}

		public override bool CanWrite { get { return false; } }

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
		{
			throw new NotImplementedException();
		}
	}
	[JsonObject(MemberSerialization.Fields, MissingMemberHandling = MissingMemberHandling.Ignore)]
	public sealed class MinecraftBlockData{
		public MinecraftBlockState[] states;
		public IReadOnlyDictionary<string, string[]>? properties;
	}

	[JsonObject(MemberSerialization.Fields, MissingMemberHandling = MissingMemberHandling.Ignore)]
	public sealed class MinecraftBlockState{
		public ushort id;
		[JsonProperty(PropertyName = "default")]
		public bool _default;
		
		public IReadOnlyDictionary<string, string>? properties;
	}
	public readonly struct MinecraftBlock{
		public readonly string blockType;
		public readonly IReadOnlyDictionary<string, object>? blockData;
		public readonly ushort blockId;

		public MinecraftBlock(string blockType, IReadOnlyDictionary<string, object>? blockData)
		{
			this.blockType = blockType;
			this.blockData = blockData;
		}
	}
	public static class MinecraftBlockFlattener
	{
		public static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> registeredDefaultStates;
		public static readonly ReadOnlyMemory<IReadOnlyDictionary<string, string>?> unflatteningPopulation;
		public static readonly ReadOnlyMemory<IReadOnlyDictionary<string, object>?> unflatteningPopulation1;
		private static readonly IReadOnlyDictionary<string, MinecraftBlockState[]> supportedBlockStates;
		public static readonly ReadOnlyMemory<string> blockNameMappings;
		private static readonly IReadOnlyDictionary<string,ushort> singleStateBlocks;
		static MinecraftBlockFlattener(){
			Dictionary<string, MinecraftBlockData> keyValuePairs = JsonConvert.DeserializeObject<Dictionary<string, MinecraftBlockData>>(HardcodedBlockReport.hardcodedBlockReport, new JsonSerializerSettings() { Converters = new[] { JsonInternedStringConverter.instance } }) ?? throw new Exception("Unexpected null block report (should not reach here)");


			Dictionary<string, IReadOnlyDictionary<string, string>> registeredDefaultStates = new();
			Dictionary<string, MinecraftBlockState[]> supportedBlockStates = new();
			Dictionary<string, ushort> singleStateBlocks = new();
			IReadOnlyDictionary<string, string>?[] unflatteningPopulation = new IReadOnlyDictionary<string, string>?[17112];
			IReadOnlyDictionary<string, object>?[] unflatteningPopulation1 = new IReadOnlyDictionary<string, object>?[17112];
			string[] blockNameMappings = new string[17112];


			foreach (KeyValuePair<string, MinecraftBlockData> keyValuePair in keyValuePairs){
				string blockName = keyValuePair.Key;
				MinecraftBlockData bd = keyValuePair.Value;
				MinecraftBlockState[] mcbs = bd.states;
				int limit = mcbs.Length;
				if(limit == 1){
					singleStateBlocks.Add(blockName, mcbs[0].id);
				}
				for (int i = 0; i < limit; ++i){
					MinecraftBlockState s = mcbs[i];
					blockNameMappings[s.id] = blockName;
					
					IReadOnlyDictionary<string, string>? props = s.properties;
					if(props is { }){
						unflatteningPopulation[s.id] = props;
						Dictionary<string,object> up1 = new Dictionary<string, object>(props.Count);
						foreach(KeyValuePair<string,string> kvp in props){
							up1.Add(kvp.Key, kvp.Value);
						}
						if (s._default)
						{
							registeredDefaultStates.Add(blockName, props);
						}
					}
					
				}
				supportedBlockStates.Add(blockName, mcbs);
			}

			MinecraftBlockFlattener.registeredDefaultStates = registeredDefaultStates;
			MinecraftBlockFlattener.unflatteningPopulation = unflatteningPopulation;
			MinecraftBlockFlattener.unflatteningPopulation1 = unflatteningPopulation1;
			MinecraftBlockFlattener.supportedBlockStates = supportedBlockStates;
			MinecraftBlockFlattener.blockNameMappings = blockNameMappings;
			MinecraftBlockFlattener.singleStateBlocks = singleStateBlocks;
		}

		public static MinecraftBlock SupplementBlockData(Dictionary<string,object>? obj, ushort blockId){
			IReadOnlyDictionary<string, string>? dict = unflatteningPopulation.Span[blockId];
			if (dict is null) return new MinecraftBlock(blockNameMappings.Span[blockId],obj);
			if(obj is null) obj = new Dictionary<string, object>();
			
			foreach(KeyValuePair<string, string> kvp in dict){
				obj[kvp.Key] = kvp.Value;
			}
			return new MinecraftBlock(blockNameMappings.Span[blockId], obj);
		}
		public static (ushort,IReadOnlyDictionary<string,object>?) FlattenBlockData(MinecraftBlock minecraftBlock)
		{
			IReadOnlyDictionary<string, object>? obj = minecraftBlock.blockData;
            if (obj is null)
            {
				if (singleStateBlocks.TryGetValue(minecraftBlock.blockType, out ushort id)) return (id, null);
				throw new Exception("Attempted to flatten multi-state block without state dictionary");
            }
            MinecraftBlockState[] minecraftBlockStates = supportedBlockStates[minecraftBlock.blockType];
			for(int i = 0, stop = minecraftBlockStates.Length; i < stop; ++i){
				MinecraftBlockState minecraftBlockState = minecraftBlockStates[i];
				IReadOnlyDictionary<string, string>? keyValuePairs = minecraftBlockState.properties;
				if(keyValuePairs is  null) return (minecraftBlockState.id,obj);
				foreach(KeyValuePair<string,string> kvp in keyValuePairs){
					if(kvp.Value != (string)obj[kvp.Key])
					{
						goto nomatch;
					}
				}
				Dictionary<string, object> d1 = new();
				foreach (KeyValuePair<string, object> kvp in obj)
				{
					string key = kvp.Key;
					if (keyValuePairs.ContainsKey(key)) continue;
					d1.Add(key,kvp.Value);
				}
				return (minecraftBlockState.id, d1);

			nomatch:;
			}
			throw new Exception("Attempted to flatten invalid block state");

		}


	}
}
