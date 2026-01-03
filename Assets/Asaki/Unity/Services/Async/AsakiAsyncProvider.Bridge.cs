using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Asaki.Unity.Services.Coroutines
{
	public partial class AsakiAsyncProvider
	{
		// =========================================================
		// Core Bridge: IEnumerator -> Task
		// =========================================================

		/// <summary>
		/// [核心桥接] 将 Unity 协程封装为标准 Task。
		/// <para>1. 支持 await 语法糖。</para>
		/// <para>2. 支持 CancellationToken 取消 (立即停止协程)。</para>
		/// <para>3. 0 轮询开销 (Zero Polling Overhead)。</para>
		/// </summary>
		/// <param name="routine">要执行的 Unity 协程迭代器</param>
		/// <param name="token">取消令牌</param>
		private Task RunRoutine(System.Collections.IEnumerator routine, CancellationToken token)
		{
			// 1. 快速检查：如果已经取消，直接返回 Canceled Task
			if (token.IsCancellationRequested) return Task.FromCanceled(token);

			// 2. 安全检查：如果 Runner 丢失 (比如游戏退出时)，抛出异常或快速返回
			if (_runner == null) return Task.FromException(new InvalidOperationException("Asaki Coroutine Runner is missing or destroyed."));

			// 3. 创建 TCS (TaskCompletionSource)
			// 关键点：RunContinuationsAsynchronously
			// 这能防止在某些极端情况下 (如协程同步完成) 导致的死锁，并强制后续代码在异步上下文中执行。
			var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

			// 4. 准备协程引用，用于后续取消
			Coroutine activeCoroutine = null;

			// 5. 注册取消回调 (Safety Valve)
			// 当 Token 被 Cancel 时，这个回调会被立即执行
			CancellationTokenRegistration registration = token.Register(() =>
			{
				// A. 停止 Unity 侧的协程 (防止后台空跑)
				if (activeCoroutine != null && _runner != null)
				{
					_runner.StopCoroutine(activeCoroutine);
				}

				// B. 将 Task 标记为取消
				tcs.TrySetCanceled(token);
			});

			// 6. 启动包装协程 (Wrapper)
			// 我们不能直接跑 routine，因为我们需要知道它什么时候结束
			activeCoroutine = _runner.StartCoroutine(ExecWrapper(routine, tcs, registration));

			return tcs.Task;
		}

		/// <summary>
		/// 内部包装器：负责等待原始协程完成，并处理结果
		/// </summary>
		private System.Collections.IEnumerator ExecWrapper(
			System.Collections.IEnumerator targetRoutine,
			TaskCompletionSource<bool> tcs,
			CancellationTokenRegistration registration)
		{
			// 等待原始业务逻辑执行完毕 (全权交给 Unity 引擎)
			yield return targetRoutine;

			// === 只有当上面这一行跑完，才会执行下面 ===

			// 清理取消注册 (既然跑完了，就不需要监听取消了)
			registration.Dispose();

			// 如果 Task 还没结束 (没被取消)，则标记为成功
			if (!tcs.Task.IsCompleted)
			{
				tcs.TrySetResult(true);
			}
		}
	}
}
