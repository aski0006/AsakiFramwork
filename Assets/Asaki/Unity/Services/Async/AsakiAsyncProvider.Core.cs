using Asaki.Core.Async;
using Asaki.Core.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Asaki.Unity.Services.Coroutines
{
	/// <summary>
	/// Asaki异步服务的核心实现类
	/// 提供了异步操作的统一管理、任务跟踪和生命周期控制
	/// </summary>
	/// <remarks>
	/// 该类是<see cref="IAsakiAsyncService"/>接口的Unity平台实现，
	/// 它集成了Unity协程系统和.NET Task系统，提供了高效的异步操作支持。
	/// 主要功能包括：
	/// 1. 管理协程运行器的生命周期
	/// 2. 提供任务跟踪和计数功能
	/// 3. 实现任务取消机制
	/// 4. 支持服务生命周期管理
	/// </remarks>
	/// <seealso cref="IAsakiAsyncService"/>
	/// <seealso cref="IDisposable"/>
	public partial class AsakiAsyncProvider : IAsakiAsyncService, IDisposable
	{
		private CancellationTokenSource _serviceCts = new CancellationTokenSource();
		private int _runningTaskCount = 0;

		private AsakiAsyncCoroutineRunner _runner;

		/// <summary>
		/// 初始化AsakiAsyncProvider实例
		/// </summary>
		/// <remarks>
		/// 在构造函数中会自动初始化协程运行器，创建持久化的GameObject用于运行协程。
		/// 注意：该构造函数必须在Unity主线程中调用，否则会抛出异常。
		/// </remarks>
		public AsakiAsyncProvider()
		{
			InitializeRunner();
		}

		/// <summary>
		/// 初始化协程运行器
		/// </summary>
		/// <remarks>
		/// 创建一个持久化的GameObject并挂载AsakiAsyncCoroutineRunner组件，
		/// 用于运行所有通过该服务创建的协程。
		/// </remarks>
		private void InitializeRunner()
		{
			// 防止重复初始化
			if (_runner != null) return;

			// 创建协程运行器的宿主GameObject
			GameObject go = new GameObject("[Asaki.Routine.Kernel]");
			// 设置为场景切换时不销毁
			Object.DontDestroyOnLoad(go);

			// 挂载协程运行器组件
			_runner = go.AddComponent<AsakiAsyncCoroutineRunner>();
		}

		/// <summary>
		/// 获取当前正在运行的任务数量
		/// </summary>
		/// <value>当前正在运行的任务数量</value>
		/// <remarks>
		/// 该属性提供了服务当前负载的实时信息，可用于监控系统性能。
		/// </remarks>
		public int RunningTaskCount => _runningTaskCount;

		/// <summary>
		/// 取消所有正在运行的任务
		/// </summary>
		/// <remarks>
		/// 该方法会取消所有通过该服务创建的任务，并重置内部的取消令牌源。
		/// 已取消的任务会收到OperationCanceledException异常。
		/// </remarks>
		public void CancelAllTasks()
		{
			if (_serviceCts != null)
			{
				// 取消所有任务
				_serviceCts.Cancel();
				// 释放资源
				_serviceCts.Dispose();
			}
			// 创建新的取消令牌源，允许后续任务继续执行
			_serviceCts = new CancellationTokenSource();
		}

		/// <summary>
		/// 创建一个链接到服务生命周期的取消令牌
		/// </summary>
		/// <param name="externalToken">可选的外部取消令牌，用于与服务生命周期令牌链接</param>
		/// <returns>链接后的取消令牌</returns>
		/// <remarks>
		/// 当调用CancelAllTasks或服务被销毁时，该令牌会被取消。
		/// 如果提供了外部令牌，当外部令牌或服务令牌中的任何一个被取消时，结果令牌都会被取消。
		/// </remarks>
		public CancellationToken CreateLinkedToken(CancellationToken externalToken = default(CancellationToken))
		{
			// 如果服务令牌已被取消，返回空令牌
			if (_serviceCts.IsCancellationRequested) return CancellationToken.None;
			// 如果没有外部令牌，直接返回服务令牌
			if (externalToken == CancellationToken.None) return _serviceCts.Token;
			// 链接服务令牌和外部令牌
			return CancellationTokenSource.CreateLinkedTokenSource(_serviceCts.Token, externalToken).Token;
		}

		/// <summary>
		/// 跟踪任务执行并管理任务计数
		/// </summary>
		/// <param name="taskFunc">要执行的任务函数</param>
		/// <returns>表示异步操作的Task</returns>
		/// <remarks>
		/// 该方法负责：
		/// 1. 增加正在运行的任务计数
		/// 2. 执行任务函数
		/// 3. 处理任务执行过程中的异常
		/// 4. 减少正在运行的任务计数
		/// 操作取消异常会被捕获并忽略，其他异常会被记录并重新抛出。
		/// </remarks>
		private async Task Track(Func<Task> taskFunc)
		{
			// 原子增加任务计数，确保线程安全
			Interlocked.Increment(ref _runningTaskCount);
			try
			{
				// 执行实际任务
				await taskFunc();
			}
			catch (OperationCanceledException)
			{
				// 任务被取消是正常操作，不需要记录日志
			}
			catch (Exception e)
			{
				// 记录其他异常
				ALog.Error($"[AsakiAsync] Task error: {e.Message}", e);
				// 重新抛出异常，让调用者知道任务执行失败
				throw;
			}
			finally
			{
				// 原子减少任务计数，确保线程安全
				Interlocked.Decrement(ref _runningTaskCount);
			}
		}

		/// <summary>
		/// 释放AsakiAsyncProvider占用的资源
		/// </summary>
		/// <remarks>
		/// 该方法会：
		/// 1. 取消所有正在运行的任务
		/// 2. 销毁协程运行器的宿主GameObject
		/// 3. 清理所有资源
		/// 调用此方法后，服务将无法再使用。
		/// </remarks>
		public void Dispose()
		{
			// 取消所有任务
			CancelAllTasks();

			// 销毁宿主GameObject
			if (_runner != null)
			{
				// 根据当前模式选择合适的销毁方法
				if (Application.isPlaying)
					Object.Destroy(_runner.gameObject);
				else
					Object.DestroyImmediate(_runner.gameObject);

				_runner = null;
			}
		}
	}
}
