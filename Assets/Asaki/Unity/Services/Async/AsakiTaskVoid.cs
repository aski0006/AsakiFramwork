using Asaki.Core.Logging;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Asaki.Unity.Services.Async
{
	/// <summary>
	/// [Asaki Native] 轻量级异步 void 替代方案，用于安全的 "Fire-and-Forget" 场景。
	/// </summary>
	/// <remarks>
	/// <para>相比传统的 async void 方法，AsakiTaskVoid 提供了以下优势：</para>
	/// <list type="bullet">
	/// <item>捕获并记录所有未处理的异常，防止程序崩溃</item>
	/// <item>避免异常静默丢失，提高调试效率</item>
	/// <item>提供编译时安全检查</item>
	/// <item>支持 Unity 编辑器和运行时环境</item>
	/// </list>
	/// <para>该类型通过 AsyncMethodBuilder 属性与 <see cref="AsakiTaskVoidMethodBuilder"/> 配合工作，
	/// 由编译器自动生成异步状态机代码。</para>
	/// </remarks>
	/// <example>
	/// <code>
	/// // 使用 AsakiTaskVoid 替代 async void
	/// public AsakiTaskVoid DoSomethingAsync()
	/// {
	///     await AsakiAsyncService.WaitForSeconds(1.0f);
	///     Debug.Log("任务完成");
	/// }
	/// 
	/// // 调用并遗忘（不会产生编译器警告）
	/// DoSomethingAsync().Forget();
	/// </code>
	/// </example>
	[AsyncMethodBuilder(typeof(AsakiTaskVoidMethodBuilder))]
	public readonly struct AsakiTaskVoid
	{
		/// <summary>
		/// 显式调用此方法以消除 "调用未等待" 的编译器警告。
		/// </summary>
		/// <remarks>
		/// 这是一个纯语义方法，运行时无实际操作。它仅用于向编译器表明
		/// 开发者有意忽略此异步操作的结果。
		/// </remarks>
		/// <example>
		/// <code>
		/// // 正确的调用方式
		/// DoSomethingAsync().Forget();
		/// </code>
		/// </example>
		public void Forget()
		{
			// 纯语义方法，运行时无操作
		}
	}

	/// <summary>
	/// [Internal] 负责构建 AsakiTaskVoid 异步状态机的方法构建器。
	/// </summary>
	/// <remarks>
	/// 该构建器实现了 <see cref="IAsyncMethodBuilder"/> 接口的核心功能，
	/// 为 AsakiTaskVoid 提供异步状态管理。其核心特性在于 <see cref="SetException(Exception)"/> 方法，
	/// 它会捕获所有未处理的异常并记录到 Unity 控制台，而不是导致程序崩溃。
	/// </remarks>
	public struct AsakiTaskVoidMethodBuilder
	{
		/// <summary>
		/// 创建一个新的 AsakiTaskVoidMethodBuilder 实例。
		/// </summary>
		/// <returns>新创建的构建器实例。</returns>
		/// <remarks>此方法由 C# 编译器自动调用。</remarks>
		public static AsakiTaskVoidMethodBuilder Create()
		{
			return default(AsakiTaskVoidMethodBuilder);
		}

		/// <summary>
		/// 获取构建的异步任务实例。
		/// </summary>
		/// <value>构建完成的 <see cref="AsakiTaskVoid"/> 实例。</value>
		/// <remarks>此属性由 C# 编译器自动调用。</remarks>
		public AsakiTaskVoid Task => default(AsakiTaskVoid);

		/// <summary>
		/// 启动异步状态机的执行。
		/// </summary>
		/// <typeparam name="TStateMachine">异步状态机的类型，必须实现 <see cref="IAsyncStateMachine"/> 接口。</typeparam>
		/// <param name="stateMachine">异步状态机实例的引用。</param>
		/// <remarks>此方法由 C# 编译器自动调用，启动状态机的 MoveNext() 方法。</remarks>
		[DebuggerHidden]
		public void Start<TStateMachine>(ref TStateMachine stateMachine)
			where TStateMachine : IAsyncStateMachine
		{
			stateMachine.MoveNext();
		}

		/// <summary>
		/// 关联异步状态机与构建器。
		/// </summary>
		/// <param name="stateMachine">要关联的异步状态机实例。</param>
		/// <remarks>
		/// 此方法由 C# 编译器自动调用。在结构体模式的状态机中，
		/// 此方法通常为空实现，因为状态机是通过值引用传递的。
		/// </remarks>
		public void SetStateMachine(IAsyncStateMachine stateMachine) { }

		/// <summary>
		/// 标记异步任务成功完成。
		/// </summary>
		/// <remarks>
		/// 此方法由 C# 编译器自动调用。对于 void 返回类型，无需设置具体结果。
		/// </remarks>
		public void SetResult()
		{
			// Do nothing - void 任务不需要结果
		}

		/// <summary>
		/// [核心功能] 处理异步任务中抛出的未处理异常。
		/// </summary>
		/// <param name="exception">异步任务中抛出的异常对象。</param>
		/// <remarks>
		/// <para>此方法是 AsakiTaskVoid 的核心安全特性。它会捕获所有原本会导致
		/// async void 方法崩溃或静默失败的异常，并将其记录到 Unity 控制台。</para>
		/// <para>异常信息通过 <see cref="AsakiTaskExceptionLogger"/> 输出，包含完整的堆栈跟踪。</para>
		/// </remarks>
		public void SetException(Exception exception)
		{
			AsakiTaskExceptionLogger.Log(exception);
		}

		/// <summary>
		/// 安排 await 操作完成后继续执行异步状态机。
		/// </summary>
		/// <typeparam name="TAwaiter">等待器的类型，必须实现 <see cref="INotifyCompletion"/> 接口。</typeparam>
		/// <typeparam name="TStateMachine">异步状态机的类型，必须实现 <see cref="IAsyncStateMachine"/> 接口。</typeparam>
		/// <param name="awaiter">等待器实例的引用。</param>
		/// <param name="stateMachine">异步状态机实例的引用。</param>
		/// <remarks>
		/// 此方法由 C# 编译器自动调用，用于处理常规的 await 操作完成后的回调。
		/// </remarks>
		public void AwaitOnCompleted<TAwaiter, TStateMachine>(
			ref TAwaiter awaiter, ref TStateMachine stateMachine)
			where TAwaiter : INotifyCompletion
			where TStateMachine : IAsyncStateMachine
		{
			awaiter.OnCompleted(stateMachine.MoveNext);
		}

		/// <summary>
		/// 以不安全方式安排 await 操作完成后继续执行异步状态机。
		/// </summary>
		/// <typeparam name="TAwaiter">等待器的类型，必须实现 <see cref="ICriticalNotifyCompletion"/> 接口。</typeparam>
		/// <typeparam name="TStateMachine">异步状态机的类型，必须实现 <see cref="IAsyncStateMachine"/> 接口。</typeparam>
		/// <param name="awaiter">等待器实例的引用。</param>
		/// <param name="stateMachine">异步状态机实例的引用。</param>
		/// <remarks>
		/// <para>此方法与 <see cref="AwaitOnCompleted{TAwaiter, TStateMachine}(ref TAwaiter, ref TStateMachine)"/> 类似，
		/// 但提供了更高的性能，同时放弃了一些安全检查。</para>
		/// <para>它通过 <see cref="ICriticalNotifyCompletion"/> 接口工作，避免了额外的安全上下文捕获，
		/// 适用于性能敏感的场景。</para>
		/// </remarks>
		public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(
			ref TAwaiter awaiter, ref TStateMachine stateMachine)
			where TAwaiter : ICriticalNotifyCompletion
			where TStateMachine : IAsyncStateMachine
		{
			awaiter.UnsafeOnCompleted(stateMachine.MoveNext);
		}
	}

	/// <summary>
	/// [Internal] 异步任务异常记录器，负责记录 AsakiTaskVoid 任务中未处理的异常。
	/// </summary>
	internal static class AsakiTaskExceptionLogger
	{
		/// <summary>
		/// 记录异步任务中抛出的异常信息。
		/// </summary>
		/// <param name="ex">要记录的异常对象。</param>
		/// <remarks>
		/// 该方法将异常信息输出到 Unity 控制台，并包含完整的堆栈跟踪，
		/// 便于开发者调试和定位问题。
		/// </remarks>
		public static void Log(Exception ex)
		{
			ALog.Error($"[AsakiAsync] Task error: {ex.Message}", ex);
		}
	}
}