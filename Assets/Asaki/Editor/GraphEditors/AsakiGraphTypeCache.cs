using Asaki.Core;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Asaki.Editor.GraphEditors
{
	public struct PortInfo
	{
		public string FieldName;
		public string PortName;
		public bool IsInput;
		public bool AllowMultiple;
		public Type DataType;
	}

	public static class AsakiGraphTypeCache
	{
		// 缓存：Type -> PortList
		private static readonly Dictionary<Type, List<PortInfo>> _portCache = new Dictionary<Type, List<PortInfo>>();

		/// <summary>
		/// 获取某个节点类型的所有端口定义 (O(1) 访问)
		/// </summary>
		public static List<PortInfo> GetPorts(Type nodeType)
		{
			if (_portCache.TryGetValue(nodeType, out var list))
			{
				return list;
			}

			// 缓存未命中，执行反射扫描
			var result = new List<PortInfo>();
			var fields = nodeType.GetFields(BindingFlags.Public | BindingFlags.Instance);

			foreach (FieldInfo field in fields)
			{
				// 扫描 Input
				AsakiNodeInputAttribute inputAttr = field.GetCustomAttribute<AsakiNodeInputAttribute>();
				if (inputAttr != null)
				{
					result.Add(new PortInfo
					{
						FieldName = field.Name,
						PortName = inputAttr.PortName,
						IsInput = true,
						AllowMultiple = inputAttr.Multiple,
						DataType = field.FieldType,
					});
				}

				// 扫描 Output
				AsakiNodeOutputAttribute outputAttr = field.GetCustomAttribute<AsakiNodeOutputAttribute>();
				if (outputAttr != null)
				{
					result.Add(new PortInfo
					{
						FieldName = field.Name,
						PortName = outputAttr.PortName,
						IsInput = false,
						AllowMultiple = outputAttr.Multiple,
						DataType = field.FieldType,
					});
				}
			}

			_portCache[nodeType] = result;
			return result;
		}

		[InitializeOnLoadMethod]
		private static void ClearCache()
		{
			_portCache.Clear();
		}
	}
}
