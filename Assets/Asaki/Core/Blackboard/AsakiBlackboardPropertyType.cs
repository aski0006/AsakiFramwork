using Asaki.Core.Blackboard.Variables;
using System;
using UnityEngine;

namespace Asaki.Core.Blackboard
{
	[Serializable]
	public class AsakiVariableDef
	{
		public string Name;

		[SerializeReference] 
		public AsakiValueBase ValueData;
		
		public bool IsExposed { get; set; }

		// 辅助属性：快速获取类型名
		public string TypeName => ValueData != null ? ValueData.TypeName : "Null";
	}
}
