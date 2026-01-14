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
		/// 将Unity协程封装为标准Task的核心桥接方法
		/// <para>1. 支持C# await语法糖，使协程可以像Task一样使用</para>
		/// <para>2. 支持CancellationToken取消机制，可以立即停止正在运行的协程</para>
		/// <para>3. 零轮询开销，直接利用Unity的协程系统进行调度</para>
		/// </summary>
		/// <param name="routine">要执行的Unity协程迭代器</param>
		/// <param name="token">用于取消操作的CancellationToken</param>
		/// <returns>表示协程执行的Task对象</returns>
		/// <exception cref="InvalidOperationException">当协程运行器(_runner)丢失或被销毁时抛出</exception>
		/// <exception cref="OperationCanceledException">当操作被取消时抛出</exception>
		/// <remarks>
		/// 该方法是Asaki异步服务的核心桥接组件，实现了Unity协程系统与.NET Task系统的无缝集成。
		/// 它通过TaskCompletionSource将Unity协程的完成信号转换为Task的完成信号，
		/// 同时确保了取消机制的双向传递和资源的正确释放。
		/// </remarks>
		private Task RunRoutine(System.Collections.IEnumerator routine, CancellationToken token)
		{
			// 1. 快速检查：如果已经取消，直接返回Canceled Task
			if (token.IsCancellationRequested) return Task.FromCanceled(token);

			// 2. 安全检查：如果Runner丢失(比如游戏退出时)，抛出异常
			if (_runner == null) return Task.FromException(new InvalidOperationException("Asaki Coroutine Runner is missing or destroyed."));

			// 3. 创建TCS (TaskCompletionSource)
			// 关键点：使用RunContinuationsAsynchronously选项
			// 这能防止在某些极端情况下(如协程同步完成)导致的死锁，
			// 并强制后续代码在异步上下文中执行，保证了异步操作的一致性
			var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

			// 4. 准备协程引用，用于后续取消操作
			Coroutine activeCoroutine = null;

			// 5. 注册取消回调 (安全阀门)
			// 当Token被Cancel时，这个回调会被立即执行
			CancellationTokenRegistration registration = token.Register(() =>
			{
				// A. 停止Unity侧的协程，防止后台空跑消耗资源
				if (activeCoroutine != null && _runner != null)
				{
					_runner.StopCoroutine(activeCoroutine);
				}

				// B. 将Task标记为取消状态
				tcs.TrySetCanceled(token);
			});

			// 6. 启动包装协程(Wrapper)
			// 不能直接运行原始routine，因为需要监控其完成状态
			activeCoroutine = _runner.StartCoroutine(ExecWrapper(routine, tcs, registration));

			return tcs.Task;
		}

		/// <summary>
		/// 协程执行包装器，负责等待原始协程完成并处理结果
		/// </summary>
		/// <param name="targetRoutine">要执行的目标协程迭代器</param>
		/// <param name="tcs">用于管理Task状态的TaskCompletionSource</param>
		/// <param name="registration">CancellationToken的注册对象，用于清理资源</param>
		/// <returns>协程迭代器</returns>
		/// <remarks>
		/// 这个包装协程负责：
		/// 1. 执行原始业务逻辑协程
		/// 2. 当原始协程完成后，清理取消令牌注册
		/// 3. 将Task标记为成功完成
		/// 它是连接Unity协程系统和.NET Task系统的关键组件
		/// </remarks>
		private System.Collections.IEnumerator ExecWrapper(
			System.Collections.IEnumerator targetRoutine,
			TaskCompletionSource<bool> tcs,
			CancellationTokenRegistration registration)
		{
			// 等待原始业务逻辑执行完毕 (全权交给Unity引擎调度)
			yield return targetRoutine;

			// 只有当原始协程执行完毕后，才会执行下面的代码

			// 清理取消注册，既然协程已经完成，就不需要再监听取消信号
			registration.Dispose();

			// 如果Task还没结束(没被取消)，则标记为成功完成
			if (!tcs.Task.IsCompleted)
			{
				tcs.TrySetResult(true);
			}
		}
	}
}
