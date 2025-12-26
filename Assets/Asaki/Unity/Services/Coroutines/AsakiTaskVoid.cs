using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Asaki.Core.Tasks
{
	/// <summary>
	/// [Asaki Native] 轻量级异步 void 替代方案。
	/// <para>用于 "Fire-and-Forget" 场景。</para>
	/// <para>相比 async void，它能捕获并记录未处理的异常，防止程序崩溃或异常静默丢失。</para>
	/// </summary>
	[AsyncMethodBuilder(typeof(AsakiTaskVoidMethodBuilder))]
	public readonly struct AsakiTaskVoid
	{
		/// <summary>
		/// 显式调用此方法以消除 "调用未等待" 的编译器警告。
		/// </summary>
		public void Forget()
		{
			// 纯语义方法，运行时无操作
		}
	}

	/// <summary>
	/// [Internal] 负责构建 AsakiTaskVoid 状态机的构建器。
	/// 核心逻辑在于 SetException 时将异常输出到 Unity 控制台。
	/// </summary>
	public struct AsakiTaskVoidMethodBuilder
	{
		// 1. 创建构建器实例 (编译器调用)
		public static AsakiTaskVoidMethodBuilder Create()
		{
			return default(AsakiTaskVoidMethodBuilder);
		}

		// 2. 返回给调用者的对象 (编译器调用)
		public AsakiTaskVoid Task => default(AsakiTaskVoid);

		// 3. 状态机启动 (编译器调用)
		[DebuggerHidden]
		public void Start<TStateMachine>(ref TStateMachine stateMachine)
			where TStateMachine : IAsyncStateMachine
		{
			stateMachine.MoveNext();
		}

		// 4. 关联状态机 (编译器调用 - 仅在类模式下使用，Struct模式下通常为空)
		public void SetStateMachine(IAsyncStateMachine stateMachine) { }

		// 5. 任务成功完成时调用
		public void SetResult()
		{
			// Do nothing - void 任务不需要结果
		}

		// 6. [核心] 任务抛出未处理异常时调用
		public void SetException(Exception exception)
		{
			// 在这里拦截了原本会导致 async void 崩溃或静默失败的异常
			// 强制输出到 Unity 控制台
			AsakiTaskExceptionLogger.Log(exception);
		}

		// 7. 处理 await (常规)
		public void AwaitOnCompleted<TAwaiter, TStateMachine>(
			ref TAwaiter awaiter, ref TStateMachine stateMachine)
			where TAwaiter : INotifyCompletion
			where TStateMachine : IAsyncStateMachine
		{
			awaiter.OnCompleted(stateMachine.MoveNext);
		}

		// 8. 处理 await (不安全/高性能)
		public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(
			ref TAwaiter awaiter, ref TStateMachine stateMachine)
			where TAwaiter : ICriticalNotifyCompletion
			where TStateMachine : IAsyncStateMachine
		{
			awaiter.UnsafeOnCompleted(stateMachine.MoveNext);
		}
	}

	/// <summary>
	/// [Internal] 简单的异常日志记录器
	/// </summary>
	internal static class AsakiTaskExceptionLogger
	{
		public static void Log(Exception ex)
		{
			// 你可以在这里扩展，比如上报到服务器或弹出错误窗口
			UnityEngine.Debug.LogException(ex);
		}
	}
}
