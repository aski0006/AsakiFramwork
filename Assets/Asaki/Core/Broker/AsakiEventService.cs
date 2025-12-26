using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Asaki.Core.Broker
{
	/// <summary>
	/// [Implementation] 基于实例的高性能事件总线。
	/// 解决了 IL2CPP 静态构造函数无法热更、无法重置的致命缺陷。
	/// </summary>
	public class AsakiEventService : IAsakiEventService
	{
		// ========================================================================
		// Internal Bucket Strategy
		// ========================================================================

		// 抽象接口用于统一管理 Cleanup
		private interface IEventBucket
		{
			void Cleanup();
		}

		// 具体的泛型桶 (Instance Class, not Static)
		private class EventBucket<T> : IEventBucket where T : IAsakiEvent
		{
			// 订阅列表 (Write heavy)
			private readonly List<IAsakiHandler<T>> _handlers = new List<IAsakiHandler<T>>(8);

			// 缓存数组 (Read heavy, Copy-On-Write)
			private IAsakiHandler<T>[] _cache = Array.Empty<IAsakiHandler<T>>();

			// 脏标记与锁
			private bool _dirty = false;
			private readonly object _bucketLock = new object();

			public void Subscribe(IAsakiHandler<T> handler)
			{
				lock (_bucketLock)
				{
					if (!_handlers.Contains(handler))
					{
						_handlers.Add(handler);
						_dirty = true;
					}
				}
			}

			public void Unsubscribe(IAsakiHandler<T> handler)
			{
				lock (_bucketLock)
				{
					if (_handlers.Remove(handler))
					{
						_dirty = true;
					}
				}
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void Publish(T e)
			{
				// 1. 检查脏标记 (Double-Check Locking 变种)
				if (_dirty)
				{
					lock (_bucketLock)
					{
						if (_dirty)
						{
							_cache = _handlers.ToArray();
							_dirty = false;
						}
					}
				}

				// 2. 获取缓存数组引用 (原子操作)
				var array = _cache;
				int count = array.Length;

				// 3. 极速遍历 (Zero GC)
				for (int i = 0; i < count; i++)
				{
					// [Fix Defect-2] 移除 try-catch，让异常冒泡
					// 这样 Debug.LogException 才能捕获到 Handler 内部的具体行号
					array[i].OnEvent(e);
				}
			}

			public void Cleanup()
			{
				lock (_bucketLock)
				{
					_handlers.Clear();
					_cache = Array.Empty<IAsakiHandler<T>>();
					_dirty = false;
				}
			}
		}

		// ========================================================================
		// Bus Implementation
		// ========================================================================

		// 存储所有类型的桶
		// Key: Event Type, Value: EventBucket<T>
		private readonly Dictionary<Type, IEventBucket> _buckets = new Dictionary<Type, IEventBucket>();
		private readonly object _busLock = new object();

		public void Subscribe<T>(IAsakiHandler<T> handler) where T : IAsakiEvent
		{
			GetBucket<T>().Subscribe(handler);
		}

		public void Unsubscribe<T>(IAsakiHandler<T> handler) where T : IAsakiEvent
		{
			GetBucket<T>().Unsubscribe(handler);
		}

		public void Publish<T>(T e) where T : IAsakiEvent
		{
			// 极速路径：如果桶不存在，说明没订阅者，直接跳过 (比静态类访问还快，因为省了泛型初始化检查)
			if (TryGetBucket<T>(out var bucket))
			{
				bucket.Publish(e);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private EventBucket<T> GetBucket<T>() where T : IAsakiEvent
		{
			// 快速检查
			if (TryGetBucket<T>(out var bucket)) return bucket;

			// 慢速创建
			lock (_busLock)
			{
				// Double check
				if (TryGetBucket(out bucket)) return bucket;

				bucket = new EventBucket<T>();
				_buckets[typeof(T)] = bucket;
				return bucket;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private bool TryGetBucket<T>(out EventBucket<T> bucket) where T : IAsakiEvent
		{
			// Dictionary 读操作在无扩容时是线程安全的
			// 但为了绝对稳健，这里依赖 _buckets 在运行时主要只增不减的特性
			if (_buckets.TryGetValue(typeof(T), out IEventBucket b))
			{
				bucket = (EventBucket<T>)b; // 强转开销极低
				return true;
			}
			bucket = null;
			return false;
		}

		public void Dispose()
		{
			// [Fix Defect-3 & 4] 真正的清理
			lock (_busLock)
			{
				foreach (IEventBucket bucket in _buckets.Values)
				{
					bucket.Cleanup();
				}
				_buckets.Clear();
			}
		}
	}
}
