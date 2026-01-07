namespace Asaki.Core.Context.Resolvers
{
	public readonly struct AsakiTransientResolver : IAsakiResolver
	{
		private readonly IAsakiResolver _parent; // 通常是 SceneContext 或 Global
		private readonly object _param;          // 临时参数
		public AsakiTransientResolver(IAsakiResolver parent, object param)
		{
			_parent = parent;
			_param = param;
		}
		public T Get<T>() where T : class, IAsakiService
		{
			// 1. 优先匹配参数 (InitArgs)
			if (_param is T t) return t;
        
			// 2. 没匹配上，交给父级去查
			return _parent.Get<T>();
		}
		
		public bool TryGet<T>(out T service) where T : class, IAsakiService
		{
			// 1. 优先匹配参数 (InitArgs)
			if (_param is not T t) return _parent.TryGet(out service);
			service = t;
			return true;

			// 2. 没匹配上，交给父级去查
		}
	}
}
