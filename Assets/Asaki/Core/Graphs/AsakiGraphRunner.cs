using Asaki.Core.Blackboard;
using Asaki.Core.Context;
using System;
using UnityEngine;

namespace Asaki.Core.Graphs
{
	public abstract class AsakiGraphRunner<TGraph> : MonoBehaviour
		where TGraph : AsakiGraphBase
	{
		[Header("Graphs Data")]
		public TGraph GraphAsset;

		protected AsakiGraphRuntimeContext _context;

		protected virtual void Start()
		{
			if (GraphAsset == null)
			{
				Debug.LogWarning($"[AsakiGraphRunner] GraphAsset is null on {name}");
				return;
			}

			// 1. 初始化图结构缓存 (O(1) Lookup)
			GraphAsset.InitializeRuntime();

			// 2. 构建运行时上下文
			InitializeContext();

			// 3. 将 Asset 中配置的变量初值填入 Runtime 黑板
			InitializeBlackboardValues();

			OnGraphInitialized();
		}

		private void InitializeContext()
		{
			_context = new AsakiGraphRuntimeContext();
			_context.Owner = gameObject;

			// --- 作用域链构建 (Scope Chain Integration) ---

			// A. 尝试获取全局黑板 (Global Scope)
			// AsakiContext 是线程安全的，可以直接访问
			IAsakiBlackboard globalScope = null;
			if (AsakiContext.TryGet<IAsakiBlackboard>(out IAsakiBlackboard globalBB))
			{
				globalScope = globalBB;
			}

			// B. 创建本地黑板 (Local Scope)
			// 将全局黑板设为 Parent，实现 "Local -> Global" 的查找链
			// 依据 v2 设计：对本地黑板的修改绝不会污染 globalScope
			_context.Blackboard = new AsakiBlackboard(globalScope);
		}

		private void InitializeBlackboardValues()
		{
			if (GraphAsset == null || _context.Blackboard == null) return;
			string globalAssetPath = "Assets/Asaki/Resources/GlobalBlackboard.asset";
			var globalAsset = UnityEngine.Resources.Load<AsakiGlobalBlackboardAsset>("GlobalBlackboard");
			if (globalAsset != null)
			{
				// 先加载全局变量（作为父作用域）
				foreach (var globalVar in globalAsset.GlobalVariables)
				{
					// 如果局部没有同名变量，则继承全局值
					if (!GraphAsset.Variables.Exists(v => v.Name == globalVar.Name))
					{
						WriteVariableToRuntime(globalVar.Name, globalVar);
					}
				}
			}
			foreach (var variable in GraphAsset.Variables)
			{
				WriteVariableToRuntime(variable.Name, variable);
			}
		}
		private void WriteVariableToRuntime(string name, AsakiVariableDef variable)
		{
			if (variable.ValueData != null)
			{
				variable.ValueData.ApplyTo(_context.Blackboard, name);
			}
			else
			{
				Debug.LogWarning($"[AsakiRunner] Variable '{name}' has no data!");
			}
		}

		// 子类可以在这里做额外的初始化（比如绑定 UI、注册特定服务）
		protected virtual void OnGraphInitialized() { }

		// --- 生命周期管理 (New) ---

		protected virtual void OnDestroy()
		{
			// 必须清理，否则 Blackboard 中的 AsakiProperty 可能会残留 UI 订阅，导致内存泄漏
			if (_context != null)
			{
				_context.Dispose();
				_context = null;
			}
			OnNodeExecuted = null;
		}

		// --- 辅助 API (Helpers) ---

		/// <summary>
		/// 获取变量 (支持 Key 字符串自动转 Hash)
		/// </summary>
		public T GetVariable<T>(string key)
		{
			if (_context?.Blackboard == null) return default(T);
			// 隐式转换: string -> AsakiBlackboardKey (FNV-1a)
			return _context.Blackboard.GetValue<T>(key);
		}

		/// <summary>
		/// 设置变量 (Shadowing: 只修改本地)
		/// </summary>
		public void SetVariable<T>(string key, T value)
		{
			if (_context?.Blackboard == null) return;
			// 隐式转换: string -> AsakiBlackboardKey (FNV-1a)
			_context.Blackboard.SetValue<T>(key, value);
		}

		/// <summary>
		/// 获取当前节点某个输入端口的值。
		/// 如果有连线，则回溯上游节点并计算返回值；
		/// 如果无连线，则返回默认值。
		/// </summary>
		protected T GetInputValue<T>(AsakiNodeBase currentNode, string inputPortName, T fallback = default(T))
		{
			// 1. 查找连接到该端口的线
			AsakiEdgeData edge = GraphAsset.GetInputConnection(currentNode, inputPortName);
			if (edge == null) return fallback;

			// 2. 找到上游节点
			AsakiNodeBase sourceNode = GraphAsset.GetNodeByGUID(edge.BaseNodeGUID);
			if (sourceNode == null) return fallback;

			// 3. 计算上游节点的值 (Resolve)
			// 这里我们实现一个简易的求值分发器
			return ResolveNodeValue<T>(sourceNode, edge.BasePortName);
		}

		/// <summary>
		/// 解析节点的值 (当节点被当作数据源连接时调用)
		/// </summary>
		protected virtual T ResolveNodeValue<T>(AsakiNodeBase node, string outputPortName)
		{
			if (node is AsakiGetVariableNode getVarNode)
			{
				if (_context.Blackboard == null) return default(T);

				T value = _context.Blackboard.GetValue<T>(getVarNode.VariableName);
				
				return value;
			}

			Debug.LogWarning($"[AsakiRunner] Cannot resolve value from node type: {node.GetType().Name}");
			return default(T);
		}



		// ================================================================
		// ★ 核心驱动逻辑 2: 执行流 (Execution Flow)
		// ================================================================

		// 节点执行事件 (Editor Only)
		public event Action<AsakiNodeBase> OnNodeExecuted;

		/// <summary>
		/// 执行单个节点逻辑
		/// </summary>
		protected virtual void ExecuteNode(AsakiNodeBase node)
		{
			if (node == null) return;

			#if UNITY_EDITOR
			// 触发调试事件
			OnNodeExecuted?.Invoke(node);
			#endif

			// --- Case A: Set Variable Node (写入黑板) ---
			if (node is AsakiSetVariableNode setVarNode)
			{
				if (_context.Blackboard == null) return;

				// 1. 获取要写入的值
				// 注意：这里需要根据变量类型动态获取值，比较棘手因为 T 未知
				// 但在 SetValue<T> 中我们需要泛型。
				// 策略：使用 object 重载或根据 VariableType switch

				object valueToWrite = GetInputValue<object>(setVarNode, "Value");

				// 2. 写入黑板 (需要处理类型分发)
				// 这里为了演示简单，我们假设 Blackboard 有 Object 重载，或者我们手动 switch
				WriteToBlackboard(setVarNode.VariableName, valueToWrite);

				// 3. 继续执行后续节点
				AsakiNodeBase nextNode = GraphAsset.GetNextNode(node, "Out");
				if (nextNode != null) ExecuteNode(nextNode);
				return;
			}

			// --- Case B: 其他执行节点 ---
			// 子类可以 override 这个方法处理自己的业务节点
			OnExecuteCustomNode(node);
		}

		// 辅助：处理类型写入
		private void WriteToBlackboard(string key, object value)
		{
			if (value == null)
			{
				Debug.LogWarning($"[AsakiRunner] Trying to set null value for key '{key}'. Ignore.");
				return;
			}

			try
			{
				// 1. 优先匹配 AsakiBlackboard 内部优化的“原生类型” (Fast Path)
				// 这些类型在 Blackboard 内部有专用桶 (IntBucket, FloatBucket 等)，直接调用最快
				switch (value)
				{
					case int i:
						_context.Blackboard.SetValue(key, i);
						break;
					case float f:
						_context.Blackboard.SetValue(key, f);
						break;
					case bool b:
						_context.Blackboard.SetValue(key, b);
						break;
					case string s:
						_context.Blackboard.SetValue(key, s);
						break;
					case Vector3 v3:
						_context.Blackboard.SetValue(key, v3);
						break;
            
					// 补充其他常用 Unity 类型
					case Vector2 v2:
						_context.Blackboard.SetValue(key, v2);
						break;
					case Vector3Int v3i:
						_context.Blackboard.SetValue(key, v3i);
						break;
					case Vector2Int v2i:
						_context.Blackboard.SetValue(key, v2i);
						break;
					case Color c:
						_context.Blackboard.SetValue(key, c);
						break;

					// 2. 兜底逻辑：处理所有用户自定义类型 (Universal Path)
					// 只要你是 struct 或 class，这里都能处理
					default:
						WriteGenericToBlackboard(key, value);
						break;
				}
			}
			catch (Exception e)
			{
				// 这里 value.GetType() 可能会有轻微 GC，但在 Error 情况下可忽略
				Debug.LogError($"[AsakiRunner] SetVariable Failed: Key={key}, Type={value.GetType().Name}, Value={value}. Error: {e.Message}");
			}
		}
		
		private void WriteGenericToBlackboard(string key, object value)
		{
			// 获取 SetValue<T> 方法的元数据
			// 注意：这里假设 IAsakiBlackboard 接口或实现类上有 SetValue<T>
			// 如果是用接口调用，需要获取 InterfaceMapping 或直接从实例类型获取
			var methodInfo = _context.Blackboard.GetType().GetMethod("SetValue");
    
			if (methodInfo != null)
			{
				// 构造 SetValue<MyCustomType>
				var genericMethod = methodInfo.MakeGenericMethod(value.GetType());
        
				// 调用
				genericMethod.Invoke(_context.Blackboard, new object[] { key, value });
			}
			else
			{
				throw new MissingMethodException("[AsakiRunner] Could not find SetValue<T> on Blackboard instance.");
			}
		}

		/// <summary>
		/// 子类重写此方法处理特定业务节点 (如 PlaySound, MoveTo)
		/// </summary>
		protected virtual void OnExecuteCustomNode(AsakiNodeBase node) { }

	}
}
