using Asaki.Core.Serialization;
using System;
using System.IO;
using UnityEngine;

// 确保引用了你的数学库

namespace Asaki.Unity.Services.Serialization
{
	// ==================================================================================
	// 写入器：极速、紧凑、忽略 Key
	// ==================================================================================
	public class AsakiBinaryWriter : IAsakiWriter
	{
		private BinaryWriter _bw;

		public AsakiBinaryWriter(Stream stream, bool ownsStream = false)
		{
			_stream = stream ?? throw new ArgumentNullException(nameof(stream));
			_ownsStream = ownsStream;
			_bw = new BinaryWriter(stream, System.Text.Encoding.UTF8, true);
		}

		public void WriteVersion(int version)
		{
			_bw.Write((short)version);
		}

		// --- 基础类型 ---
		public void WriteByte(string key, byte value)
		{
			_bw.Write(value);
		}
		public void WriteInt(string key, int value)
		{
			_bw.Write(value);
		}
		public void WriteLong(string key, long value)
		{
			_bw.Write(value);
		}
		public void WriteFloat(string key, float value)
		{
			_bw.Write(value);
		}
		public void WriteDouble(string key, double value)
		{
			_bw.Write(value);
		}
		public void WriteString(string key, string value)
		{
			_bw.Write(value ?? string.Empty);
		}
		public void WriteBool(string key, bool value)
		{
			_bw.Write(value);
		}
		public void WriteUInt(string key, uint value)
		{
			_bw.Write(value);
		}
		public void WriteULong(string key, ulong value)
		{
			_bw.Write(value);
		}


		// --- Asaki Math (直接打平写入，Zero GC) ---
		public void WriteVector2Int(string key, Vector2Int value)
		{
			_bw.Write(value.x);
			_bw.Write(value.y);
		}
		public void WriteVector3Int(string key, Vector3Int value)
		{
			_bw.Write(value.x);
			_bw.Write(value.y);
			_bw.Write(value.z);
		}


		public void WriteVector2(string key, Vector2 value)
		{
			_bw.Write(value.x);
			_bw.Write(value.y);
		}
		public void WriteVector3(string key, Vector3 value)
		{
			_bw.Write(value.x);
			_bw.Write(value.y);
			_bw.Write(value.z);
		}
		public void WriteVector4(string key, Vector4 value)
		{
			_bw.Write(value.x);
			_bw.Write(value.y);
			_bw.Write(value.z);
			_bw.Write(value.w);
		}

		public void WriteQuaternion(string key, Quaternion value)
		{
			_bw.Write(value.x);
			_bw.Write(value.y);
			_bw.Write(value.z);
			_bw.Write(value.w);
		}

		public void WriteBounds(string key, Bounds value)
		{
			// 假设 AABB 包含 min 和 max 两个 float3
			WriteVector3(null, value.min);
			WriteVector3(null, value.max);
		}

		// --- 复杂对象 ---
		public void WriteObject<T>(string key, T value) where T : IAsakiSavable
		{
			// 写入 Null 标记位 (False = Null, True = Not Null)
			if (value == null)
			{
				_bw.Write(false);
				return;
			}
			_bw.Write(true);
			value.Serialize(this); // 递归调用
		}

		public void WriteObject(string key, IAsakiSavable value)
		{
			if (value == null)
			{
				_bw.Write(false);
				return;
			}
			_bw.Write(true);
			value.Serialize(this); // 多态调用
		}

		// --- 集合与结构控制 ---
		public void BeginList(string key, int count)
		{
			_bw.Write(count);
			// 只写数量
		}
		public void EndList() { } // 二进制不需要结束符

		public void BeginObject(string key) { } // 二进制不需要结构标记
		public void EndObject() { }

		private Stream _stream;
		private bool _ownsStream;
	}

	// ==================================================================================
	// 读取器：顺序读取、对象复用
	// ==================================================================================
	public class AsakiBinaryReader : IAsakiReader
	{
		private BinaryReader _br;
		private Stream _stream;
		private bool _ownsStream;

