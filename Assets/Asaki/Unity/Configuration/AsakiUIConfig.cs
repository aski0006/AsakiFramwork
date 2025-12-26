using Asaki.Core.UI;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Asaki.Unity.Configuration
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

	[CreateAssetMenu(fileName = "AsakiUIConfig", menuName = "Asaki/Configuration/UI Configuration")]
	public class AsakiUIConfig : ScriptableObject
	{
		public List<UIInfo> UIList = new List<UIInfo>();
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
			return _lookup!.TryGetValue(id, out info);
		}
	}
}
