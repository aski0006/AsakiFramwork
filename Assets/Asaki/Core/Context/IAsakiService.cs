namespace Asaki.Core.Context
{
	public interface IAsakiService { }

	public interface IAsakiSceneContextService : IAsakiService { }

	public interface IAsakiGlobalMonoBehaviourService : IAsakiService
	{
		void OnBootstrapInit();
	}
}
