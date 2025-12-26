#if ASAKI_USE_ADDRESSABLE

using Asaki.Core.Coroutines;
using Asaki.Core.Resources;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace Asaki.Unity.Services.Resources.Strategies
{
	public class AsakiAddressablesStrategy : IAsakiResStrategy
	{
		public string StrategyName => "Addressables (Pro)";

		// 引用 RoutineService 以使用 WaitFrame
		private readonly IAsakiCoroutineService _routine;

		public AsakiAddressablesStrategy(IAsakiCoroutineService routine)
		{
			_routine = routine;
		}

		public async Task InitializeAsync()
		{
			// Addressables 初始化
			var handle = Addressables.InitializeAsync();
			await handle.Task; // 原生支持 Task
		}

		public async Task<Object> LoadAssetInternalAsync(string location, Type type, Action<float> onProgress, CancellationToken token)
		{
			// 1. 发起加载 (不使用泛型，为了兼容性)
			// location 可以是 Addressable Name, Label, 或 Key
			var handle = Addressables.LoadAssetAsync<Object>(location);

			try
			{
				// 2. 如果不需要进度，直接使用 Task (支持取消)
				if (onProgress == null)
				{
					// 使用 WaitAsync 扩展来支持 token 取消
					// 注意：Addressables 的 handle.Task 本身不支持 CancellationToken，需要我们在外部处理
					return await WrapTask(handle, token);
				}

				// 3. 进度轮询模式 (基于 AsakiRoutine)
				while (!handle.IsDone)
				{
					if (token.IsCancellationRequested)
					{
						// 释放正在进行的句柄
						Addressables.Release(handle);
						throw new OperationCanceledException(token);
					}

					// 报告进度 (Addressables 的 PercentComplete 比较准确)
					onProgress.Invoke(handle.PercentComplete);

					// 使用框架标准的帧等待
					await _routine.WaitFrame(token);
				}

				if (handle.Status == AsyncOperationStatus.Succeeded)
				{
					onProgress.Invoke(1f);
					return handle.Result;
				}
				else
				{
					// 抛出详细异常
					Exception exception = handle.OperationException ?? new Exception($"[Addressables] Failed to load: {location}");
					// 失败时也要 Release handle，防止内存泄露 (虽然 Failed 状态通常不占资源，但保持习惯)
					Addressables.Release(handle);
					throw exception;
				}
			}
			catch (Exception)
			{
				// 双重保险：发生任何异常导致流程中断，确保 Handle 被释放
				// 注意：如果 Handle 已经成功并返回了 Result，这里不应该释放，应该交给 Service 的 Release 逻辑
				if (handle.IsValid() && handle.Status != AsyncOperationStatus.Succeeded)
				{
					Addressables.Release(handle);
				}
				throw;
			}
		}

		public void UnloadAssetInternal(string location, Object asset)
		{
			// Addressables 释放资源需要传入 Asset 实例 或 Handle
			// 只要当初是用 LoadAssetAsync 加载出来的对象，直接 Release 对象即可，
			// Addressables 内部会查找对应的 Handle 并减少引用计数。
			if (asset != null)
			{
				Addressables.Release(asset);
			}
		}

		// --- 辅助：将 Addressables Task 包装为支持取消的 Task ---
		private async Task<Object> WrapTask(AsyncOperationHandle<Object> handle, CancellationToken token)
		{
			var tcs = new TaskCompletionSource<Object>();

			// 注册取消回调
			using (token.Register(() =>
				{
					if (handle.IsValid()) Addressables.Release(handle);
					tcs.TrySetCanceled();
				}))
			{
				try
				{
					Object result = await handle.Task;
					return result;
				}
				catch (Exception ex)
				{
					// Addressables 内部异常
					if (handle.IsValid()) Addressables.Release(handle);
					throw ex;
				}
			}
		}
	}
}
#endif
