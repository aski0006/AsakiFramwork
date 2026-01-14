using Cysharp.Threading.Tasks; // 如果没有定义宏，这行会被忽略或报错，但在 Asaki 中通常通过 ASMDEF 隔离
using System;
using System.Collections; // 必须引用
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Asaki.Unity.Services.Coroutines
{
	/// <summary>
	/// Asaki异步服务的时间和等待实现
	/// 提供基于时间、帧和条件的高效等待功能
	/// </summary>
	/// <remarks>
	/// 该部分实现了<see cref="IAsakiAsyncService"/>接口中的时间和等待相关方法，
	/// 采用Unity原生Coroutine而非Task.Yield轮询，实现了更高的性能和更低的GC开销。
	/// 支持两种实现路径：
	/// 1. UniTask路径：当定义了ASAKI_USE_UNITASK宏时，使用UniTask库提供的高效实现
	/// 2. Native路径：默认使用Unity原生Coroutine实现，零依赖，高性能
	/// </remarks>
	public partial class AsakiAsyncProvider
	{
		// =========================================================
		// 1. 辅助协程实现 (Impls)
		// 这里的很多指令是 YieldInstruction，需要包裹在 IEnumerator 中
		// =========================================================

		/// <summary>
		/// 基于TimeScale的时间等待协程实现
		/// </summary>
		/// <param name="seconds">等待的秒数</param>
		/// <returns>协程迭代器</returns>
		/// <remarks>使用Unity的<see cref="WaitForSeconds"/>实现，受TimeScale影响</remarks>
		private IEnumerator WaitForSecondsImpl(float seconds)
		{
			yield return new WaitForSeconds(seconds);
		}

		/// <summary>
		/// 真实时间等待协程实现
		/// </summary>
		/// <param name="seconds">等待的真实秒数</param>
		/// <returns>协程迭代器</returns>
		/// <remarks>使用Unity的<see cref="WaitForSecondsRealtime"/>实现，不受TimeScale影响</remarks>
		private IEnumerator WaitForSecondsRealtimeImpl(float seconds)
		{
			yield return new WaitForSecondsRealtime(seconds);
		}

		/// <summary>
		/// 帧等待协程实现
		/// </summary>
		/// <param name="count">等待的帧数</param>
		/// <returns>协程迭代器</returns>
		/// <remarks>通过yield return null实现，在Update循环中等待</remarks>
		private IEnumerator WaitFramesImpl(int count)
		{
			for (int i = 0; i < count; i++) yield return null;
		}

		/// <summary>
		/// 物理帧等待协程实现
		/// </summary>
		/// <param name="count">等待的物理帧数</param>
		/// <returns>协程迭代器</returns>
		/// <remarks>使用Unity的<see cref="WaitForFixedUpdate"/>实现，在FixedUpdate循环中等待</remarks>
		private IEnumerator WaitFixedFramesImpl(int count)
		{
			WaitForFixedUpdate wait = new WaitForFixedUpdate();
			for (int i = 0; i < count; i++) yield return wait;
		}

		/// <summary>
		/// 条件等待直到满足协程实现
		/// </summary>
		/// <param name="predicate">条件判断函数</param>
		/// <returns>协程迭代器</returns>
		/// <remarks>使用Unity的<see cref="WaitUntil"/>实现</remarks>
		private IEnumerator WaitUntilImpl(Func<bool> predicate)
		{
			yield return new WaitUntil(predicate);
		}

		/// <summary>
		/// 条件等待直到不满足协程实现
		/// </summary>
		/// <param name="predicate">条件判断函数</param>
		/// <returns>协程迭代器</returns>
		/// <remarks>使用Unity的<see cref="WaitWhile"/>实现</remarks>
		private IEnumerator WaitWhileImpl(Func<bool> predicate)
		{
			yield return new WaitWhile(predicate);
		}

		// =========================================================
		// 2. 时间等待 (Time)
		// =========================================================

		/// <summary>
		/// 等待指定秒数，受Unity TimeScale影响
		/// </summary>
		/// <param name="seconds">等待的秒数，必须大于等于0</param>
		/// <param name="token">可选的取消令牌，用于取消等待操作</param>
		/// <returns>表示异步等待操作的Task</returns>
		/// <exception cref="ArgumentOutOfRangeException">当seconds小于0时抛出</exception>
		/// <exception cref="OperationCanceledException">当等待操作被取消时抛出</exception>
		/// <remarks>
		/// 该方法使用Unity的<see cref="WaitForSeconds"/>实现，受TimeScale影响。
		/// 当seconds小于等于0时，直接返回已完成的Task，避免不必要的协程创建。
		/// </remarks>
		public Task WaitSeconds(float seconds, CancellationToken token = default(CancellationToken))
		{
			// 链接取消令牌
			CancellationToken linkedToken = CreateLinkedToken(token);

			#if ASAKI_USE_UNITASK
			// UniTask路径：使用UniTask.Delay实现
			return UniTask.Delay(TimeSpan.FromSeconds(seconds), DelayType.DeltaTime, PlayerLoopTiming.Update, linkedToken).AsTask();
			#else
			// Native路径：快速路径处理
			if (seconds <= 0) return Task.CompletedTask;
			
			// 桥接到原生协程
			return RunRoutine(WaitForSecondsImpl(seconds), linkedToken);
			#endif
		}

		/// <summary>
		/// 等待指定秒数，使用真实时间，不受Unity TimeScale影响
		/// </summary>
		/// <param name="seconds">等待的真实秒数，必须大于等于0</param>
		/// <param name="token">可选的取消令牌，用于取消等待操作</param>
		/// <returns>表示异步等待操作的Task</returns>
		/// <exception cref="ArgumentOutOfRangeException">当seconds小于0时抛出</exception>
		/// <exception cref="OperationCanceledException">当等待操作被取消时抛出</exception>
		/// <remarks>
		/// 该方法使用Unity的<see cref="WaitForSecondsRealtime"/>实现，不受TimeScale影响。
		/// 当seconds小于等于0时，直接返回已完成的Task，避免不必要的协程创建。
		/// </remarks>
		public Task WaitSecondsUnscaled(float seconds, CancellationToken token = default(CancellationToken))
		{
			// 链接取消令牌
			CancellationToken linkedToken = CreateLinkedToken(token);

			#if ASAKI_USE_UNITASK
			// UniTask路径：使用UniTask.Delay实现
			return UniTask.Delay(TimeSpan.FromSeconds(seconds), DelayType.UnscaledDeltaTime, PlayerLoopTiming.Update, linkedToken).AsTask();
			#else
			// Native路径：快速路径处理
			if (seconds <= 0) return Task.CompletedTask;
			// 桥接到原生协程
			return RunRoutine(WaitForSecondsRealtimeImpl(seconds), linkedToken);
			#endif
		}

		// =========================================================
		// 3. 帧等待 (Frames)
		// =========================================================

		/// <summary>
		/// 等待下一帧Update调用完成
		/// </summary>
		/// <param name="token">可选的取消令牌，用于取消等待操作</param>
		/// <returns>表示异步等待操作的Task</returns>
		/// <exception cref="OperationCanceledException">当等待操作被取消时抛出</exception>
		/// <remarks>
		/// 该方法确保在Unity主线程的下一帧Update执行完成后继续执行。
		/// </remarks>
		public Task WaitFrame(CancellationToken token = default(CancellationToken))
		{
			// 链接取消令牌
			CancellationToken linkedToken = CreateLinkedToken(token);
			#if ASAKI_USE_UNITASK
			// UniTask路径：使用UniTask.Yield实现
			return UniTask.Yield(PlayerLoopTiming.Update, linkedToken).AsTask();
			#else
			// Native路径：等待1帧
			return RunRoutine(WaitFramesImpl(1), linkedToken);
			#endif
		}

		/// <summary>
		/// 等待指定数量的Update帧完成
		/// </summary>
		/// <param name="count">等待的帧数，必须大于等于0</param>
		/// <param name="token">可选的取消令牌，用于取消等待操作</param>
		/// <returns>表示异步等待操作的Task</returns>
		/// <exception cref="ArgumentOutOfRangeException">当count小于0时抛出</exception>
		/// <exception cref="OperationCanceledException">当等待操作被取消时抛出</exception>
		/// <remarks>
		/// 当count小于等于0时，直接返回已完成的Task，避免不必要的协程创建。
		/// </remarks>
		public Task WaitFrames(int count, CancellationToken token = default(CancellationToken))
		{
			// 链接取消令牌
			CancellationToken linkedToken = CreateLinkedToken(token);
			#if ASAKI_USE_UNITASK
			// UniTask路径：使用UniTask.DelayFrame实现
			return UniTask.DelayFrame(count, PlayerLoopTiming.Update, linkedToken).AsTask();
			#else
			// Native路径：快速路径处理
			if (count <= 0) return Task.CompletedTask;
			// 桥接到原生协程
			return RunRoutine(WaitFramesImpl(count), linkedToken);
			#endif
		}

		/// <summary>
		/// 等待下一帧FixedUpdate调用完成
		/// </summary>
		/// <param name="token">可选的取消令牌，用于取消等待操作</param>
		/// <returns>表示异步等待操作的Task</returns>
		/// <exception cref="OperationCanceledException">当等待操作被取消时抛出</exception>
		/// <remarks>
		/// 该方法确保在Unity物理更新的下一帧FixedUpdate执行完成后继续执行。
		/// </remarks>
		public Task WaitFixedFrame(CancellationToken token = default(CancellationToken))
		{
			// 链接取消令牌
			CancellationToken linkedToken = CreateLinkedToken(token);
			#if ASAKI_USE_UNITASK
			// UniTask路径：使用UniTask.Yield实现
			return UniTask.Yield(PlayerLoopTiming.FixedUpdate, linkedToken).AsTask();
			#else
			// Native路径：等待1个物理帧
			return RunRoutine(WaitFixedFramesImpl(1), linkedToken);
			#endif
		}

		/// <summary>
		/// 等待指定数量的FixedUpdate帧完成
		/// </summary>
		/// <param name="count">等待的物理帧数，必须大于等于0</param>
		/// <param name="token">可选的取消令牌，用于取消等待操作</param>
		/// <returns>表示异步等待操作的Task</returns>
		/// <exception cref="ArgumentOutOfRangeException">当count小于0时抛出</exception>
		/// <exception cref="OperationCanceledException">当等待操作被取消时抛出</exception>
		/// <remarks>
		/// 当count小于等于0时，直接返回已完成的Task，避免不必要的协程创建。
		/// </remarks>
		public Task WaitFixedFrames(int count, CancellationToken token = default(CancellationToken))
		{
			// 链接取消令牌
			CancellationToken linkedToken = CreateLinkedToken(token);
			#if ASAKI_USE_UNITASK
			// UniTask路径：使用UniTask.DelayFrame实现
			return UniTask.DelayFrame(count, PlayerLoopTiming.FixedUpdate, linkedToken).AsTask();
			#else
			// Native路径：快速路径处理
			if (count <= 0) return Task.CompletedTask;
			// 桥接到原生协程
			return RunRoutine(WaitFixedFramesImpl(count), linkedToken);
			#endif
		}

		// =========================================================
		// 4. 条件等待 (Conditions)
		// =========================================================

		/// <summary>
		/// 挂起执行，直到指定条件变为true
		/// </summary>
		/// <param name="predicate">条件判断函数，返回true时继续执行</param>
		/// <param name="token">可选的取消令牌，用于取消等待操作</param>
		/// <returns>表示异步等待操作的Task</returns>
		/// <exception cref="ArgumentNullException">当predicate为null时抛出</exception>
		/// <exception cref="OperationCanceledException">当等待操作被取消时抛出</exception>
		/// <remarks>
		/// 条件判断在Update循环中执行。
		/// </remarks>
		public Task WaitUntil(Func<bool> predicate, CancellationToken token = default(CancellationToken))
		{
			// 链接取消令牌
			CancellationToken linkedToken = CreateLinkedToken(token);
			#if ASAKI_USE_UNITASK
			// UniTask路径：使用UniTask.WaitUntil实现
			return UniTask.WaitUntil(predicate, PlayerLoopTiming.Update, linkedToken).AsTask();
			#else
			// Native路径：桥接到原生协程
			return RunRoutine(WaitUntilImpl(predicate), linkedToken);
			#endif
		}

		/// <summary>
		/// 挂起执行，直到指定条件变为false
		/// </summary>
		/// <param name="predicate">条件判断函数，返回false时继续执行</param>
		/// <param name="token">可选的取消令牌，用于取消等待操作</param>
		/// <returns>表示异步等待操作的Task</returns>
		/// <exception cref="ArgumentNullException">当predicate为null时抛出</exception>
		/// <exception cref="OperationCanceledException">当等待操作被取消时抛出</exception>
		/// <remarks>
		/// 条件判断在Update循环中执行。
		/// </remarks>
		public Task WaitWhile(Func<bool> predicate, CancellationToken token = default(CancellationToken))
		{
			// 链接取消令牌
			CancellationToken linkedToken = CreateLinkedToken(token);
			#if ASAKI_USE_UNITASK
			// UniTask路径：使用UniTask.WaitWhile实现
			return UniTask.WaitWhile(predicate, PlayerLoopTiming.Update, linkedToken).AsTask();
			#else
			// Native路径：桥接到原生协程
			return RunRoutine(WaitWhileImpl(predicate), linkedToken);
			#endif
		}

		// =========================================================
		// 5. 复杂逻辑：带超时的等待 (Complex Logic)
		// =========================================================

		/// <summary>
		/// 挂起执行，直到指定条件变为true或超时
		/// </summary>
		/// <param name="predicate">条件判断函数，返回true时继续执行</param>
		/// <param name="timeoutSeconds">超时时间（秒），必须大于0</param>
		/// <param name="token">可选的取消令牌，用于取消等待操作</param>
		/// <returns>如果条件满足返回true，超时或取消返回false的Task</returns>
		/// <exception cref="ArgumentNullException">当predicate为null时抛出</exception>
		/// <exception cref="ArgumentOutOfRangeException">当timeoutSeconds小于等于0时抛出</exception>
		/// <remarks>
		/// 条件判断在Update循环中执行。
		/// </remarks>
		public async Task<bool> WaitUntil(Func<bool> predicate, float timeoutSeconds, CancellationToken token = default(CancellationToken))
		{
			// 链接取消令牌
			CancellationToken linkedToken = CreateLinkedToken(token);

			#if ASAKI_USE_UNITASK
			// UniTask路径：使用UniTask.WaitUntil和Timeout扩展
			try
			{
				await UniTask.WaitUntil(predicate, PlayerLoopTiming.Update, linkedToken)
				             .Timeout(TimeSpan.FromSeconds(timeoutSeconds));
				return true;
			}
			catch (TimeoutException) { return false; }
			catch (OperationCanceledException) { return false; } // 取消也视为"未成功"
			#else
			// Native路径：手动实现带超时的等待

			// 快速检查
			if (linkedToken.IsCancellationRequested) return false;
			if (_runner == null) return false;

			// 创建带返回值的TaskCompletionSource
			var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

			// 准备协程引用
			Coroutine activeCoroutine = null;

			// 注册取消回调
			using (linkedToken.Register(() => 
			{
				// 停止协程
				if (activeCoroutine != null && _runner != null) _runner.StopCoroutine(activeCoroutine);
				// 标记为取消
				tcs.TrySetCanceled(linkedToken);
			}))
			{
				// 启动带超时的条件等待协程
				activeCoroutine = _runner.StartCoroutine(WaitUntilTimeoutRoutine(predicate, timeoutSeconds, tcs));

				try 
				{
					return await tcs.Task;
				}
				catch (OperationCanceledException)
				{
					return false;
				}
			}
			#endif
		}

		/// <summary>
		/// 带超时的条件等待协程实现
		/// </summary>
		/// <param name="predicate">条件判断函数</param>
		/// <param name="timeout">超时时间（秒）</param>
		/// <param name="tcs">用于设置结果的TaskCompletionSource</param>
		/// <returns>协程迭代器</returns>
		/// <remarks>
		/// 该协程在Unity协程调度器中运行，避免了Task.Yield循环的上下文切换开销。
		/// 在每一帧检查条件，如果条件满足则设置结果为true，如果超时则设置结果为false。
		/// </remarks>
		private IEnumerator WaitUntilTimeoutRoutine(Func<bool> predicate, float timeout, TaskCompletionSource<bool> tcs)
		{
			float timer = 0f;

			// 核心循环：在Unity协程调度器中运行
			while (timer < timeout)
			{
				// 检查条件
				if (predicate())
				{
					tcs.TrySetResult(true); // 条件满足，设置结果为true
					yield break;
				}

				// 检查是否已取消
				if (tcs.Task.IsCanceled) yield break;

				// 等待下一帧（原生Unity等待）
				yield return null;

				// 累加时间
				timer += UnityEngine.Time.deltaTime;
			}

			// 超时：设置结果为false
			tcs.TrySetResult(false);
		}

		/// <summary>
		/// 挂起执行，直到指定条件变为false或超时
		/// </summary>
		/// <param name="predicate">条件判断函数，返回false时继续执行</param>
		/// <param name="timeoutSeconds">超时时间（秒），必须大于0</param>
		/// <param name="token">可选的取消令牌，用于取消等待操作</param>
		/// <returns>如果条件满足返回true，超时或取消返回false的Task</returns>
		/// <exception cref="ArgumentNullException">当predicate为null时抛出</exception>
		/// <exception cref="ArgumentOutOfRangeException">当timeoutSeconds小于等于0时抛出</exception>
		/// <remarks>
		/// 该方法复用了WaitUntil的逻辑，只是条件取反。
		/// 条件判断在Update循环中执行。
		/// </remarks>
		public async Task<bool> WaitWhile(Func<bool> predicate, float timeoutSeconds, CancellationToken token = default(CancellationToken))
		{
			// 复用WaitUntil的逻辑，只是条件取反
			return await WaitUntil(() => !predicate(), timeoutSeconds, token);
		}
	}
}