		public long Position => _stream?.Position ?? 0;
		public long BytesRemaining => _stream?.Length - _stream?.Position ?? 0;

		public AsakiBinaryReader(Stream stream, bool ownsStream = false)
		{
			_stream = stream ?? throw new ArgumentNullException(nameof(stream));
			_ownsStream = ownsStream;
			_br = new BinaryReader(stream, System.Text.Encoding.UTF8, true);
		}

		public int ReadVersion()
		{
			return _br.ReadInt16();
		}

		// --- 基础类型 ---
		public byte ReadByte(string key)
		{
			return _br.ReadByte();
		}
		public int ReadInt(string key)
		{
			return _br.ReadInt32();
		}
		public long ReadLong(string key)
		{
			return _br.ReadInt64();
		}
		public float ReadFloat(string key)
		{
			return _br.ReadSingle();
		}
		public double ReadDouble(string key)
		{
			return _br.ReadDouble();
		}
		public string ReadString(string key)
		{
			return _br.ReadString();
		}
		public bool ReadBool(string key)
		{
			return _br.ReadBoolean();
		}
		public uint ReadUInt(string key)
		{
			return _br.ReadUInt32();
		}
		public ulong ReadULong(string key)
		{
			return _br.ReadUInt64();
		}

		// --- Asaki Math ---
		public Vector2Int ReadVector2Int(string key)
		{
			return new Vector2Int(_br.ReadInt32(), _br.ReadInt32());
		}
		public Vector3Int ReadVector3Int(string key)
		{
			return new Vector3Int(_br.ReadInt32(), _br.ReadInt32(), _br.ReadInt32());
		}

		public Vector2 ReadVector2(string key)
		{
			return new Vector2(_br.ReadSingle(), _br.ReadSingle());
		}
		public Vector3 ReadVector3(string key)
		{
			return new Vector3(_br.ReadSingle(), _br.ReadSingle(), _br.ReadSingle());
		}
		public Vector4 ReadVector4(string key)
		{
			return new Vector4(_br.ReadSingle(), _br.ReadSingle(), _br.ReadSingle(), _br.ReadSingle());
		}

		public Quaternion ReadQuaternion(string key)
		{
			return new Quaternion(_br.ReadSingle(), _br.ReadSingle(), _br.ReadSingle(), _br.ReadSingle());
		}

		public Bounds ReadBounds(string key)
		{
			// 顺序必须与 Write 一致
			Vector3 min = ReadVector3(null);
			Vector3 max = ReadVector3(null);
			return new Bounds(min, max);
		}

		// --- 复杂对象 ---
		public T ReadObject<T>(string key, T existingObj = default(T)) where T : IAsakiSavable, new()
		{
			// 1. 检查 Null 标记
			bool isNotNull = _br.ReadBoolean();
			if (!isNotNull) return default(T);

			// 2. 实例复用 (Zero GC 关键)
			T instance = existingObj;
			if (instance == null) instance = new T();

			// 3. 填充数据
			instance.Deserialize(this);
			return instance;
		}

		public object ReadObject(string key, Type type)
		{
			// 必须手动检查是否实现了 IAsakiSavable (安全网)
			// 在 Dispatcher 中我们已经检查过了，这里可以由 Release 宏屏蔽，但为了安全保留
			if (!typeof(IAsakiSavable).IsAssignableFrom(type))
				throw new InvalidOperationException($"Type {type.Name} is not IAsakiSavable");

			bool isNotNull = _br.ReadBoolean();
			if (!isNotNull) return null;

			// [核心] 使用 Activator 创建实例
			// 这是本框架中唯一允许的“运行时实例化”手段，它是为了支持泛型容器所必须的代价
			IAsakiSavable instance = (IAsakiSavable)Activator.CreateInstance(type);

			instance.Deserialize(this);
			return instance;
		}
		// --- 集合控制 ---
		public int BeginList(string key)
		{
			return _br.ReadInt32();
			// 读取数量
		}
		public void EndList() { }
	}
}
