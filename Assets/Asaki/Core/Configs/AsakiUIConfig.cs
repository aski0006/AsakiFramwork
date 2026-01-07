using Asaki.Core.UI;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Asaki.Core.Configs
{
	[Serializable]
	public struct UIInfo
	{
		public string Name;
		public int ID;
		public AsakiUILayer Layer;
		public string AssetPath;
		public bool UsePool;
	}

	[Serializable]
	public struct WidgetTemplate
	{
		public AsakiUIWidgetType Type;
		public GameObject Prefab;
	}

	[Serializable] // 关键：标记为可序列化
	public class AsakiUIConfig
	{
		[Header("Global Settings")]
		public Vector2 ReferenceResolution = new Vector2(1920, 1080);
		[Range(0f, 1f)] public float MatchWidthOrHeight = 0.5f;

		[Header("Registry")]
		public List<UIInfo> UIList = new List<UIInfo>();

		[Header("Templates")]
		public List<WidgetTemplate> Templates = new List<WidgetTemplate>();
		public GameObject GetTemplate(AsakiUIWidgetType type)
		{
			// 简单的线性查找
			foreach (WidgetTemplate t in Templates)
			{
				if (t.Type == type) return t.Prefab;
			}
			return null;
		}
		// 运行时缓存 (不序列化)
		private Dictionary<int, UIInfo> _lookup;
		public void InitializeLookup()
		{
			if (_lookup != null) return;

			_lookup = new Dictionary<int, UIInfo>(UIList.Count);

			foreach (UIInfo item in UIList)
			{
				_lookup.TryAdd(item.ID, item);
			}
		}

		public bool TryGet(int id, out UIInfo info)
		{
			if (_lookup == null) InitializeLookup();
			return _lookup.TryGetValue(id, out info);
		}
	}
}
