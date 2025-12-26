using Asaki.Core.Coroutines;
using Asaki.Core.Resources;
using Asaki.Unity.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Asaki.Unity.Services.Resources
{
	public class AsakiResService : IAsakiResService
	{
		private readonly IAsakiResStrategy _strategy;
		private readonly IAsakiCoroutineService _coroutineService;
		private readonly IAsakiResDependencyLookup _asakiResDependencyLookup;

		private class ResRecord
		{
			public string Location;
			public Object Asset;
			public int RefCount;
			// 使用 HashSet 防止重复依赖
			public HashSet<string> DependencyLocations = new HashSet<string>();
			public TaskCompletionSource<Object> LoadingTcs = new TaskCompletionSource<Object>(TaskCreationOptions.RunContinuationsAsynchronously);

			public Action<float> ProgressCallbacks;

			public void ReportProgress(float progress)
			{
				ProgressCallbacks?.Invoke(progress);
			}
		}

		private readonly Dictionary<string, ResRecord> _cache = new Dictionary<string, ResRecord>();
		private readonly object _lock = new object();
		private int _timeoutSeconds = DefaultTimeoutSeconds;
		private const int DefaultTimeoutSeconds = 10000;
		public AsakiResService(IAsakiResStrategy strategy, IAsakiCoroutineService coroutineService, IAsakiResDependencyLookup asakiResDependencyLookup)
		{
			_strategy = strategy;
			_coroutineService = coroutineService;
			_asakiResDependencyLookup = asakiResDependencyLookup;
		}

		public void SetTimeoutSeconds(int timeoutSeconds)
		{
			_timeoutSeconds = Mathf.Max(DefaultTimeoutSeconds, timeoutSeconds);
		}
		public Task OnInitAsync()
		{
			return _strategy.InitializeAsync();
		}
		public void OnInit() { }

		public void OnDispose()
		{
			lock (_lock)
			{
				foreach (var kvp in _cache)
				{
					if (kvp.Value.Asset != null)
						_strategy.UnloadAssetInternal(kvp.Key, kvp.Value.Asset);
				}
				_cache.Clear();
			}
		}

		// =========================================================
		// Load Interface
		// =========================================================

		public Task<ResHandle<T>> LoadAsync<T>(string location, CancellationToken token) where T : class
		{
			return LoadAsync<T>(location, null, token);
		}

		public async Task<ResHandle<T>> LoadAsync<T>(string location, Action<float> onProgress, CancellationToken token) where T : class
		{
			ResRecord record = GetOrCreateRecord(location);

			// 进度回调注册
			if (onProgress != null)
			{
				if (record.Asset != null) onProgress(1f);
				else record.ProgressCallbacks += onProgress;
			}

			// 乐观锁引用计数
			Interlocked.Increment(ref record.RefCount);

			try
			{
				Object assetObj = await record.LoadingTcs.Task.WaitAsync(token);

				if (assetObj is T tAsset)
				{
					return new ResHandle<T>(location, tAsset, this);
				}
				else
				{
					throw new InvalidCastException($"[Resources] Type mismatch for {location}. Expected {typeof(T)}, got {assetObj?.GetType()}");
				}
			}
			catch (Exception)
			{
				// 发生取消或错误时，回滚引用
				ReleaseInternal(location);
				throw;
			}
			finally
			{
				// 清理进度委托，防止内存泄漏
				if (onProgress != null)
				{
					record.ProgressCallbacks -= onProgress;
				}
			}
		}

		// =========================================================
		// Internal Logic
		// =========================================================

		private ResRecord GetOrCreateRecord(string location)
		{
			ResRecord record;
			bool isOwner = false;

			lock (_lock)
			{
				if (!_cache.TryGetValue(location, out record))
				{
					record = new ResRecord { Location = location };
					_cache.Add(location, record);
					isOwner = true;
				}
			}

			if (isOwner)
			{
				SafeStartLoadTask(record);
			}

			return record;
		}

		private async void SafeStartLoadTask(ResRecord record)
		{
			try
			{
				await LoadTaskInternal(record);
			}
			catch (Exception ex)
			{
				// 确保 TCS 终结
				if (!record.LoadingTcs.Task.IsCompleted)
				{
					record.LoadingTcs.TrySetException(ex);
				}

				// 从缓存移除
				lock (_lock) { _cache.Remove(record.Location); }

				// 错误回滚：释放已加载的依赖
				// 注意：这里需要对 DependencyLocations 加锁，防止与 ReleaseInternal 冲突
				lock (record.DependencyLocations)
				{
					foreach (string dep in record.DependencyLocations) ReleaseInternal(dep);
				}
			}
		}

		private async Task LoadTaskInternal(ResRecord record)
		{
			try
			{
				// --- 1. 依赖加载 ---
				var deps = _asakiResDependencyLookup.GetDependencies(record.Location);
				if (deps != null)
				{
					foreach (string depLoc in deps)
					{
						ResRecord depRecord = GetOrCreateRecord(depLoc);

						// [关键修正1] 先增加引用计数
						Interlocked.Increment(ref depRecord.RefCount);

						// [关键修正2] 检查主资源有效性并记录依赖
						// 如果此时主资源已经被 Release 并移出缓存，我们不能继续持有依赖，否则会导致依赖泄露
						bool isValid = false;
						lock (_lock)
						{
							if (_cache.ContainsKey(record.Location))
							{
								lock (record.DependencyLocations)
								{
									record.DependencyLocations.Add(depLoc);
								}
								isValid = true;
							}
						}

						// 如果主资源已失效（被取消），立即释放刚才获取的依赖并终止
						if (!isValid)
						{
							ReleaseInternal(depLoc);
							throw new OperationCanceledException($"[Resources] Loading aborted for {record.Location}");
						}

						// [关键修正3] 记录之后再等待
						// 这样即使 Wait 超时抛出异常，catch 块也能在 DependencyLocations 中找到并释放它
						var dependencyTask = depRecord.LoadingTcs.Task;
						Task finishedTask = await Task.WhenAny(dependencyTask, Task.Delay(_timeoutSeconds));

						if (finishedTask != dependencyTask)
						{
							throw new TimeoutException($"[Resources] Dependency Timeout: {depLoc} (Possible circular dependency)");
						}

						// 确保依赖任务没有报错
						if (dependencyTask.IsFaulted) throw dependencyTask.Exception;
					}
				}

				// --- 2. 自身加载 ---

				// 切换到主线程
				Object asset = await _coroutineService.RunTask(async () => await _strategy.LoadAssetInternalAsync(
					record.Location,
					typeof(Object),
					record.ReportProgress,
					CancellationToken.None
				));

				if (asset == null) throw new Exception($"[Resources] Asset not found: {record.Location}");

				record.Asset = asset;
				record.LoadingTcs.TrySetResult(asset);
				record.ReportProgress(1f);
			}
			catch (Exception ex)
			{
				record.LoadingTcs.TrySetException(ex);

				// 清理缓存
				lock (_lock) { _cache.Remove(record.Location); }

				// 回滚依赖
				lock (record.DependencyLocations)
				{
					foreach (string dep in record.DependencyLocations) ReleaseInternal(dep);
				}
			}
		}

		// =========================================================
		// Release Logic
		// =========================================================

		public void Release(string location)
		{
			ReleaseInternal(location);
		}

		private void ReleaseInternal(string rootLocation)
		{
			var pendingRelease = new Stack<string>();
			pendingRelease.Push(rootLocation);

			lock (_lock)
			{
				while (pendingRelease.Count > 0)
				{
					string currentLocation = pendingRelease.Pop();

					if (!_cache.TryGetValue(currentLocation, out ResRecord record)) continue;
					record.RefCount--;

					if (record.RefCount > 0) continue;
					if (record.Asset != null)
					{
						try { _strategy.UnloadAssetInternal(currentLocation, record.Asset); }
						catch (Exception e) { Debug.LogError(e); } // TODO: [Asaki] -> Asaki.ALog.Error
					}

					// 必须先移除，防止 LoadTaskInternal 继续往里面加依赖
					_cache.Remove(currentLocation);

					if (record.DependencyLocations == null) continue;

					foreach (string dep in record.DependencyLocations)
					{
						pendingRelease.Push(dep);
					}
				}
			}
		}

		// =========================================================
		// Batch Operations
		// =========================================================

		public async Task<List<ResHandle<T>>> LoadBatchAsync<T>(IEnumerable<string> locations, Action<float> onProgress, CancellationToken token) where T : class
		{
			var locList = locations.ToList();
			if (locList.Count == 0)
			{
				onProgress?.Invoke(1f);
				return new List<ResHandle<T>>();
			}

			// [关键修正4] 批量进度聚合
			// 我们不能直接把 onProgress 传给每个任务，否则进度条会乱跳
			float[] progresses = new float[locList.Count];

			Action<float> GetProgressHandler(int index)
			{
				return (p) =>
				{
					progresses[index] = p;
					{
						// 计算平均进度
						float total = 0f;
						for (int i = 0; i < progresses.Length; i++) total += progresses[i];
						onProgress(total / progresses.Length);
					}
				};
			}

			var tasks = new Task<ResHandle<T>>[locList.Count];
			for (int i = 0; i < locList.Count; i++)
			{
				// 为每个任务分配一个专属的进度回调
				tasks[i] = LoadAsync<T>(locList[i], onProgress == null ? null : GetProgressHandler(i), token);
			}

			var results = await Task.WhenAll(tasks);
			return results.ToList();
		}

		public Task<List<ResHandle<T>>> LoadBatchAsync<T>(IEnumerable<string> locations, CancellationToken token) where T : class
		{
			return LoadBatchAsync<T>(locations, null, token);
		}

		public void ReleaseBatch(IEnumerable<string> locations)
		{
			foreach (string location in locations) Release(location);
		}
	}
}
