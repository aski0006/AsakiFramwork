using System;
using System.Collections.Generic;
using UnityEngine;

namespace Asaki.Unity.Configuration
{
	public enum AsakiAudioGroup
	{
		SFX = 0,   // 普通音效
		BGM = 1,   // 背景音乐
		UI = 2,    // UI 交互音
		Voice = 3, // 语音
	}

	[Serializable]
	public class AudioItem
	{
		public string Key; // 代码引用的 Enum Key (如 "Player_Jump")
		public int ID;     // Hash ID

		[Tooltip("编辑器引用，用于预览和生成路径")]
		public AudioClip Clip;

		[Tooltip("运行时加载路径 (自动生成)")]
		public string AssetPath;

		[Range(0f, 1f)]
		public float Volume = 1f; // 基础音量

		[Range(0.1f, 3f)]
		public float Pitch = 1f; // 基础音高

		public bool Loop = false; // 默认是否循环

		public AsakiAudioGroup Group = AsakiAudioGroup.SFX; // 分组

		[Tooltip("是否启用随机音高扰动 (丰富听感)")]
		public bool RandomPitch = false;

		// 编辑器折叠状态
		#if UNITY_EDITOR
		public bool _editorExpanded = false;
		#endif
	}

	[CreateAssetMenu(fileName = "AsakiAudioConfig", menuName = "Asaki/Configuration/Audio Configuration")]
	public class AsakiAudioConfig : ScriptableObject
	{
		// 新的核心数据列表
		public List<AudioItem> Items = new List<AudioItem>();

		// 运行时查找缓存
		private Dictionary<int, AudioItem> _lookup;

		public void InitializeLookup()
		{
			if (_lookup != null) return;
			_lookup = new Dictionary<int, AudioItem>(Items.Count);
			foreach (AudioItem item in Items)
			{
				if (!_lookup.ContainsKey(item.ID))
				{
					_lookup.Add(item.ID, item);
				}
			}
		}

		public bool TryGet(int id, out AudioItem item)
		{
			if (_lookup == null) InitializeLookup();
			return _lookup.TryGetValue(id, out item);
		}

		// 兼容旧接口，防止报错 (虽然逻辑变了，但先保留方法签名)
		public string GetPath(int id)
		{
			if (TryGet(id, out AudioItem item)) return item.AssetPath;
			return null;
		}
	}
}
