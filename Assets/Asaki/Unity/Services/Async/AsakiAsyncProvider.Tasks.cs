using Asaki.Core.Async;
using Cysharp.Threading.Tasks;
using System;
using System.Collections; // 引入 IEnumerator
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Asaki.Unity.Services.Coroutines
{
	/// <summary>
	/// Asaki异步服务的任务管理和编排实现
	/// 提供任务执行、批量操作、流程控制和自定义等待源支持
	/// </summary>
	/// <remarks>
	/// 该部分实现了<see cref="IAsakiAsyncService"/>接口中的任务管理相关方法，
	/// 包括任务执行包装、快捷调用、批量与流程控制、自定义等待源和等待构建器。
	/// 所有方法都支持CancellationToken取消机制，并自动计入任务计数。
	/// </remarks>
	public partial class AsakiAsyncProvider
	{
		// =========================================================
		// 1. 任务执行包装
		// =========================================================

		/// <summary>
		/// 异步执行一个任务，自动处理取消和异常
		/// </summary>
		/// <param name="taskFunc">要执行的异步任务函数</param>
		/// <param name="token">可选的取消令牌，用于取消任务执行</param>
		/// <returns>表示异步任务执行的Task</returns>
		/// <exception cref="ArgumentNullException">当taskFunc为null时抛出</exception>
		/// <exception cref="OperationCanceledException">当任务被取消时抛出</exception>
		/// <remarks>
		/// 该方法会自动跟踪任务执行状态，计入RunningTaskCount，并处理异常。
		/// </remarks>
		public Task RunTask(Func<Task> taskFunc, CancellationToken token = default(CancellationToken))
		{
			// 使用Track方法处理任务计数和异常
			return Track(taskFunc);
		}

		/// <summary>
		/// 异步执行一个带返回值的任务，自动处理取消和异常
		/// </summary>
		/// <typeparam name="T">任务返回值的类型</typeparam>
		/// <param name="taskFunc">要执行的带返回值的异步任务函数</param>
		/// <param name="token">可选的取消令牌，用于取消任务执行</param>
		/// <returns>表示异步任务执行的Task，包含任务的返回值</returns>
		/// <exception cref="ArgumentNullException">当taskFunc为null时抛出</exception>
		/// <exception cref="OperationCanceledException">当任务被取消时抛出</exception>
		/// <remarks>
		/// 该方法会自动跟踪任务执行状态，计入RunningTaskCount，并处理异常。
		/// </remarks>
		public async Task<T> RunTask<T>(Func<Task<T>> taskFunc, CancellationToken token = default(CancellationToken))
		{
			// 检查任务是否已被取消
			if (token.IsCancellationRequested) throw new OperationCanceledException(token);

			// 增加任务计数
			Interlocked.Increment(ref _runningTaskCount);
			try
			{
				// 执行任务并返回结果
				return await taskFunc();
			}
			finally
			{
				// 减少任务计数
				Interlocked.Decrement(ref _runningTaskCount);
			}
		}

		// =========================================================
		// 2. 快捷调用 (自动享受Native优化)
		// =========================================================

		/// <summary>
		/// 延迟指定时间后执行一个动作
		/// </summary>
		/// <param name="delaySeconds">延迟的秒数，必须大于等于0</param>
		/// <param name="action">要执行的动作委托</param>
		/// <param name="token">可选的取消令牌，用于取消延迟执行</param>
		/// <param name="unscaled">是否使用不受TimeScale影响的真实时间，默认为false</param>
		/// <returns>表示异步延迟操作的Task</returns>
		/// <exception cref="ArgumentNullException">当action为null时抛出</exception>
		/// <exception cref="ArgumentOutOfRangeException">当delaySeconds小于0时抛出</exception>
		/// <exception cref="OperationCanceledException">当延迟操作被取消时抛出</exception>
		/// <remarks>
		/// 该方法使用Track确保计入RunningTaskCount，并自动处理取消和异常。
		/// </remarks>
		public async Task DelayedCall(float delaySeconds, Action action, CancellationToken token = default(CancellationToken), bool unscaled = false)
		{
			await Track(async () =>
			{
				// 创建链接的取消令牌
				CancellationToken linkedToken = CreateLinkedToken(token);

				// 等待指定时间
				if (unscaled) await WaitSecondsUnscaled(delaySeconds, linkedToken);
				else await WaitSeconds(delaySeconds, linkedToken);

				// 如果未取消，执行动作
				if (!linkedToken.IsCancellationRequested) action?.Invoke();
			});
		}

		/// <summary>
		/// 在下一帧Update执行时执行一个动作
		/// </summary>
		/// <param name="action">要执行的动作委托</param>
		/// <param name="token">可选的取消令牌，用于取消下一帧执行</param>
		/// <returns>表示异步操作的Task</returns>
		/// <exception cref="ArgumentNullException">当action为null时抛出</exception>
		/// <exception cref="OperationCanceledException">当操作被取消时抛出</exception>
		/// <remarks>
		/// 该方法使用Track确保计入RunningTaskCount，并自动处理取消和异常。
		/// </remarks>
		public async Task NextFrameCall(Action action, CancellationToken token = default(CancellationToken))
		{
			await Track(async () =>
			{
				// 创建链接的取消令牌
				CancellationToken linkedToken = CreateLinkedToken(token);
				// 等待下一帧
				await WaitFrame(linkedToken);
				// 如果未取消，执行动作
				if (!linkedToken.IsCancellationRequested) action?.Invoke();
			});
		}

		/// <summary>
		/// 当指定条件满足时执行一个动作
		/// </summary>
		/// <param name="condition">条件判断函数，返回true时执行动作</param>
		/// <param name="action">要执行的动作委托</param>
		/// <param name="token">可选的取消令牌，用于取消条件等待</param>
		/// <returns>表示异步操作的Task</returns>
		/// <exception cref="ArgumentNullException">当condition或action为null时抛出</exception>
		/// <exception cref="OperationCanceledException">当操作被取消时抛出</exception>
		/// <remarks>
		/// 该方法使用Track确保计入RunningTaskCount，并自动处理取消和异常。
		/// 条件判断在Update循环中执行。
		/// </remarks>
		public async Task When(Func<bool> condition, Action action, CancellationToken token = default(CancellationToken))
		{
			await Track(async () =>
			{
				// 创建链接的取消令牌
				CancellationToken linkedToken = CreateLinkedToken(token);
				// 等待条件满足
				await WaitUntil(condition, linkedToken);
				// 如果未取消，执行动作
				if (!linkedToken.IsCancellationRequested) action?.Invoke();
			});
		}

		// =========================================================
		// 3. 批量与流程控制
		// =========================================================

		/// <summary>
		/// 等待所有指定的任务完成
		/// </summary>
		/// <param name="tasks">要等待完成的任务数组</param>
		/// <returns>表示所有任务完成的Task</returns>
		/// <exception cref="ArgumentNullException">当tasks为null时抛出</exception>
		/// <exception cref="ArgumentException">当tasks数组包含null元素时抛出</exception>
		/// <remarks>
		/// 该方法是对Task.WhenAll的包装，确保与Asaki异步服务的一致性。
		/// </remarks>
		public Task WaitAll(params Task[] tasks)
		{
			return Task.WhenAll(tasks);
		}

		/// <summary>
		/// 等待任意一个指定的任务完成
		/// </summary>
		/// <param name="tasks">要等待的任务数组</param>
		/// <returns>表示任意任务完成的Task</returns>
		/// <exception cref="ArgumentNullException">当tasks为null时抛出</exception>
		/// <exception cref="ArgumentException">当tasks数组包含null元素或为空时抛出</exception>
		/// <remarks>
		/// 该方法是对Task.WhenAny的包装，确保与Asaki异步服务的一致性。
		/// </remarks>
		public Task WaitAny(params Task[] tasks)
		{
			return Task.WhenAny(tasks);
		}

		/// <summary>
		/// 顺序执行多个异步操作
		/// </summary>
		/// <param name="actions">要顺序执行的异步操作数组</param>
		/// <returns>表示所有操作顺序完成的Task</returns>
		/// <exception cref="ArgumentNullException">当actions为null时抛出</exception>
		/// <remarks>
		/// 操作按数组顺序依次执行，前一个操作完成后才开始下一个操作。
		/// 该方法使用Track确保计入RunningTaskCount，并自动处理取消和异常。
		/// </remarks>
		public async Task Sequence(params Func<Task>[] actions)
		{
			await Track(async () =>
			{
				// 依次执行每个操作
				foreach (var action in actions)
				{
					await action();
				}
			});
		}

		/// <summary>
		/// 并行执行多个异步操作
		/// </summary>
		/// <param name="actions">要并行执行的异步操作数组</param>
		/// <returns>表示所有操作并行完成的Task</returns>
		/// <exception cref="ArgumentNullException">当actions为null时抛出</exception>
		/// <remarks>
		/// 所有操作同时开始执行，等待所有操作完成后返回。
		/// 该方法使用Track确保计入RunningTaskCount，并自动处理取消和异常。
		/// </remarks>
		public async Task Parallel(params Func<Task>[] actions)
		{
			await Track(async () =>
			{
				// 创建任务数组
				var tasks = new Task[actions.Length];
				// 启动所有任务
				for (int i = 0; i < actions.Length; i++)
				{
					tasks[i] = actions[i]();
				}
				// 等待所有任务完成
				await Task.WhenAll(tasks);
			});
		}

		/// <summary>
		/// 重试执行异步操作，直到成功或达到最大重试次数
		/// </summary>
		/// <param name="action">要重试执行的异步操作</param>
		/// <param name="maxRetries">最大重试次数，默认为3次</param>
		/// <param name="retryDelay">重试间隔时间（秒），默认为1秒</param>
		/// <param name="token">可选的取消令牌，用于取消重试操作</param>
		/// <returns>表示重试操作的Task</returns>
		/// <exception cref="ArgumentNullException">当action为null时抛出</exception>
		/// <exception cref="ArgumentOutOfRangeException">当maxRetries小于0或retryDelay小于0时抛出</exception>
		/// <exception cref="OperationCanceledException">当重试操作被取消时抛出</exception>
		/// <remarks>
		/// 操作执行成功则不再重试；执行失败则等待retryDelay后重试，直到达到maxRetries次。
		/// 该方法使用Track确保计入RunningTaskCount，并自动处理取消和异常。
		/// </remarks>
		public async Task Retry(Func<Task> action, int maxRetries = 3, float retryDelay = 1f, CancellationToken token = default(CancellationToken))
		{
			// 创建链接的取消令牌
			CancellationToken linkedToken = CreateLinkedToken(token);
			await Track(async () =>
			{
				// 执行重试逻辑
				for (int i = 0; i < maxRetries; i++)
				{
					try
					{
						// 执行操作
						await action();
						return; // 成功，退出重试
					}
					catch (Exception)
					{
						// 如果是最后一次重试，抛出异常
						if (i == maxRetries - 1) throw;
						// 如果已取消，退出重试
						if (linkedToken.IsCancellationRequested) return;

						// 等待重试间隔
						await WaitSeconds(retryDelay, linkedToken);
					}
				}
			});
		}

		// =========================================================
		// 4. 自定义等待源 (Native Optimized)
		// =========================================================

		/// <summary>
		/// 等待一个自定义的等待源完成
		/// </summary>
		/// <param name="waitSource">自定义的等待源实例</param>
		/// <param name="token">可选的取消令牌，用于取消等待操作</param>
		/// <returns>表示异步等待操作的Task</returns>
		/// <exception cref="ArgumentNullException">当waitSource为null时抛出</exception>
		/// <exception cref="OperationCanceledException">当等待操作被取消时抛出</exception>
		/// <remarks>
		/// 该方法根据ASAKI_USE_UNITASK宏选择不同的实现方式：
		/// 1. 如果启用了UniTask，使用UniTask.Yield进行高效轮询
		/// 2. 否则，使用Unity协程进行轮询
		/// 两种方式都实现了零GC分配的高效等待。
		/// </remarks>
		/// <seealso cref="IAsakiWaitSource"/>
		public Task WaitCustom(IAsakiWaitSource waitSource, CancellationToken token = default(CancellationToken))
		{
			// 创建链接的取消令牌
			CancellationToken linkedToken = CreateLinkedToken(token);

			#if ASAKI_USE_UNITASK
			// UniTask路径：使用UniTask进行高效轮询
			return WaitCustomUniTask(waitSource, linkedToken).AsTask();
			#else
            // Native路径：使用RunRoutine下沉到协程
            return RunRoutine(WaitCustomRoutine(waitSource), linkedToken);
			#endif
		}

		#if ASAKI_USE_UNITASK
		/// <summary>
		/// 使用UniTask实现的自定义等待源轮询
		/// </summary>
		/// <param name="source">自定义等待源</param>
		/// <param name="token">取消令牌</param>
		/// <returns>表示异步等待操作的UniTask</returns>
		private async UniTask WaitCustomUniTask(IAsakiWaitSource source, CancellationToken token)
		{
			// 轮询等待源直到完成
			while (!source.IsCompleted)
			{
				// 更新等待源状态
				source.Update();
				// 在下一帧Update时继续轮询
				await UniTask.Yield(PlayerLoopTiming.Update, token);
			}
		}
		#endif

		/// <summary>
		/// 使用Unity协程实现的自定义等待源轮询
		/// </summary>
		/// <param name="source">自定义等待源</param>
		/// <returns>协程迭代器</returns>
		private IEnumerator WaitCustomRoutine(IAsakiWaitSource source)
		{
			// 轮询等待源直到完成
			while (!source.IsCompleted)
			{
				// 更新等待源状态
				source.Update();

				// 等待一帧（零GC分配）
				yield return null;
			}
		}

		// =========================================================
		// 5. 等待构建器实现
		// =========================================================

		/// <summary>
		/// 创建一个可配置的等待构建器，用于构建复杂的等待条件
		/// </summary>
		/// <returns>新创建的等待构建器实例</returns>
		/// <remarks>
		/// 等待构建器提供流畅API风格的等待条件构建，支持链式调用。
		/// </remarks>
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
		/// <seealso cref="IWaitBuilder"/>
		public IWaitBuilder CreateWaitBuilder()
		{
			return new AsakiWaitBuilder(this);
		}

		/// <summary>
		/// 等待构建器的实现类
		/// 提供流畅API风格的等待条件构建
		/// </summary>
		private class AsakiWaitBuilder : IWaitBuilder
		{
			private readonly IAsakiAsyncService _service;
			private readonly List<Func<CancellationToken, Task>> _steps = new List<Func<CancellationToken, Task>>();

			/// <summary>
			/// 初始化AsakiWaitBuilder实例
			/// </summary>
			/// <param name="service">异步服务实例</param>
			public AsakiWaitBuilder(IAsakiAsyncService service)
			{
				_service = service;
			}

			/// <summary>
			/// 添加等待指定秒数的条件
			/// </summary>
			/// <param name="seconds">等待的秒数，必须大于等于0</param>
			/// <param name="unscaled">是否使用不受TimeScale影响的真实时间，默认为false</param>
			/// <returns>当前等待构建器实例，用于链式调用</returns>
			public IWaitBuilder Seconds(float seconds, bool unscaled = false)
			{
				// 添加等待秒数的步骤
				_steps.Add(ct => unscaled
					? _service.WaitSecondsUnscaled(seconds, ct)
					: _service.WaitSeconds(seconds, ct));
				return this;
			}

			/// <summary>
			/// 添加等待指定数量Update帧的条件
			/// </summary>
			/// <param name="count">等待的帧数，必须大于等于0</param>
			/// <returns>当前等待构建器实例，用于链式调用</returns>
			public IWaitBuilder Frames(int count)
			{
				// 添加等待帧数的步骤
				_steps.Add(ct => _service.WaitFrames(count, ct));
				return this;
			}

			/// <summary>
			/// 添加等待指定数量FixedUpdate帧的条件
			/// </summary>
			/// <param name="count">等待的物理帧数，必须大于等于0</param>
			/// <returns>当前等待构建器实例，用于链式调用</returns>
			public IWaitBuilder FixedFrames(int count)
			{
				// 添加等待物理帧数的步骤
				_steps.Add(ct => _service.WaitFixedFrames(count, ct));
				return this;
			}

			/// <summary>
			/// 添加等待指定条件变为true的条件
			/// </summary>
			/// <param name="condition">条件判断函数，返回true时继续执行</param>
			/// <returns>当前等待构建器实例，用于链式调用</returns>
			public IWaitBuilder Until(Func<bool> condition)
			{
				// 添加条件等待步骤
				_steps.Add(ct => _service.WaitUntil(condition, ct));
				return this;
			}

			/// <summary>
			/// 添加等待指定条件变为false的条件
			/// </summary>
			/// <param name="condition">条件判断函数，返回false时继续执行</param>
			/// <returns>当前等待构建器实例，用于链式调用</returns>
			public IWaitBuilder While(Func<bool> condition)
			{
				// 添加条件等待步骤
				_steps.Add(ct => _service.WaitWhile(condition, ct));
				return this;
			}

			/// <summary>
			/// 构建并返回表示所有等待条件的Task
			/// </summary>
			/// <param name="token">可选的取消令牌，用于取消等待操作</param>
			/// <returns>表示所有等待条件的Task</returns>
			public async Task Build(CancellationToken token = default(CancellationToken))
			{
				// 创建链接的取消令牌
				CancellationToken linkedToken = _service.CreateLinkedToken(token);

				// 依次执行所有等待步骤
				foreach (var step in _steps)
				{
					// 如果已取消，退出执行
					if (linkedToken.IsCancellationRequested) break;
					// 执行当前步骤
					await step(linkedToken);
				}
			}
		}
	}
}
