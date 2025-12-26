using Asaki.Core.Context;
using System.Collections.Generic;

// 仅用于排序

namespace Asaki.Core.Simulation
{
	public class AsakiSimulationManager : IAsakiService
	{
		// 包装器，用于绑定优先级
		private struct TickableWrapper
		{
			public IAsakiTickable Tickable;
			public int Priority;
		}

		private readonly List<TickableWrapper> _tickables = new List<TickableWrapper>();
		private readonly List<IAsakiFixedTickable> _fixedTickables = new List<IAsakiFixedTickable>();

		// 脏标记，用于避免每帧排序
		private bool _isDirty = false;

		// --- Standard Tick ---

		public void Register(IAsakiTickable tickable, int priority = (int)TickPriority.Normal)
		{
			// 查重 (O(N)，启动阶段可接受)
			for (int i = 0; i < _tickables.Count; i++)
			{
				if (_tickables[i].Tickable == tickable) return;
			}

			_tickables.Add(new TickableWrapper { Tickable = tickable, Priority = priority });
			_isDirty = true;
		}

		public void Unregister(IAsakiTickable tickable)
		{
			// 简单移除，O(N)
			for (int i = 0; i < _tickables.Count; i++)
			{
				if (_tickables[i].Tickable == tickable)
				{
					_tickables.RemoveAt(i);
					return;
				}
			}
		}

		public void Tick(float deltaTime)
		{
			// 1. 如果有新注册的，先排序 (Stable Sort 保证同优先级按注册顺序)
			if (_isDirty)
			{
				_tickables.Sort((a, b) => a.Priority.CompareTo(b.Priority));
				_isDirty = false;
			}

			// 2. 正序遍历 (FIFO)
			// 这里的安全性假设：不会在 Tick 循环中 Unregister 自身
			// 如果需要支持 Tick 中移除，需要改用 "for i" 并处理索引回退，或者使用 "PendingRemoveQueue"
			// 鉴于 Asaki 是架构级模块，模块生命周期通常很长，这里用 for i 足够安全且高性能
			for (int i = 0; i < _tickables.Count; i++)
			{
				// [防御性编程] 防止空引用
				TickableWrapper wrapper = _tickables[i];
				if (wrapper.Tickable != null)
				{
					wrapper.Tickable.Tick(deltaTime);
				}
			}
		}

		// --- Fixed Tick ---
		// (FixedTick 同理，建议也加上 deltaTime 参数以防万一，虽然 physics step 也就是 Time.fixedDeltaTime)
		public void FixedTick(float fixedDeltaTime)
		{
			for (int i = 0; i < _fixedTickables.Count; i++)
			{
				_fixedTickables[i].FixedTick(fixedDeltaTime);
			}
		}
	}
}
