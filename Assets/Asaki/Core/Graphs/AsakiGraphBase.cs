using System.Collections.Generic;
using UnityEngine;

namespace Asaki.Core.Graphs
{
	public abstract class AsakiGraphBase : ScriptableObject
	{
		[SerializeReference]
		public List<AsakiNodeBase> Nodes = new List<AsakiNodeBase>();
		public List<AsakiEdgeData> Edges = new List<AsakiEdgeData>();
		[Header("Blackboard")]
		[SerializeReference] // 支持多态，虽然目前是 List<T>
		public List<AsakiVariableDef> Variables = new List<AsakiVariableDef>();

		// 缓存：快速通过 GUID 找到节点实例
		private Dictionary<string, AsakiNodeBase> _nodeLookup;
		private Dictionary<AsakiNodeBase, Dictionary<string, AsakiEdgeData>> _incomingCache;
		// 缓存：快速查找连线关系
		// Key: 源节点, Value: { 端口名 -> 目标节点列表 }
		private Dictionary<AsakiNodeBase, Dictionary<string, List<AsakiNodeBase>>> _outgoingCache;
		[System.NonSerialized]
		private bool _isInitialized = false;

		private void EnsureRuntimeInitialized()
		{
			if (!_isInitialized || _outgoingCache == null || _nodeLookup == null)
			{
				InitializeRuntime();
			}
		}

		/// <summary>
		/// 运行时初始化。建议在 Runner 的 Start() 中调用。
		/// 会构建拓扑缓存，将查找复杂度从 O(N) 降为 O(1)。
		/// </summary>
		public void InitializeRuntime()
		{
			if (_isInitialized && _outgoingCache != null && _nodeLookup != null) return;

			// 1. 构建节点查找表
			_nodeLookup = new Dictionary<string, AsakiNodeBase>();
			foreach (AsakiNodeBase node in Nodes)
			{
				if (node != null) _nodeLookup[node.GUID] = node;
			}

			// 2. 构建连线关系缓存
			_outgoingCache = new Dictionary<AsakiNodeBase, Dictionary<string, List<AsakiNodeBase>>>();
			_incomingCache = new Dictionary<AsakiNodeBase, Dictionary<string, AsakiEdgeData>>();
			foreach (AsakiEdgeData edge in Edges)
			{
				if (!_nodeLookup.TryGetValue(edge.BaseNodeGUID, out AsakiNodeBase source)) continue;
				if (!_nodeLookup.TryGetValue(edge.TargetNodeGUID, out AsakiNodeBase target)) continue;

				// 1. 构建 Outgoing (正向)
				if (!_outgoingCache.ContainsKey(source))
					_outgoingCache[source] = new Dictionary<string, List<AsakiNodeBase>>();
				if (!_outgoingCache[source].ContainsKey(edge.BasePortName))
					_outgoingCache[source][edge.BasePortName] = new List<AsakiNodeBase>();
				_outgoingCache[source][edge.BasePortName].Add(target);

				// 2. [New] 构建 Incoming (反向) - 用于数据回溯
				if (!_incomingCache.ContainsKey(target))
					_incomingCache[target] = new Dictionary<string, AsakiEdgeData>();

				// 记录哪条线连到了 target 的哪个端口
				_incomingCache[target][edge.TargetPortName] = edge;
			}

			_isInitialized = true;
		}

		// --------------------------------------------------------
		// ★ 常用 API (Helper Methods)
		// --------------------------------------------------------

		/// <summary>
		/// 获取图的入口节点（默认返回第一个添加的节点，也可以按名字查找）
		/// </summary>
		public T GetEntryNode<T>() where T : AsakiNodeBase
		{
			if (Nodes.Count == 0) return null;
			return Nodes[0] as T; // 简单粗暴，返回第一个
		}

		public AsakiEdgeData GetInputConnection(AsakiNodeBase targetNode, string inputPortName)
		{
			EnsureRuntimeInitialized();
			if (_incomingCache.TryGetValue(targetNode, out var portMap))
			{
				if (portMap.TryGetValue(inputPortName, out AsakiEdgeData edge))
				{
					return edge;
				}
			}
			return null;
		}

		public AsakiNodeBase GetNodeByGUID(string guid)
		{
			return _nodeLookup.GetValueOrDefault(guid);
		}

		/// <summary>
		/// 获取指定端口连接的【单个】下一个节点。
		/// 适用于单输出端口（如 "Next"）。
		/// </summary>
		public AsakiNodeBase GetNextNode(AsakiNodeBase current, string portName = "Out")
		{
			EnsureRuntimeInitialized();

			if (_outgoingCache.TryGetValue(current, out var portMap))
			{
				if (portMap.TryGetValue(portName, out var targets) && targets.Count > 0)
				{
					return targets[0];
				}
			}
			return null;
		}

		/// <summary>
		/// 获取指定端口连接的【所有】下一个节点。
		/// 适用于多输出端口（如 "BroadCast"）。
		/// </summary>
		public List<AsakiNodeBase> GetNextNodes(AsakiNodeBase current, string portName = "Out")
		{
			EnsureRuntimeInitialized();

			if (_outgoingCache.TryGetValue(current, out var portMap))
			{
				if (portMap.TryGetValue(portName, out var targets))
				{
					return targets;
				}
			}
			return new List<AsakiNodeBase>(); // 返回空列表防止 NullReference
		}

		/// <summary>
		/// 泛型版本，自动转换类型
		/// </summary>
		public T GetNextNode<T>(AsakiNodeBase current, string portName = "Out") where T : AsakiNodeBase
		{
			return GetNextNode(current, portName) as T;
		}

		public void OnBeforeSerialize()
		{
			// 序列化前无需操作，数据都在 List 中
		}

		public void OnAfterDeserialize()
		{
			// 反序列化后（如 Ctrl+Z，或资源加载），缓存表是空的
			// 我们标记为未初始化，下次访问时会自动重建 (Lazy Load)
			// 或者更激进地：如果已经在运行中，强制刷新

			_isInitialized = false;
			_nodeLookup = null;
			_outgoingCache = null;
			_incomingCache = null;

			// 注意：不要在这里直接调用 InitializeRuntime()
			// 因为 Unity 反序列化是在非主线程或受限环境下进行的，
			// 复杂的字典操作可能不安全。
			// 我们通过置空标志位，让 GetInputConnection 等方法在下次调用时自动重建。
		}
	}
}
