using System.Collections.Generic;
using UnityEngine;

namespace Asaki.Core.Graphs
{
	public class AsakiGlobalBlackboardAsset : ScriptableObject
	{
		[SerializeReference]
		public List<AsakiVariableDef> GlobalVariables = new();
		
		public AsakiVariableDef GetOrCreateVariable(string name, AsakiBlackboardPropertyType type)
		{
			var existing = GlobalVariables.Find(v => v.Name == name);
			if (existing != null) return existing;

			var newVar = new AsakiVariableDef
			{
				Name = name,
				Type = type,
				IsExposed = true  // 全局变量默认暴露
			};
			GlobalVariables.Add(newVar);
			return newVar;
		}
		
		public bool RemoveVariable(string name)
		{
			return GlobalVariables.RemoveAll(v => v.Name == name) > 0;
		}
	}
}
