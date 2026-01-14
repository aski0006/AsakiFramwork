using Asaki.Core.Context;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Asaki.Core.Async
{
	/// <summary>
	/// Asaki框架异步服务接口 (v3.0 Enhanced)
	/// 提供完整的时间控制、条件等待和任务管理能力，是Unity异步操作的统一抽象层。
	/// </summary>
	/// <remarks>
	/// 该接口提供了以下核心功能：
	/// - 时间控制：支持基于TimeScale和真实时间的等待
	/// - 帧控制：支持等待Update帧、FixedUpdate帧及其数量
	/// - 条件等待：支持基于谓词的条件等待，可选超时机制
	/// - 任务管理：支持异步任务的执行、延迟调用、条件调用
	/// - 批量任务：支持并行、顺序执行多个异步操作，以及任务重试
	/// - 高级功能：支持自定义等待源和流畅API式的等待构建器
	/// - 状态管理：支持任务取消和生命周期关联的取消令牌
	/// 
	/// 该服务设计为线程安全的，可在Unity主线程和其他线程中使用。
	/// </remarks>
	/// <seealso cref="Asaki.Core.Context.IAsakiService"/>
	/// <example>
	/// <code>
	/// // 获取异步服务实例
	/// var asyncService = serviceLocator.GetService&lt;IAsakiAsyncService&gt;();
	/// 
	/// // 使用示例：等待1秒后执行操作
	/// asyncService.DelayedCall(1.0f, () => Debug.Log("1秒后执行"));
	/// 
	/// // 使用示例：顺序执行多个异步操作
	/// await asyncService.Sequence(
	///     async () => await asyncService.WaitSeconds(1.0f),
	///     async () => await asyncService.WaitUntil(() => player.IsReady),
	///     () => player.StartGame()
	/// );
	/// </code>
	/// </example>
	public interface IAsakiAsyncService : IAsakiService
	{
		// === 基本等待方法 ===

		/// <summary>
		/// 等待指定秒数，受Unity TimeScale影响。
		/// </summary>
		/// <param name="seconds">等待的秒数，必须大于等于0。</param>
		/// <param name="token">可选的取消令牌，用于取消等待操作。</param>
		/// <returns>表示异步等待操作的Task。</returns>
		/// <exception cref="ArgumentOutOfRangeException">当seconds小于0时抛出。</exception>
		/// <exception cref="OperationCanceledException">当等待操作被取消时抛出。</exception>
		Task WaitSeconds(float seconds, CancellationToken token = default(CancellationToken));

		/// <summary>
		/// 等待指定秒数，使用真实时间，不受Unity TimeScale影响。
		/// </summary>
		/// <param name="seconds">等待的真实秒数，必须大于等于0。</param>
		/// <param name="token">可选的取消令牌，用于取消等待操作。</param>
		/// <returns>表示异步等待操作的Task。</returns>
		/// <exception cref="ArgumentOutOfRangeException">当seconds小于0时抛出。</exception>
		/// <exception cref="OperationCanceledException">当等待操作被取消时抛出。</exception>
		Task WaitSecondsUnscaled(float seconds, CancellationToken token = default(CancellationToken));

		// === 帧等待 ===

		/// <summary>
		/// 等待下一帧Update调用完成。
		/// </summary>
		/// <param name="token">可选的取消令牌，用于取消等待操作。</param>
		/// <returns>表示异步等待操作的Task。</returns>
		/// <exception cref="OperationCanceledException">当等待操作被取消时抛出。</exception>
		/// <remarks>该方法确保在Unity主线程的下一帧Update执行完成后继续执行。</remarks>
		Task WaitFrame(CancellationToken token = default(CancellationToken));

		/// <summary>
		/// 等待指定数量的Update帧完成。
		/// </summary>
		/// <param name="count">等待的帧数，必须大于等于0。</param>
		/// <param name="token">可选的取消令牌，用于取消等待操作。</param>
		/// <returns>表示异步等待操作的Task。</returns>
		/// <exception cref="ArgumentOutOfRangeException">当count小于0时抛出。</exception>
		/// <exception cref="OperationCanceledException">当等待操作被取消时抛出。</exception>
		Task WaitFrames(int count, CancellationToken token = default(CancellationToken));

		/// <summary>
		/// 等待下一帧FixedUpdate调用完成。
		/// </summary>
		/// <param name="token">可选的取消令牌，用于取消等待操作。</param>
		/// <returns>表示异步等待操作的Task。</returns>
		/// <exception cref="OperationCanceledException">当等待操作被取消时抛出。</exception>
		/// <remarks>该方法确保在Unity物理更新的下一帧FixedUpdate执行完成后继续执行。</remarks>
		Task WaitFixedFrame(CancellationToken token = default(CancellationToken));

		/// <summary>
		/// 等待指定数量的FixedUpdate帧完成。
		/// </summary>
		/// <param name="count">等待的物理帧数，必须大于等于0。</param>
		/// <param name="token">可选的取消令牌，用于取消等待操作。</param>
		/// <returns>表示异步等待操作的Task。</returns>
		/// <exception cref="ArgumentOutOfRangeException">当count小于0时抛出。</exception>
		/// <exception cref="OperationCanceledException">当等待操作被取消时抛出。</exception>
		Task WaitFixedFrames(int count, CancellationToken token = default(CancellationToken));

		// === 条件等待 ===

		/// <summary>
		/// 挂起执行，直到指定条件变为true。
		/// </summary>
		/// <param name="predicate">条件判断函数，返回true时继续执行。</param>
		/// <param name="token">可选的取消令牌，用于取消等待操作。</param>
		/// <returns>表示异步等待操作的Task。</returns>
		/// <exception cref="ArgumentNullException">当predicate为null时抛出。</exception>
		/// <exception cref="OperationCanceledException">当等待操作被取消时抛出。</exception>
		/// <remarks>条件判断在Update循环中执行。</remarks>
		Task WaitUntil(Func<bool> predicate, CancellationToken token = default(CancellationToken));

		/// <summary>
		/// 挂起执行，直到指定条件变为false。
		/// </summary>
		/// <param name="predicate">条件判断函数，返回false时继续执行。</param>
		/// <param name="token">可选的取消令牌，用于取消等待操作。</param>
		/// <returns>表示异步等待操作的Task。</returns>
		/// <exception cref="ArgumentNullException">当predicate为null时抛出。</exception>
		/// <exception cref="OperationCanceledException">当等待操作被取消时抛出。</exception>
		/// <remarks>条件判断在Update循环中执行。</remarks>
		Task WaitWhile(Func<bool> predicate, CancellationToken token = default(CancellationToken));

		/// <summary>
		/// 挂起执行，直到指定条件变为true或超时。
		/// </summary>
		/// <param name="predicate">条件判断函数，返回true时继续执行。</param>
		/// <param name="timeoutSeconds">超时时间（秒），必须大于0。</param>
		/// <param name="token">可选的取消令牌，用于取消等待操作。</param>
		/// <returns>如果条件满足返回true，超时返回false的Task。</returns>
		/// <exception cref="ArgumentNullException">当predicate为null时抛出。</exception>
		/// <exception cref="ArgumentOutOfRangeException">当timeoutSeconds小于等于0时抛出。</exception>
		/// <exception cref="OperationCanceledException">当等待操作被取消时抛出。</exception>
		/// <remarks>条件判断在Update循环中执行。</remarks>
		Task<bool> WaitUntil(Func<bool> predicate, float timeoutSeconds, CancellationToken token = default(CancellationToken));

		/// <summary>
		/// 挂起执行，直到指定条件变为false或超时。
		/// </summary>
		/// <param name="predicate">条件判断函数，返回false时继续执行。</param>
		/// <param name="timeoutSeconds">超时时间（秒），必须大于0。</param>
		/// <param name="token">可选的取消令牌，用于取消等待操作。</param>
		/// <returns>如果条件满足返回true，超时返回false的Task。</returns>
		/// <exception cref="ArgumentNullException">当predicate为null时抛出。</exception>
		/// <exception cref="ArgumentOutOfRangeException">当timeoutSeconds小于等于0时抛出。</exception>
		/// <exception cref="OperationCanceledException">当等待操作被取消时抛出。</exception>
		/// <remarks>条件判断在Update循环中执行。</remarks>
		Task<bool> WaitWhile(Func<bool> predicate, float timeoutSeconds, CancellationToken token = default(CancellationToken));

		// === 任务管理 ===

		/// <summary>
		/// 异步执行一个任务，自动处理取消和异常。
		/// </summary>
		/// <param name="taskFunc">要执行的异步任务函数。</param>
		/// <param name="token">可选的取消令牌，用于取消任务执行。</param>
		/// <returns>表示异步任务执行的Task。</returns>
		/// <exception cref="ArgumentNullException">当taskFunc为null时抛出。</exception>
		/// <exception cref="OperationCanceledException">当任务被取消时抛出。</exception>
		/// <remarks>任务执行过程中的异常会被捕获并向上传递。</remarks>
		Task RunTask(Func<Task> taskFunc, CancellationToken token = default(CancellationToken));

		/// <summary>
		/// 异步执行一个带返回值的任务，自动处理取消和异常。
		/// </summary>
		/// <typeparam name="T">任务返回值的类型。</typeparam>
		/// <param name="taskFunc">要执行的带返回值的异步任务函数。</param>
		/// <param name="token">可选的取消令牌，用于取消任务执行。</param>
		/// <returns>表示异步任务执行的Task，包含任务的返回值。</returns>
		/// <exception cref="ArgumentNullException">当taskFunc为null时抛出。</exception>
		/// <exception cref="OperationCanceledException">当任务被取消时抛出。</exception>
		/// <remarks>任务执行过程中的异常会被捕获并向上传递。</remarks>
		Task<T> RunTask<T>(Func<Task<T>> taskFunc, CancellationToken token = default(CancellationToken));

		/// <summary>
		/// 延迟指定时间后执行一个动作。
		/// </summary>
		/// <param name="delaySeconds">延迟的秒数，必须大于等于0。</param>
		/// <param name="action">要执行的动作委托。</param>
		/// <param name="token">可选的取消令牌，用于取消延迟执行。</param>
		/// <param name="unscaledTime">是否使用不受TimeScale影响的真实时间，默认为false。</param>
		/// <returns>表示异步延迟操作的Task。</returns>
		/// <exception cref="ArgumentNullException">当action为null时抛出。</exception>
		/// <exception cref="ArgumentOutOfRangeException">当delaySeconds小于0时抛出。</exception>
		/// <exception cref="OperationCanceledException">当延迟操作被取消时抛出。</exception>
		Task DelayedCall(float delaySeconds, Action action, CancellationToken token = default(CancellationToken), bool unscaledTime = false);

		/// <summary>
		/// 在下一帧Update执行时执行一个动作。
		/// </summary>
		/// <param name="action">要执行的动作委托。</param>
		/// <param name="token">可选的取消令牌，用于取消下一帧执行。</param>
		/// <returns>表示异步操作的Task。</returns>
		/// <exception cref="ArgumentNullException">当action为null时抛出。</exception>
		/// <exception cref="OperationCanceledException">当操作被取消时抛出。</exception>
		Task NextFrameCall(Action action, CancellationToken token = default(CancellationToken));

		/// <summary>
		/// 当指定条件满足时执行一个动作。
		/// </summary>
		/// <param name="condition">条件判断函数，返回true时执行动作。</param>
		/// <param name="action">要执行的动作委托。</param>
		/// <param name="token">可选的取消令牌，用于取消条件等待。</param>
		/// <returns>表示异步操作的Task。</returns>
		/// <exception cref="ArgumentNullException">当condition或action为null时抛出。</exception>
		/// <exception cref="OperationCanceledException">当操作被取消时抛出。</exception>
		/// <remarks>条件判断在Update循环中执行。</remarks>
		Task When(Func<bool> condition, Action action, CancellationToken token = default(CancellationToken));

		// === 批量任务管理 ===

		/// <summary>
		/// 等待所有指定的任务完成。
		/// </summary>
		/// <param name="tasks">要等待完成的任务数组。</param>
		/// <returns>表示所有任务完成的Task。</returns>
		/// <exception cref="ArgumentNullException">当tasks为null时抛出。</exception>
		/// <exception cref="ArgumentException">当tasks数组包含null元素时抛出。</exception>
		Task WaitAll(params Task[] tasks);

		/// <summary>
		/// 等待任意一个指定的任务完成。
		/// </summary>
		/// <param name="tasks">要等待的任务数组。</param>
		/// <returns>表示任意任务完成的Task。</returns>
		/// <exception cref="ArgumentNullException">当tasks为null时抛出。</exception>
		/// <exception cref="ArgumentException">当tasks数组包含null元素或为空时抛出。</exception>
		Task WaitAny(params Task[] tasks);

		/// <summary>
		/// 顺序执行多个异步操作。
		/// </summary>
		/// <param name="actions">要顺序执行的异步操作数组。</param>
		/// <returns>表示所有操作顺序完成的Task。</returns>
		/// <exception cref="ArgumentNullException">当actions为null时抛出。</exception>
		/// <remarks>操作按数组顺序依次执行，前一个操作完成后才开始下一个操作。</remarks>
		Task Sequence(params Func<Task>[] actions);

		/// <summary>
		/// 并行执行多个异步操作。
		/// </summary>
		/// <param name="actions">要并行执行的异步操作数组。</param>
		/// <returns>表示所有操作并行完成的Task。</returns>
		/// <exception cref="ArgumentNullException">当actions为null时抛出。</exception>
		/// <remarks>所有操作同时开始执行，等待所有操作完成后返回。</remarks>
		Task Parallel(params Func<Task>[] actions);

		/// <summary>
		/// 重试执行异步操作，直到成功或达到最大重试次数。
		/// </summary>
		/// <param name="action">要重试执行的异步操作。</param>
		/// <param name="maxRetries">最大重试次数，默认为3次。</param>
		/// <param name="retryDelay">重试间隔时间（秒），默认为1秒。</param>
		/// <param name="token">可选的取消令牌，用于取消重试操作。</param>
		/// <returns>表示重试操作的Task。</returns>
		/// <exception cref="ArgumentNullException">当action为null时抛出。</exception>
		/// <exception cref="ArgumentOutOfRangeException">当maxRetries小于0或retryDelay小于0时抛出。</exception>
		/// <exception cref="OperationCanceledException">当重试操作被取消时抛出。</exception>
		/// <remarks>操作执行成功则不再重试；执行失败则等待retryDelay后重试，直到达到maxRetries次。</remarks>
		Task Retry(Func<Task> action, int maxRetries = 3, float retryDelay = 1f, CancellationToken token = default(CancellationToken));

		// === 高级等待模式 ===

		/// <summary>
		/// 等待一个自定义的等待源完成。
		/// </summary>
		/// <param name="waitSource">自定义的等待源实例。</param>
		/// <param name="token">可选的取消令牌，用于取消等待操作。</param>
		/// <returns>表示异步等待操作的Task。</returns>
		/// <exception cref="ArgumentNullException">当waitSource为null时抛出。</exception>
		/// <exception cref="OperationCanceledException">当等待操作被取消时抛出。</exception>
		/// <seealso cref="IAsakiWaitSource"/>
		Task WaitCustom(IAsakiWaitSource waitSource, CancellationToken token = default(CancellationToken));

		/// <summary>
		/// 创建一个可配置的等待构建器，用于构建复杂的等待条件。
		/// </summary>
		/// <returns>新创建的等待构建器实例。</returns>
		/// <seealso cref="IWaitBuilder"/>
		/// <example>
		/// <code>
		/// // 使用等待构建器创建复杂等待条件
		/// await asyncService.CreateWaitBuilder()
		///     .Seconds(1.0f)
		///     .Until(() => player.IsReady)
		///     .Frames(5)
		///     .Build();
		/// </code>
		/// </example>
		IWaitBuilder CreateWaitBuilder();

		// === 状态和取消 ===

		/// <summary>
		/// 获取当前正在运行的任务数量。
		/// </summary>
		/// <value>当前正在运行的任务数量。</value>
		/// <remarks>该属性提供了服务当前负载的快照信息。</remarks>
		int RunningTaskCount { get; }

		/// <summary>
		/// 取消所有正在运行的任务。
		/// </summary>
		/// <remarks>所有使用该服务创建的任务都将收到取消信号。</remarks>
		void CancelAllTasks();

		/// <summary>
		/// 创建一个链接到服务生命周期的取消令牌。
		/// </summary>
		/// <param name="externalToken">可选的外部取消令牌，用于与服务生命周期令牌链接。</param>
		/// <returns>链接到服务生命周期的取消令牌。</returns>
		/// <remarks>当服务被销毁或调用CancelAllTasks时，该令牌将被取消。</remarks>
		CancellationToken CreateLinkedToken(CancellationToken externalToken = default(CancellationToken));
	}

	// === 扩展接口 ===

	/// <summary>
	/// 自定义等待源接口，用于创建复杂的等待条件。
		/// </summary>
	/// <remarks>
	/// 实现此接口可以创建自定义的等待逻辑，例如等待资源加载完成、网络请求返回等。
	/// Update方法会在每一帧被调用，用于检查等待条件是否完成。
	/// </remarks>
	public interface IAsakiWaitSource
	{
		/// <summary>
		/// 获取等待源是否已完成。
		/// </summary>
		/// <value>如果等待源已完成返回true，否则返回false。</value>
		bool IsCompleted { get; }

		/// <summary>
		/// 获取等待源的进度，范围为0到1。
		/// </summary>
		/// <value>等待源的进度值，0表示未开始，1表示已完成。</value>
		float Progress { get; }

		/// <summary>
		/// 更新等待源的状态。
		/// </summary>
		/// <remarks>该方法在每一帧被调用，用于检查等待条件是否完成并更新进度。</remarks>
		void Update();
	}

	/// <summary>
		/// 等待构建器接口，提供流畅API风格的等待条件构建。
		/// </summary>
	/// <remarks>
		/// 使用该接口可以通过链式调用构建复杂的等待条件，提高代码可读性。
		/// 所有条件将按添加顺序依次执行。
		/// </remarks>
	public interface IWaitBuilder
	{
		/// <summary>
		/// 添加等待指定秒数的条件。
		/// </summary>
		/// <param name="seconds">等待的秒数，必须大于等于0。</param>
		/// <param name="unscaled">是否使用不受TimeScale影响的真实时间，默认为false。</param>
		/// <returns>当前等待构建器实例，用于链式调用。</returns>
		IWaitBuilder Seconds(float seconds, bool unscaled = false);

		/// <summary>
		/// 添加等待指定数量Update帧的条件。
		/// </summary>
		/// <param name="count">等待的帧数，必须大于等于0。</param>
		/// <returns>当前等待构建器实例，用于链式调用。</returns>
		IWaitBuilder Frames(int count);

		/// <summary>
		/// 添加等待指定数量FixedUpdate帧的条件。
		/// </summary>
		/// <param name="count">等待的物理帧数，必须大于等于0。</param>
		/// <returns>当前等待构建器实例，用于链式调用。</returns>
		IWaitBuilder FixedFrames(int count);

		/// <summary>
		/// 添加等待指定条件变为true的条件。
		/// </summary>
		/// <param name="condition">条件判断函数，返回true时继续执行。</param>
		/// <returns>当前等待构建器实例，用于链式调用。</returns>
		IWaitBuilder Until(Func<bool> condition);

		/// <summary>
		/// 添加等待指定条件变为false的条件。
		/// </summary>
		/// <param name="condition">条件判断函数，返回false时继续执行。</param>
		/// <returns>当前等待构建器实例，用于链式调用。</returns>
		IWaitBuilder While(Func<bool> condition);

		/// <summary>
		/// 构建并返回表示所有等待条件的Task。
		/// </summary>
		/// <param name="token">可选的取消令牌，用于取消等待操作。</param>
		/// <returns>表示所有等待条件的Task。</returns>
		Task Build(CancellationToken token = default(CancellationToken));
	}
}
