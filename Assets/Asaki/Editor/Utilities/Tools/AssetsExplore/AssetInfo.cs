using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Asaki.Editor.Utilities.Tools.AssetsExplore
{
	/// <summary>
	/// 资产数据模型 - 存储单个资源的元数据
	/// </summary>
	[Serializable]
	public class AssetInfo
	{
		public string guid;            // Unity GUID
		public string name;            // 文件名
		public string path;            // 项目相对路径
		public string extension;       // 扩展名
		public long fileSize;          // 文件大小(字节)
		public DateTime lastModified;  // 最后修改时间
		public AssetCategory category; // 资产分类
		public Object cachedObject;    // 缓存的Unity对象引用
		public bool isFavorite;        // 是否收藏
		public Texture2D previewIcon;  // 预览图标

		// 性能优化：延迟加载
		private bool _objectLoaded = false;

		public Object GetAssetObject()
		{
			if (!_objectLoaded && !string.IsNullOrEmpty(guid))
			{
				string assetPath = AssetDatabase.GUIDToAssetPath(guid);
				if (!string.IsNullOrEmpty(assetPath))
				{
					cachedObject = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
				}
				_objectLoaded = true;
			}
			return cachedObject;
		}

		// 获取格式化的大小字符串
		public string GetFormattedSize()
		{
			const long kb = 1024;
			const long mb = kb * 1024;
			const long gb = mb * 1024;

			if (fileSize >= gb) return $"{fileSize / (float)gb:F2} GB";
			if (fileSize >= mb) return $"{fileSize / (float)mb:F2} MB";
			if (fileSize >= kb) return $"{fileSize / (float)kb:F2} KB";
			return $"{fileSize} B";
		}
	}
}
