using System;
using UnityEngine;

namespace Asaki.Core.Graphs
{
	public enum AsakiBlackboardPropertyType
	{
		Int,
		Float,
		Bool,
		String,
		Vector3,
		Vector2,
		Vector2Int,
		Vector3Int,
		Color,
	}

	[Serializable]
	public class AsakiVariableDef
	{
		public string Name;
		public AsakiBlackboardPropertyType Type;
		public bool IsExposed = true;

		public int IntVal;
		public float FloatVal;
		public bool BoolVal;
		public string StringVal;
		public Vector3 Vector3Val;
		public Vector2 Vector2Val;
		public Vector3Int Vector3IntVal;
		public Vector2Int Vector2IntVal;
		public Color ColorVal;
	}
}
