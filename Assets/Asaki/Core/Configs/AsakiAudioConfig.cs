using System;
using System.Collections.Generic;
using UnityEngine;

namespace Asaki.Core.Configs
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
		public string Key; // 代码引用的 Enum Key
		public int ID;     // Hash ID

		[Tooltip("编辑器引用，用于预览和生成路径")]
		public AudioClip Clip;

		[Tooltip("运行时加载路径 (自动生成)")]
		public string AssetPath;

		[Range(0f, 1f)] public float Volume = 1f;
		[Range(0.1f, 3f)] public float Pitch = 1f;
		public bool Loop = false;
		public AsakiAudioGroup Group = AsakiAudioGroup.SFX;
		public bool RandomPitch = false;

		#if UNITY_EDITOR
		public bool _editorExpanded = false;
		#endif
	}

	[Serializable] // 关键：标记为可序列化
	public class AsakiAudioConfig
	{
		[Header("Global Settings")]
		public string SoundAgentPrefabAssetKey;
		public int InitialPoolSize = 16;

		[Header("Registry")]
		public List<AudioItem> Items = new List<AudioItem>();

		// 运行时缓存
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
	}
}
