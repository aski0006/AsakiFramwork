using Asaki.Core;
using Asaki.Core.UI;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Asaki.Unity
{
	[Serializable]
	public struct WidgetTemplate
	{
		public AsakiUIWidgetType Type;
		public GameObject Prefab;
	}

	[CreateAssetMenu(fileName = "AsakiUITemplateConfig", menuName = "Asaki/UI/Template Configuration")]
	public class AsakiUITemplateConfig : ScriptableObject
	{
		public List<WidgetTemplate> Templates = new List<WidgetTemplate>();

		// 运行时/编辑器查找缓存
		public GameObject GetTemplate(AsakiUIWidgetType type)
		{
			foreach (WidgetTemplate t in Templates)
			{
				if (t.Type == type) return t.Prefab;
			}
			return null;
		}
	}
}
