using System;
using UnityEngine;

namespace Asaki.Core.Graphs
{

	[Serializable]
	public abstract class AsakiNodeBase
	{
		[HideInInspector] public Vector2 Position;
		[HideInInspector] public string GUID;
		[HideInInspector] public int ExecutionOrder;
		// 获取标题的虚方法，默认返回类名
		public virtual string Title => GetType().Name;
		public virtual void OnCreated() { }
	}

	[Serializable]
	public class AsakiEdgeData
	{
		public string BaseNodeGUID;   // 输出节点 GUID
		public string BasePortName;   // 输出端口名
		public string TargetNodeGUID; // 输入节点 GUID
		public string TargetPortName; // 输入端口名
	}

}
