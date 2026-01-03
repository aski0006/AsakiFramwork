namespace Asaki.Core.FSM
{
	/// <summary>
	/// Asaki 标准状态基类
	/// <para>TContext: 状态持有者类型 (e.g., PlayerController, GameManager)</para>
	/// </summary>
	public abstract class AsakiState<TContext> : IAsakiState
	{
		/// <summary>
		/// 状态持有者上下文
		/// </summary>
		protected TContext Context { get; private set; }
        
		/// <summary>
		/// 所属状态机引用 (用于内部切换状态)
		/// </summary>
		protected AsakiStateMachine<TContext> Machine { get; private set; }

		/// <summary>
		/// 初始化方法 (仅在状态首次创建时调用一次)
		/// </summary>
		public virtual void Initialize(AsakiStateMachine<TContext> machine, TContext context)
		{
			Machine = machine;
			Context = context;
		}

		public virtual void OnEnter() { }
		public virtual void OnUpdate(float deltaTime) { }
		public virtual void OnFixedUpdate(float fixedDeltaTime) { }
		public virtual void OnExit() { }
	}
}
