using Asaki.Core.Broker;
using Asaki.Core.Context;
using Asaki.Core.Pooling;
using Asaki.Core.Resources;
using Asaki.Core.Simulation;
using Asaki.Core.UI;
using Asaki.Unity.Configuration;
using Asaki.Unity.Extensions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Asaki.Unity.Services.UI
{
	public class AsakiUIManager : IAsakiUIService, IAsakiTickable
	{
		private AsakiUIRoot _asakiUIRoot;
		private IAsakiResService _resService;
		private IAsakiPoolService _poolService;
		private IAsakiEventService _eventService;
		private AsakiSimulationManager _asakiSimulationManager;
		private AsakiUIConfig _uiConfig;
		private readonly Vector2 _refRes;
		private readonly float _matchMode;

		// 窗口栈 (Normal层)
		private Stack<IAsakiWindow> _normalStack = new Stack<IAsakiWindow>();

		// [新增] Popup层计数器 (用于判断是否需要恢复 Normal 层输入)
		private int _activePopupCount = 0;

		// [新增] 窗口实例到层级的映射缓存 (避免 Close 时无法获知层级)
		private readonly Dictionary<IAsakiWindow, AsakiUILayer> _windowLayerMap = new Dictionary<IAsakiWindow, AsakiUILayer>();

		private readonly HashSet<string> _pooledAssets = new HashSet<string>();

		// 线程安全的销毁队列
		private readonly ConcurrentQueue<IAsakiWindow> _pendingDestroyQueue = new ConcurrentQueue<IAsakiWindow>();

		public AsakiUIManager(AsakiUIConfig configAsset, Vector2 refRes, float matchMode, IAsakiEventService eventService, IAsakiResService resService, IAsakiPoolService poolService)
		{
			_uiConfig = configAsset;
			_refRes = refRes;
			_matchMode = matchMode;
			_eventService = eventService;
			_resService = resService;
			_poolService = poolService;
		}

		public void OnInit()
		{
			_asakiSimulationManager = AsakiContext.Get<AsakiSimulationManager>();
			_asakiSimulationManager.Register(this);

			if (_asakiUIRoot == null)
			{
				GameObject rootGo = new GameObject("Asaki_UIRoot");
				Object.DontDestroyOnLoad(rootGo);
				_asakiUIRoot = rootGo.AddComponent<AsakiUIRoot>();
				_asakiUIRoot.Initialize(_refRes, _matchMode);
			}
		}

		public Task OnInitAsync()
		{
			if (_uiConfig != null)
			{
				_uiConfig.InitializeLookup();
			}
			else
			{
				Debug.LogWarning("[AsakiUI] No UIConfig assigned in AsakiConfig!"); // TODO: [Asaki] -> Asaki.ALog.Warn
			}
			return Task.CompletedTask;
		}

		public async Task<T> OpenAsync<T>(int uiId, object args = null, CancellationToken token = default(CancellationToken))
			where T : class, IAsakiWindow
		{
			// 基础检查
			if (token.IsCancellationRequested) return null;

			if (_uiConfig == null || !_uiConfig.TryGet(uiId, out UIInfo info))
			{
				Debug.LogError($"[AsakiUI] UIID {uiId} not found.");
				return null;
			}

			ResHandle<GameObject> rawHandle = null;
			GameObject instance = null;
			T window;

			try
			{
				Transform parent = _asakiUIRoot.GetLayerNode(info.Layer);

				// === V5.1 池化分支 ===
				if (info.UsePool)
				{
					// [Step 1] 异步预热
					await _poolService.PrewarmAsync(info.AssetPath, 1);
					if (token.IsCancellationRequested) return null;

					// [Step 2] 同步生成
					instance = _poolService.Spawn(info.AssetPath, Vector3.zero, Quaternion.identity, parent);
					_pooledAssets.Add(info.AssetPath);

					window = instance.GetComponent<T>();
					if (window is AsakiUIWindow baseWindow)
					{
						baseWindow.IsPooled = true;
						baseWindow.PoolKey = info.AssetPath;
						baseWindow.ResHandle = null;
					}
				}
				// === V5.1 普通分支 ===
				else
				{
					// [Step 1] 异步加载
					rawHandle = await _resService.LoadAsync<GameObject>(info.AssetPath, token);
					if (!rawHandle.IsValid) return null;
					if (token.IsCancellationRequested)
					{
						rawHandle.Dispose();
						return null;
					}

					// [Step 2] 实例化
					instance = Object.Instantiate(rawHandle.Asset, parent);
					window = instance.GetComponent<T>();
					if (window is AsakiUIWindow baseWindow)
					{
						baseWindow.IsPooled = false;
						baseWindow.ResHandle = new AsakiUIResourceHandleAdapter(rawHandle);
						rawHandle = null;
					}
				}

				if (window == null) throw new Exception($"Window component missing on {instance.name}");

				// [新增] 记录窗口层级，便于 Close 时处理逻辑
				_windowLayerMap[window] = info.Layer;

				// === [核心优化] 输入屏蔽逻辑 ===
				// 如果打开的是 Popup，物理屏蔽 Normal 层的输入
				if (info.Layer == AsakiUILayer.Popup)
				{
					_activePopupCount++;
					if (_activePopupCount == 1) // 只要有一个 Popup 存在，就屏蔽下层
					{
						_asakiUIRoot.SetLayerRaycast(AsakiUILayer.Normal, false);
					}
				}

				await window.OnOpenAsync(args, token);

				// 栈管理 (仅 Normal 层入栈)
				if (info.Layer == AsakiUILayer.Normal)
				{
					if (_normalStack.Count > 0) _normalStack.Peek().OnCover();
					_normalStack.Push(window);
				}

				return window;
			}
			catch (Exception e)
			{
				// 异常回滚
				if (instance != null)
				{
					if (info.UsePool) _poolService.Despawn(instance, info.AssetPath);
					else Object.Destroy(instance);
				}
				rawHandle?.Dispose();

				Debug.LogError($"[AsakiUI] OpenUI Failed: {e}");
				return null;
			}
		}

		// [修改] 泛型关闭接口
		public void Close<T>() where T : IAsakiWindow
		{
			if (_normalStack.Count > 0 && _normalStack.Peek() is T)
			{
				Close(_normalStack.Peek());
				return;
			}

			IAsakiWindow target = _normalStack.FirstOrDefault(w => w is T);
			if (target != null)
			{
				Close(target);
			}
			else
			{
				Debug.LogWarning($"[AsakiUI] Window {typeof(T).Name} not found in stack.");
			}
		}

		// [修改] 线程安全的关闭入口
		public void Close(IAsakiWindow window)
		{
			if (window == null) return;
			_pendingDestroyQueue.Enqueue(window);
		}

		public void Back()
		{
			if (_normalStack.Count > 0)
			{
				Close(_normalStack.Peek());
			}
		}

		// [新增] 心跳驱动，处理销毁列表
		public void Tick(float deltaTime)
		{
			while (_pendingDestroyQueue.TryDequeue(out IAsakiWindow window))
			{
				ProcessCloseRequest(window);
			}
		}

		// [修改] 实际的主线程关闭逻辑
		private void ProcessCloseRequest(IAsakiWindow window)
		{
			// 1. 栈状态维护
			if (_normalStack.Count > 0 && _normalStack.Peek() == window)
			{
				_normalStack.Pop();
				if (_normalStack.Count > 0) _normalStack.Peek().OnReveal();
			}
			else if (_normalStack.Contains(window))
			{
				RemoveWindowFromStackMiddle(window);
			}

			// 2. === [核心优化] 恢复输入逻辑 ===
			if (_windowLayerMap.TryGetValue(window, out AsakiUILayer layer))
			{
				if (layer == AsakiUILayer.Popup)
				{
					_activePopupCount--;
					if (_activePopupCount <= 0)
					{
						_activePopupCount = 0; // 防御性归零
						// 如果没有 Popup 了，恢复 Normal 层输入
						_asakiUIRoot.SetLayerRaycast(AsakiUILayer.Normal, true);
					}
				}
				_windowLayerMap.Remove(window); // 清理映射
			}

			// 3. 执行关闭
			HandleCloseAsync(window).FireAndForget();
		}

		private void RemoveWindowFromStackMiddle(IAsakiWindow target)
		{
			var temp = new Stack<IAsakiWindow>();
			while (_normalStack.Count > 0)
			{
				IAsakiWindow cur = _normalStack.Pop();
				if (cur == target) break; // 找到并丢弃
				temp.Push(cur);
			}
			while (temp.Count > 0) _normalStack.Push(temp.Pop());
		}

		private async Task HandleCloseAsync(IAsakiWindow window)
		{
			// Window 内部处理回收/销毁
			await window.OnCloseAsync(CancellationToken.None);
		}

		public void OnDispose()
		{
			// 1. 关闭所有窗口
			while (_normalStack.Count > 0)
			{
				HandleCloseAsync(_normalStack.Pop()).FireAndForget();
			}
			_normalStack.Clear();

			// 2. 释放池
			if (_poolService != null)
			{
				foreach (string assetPath in _pooledAssets)
				{
					_poolService.ReleasePool(assetPath);
				}
			}
			_pooledAssets.Clear();
			_windowLayerMap.Clear();

			// 3. 注销 Tick
			if (_asakiSimulationManager != null)
			{
				_asakiSimulationManager.Unregister(this);
			}

			// 4. 销毁 Root
			if (_asakiUIRoot != null)
			{
				Object.Destroy(_asakiUIRoot.gameObject);
				_asakiUIRoot = null;
			}
		}
	}
}
