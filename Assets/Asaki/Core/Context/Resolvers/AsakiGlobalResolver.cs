namespace Asaki.Core.Context.Resolvers
{
	public readonly struct AsakiGlobalResolver : IAsakiResolver
	{
		public static readonly AsakiGlobalResolver Instance = new AsakiGlobalResolver();
		public T Get<T>() where T : class, IAsakiService => AsakiContext.Get<T>();
		public bool TryGet<T>(out T service) where T : class, IAsakiService => AsakiContext.TryGet(out service);
	}
}
