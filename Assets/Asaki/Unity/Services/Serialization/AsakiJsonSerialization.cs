using Asaki.Core.Serialization;
using Asaki.Unity.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Asaki.Unity.Services.Serialization
{
	public class AsakiJsonWriter : IAsakiWriter
	{
		private StringBuilder _sb;
		private int _indent;
		private bool _skipNextComma;
		private readonly Stack<ContainerContext> _contextStack;
		private bool _isResetting;
		private bool _isRented;
		private class ContainerContext
		{
			public bool IsArray;
			public bool HasWrittenFirstElement;
		}

		public long Position => _sb?.Length ?? 0;


		public AsakiJsonWriter(StringBuilder sb = null, bool rentFromPool = true)
		{
			_isRented = rentFromPool;

			if (sb != null)
			{
				// 使用外部提供的StringBuilder
				_sb = sb;
				_isRented = false;
			}
			else if (rentFromPool)
			{
				// 从池中借用
				_sb = AsakiStringBuilderPool.Rent();
			}
			else
			{
				// 创建新的StringBuilder
				_sb = new StringBuilder(256);
			}

			_indent = 0;
			_skipNextComma = false;
			_contextStack = new Stack<ContainerContext>();
			_contextStack.Push(new ContainerContext { IsArray = false });
			_isResetting = false;
		}

		public string GetResult()
		{
			return _sb.ToString();
		}

		// =========================================================
		// 核心辅助逻辑
		// =========================================================
		private void WritePrefix(string key)
		{
			bool skipComma = _skipNextComma;
			_skipNextComma = false; // 消费掉这个 flag

			ContainerContext ctx = _contextStack.Peek();

			// 写入逗号
			if (ctx.HasWrittenFirstElement && !skipComma)
			{
				_sb.AppendLine(",");
			}
			ctx.HasWrittenFirstElement = true;

			// 写入缩进
			_sb.Append(' ', _indent * 4);

			// 写入 Key
			if (!string.IsNullOrEmpty(key) && !ctx.IsArray)
			{
				_sb.Append($"\"{key}\": ");
				_skipNextComma = true; // Key 写完了，接下来的 Value 不要写逗号
			}
		}

		private void PushContext(bool isArray)
		{
			_contextStack.Push(new ContainerContext { IsArray = isArray });
		}
		private void PopContext()
		{
			if (_contextStack.Count > 0) _contextStack.Pop();
		}

		// =========================================================
		// 基础类型 (关键修复：写入后重置 flag)
		// =========================================================

		public void WriteVersion(int version)
		{
			WriteInt("version", version);
		}

		public void WriteByte(string key, byte value)
		{
			WritePrefix(key);
			_sb.Append(value);
			_skipNextComma = false;
		}

		public void WriteInt(string key, int value)
		{
			WritePrefix(key);
			_sb.Append(value);
			_skipNextComma = false;
		}
		public void WriteLong(string key, long value)
		{
			WritePrefix(key);
			_sb.Append(value);
			_skipNextComma = false;
		}
		public void WriteFloat(string key, float value)
		{
			WritePrefix(key);
			_sb.Append(value.ToString("F3"));
			_skipNextComma = false;
		}
		public void WriteDouble(string key, double value)
		{
			WritePrefix(key);
			_sb.Append(value.ToString("F4"));
			_skipNextComma = false;
		}
		public void WriteString(string key, string value)
		{
			WritePrefix(key);
			string escaped = value?.Replace("\"", "\\\"").Replace("\n", "\\n") ?? "";
			_sb.Append($"\"{escaped}\"");
			_skipNextComma = false;
		}
		public void WriteBool(string key, bool value)
		{
			WritePrefix(key);
			_sb.Append(value ? "true" : "false");
			_skipNextComma = false;
		}
		public void WriteUInt(string key, uint value)
		{
			WriteLong(key, value);
		}
		public void WriteULong(string key, ulong value)
		{
			WriteString(key, value.ToString());
		}

		// --- Math (同样需要重置) ---
		public void WriteVector2(string key, Vector2 value)
		{
			WritePrefix(key);
			_sb.Append($"{{ \"x\": {value.x:F2}, \"y\": {value.y:F2} }}");
			_skipNextComma = false;
		}
		public void WriteVector3(string key, Vector3 value)
		{
			WritePrefix(key);
			_sb.Append($"{{ \"x\": {value.x:F2}, \"y\": {value.y:F2}, \"z\": {value.z:F2} }}");
			_skipNextComma = false;
		}
		public void WriteVector2Int(string key, Vector2Int value)
		{
			WritePrefix(key);
			_sb.Append($"{{ \"x\": {value.x}, \"y\": {value.y} }}");
			_skipNextComma = false;
		}
		public void WriteVector3Int(string key, Vector3Int value)
		{
			WritePrefix(key);
			_sb.Append($"{{ \"x\": {value.x}, \"y\": {value.y}, \"z\": {value.z} }}");
			_skipNextComma = false;
		}
		public void WriteVector4(string key, Vector4 value)
		{
			WritePrefix(key);
			_sb.Append($"{{ \"x\": {value.x:F2}, \"y\": {value.y:F2}, \"z\": {value.z:F2}, \"w\": {value.w:F2} }}");
			_skipNextComma = false;
		}
		public void WriteQuaternion(string key, Quaternion value)
		{
			WritePrefix(key);
			_sb.Append($"{{ \"x\": {value.x:F2}, \"y\": {value.y:F2}, \"z\": {value.z:F2}, \"w\": {value.w:F2} }}");
			_skipNextComma = false;
		}
		public void WriteBounds(string key, Bounds value)
		{
			WritePrefix(key);
			_sb.Append($"{{ \"center\": {{ \"x\": {value.center.x:F2}, \"y\": {value.center.y:F2}, \"z\": {value.center.z:F2} }}, \"size\": {{ \"x\": {value.size.x:F2}, \"y\": {value.size.y:F2}, \"z\": {value.size.z:F2} }} }}");
			_skipNextComma = false;
		}

		// --- 结构控制 ---

		public void BeginObject(string key)
		{
			WritePrefix(null);
			_sb.AppendLine("{");
			_indent++;
			PushContext(false);
			_skipNextComma = false;
		}

		public void EndObject()
		{
			_indent--;
			PopContext();
			_sb.AppendLine();
			_sb.Append(' ', _indent * 4);
			_sb.Append("}");
			// 对象结束也是一个值的结束，但在流式接口中，EndObject 后通常紧接着父级的下一个调用
			// 父级调用 WritePrefix 时会检查 _skipNextComma。
			// 这里的 EndObject 并不直接负责逗号逻辑，它只是闭合括号。
			// 但作为父级的一个"Value"，它结束了。我们需要确保父级继续写逗号。
			_skipNextComma = false;
		}

		public void BeginList(string key, int count)
		{
			WritePrefix(key);
			_skipNextComma = false; // 消费掉 key 带来的 skip，准备写 [

			_sb.AppendLine("[");
			_indent++;
			PushContext(true);
		}

		public void EndList()
		{
			_indent--;
			PopContext();
			_sb.AppendLine();
			_sb.Append(' ', _indent * 4);
			_sb.Append("]");
			_skipNextComma = false;
		}

		// --- 递归对象 ---

		public void WriteObject<T>(string key, T value) where T : IAsakiSavable
		{
			WriteObject(key, (IAsakiSavable)value);
		}

		public void WriteObject(string key, IAsakiSavable value)
		{
			if (value == null)
			{
				WritePrefix(key);
				_sb.Append("null");
				_skipNextComma = false; // [修复] null 也是值，写完要重置
				return;
			}

			WritePrefix(key);
			_skipNextComma = true; // 告诉 BeginObject 不要写逗号

			value.Serialize(this);
			// Serialize 内部调用 BeginObject -> EndObject
			// EndObject 会重置 flag
		}

	}

	// ==================================================================================
	// Part 2: Asaki JSON Reader (新增核心实现)
	// ==================================================================================
	public class AsakiJsonReader : IAsakiReader
	{
		// 上下文节点：可能是 Dictionary (对象) 或 List (数组)
		private readonly object _currentNode;

		// 列表上下文栈：处理 List 嵌套
		private Stack<ListContext> _listStack = new Stack<ListContext>();
		private class ListContext
		{
			public List<object> List;
			public int Index;
		}

		private AsakiJsonReader(object node)
		{
			_currentNode = node;
		}

		/// <summary>
		/// [API] 从 JSON 字符串创建 Reader
		/// </summary>
		public static AsakiJsonReader FromJson(string json)
		{
			object data = AsakiTinyJsonParser.Parse(json);
			return new AsakiJsonReader(data);
		}

		public int ReadVersion()
		{
			return 1;
		}

		// --- 核心取值逻辑 (适配流式接口) ---
		private object GetValue(string key)
		{
			// 1. 优先检查是否正在读取 List (忽略 Key)
			if (_listStack.Count > 0)
			{
				ListContext ctx = _listStack.Peek();
				if (ctx.Index < ctx.List.Count)
				{
					object val = ctx.List[ctx.Index];
					ctx.Index++;
					return val;
				}
				return null;
			}

			// 2. 正常对象模式 (使用 Key)
			if (_currentNode is Dictionary<string, object> dict)
			{
				if (dict.TryGetValue(key, out object val)) return val;
			}

			return null;
		}

		// --- 基础类型实现 ---
		public byte ReadByte(string key)
		{
			return Convert.ToByte(GetValue(key));
		}
		public int ReadInt(string key)
		{
			return Convert.ToInt32(GetValue(key));
		}
		public long ReadLong(string key)
		{
			return Convert.ToInt64(GetValue(key));
		}
		public float ReadFloat(string key)
		{
			return Convert.ToSingle(GetValue(key));
		}
		public double ReadDouble(string key)
		{
			return Convert.ToDouble(GetValue(key));
		}
		public string ReadString(string key)
		{
			return Convert.ToString(GetValue(key));
		}
		public bool ReadBool(string key)
		{
			return Convert.ToBoolean(GetValue(key));
		}
		public uint ReadUInt(string key)
		{
			return Convert.ToUInt32(GetValue(key));
		}
		public ulong ReadULong(string key)
		{
			return Convert.ToUInt64(GetValue(key));
		}

		// --- Math 类型 (自动处理嵌套对象) ---
		public Vector2 ReadVector2(string key)
		{
			return ParseVec2(GetValue(key));
		}
		public Vector3 ReadVector3(string key)
		{
			return ParseVec3(GetValue(key));
		}
		public Vector2Int ReadVector2Int(string key)
		{
			return ParseVec2Int(GetValue(key));
		}
		public Vector3Int ReadVector3Int(string key)
		{
			return ParseVec3Int(GetValue(key));
		}
		public Vector4 ReadVector4(string key)
		{
			return Vector4.zero;
		}
		public Quaternion ReadQuaternion(string key)
		{
			return Quaternion.identity;
		}
		public Bounds ReadBounds(string key)
		{
			return default(Bounds);
		}

		// --- 复杂对象 (递归) ---

		// 1. 标准泛型路径 (编译器生成)
		public T ReadObject<T>(string key, T existingObj = default(T)) where T : IAsakiSavable, new()
		{
			object childNode = GetValue(key);
			if (childNode == null) return default(T);

			// 递归创建子 Reader
			AsakiJsonReader childReader = new AsakiJsonReader(childNode);

			T instance = existingObj ?? new T();
			instance.Deserialize(childReader);
			return instance;
		}

		// 2. 弱类型路径 (泛型容器/网络层使用)
		public object ReadObject(string key, Type type)
		{
			object childNode = GetValue(key);
			if (childNode == null) return null;

			if (!typeof(IAsakiSavable).IsAssignableFrom(type))
				throw new InvalidOperationException($"Type {type.Name} is not IAsakiSavable");

			AsakiJsonReader childReader = new AsakiJsonReader(childNode);
			IAsakiSavable instance = (IAsakiSavable)Activator.CreateInstance(type);
			instance.Deserialize(childReader);
			return instance;
		}

		// --- 列表控制 ---
		public int BeginList(string key)
		{
			// 获取 List 对象 (注意：不要自增 Index，因为 BeginList 本身不消耗数据，只是切换上下文)
			// 这里有一个棘手的问题：GetValue 会消耗 Index。
			// 我们需要 PeekValue 吗？或者特殊处理 Key。

			// 特殊逻辑：如果是 List 模式，我们已经在 List 里了，现在的 key 是 "Item" (通常被忽略)。
			// 但 BeginList 是为了开启 *下一层* 的 List。

			var rawList = GetValue(key) as List<object>;
			if (rawList == null) return 0;

			// 推入新栈，后续的 ReadX 调用将操作这个新 List
			_listStack.Push(new ListContext { List = rawList, Index = 0 });

			return rawList.Count;
		}

		public void EndList()
		{
			if (_listStack.Count > 0) _listStack.Pop();
		}

		// --- 辅助解析 ---
		private Vector2 ParseVec2(object obj)
		{
			if (obj is Dictionary<string, object> d) return new Vector2(Convert.ToSingle(d["x"]), Convert.ToSingle(d["y"]));
			return Vector2.zero;
		}
		private Vector3 ParseVec3(object obj)
		{
			if (obj is Dictionary<string, object> d) return new Vector3(Convert.ToSingle(d["x"]), Convert.ToSingle(d["y"]), Convert.ToSingle(d["z"]));
			return Vector3.zero;
		}
		private Vector2Int ParseVec2Int(object obj)
		{
			if (obj is Dictionary<string, object> d) return new Vector2Int(Convert.ToInt32(d["x"]), Convert.ToInt32(d["y"]));
			return Vector2Int.zero;
		}
		private Vector3Int ParseVec3Int(object obj)
		{
			if (obj is Dictionary<string, object> d) return new Vector3Int(Convert.ToInt32(d["x"]), Convert.ToInt32(d["y"]), Convert.ToInt32(d["z"]));
			return Vector3Int.zero;
		}
	}

	// ==================================================================================
	// Part 3: Asaki Tiny JSON Parser (零依赖内置解析器)
	// ==================================================================================
	public static class AsakiTinyJsonParser
	{
		public static object Parse(string json)
		{
			if (string.IsNullOrEmpty(json)) return null;
			int index = 0;
			return ParseValue(json, ref index);
		}

		private static object ParseValue(string json, ref int index)
		{
			SkipWhitespace(json, ref index);
			if (index >= json.Length) return null;

			char c = json[index];
			if (c == '{') return ParseObject(json, ref index);
			if (c == '[') return ParseArray(json, ref index);
			if (c == '"') return ParseString(json, ref index);
			if (char.IsDigit(c) || c == '-') return ParseNumber(json, ref index);
			if (c == 't')
			{
				index += 4;
				return true;
			}
			if (c == 'f')
			{
				index += 5;
				return false;
			}
			if (c == 'n')
			{
				index += 4;
				return null;
			}
			return null;
		}

		private static Dictionary<string, object> ParseObject(string json, ref int index)
		{
			var dict = new Dictionary<string, object>();
			index++; // skip '{'
			while (index < json.Length)
			{
				SkipWhitespace(json, ref index);
				if (json[index] == '}')
				{
					index++;
					break;
				}
				string key = ParseString(json, ref index);
				SkipWhitespace(json, ref index);
				if (json[index] == ':') index++;
				object value = ParseValue(json, ref index);
				dict[key] = value;
				SkipWhitespace(json, ref index);
				if (json[index] == ',') index++;
			}
			return dict;
		}

		private static List<object> ParseArray(string json, ref int index)
		{
			var list = new List<object>();
			index++; // skip '['
			while (index < json.Length)
			{
				SkipWhitespace(json, ref index);
				if (json[index] == ']')
				{
					index++;
					break;
				}
				list.Add(ParseValue(json, ref index));
				SkipWhitespace(json, ref index);
				if (json[index] == ',') index++;
			}
			return list;
		}

		private static string ParseString(string json, ref int index)
		{
			StringBuilder sb = new StringBuilder();
			index++; // skip start quote
			while (index < json.Length)
			{
				char c = json[index++];
				if (c == '"') break;
				if (c == '\\') index++; // Simple skip escape
				else sb.Append(c);
			}
			return sb.ToString();
		}

		private static object ParseNumber(string json, ref int index)
		{
			int start = index;
			while (index < json.Length && (char.IsDigit(json[index]) || json[index] == '.' || json[index] == '-')) index++;
			string numStr = json.Substring(start, index - start);
			if (numStr.Contains(".")) return double.Parse(numStr, CultureInfo.InvariantCulture);
			return long.Parse(numStr);
		}

		private static void SkipWhitespace(string json, ref int index)
		{
			while (index < json.Length && char.IsWhiteSpace(json[index])) index++;
		}
	}
}
