using Asaki.Core.Coroutines;
using Cysharp.Threading.Tasks;
using System;
using System.Collections; // 引入 IEnumerator
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Asaki.Unity.Services.Coroutines
{
	/// <summary>
	/// [异步服务实现] Part 3: Tasks & Orchestration (Native Refactored)
	/// </summary>
	public partial class AsakiCoroutineProvider
	{
		// =========================================================
		// 1. 任务执行包装
		// =========================================================

		public Task RunTask(Func<Task> taskFunc, CancellationToken token = default(CancellationToken))
		{
			// Track 负责了计数和异常时的计数恢复，直接复用
			return Track(taskFunc);
		}

		public async Task<T> RunTask<T>(Func<Task<T>> taskFunc, CancellationToken token = default(CancellationToken))
		{
			// 链接 Token (虽然 taskFunc 内部可能不使用，但作为 API 契约建议处理)
			// 注意：这里我们无法强行取消 taskFunc 内部的逻辑，除非 taskFunc 接收 token
			// 所以这里的 token 主要是为了 Check
			if (token.IsCancellationRequested) throw new OperationCanceledException(token);

			Interlocked.Increment(ref _runningTaskCount);
			try
			{
				return await taskFunc();
			}
			finally
			{
				Interlocked.Decrement(ref _runningTaskCount);
			}
		}

		// =========================================================
		// 2. 快捷调用 (自动享受 Native 优化)
		// =========================================================

		public async Task DelayedCall(float delaySeconds, Action action, CancellationToken token = default(CancellationToken), bool unscaled = false)
		{
			// 使用 Track 确保计入 RunningTaskCount
			await Track(async () =>
			{
				CancellationToken linkedToken = CreateLinkedToken(token);

				// 这里调用的是 Part 2 中已经优化的 Native 方法
				if (unscaled) await WaitSecondsUnscaled(delaySeconds, linkedToken);
				else await WaitSeconds(delaySeconds, linkedToken);

				if (!linkedToken.IsCancellationRequested) action?.Invoke();
			});
		}

		public async Task NextFrameCall(Action action, CancellationToken token = default(CancellationToken))
		{
			await Track(async () =>
			{
				CancellationToken linkedToken = CreateLinkedToken(token);
				await WaitFrame(linkedToken);
				if (!linkedToken.IsCancellationRequested) action?.Invoke();
			});
		}

		public async Task When(Func<bool> condition, Action action, CancellationToken token = default(CancellationToken))
		{
			await Track(async () =>
			{
				CancellationToken linkedToken = CreateLinkedToken(token);
				await WaitUntil(condition, linkedToken);
				if (!linkedToken.IsCancellationRequested) action?.Invoke();
			});
		}

		// =========================================================
		// 3. 批量与流程控制
		// =========================================================

		public Task WaitAll(params Task[] tasks)
		{
			return Task.WhenAll(tasks);
		}

		public Task WaitAny(params Task[] tasks)
		{
			return Task.WhenAny(tasks);
		}

		public async Task Sequence(params Func<Task>[] actions)
		{
			await Track(async () =>
			{
				foreach (var action in actions)
				{
					// 依次执行，只要子任务是 Native 的，这里就是高效的
					await action();
				}
			});
		}

		public async Task Parallel(params Func<Task>[] actions)
		{
			await Track(async () =>
			{
				var tasks = new Task[actions.Length];
				for (int i = 0; i < actions.Length; i++)
				{
					tasks[i] = actions[i]();
				}
				await Task.WhenAll(tasks);
			});
		}

		public async Task Retry(Func<Task> action, int maxRetries = 3, float retryDelay = 1f, CancellationToken token = default(CancellationToken))
		{
			CancellationToken linkedToken = CreateLinkedToken(token);
			await Track(async () =>
			{
				for (int i = 0; i < maxRetries; i++)
				{
					try
					{
						await action();
						return; // 成功
					}
					catch (Exception)
					{
						if (i == maxRetries - 1) throw; // 最后一次失败
						if (linkedToken.IsCancellationRequested) return;

						// 失败等待 (Native 优化)
						await WaitSeconds(retryDelay, linkedToken);
					}
				}
			});
		}

		// =========================================================
		// 4. [重构重点] 自定义等待源 (Native Optimized)
		// =========================================================

		public Task WaitCustom(IAsakiWaitSource waitSource, CancellationToken token = default(CancellationToken))
		{
			CancellationToken linkedToken = CreateLinkedToken(token);

			#if ASAKI_USE_UNITASK
			// UniTask 路径：使用 ToUniTask 或手动轮询
			// 为了保持一致性，且 IAsakiWaitSource 是 C# 接口，我们依然需要轮询
			// 但 UniTask 的轮询开销极低
			return WaitCustomUniTask(waitSource, linkedToken).AsTask();
			#else
            // Native 路径：使用 RunRoutine 下沉到协程
            return RunRoutine(WaitCustomRoutine(waitSource), linkedToken);
			#endif
		}

		#if ASAKI_USE_UNITASK
		private async UniTask WaitCustomUniTask(IAsakiWaitSource source, CancellationToken token)
		{
			while (!source.IsCompleted)
			{
				source.Update();
				await UniTask.Yield(PlayerLoopTiming.Update, token);
			}
		}
		#endif

		/// <summary>
		/// [内部协程] 将 Update 循环下沉到 Unity 引擎层
		/// </summary>
		private IEnumerator WaitCustomRoutine(IAsakiWaitSource source)
		{
			while (!source.IsCompleted)
			{
				// 执行用户的自定义 Update 逻辑
				source.Update();

				// 等待一帧 (0 GC)
				yield return null;
			}
		}

		// =========================================================
		// 5. Builder 实现 (无需修改，逻辑正确)
		// =========================================================

		public IWaitBuilder CreateWaitBuilder()
		{
			return new AsakiWaitBuilder(this);
		}

		private class AsakiWaitBuilder : IWaitBuilder
		{
			private readonly IAsakiCoroutineService _service;
			private readonly List<Func<CancellationToken, Task>> _steps = new List<Func<CancellationToken, Task>>();

			public AsakiWaitBuilder(IAsakiCoroutineService service)
			{
				_service = service;
			}

			public IWaitBuilder Seconds(float seconds, bool unscaled = false)
			{
				_steps.Add(ct => unscaled
					? _service.WaitSecondsUnscaled(seconds, ct)
					: _service.WaitSeconds(seconds, ct));
				return this;
			}

			public IWaitBuilder Frames(int count)
			{
				_steps.Add(ct => _service.WaitFrames(count, ct));
				return this;
			}

			public IWaitBuilder FixedFrames(int count)
			{
				_steps.Add(ct => _service.WaitFixedFrames(count, ct));
				return this;
			}

			public IWaitBuilder Until(Func<bool> condition)
			{
				_steps.Add(ct => _service.WaitUntil(condition, ct));
				return this;
			}

			public IWaitBuilder While(Func<bool> condition)
			{
				_steps.Add(ct => _service.WaitWhile(condition, ct));
				return this;
			}

			public async Task Build(CancellationToken token = default(CancellationToken))
			{
				// 这里再次 Link 是为了安全，确保 Builder 执行过程中的取消能被响应
				CancellationToken linkedToken = _service.CreateLinkedToken(token);

				foreach (var step in _steps)
				{
					if (linkedToken.IsCancellationRequested) break;
					await step(linkedToken);
				}
			}
		}
	}
}
