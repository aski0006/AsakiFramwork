using Asaki.Core.Context;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Asaki.Core.Async
{
	/// <summary>
	/// [异步服务接口] (v3.0 Enhanced)
	/// 提供完整的时间控制、条件等待和任务管理能力。
	/// </summary>
	public interface IAsakiAsyncService : IAsakiService
	{
		// === 基本等待方法 ===

		/// <summary>
		/// 等待指定秒数 (受 TimeScale 影响)
		/// </summary>
		Task WaitSeconds(float seconds, CancellationToken token = default(CancellationToken));

		/// <summary>
		/// 等待指定秒数 (真实时间，不受 TimeScale 影响)
		/// </summary>
		Task WaitSecondsUnscaled(float seconds, CancellationToken token = default(CancellationToken));

		// === 帧等待 ===

		/// <summary>
		/// 等待下一帧 (Update)
		/// </summary>
		Task WaitFrame(CancellationToken token = default(CancellationToken));

		/// <summary>
		/// 等待指定数量的帧
		/// </summary>
		Task WaitFrames(int count, CancellationToken token = default(CancellationToken));

		/// <summary>
		/// 等待物理/固定帧 (FixedUpdate)
		/// </summary>
		Task WaitFixedFrame(CancellationToken token = default(CancellationToken));

		/// <summary>
		/// 等待指定数量的物理帧
		/// </summary>
		Task WaitFixedFrames(int count, CancellationToken token = default(CancellationToken));

		// === 条件等待 ===

		/// <summary>
		/// 挂起直到条件为 true
		/// </summary>
		Task WaitUntil(Func<bool> predicate, CancellationToken token = default(CancellationToken));

		/// <summary>
		/// 挂起直到条件为 false
		/// </summary>
		Task WaitWhile(Func<bool> predicate, CancellationToken token = default(CancellationToken));

		/// <summary>
		/// 等待直到条件为 true，带超时时间
		/// </summary>
		Task<bool> WaitUntil(Func<bool> predicate, float timeoutSeconds, CancellationToken token = default(CancellationToken));

		/// <summary>
		/// 等待直到条件为 false，带超时时间
		/// </summary>
		Task<bool> WaitWhile(Func<bool> predicate, float timeoutSeconds, CancellationToken token = default(CancellationToken));

		// === 任务管理 ===

		/// <summary>
		/// 异步执行一个任务，自动处理取消和异常
		/// </summary>
		Task RunTask(Func<Task> taskFunc, CancellationToken token = default(CancellationToken));

		/// <summary>
		/// 异步执行一个带返回值的任务
		/// </summary>
		Task<T> RunTask<T>(Func<Task<T>> taskFunc, CancellationToken token = default(CancellationToken));

		/// <summary>
		/// 延迟执行一个动作
		/// </summary>
		Task DelayedCall(float delaySeconds, Action action, CancellationToken token = default(CancellationToken), bool unscaledTime = false);

		/// <summary>
		/// 在下一帧执行一个动作
		/// </summary>
		Task NextFrameCall(Action action, CancellationToken token = default(CancellationToken));

		/// <summary>
		/// 当条件满足时执行一个动作
		/// </summary>
		Task When(Func<bool> condition, Action action, CancellationToken token = default(CancellationToken));

		// === 批量任务管理 ===

		/// <summary>
		/// 等待所有任务完成
		/// </summary>
		Task WaitAll(params Task[] tasks);

		/// <summary>
		/// 等待任意一个任务完成
		/// </summary>
		Task WaitAny(params Task[] tasks);

		/// <summary>
		/// 顺序执行多个异步操作
		/// </summary>
		Task Sequence(params Func<Task>[] actions);

		/// <summary>
		/// 并行执行多个异步操作
		/// </summary>
		Task Parallel(params Func<Task>[] actions);

		/// <summary>
		/// 重试执行异步操作
		/// </summary>
		Task Retry(Func<Task> action, int maxRetries = 3, float retryDelay = 1f, CancellationToken token = default(CancellationToken));

		// === 高级等待模式 ===

		/// <summary>
		/// 等待一个自定义的等待源
		/// </summary>
		Task WaitCustom(IAsakiWaitSource waitSource, CancellationToken token = default(CancellationToken));

		/// <summary>
		/// 创建可配置的等待构建器
		/// </summary>
		IWaitBuilder CreateWaitBuilder();

		// === 状态和取消 ===

		/// <summary>
		/// 当前运行的任务数量
		/// </summary>
		int RunningTaskCount { get; }

		/// <summary>
		/// 取消所有正在运行的任务
		/// </summary>
		void CancelAllTasks();

		/// <summary>
		/// 创建一个链接到服务生命周期的取消令牌
		/// </summary>
		CancellationToken CreateLinkedToken(CancellationToken externalToken = default(CancellationToken));
	}

	// === 扩展接口 ===

	/// <summary>
	/// 自定义等待源接口
	/// </summary>
	public interface IAsakiWaitSource
	{
		bool IsCompleted { get; }
		float Progress { get; }
		void Update();
	}

	/// <summary>
	/// 等待构建器接口（流畅API）
	/// </summary>
	public interface IWaitBuilder
	{
		IWaitBuilder Seconds(float seconds, bool unscaled = false);
		IWaitBuilder Frames(int count);
		IWaitBuilder FixedFrames(int count);
		IWaitBuilder Until(Func<bool> condition);
		IWaitBuilder While(Func<bool> condition);
		Task Build(CancellationToken token = default(CancellationToken));
	}
}
