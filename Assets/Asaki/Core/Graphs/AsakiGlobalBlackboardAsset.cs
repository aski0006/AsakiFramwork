using Asaki.Core.Blackboard;
using Asaki.Core.Blackboard.Variables;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Asaki.Core.Graphs
{
	public class AsakiGlobalBlackboardAsset : ScriptableObject
	{
		// 仍然是一个 List，但 AsakiVariableDef 内部现在是多态的 ValueData
		[SerializeReference]
		public List<AsakiVariableDef> GlobalVariables = new();
		
		/// <summary>
		/// [重构] 根据 Type 创建变量
		/// </summary>
		public AsakiVariableDef GetOrCreateVariable(string name, Type valueType)
		{
			var existing = GlobalVariables.Find(v => v.Name == name);
			if (existing != null) return existing;

			// 创建多态数据实例
			AsakiValueBase dataInstance = Activator.CreateInstance(valueType) as AsakiValueBase;

			var newVar = new AsakiVariableDef
			{
				Name = name,
				ValueData = dataInstance, // [New] 存入多态数据
				IsExposed = true
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
