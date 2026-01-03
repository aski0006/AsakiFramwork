using Cysharp.Threading.Tasks; // 如果没有定义宏，这行会被忽略或报错，但在 Asaki 中通常通过 ASMDEF 隔离
using System;
using System.Collections; // 必须引用
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Asaki.Unity.Services.Coroutines
{
	/// <summary>
	/// [异步服务实现] Part 2: Time & Wait (Native Refactored)
	/// <para>V5.0 重构版：抛弃 Task.Yield 轮询，全面拥抱 Unity 原生 Coroutine。</para>
	/// </summary>
	public partial class AsakiAsyncProvider
	{
		// =========================================================
		// 1. 辅助协程实现 (Impls)
		// 这里的很多指令是 YieldInstruction，需要包裹在 IEnumerator 中
		// =========================================================

		private IEnumerator WaitForSecondsImpl(float seconds)
		{
			yield return new WaitForSeconds(seconds);
		}

		private IEnumerator WaitForSecondsRealtimeImpl(float seconds)
		{
			yield return new WaitForSecondsRealtime(seconds);
		}

		private IEnumerator WaitFramesImpl(int count)
		{
			for (int i = 0; i < count; i++) yield return null;
		}

		private IEnumerator WaitFixedFramesImpl(int count)
		{
			WaitForFixedUpdate wait = new WaitForFixedUpdate();
			for (int i = 0; i < count; i++) yield return wait;
		}

		private IEnumerator WaitUntilImpl(Func<bool> predicate)
		{
			yield return new WaitUntil(predicate);
		}

		private IEnumerator WaitWhileImpl(Func<bool> predicate)
		{
			yield return new WaitWhile(predicate);
		}

		// =========================================================
		// 2. 时间等待 (Time)
		// =========================================================

		public Task WaitSeconds(float seconds, CancellationToken token = default(CancellationToken))
		{
			// 1. 链接 Token
			CancellationToken linkedToken = CreateLinkedToken(token);

			#if ASAKI_USE_UNITASK
			return UniTask.Delay(TimeSpan.FromSeconds(seconds), DelayType.DeltaTime, PlayerLoopTiming.Update, linkedToken).AsTask();
			#else
			// 2. 快速路径：时间为0或已取消
			if (seconds <= 0) return Task.CompletedTask;
			
			// 3. 桥接到原生协程
			return RunRoutine(WaitForSecondsImpl(seconds), linkedToken);
			#endif
		}

		public Task WaitSecondsUnscaled(float seconds, CancellationToken token = default(CancellationToken))
		{
			CancellationToken linkedToken = CreateLinkedToken(token);

			#if ASAKI_USE_UNITASK
			return UniTask.Delay(TimeSpan.FromSeconds(seconds), DelayType.UnscaledDeltaTime, PlayerLoopTiming.Update, linkedToken).AsTask();
			#else
			if (seconds <= 0) return Task.CompletedTask;
			return RunRoutine(WaitForSecondsRealtimeImpl(seconds), linkedToken);
			#endif
		}

		// =========================================================
		// 3. 帧等待 (Frames)
		// =========================================================

		public Task WaitFrame(CancellationToken token = default(CancellationToken))
		{
			CancellationToken linkedToken = CreateLinkedToken(token);
			#if ASAKI_USE_UNITASK
			return UniTask.Yield(PlayerLoopTiming.Update, linkedToken).AsTask();
			#else
			// 这里的 1 代表等待 1 帧 (yield return null)
			return RunRoutine(WaitFramesImpl(1), linkedToken);
			#endif
		}

		public Task WaitFrames(int count, CancellationToken token = default(CancellationToken))
		{
			CancellationToken linkedToken = CreateLinkedToken(token);
			#if ASAKI_USE_UNITASK
			return UniTask.DelayFrame(count, PlayerLoopTiming.Update, linkedToken).AsTask();
			#else
			if (count <= 0) return Task.CompletedTask;
			return RunRoutine(WaitFramesImpl(count), linkedToken);
			#endif
		}

		public Task WaitFixedFrame(CancellationToken token = default(CancellationToken))
		{
			CancellationToken linkedToken = CreateLinkedToken(token);
			#if ASAKI_USE_UNITASK
			return UniTask.Yield(PlayerLoopTiming.FixedUpdate, linkedToken).AsTask();
			#else
			return RunRoutine(WaitFixedFramesImpl(1), linkedToken);
			#endif
		}

		public Task WaitFixedFrames(int count, CancellationToken token = default(CancellationToken))
		{
			CancellationToken linkedToken = CreateLinkedToken(token);
			#if ASAKI_USE_UNITASK
			return UniTask.DelayFrame(count, PlayerLoopTiming.FixedUpdate, linkedToken).AsTask();
			#else
			if (count <= 0) return Task.CompletedTask;
			return RunRoutine(WaitFixedFramesImpl(count), linkedToken);
			#endif
		}

		// =========================================================
		// 4. 条件等待 (Conditions)
		// =========================================================

		public Task WaitUntil(Func<bool> predicate, CancellationToken token = default(CancellationToken))
		{
			CancellationToken linkedToken = CreateLinkedToken(token);
			#if ASAKI_USE_UNITASK
			return UniTask.WaitUntil(predicate, PlayerLoopTiming.Update, linkedToken).AsTask();
			#else
			return RunRoutine(WaitUntilImpl(predicate), linkedToken);
			#endif
		}

		public Task WaitWhile(Func<bool> predicate, CancellationToken token = default(CancellationToken))
		{
			CancellationToken linkedToken = CreateLinkedToken(token);
			#if ASAKI_USE_UNITASK
			return UniTask.WaitWhile(predicate, PlayerLoopTiming.Update, linkedToken).AsTask();
			#else
			return RunRoutine(WaitWhileImpl(predicate), linkedToken);
			#endif
		}

		// =========================================================
		// 5. 复杂逻辑：带超时的等待 (Complex Logic)
		// =========================================================

		public async Task<bool> WaitUntil(Func<bool> predicate, float timeoutSeconds, CancellationToken token = default(CancellationToken))
		{
			CancellationToken linkedToken = CreateLinkedToken(token);

			#if ASAKI_USE_UNITASK
			try
			{
				await UniTask.WaitUntil(predicate, PlayerLoopTiming.Update, linkedToken)
				             .Timeout(TimeSpan.FromSeconds(timeoutSeconds));
				return true;
			}
			catch (TimeoutException) { return false; }
			catch (OperationCanceledException) { return false; } // 根据需求，取消也可以视为"未成功"
			#else
			// === Native Implementation ===
			// 这是一个手动构建的"有返回值"的桥接器

			// 1. 快速检查
			if (linkedToken.IsCancellationRequested) return false;
			if (_runner == null) return false;

			// 2. 创建 TCS (返回 bool)
			var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

			// 3. 启动协程引用
			Coroutine activeCoroutine = null;

			// 4. 注册取消回调
			using (linkedToken.Register(() => 
			{
				if (activeCoroutine != null && _runner != null) _runner.StopCoroutine(activeCoroutine);
				tcs.TrySetCanceled(linkedToken);
			}))
			{
				// 5. 启动自定义的超时协程
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
		/// [内部协程] 执行带超时的条件等待
		/// </summary>
		private IEnumerator WaitUntilTimeoutRoutine(Func<bool> predicate, float timeout, TaskCompletionSource<bool> tcs)
		{
			float timer = 0f;

			// 核心循环：完全在 Unity 协程调度器中运行
			// 相比 C# 的 Task.Yield 循环，这里避免了大量的 Task 状态机上下文切换
			while (timer < timeout)
			{
				// 检查条件
				if (predicate())
				{
					tcs.TrySetResult(true); // 成功
					yield break;
				}

				// 如果外部 Task 已经被取消 (通过 Register 回调)，则退出协程
				if (tcs.Task.IsCanceled) yield break;

				// 等待下一帧 (原生 Unity 等待)
				yield return null;

				// 累加时间
				timer += UnityEngine.Time.deltaTime;
			}

			// 循环结束仍未满足 -> 超时
			tcs.TrySetResult(false);
		}

		public async Task<bool> WaitWhile(Func<bool> predicate, float timeoutSeconds, CancellationToken token = default(CancellationToken))
		{
			// 复用 WaitUntil 的逻辑，只是条件取反
			return await WaitUntil(() => !predicate(), timeoutSeconds, token);
		}
	}
}
