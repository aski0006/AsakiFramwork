using System;
using UnityEditor;
using UnityEngine;

namespace Asaki.Editor.Utilities.Tools.BatchRename
{
	/// <summary>
	/// 不可变的重命名操作记录，用于Undo/Redo系统
	/// 关键设计：使用InstanceID而非直接引用，解决场景重加载后的引用丢失问题
	/// </summary>
	[Serializable]
	public class RenameOperation
	{
		[SerializeField] private string _originalName;
		[SerializeField] private string _newName;
		[SerializeField] private int _instanceId;

		/// <summary>
		/// 原始名称（用于回滚）
		/// </summary>
		public string OriginalName => _originalName;

		/// <summary>
		/// 新名称（用于应用）
		/// </summary>
		public string NewName => _newName;

		/// <summary>
		/// 目标GameObject的InstanceID（唯一标识）
		/// </summary>
		public int InstanceId => _instanceId;

		/// <summary>
		/// 尝试获取目标GameObject
		/// 安全性：处理场景卸载、对象销毁等边界情况
		/// </summary>
		public bool TryGetGameObject(out GameObject go)
		{
			// EditorUtility.InstanceIDToObject能处理跨Undo重加载的场景
			go = EditorUtility.InstanceIDToObject(_instanceId) as GameObject;
			return go != null;
		}

		/// <summary>
		/// 应用重命名操作
		/// 自动标记对象为Dirty，确保变更可被序列化保存
		/// </summary>
		public void Apply()
		{
			if (TryGetGameObject(out GameObject go) && go.name != _newName)
			{
				go.name = _newName;
				EditorUtility.SetDirty(go); // 关键：必须标记，否则Undo无法捕获状态
			}
		}

		/// <summary>
		/// 回滚重命名操作
		/// 安全性：仅当当前名称匹配_newName时才回滚，防止意外覆盖
		/// </summary>
		public void Revert()
		{
			if (TryGetGameObject(out GameObject go) && go.name == _newName)
			{
				go.name = _originalName;
				EditorUtility.SetDirty(go);
			}
		}

		/// <summary>
		/// 工厂方法：从GameObject创建操作记录
		/// 封装正确性：确保InstanceID和名称快照的准确性
		/// </summary>
		public static RenameOperation CreateFrom(GameObject target, string newName)
		{
			if (target == null)
				throw new ArgumentNullException(nameof(target));

			return new RenameOperation
			{
				_originalName = target.name,
				_newName = newName,
				_instanceId = target.GetInstanceID(),
			};
		}
	}
}
