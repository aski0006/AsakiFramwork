using System;
using UnityEngine;

namespace Asaki.Core.Blackboard.Variables
{
	[Serializable]
	public class AsakiInt : AsakiValue<int> { }
	[Serializable]
	public class AsakiFloat : AsakiValue<float> { }
	[Serializable]
	public class AsakiBool : AsakiValue<bool> { }
	[Serializable]
	public class AsakiString : AsakiValue<string> { }
	[Serializable]
	public class AsakiVector3 : AsakiValue<Vector3> { }
	[Serializable]
	public class AsakiVector2 : AsakiValue<Vector2> { }
	[Serializable]
	public class AsakiVector2Int : AsakiValue<Vector2Int> { }
	[Serializable]
	public class AsakiVector3Int : AsakiValue<Vector3Int> { }
	[Serializable]
	public class AsakiColor : AsakiValue<Color> { }
}
