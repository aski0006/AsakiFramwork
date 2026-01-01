using System;
using System.IO;
using UnityEngine;

namespace Asaki.Core.Serialization
{
	public interface IAsakiReader
	{
		int ReadVersion();

		// --- 基础类型 ---
		byte ReadByte(string key);
		int ReadInt(string key);
		long ReadLong(string key);
		float ReadFloat(string key);
		double ReadDouble(string key);
		string ReadString(string key);
		bool ReadBool(string key);
		uint ReadUInt(string key);
		ulong ReadULong(string key);

		// --- Asaki Math ---
		Vector2Int ReadVector2Int(string key);
		Vector3Int ReadVector3Int(string key);
		Vector2 ReadVector2(string key);
		Vector3 ReadVector3(string key);
		Vector4 ReadVector4(string key);
		Bounds ReadBounds(string key);
		Quaternion ReadQuaternion(string key);

		// --- 复杂对象 ---
		// Reader 不需要 EndObject，因为 ReadObject 方法的结束就是 End
		T ReadObject<T>(string key, T existingObj = default(T)) where T : IAsakiSavable, new();

		object ReadObject(string key, Type type);
		// --- 集合控制 ---
		int BeginList(string key);
		void EndList(); // List 需要 End，因为 List 内部是循环，Reader 需要知道何时跳出循环逻辑

	}
}
