using System;
using System.Collections.Generic;

namespace Asaki.Core.FSM
{
	/// <summary>
	/// 纯代码状态机管理器
	/// <para>特性：懒加载缓存、零运行时GC、泛型驱动</para>
	/// </summary>
	public class AsakiStateMachine<TContext>
	{
		// 状态持有者 (e.g. PlayerController)
		public TContext Context { get; }

		// 当前运行状态
		private AsakiState<TContext> _currentState;
		public AsakiState<TContext> CurrentState => _currentState;

		// 状态缓存池 (Key: 状态类型, Value: 状态实例)
		// 只有第一次切换到某状态时会 new，之后全部复用
		private readonly Dictionary<Type, AsakiState<TContext>> _stateCache;

		public AsakiStateMachine(TContext context)
		{
			Context = context;
			_stateCache = new Dictionary<Type, AsakiState<TContext>>();
		}

		/// <summary>
		/// 切换状态
		/// </summary>
		/// <typeparam name="TState">目标状态类型</typeparam>
		public void ChangeState<TState>() where TState : AsakiState<TContext>, new()
		{
			// 1. 退出当前状态
			_currentState?.OnExit();

			// 2. 获取目标状态 (查缓存 -> 懒加载)
			Type type = typeof(TState);
			if (!_stateCache.TryGetValue(type, out var newState))
			{
				newState = new TState();
				newState.Initialize(this, Context);
				_stateCache.Add(type, newState);
			}

			// 3. 进入新状态
			_currentState = newState;
			_currentState.OnEnter();
		}

		/// <summary>
		/// 轮询更新 (建议在 MonoBehaviour.Update 中调用)
		/// </summary>
		public void Update(float deltaTime)
		{
			_currentState?.OnUpdate(deltaTime);
		}

		/// <summary>
		/// 物理更新 (建议在 MonoBehaviour.FixedUpdate 中调用)
		/// </summary>
		public void FixedUpdate(float fixedDeltaTime)
		{
			_currentState?.OnFixedUpdate(fixedDeltaTime);
		}

		/// <summary>
		/// 停止并清理状态机
		/// </summary>
		public void Stop()
		{
			_currentState?.OnExit();
			_currentState = null;
			_stateCache.Clear();
		}
	}
}
