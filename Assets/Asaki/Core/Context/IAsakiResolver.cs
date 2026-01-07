namespace Asaki.Core.Context
{
	public interface IAsakiResolver
	{
		T Get<T>() where T : class, IAsakiService;
		bool TryGet<T>(out T service) where T : class, IAsakiService;
	}
}
