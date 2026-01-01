using System;
using System.IO;
using UnityEngine;

namespace Asaki.Core.Serialization
{
	public interface IAsakiWriter
	{
		void WriteVersion(int version);

		// --- 基础类型 ---
		void WriteByte(string key, byte value);
		void WriteInt(string key, int value);
		void WriteLong(string key, long value);
		void WriteFloat(string key, float value);
		void WriteDouble(string key, double value);
		void WriteString(string key, string value);
		void WriteBool(string key, bool value);
		void WriteUInt(string key, uint value);
		void WriteULong(string key, ulong value);

		// --- Asaki Math (保留你的扩展，非常好) ---
		// 针对结构体的特定优化，避免 WriteObject 的开销
		void WriteVector2Int(string key, Vector2Int value);
		void WriteVector3Int(string key, Vector3Int value);
		void WriteVector2(string key, Vector2 value);
		void WriteVector3(string key, Vector3 value);
		void WriteVector4(string key, Vector4 value);
		void WriteBounds(string key, Bounds value);
		void WriteQuaternion(string key, Quaternion value);

		// --- 复杂对象 ---
		void WriteObject<T>(string key, T value) where T : IAsakiSavable;

		void WriteObject(string key, IAsakiSavable value);
		// --- 结构控制 (移除 Key 参数) ---
		void BeginList(string key, int count);
		void EndList(); // 修正：不需要 Key

		void BeginObject(string key);
		void EndObject(); // 修正：不需要 Key

	}
}
