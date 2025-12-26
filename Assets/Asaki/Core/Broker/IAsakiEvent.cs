namespace Asaki.Core.Broker
{
	public interface IAsakiEvent { }

	public interface IAsakiHandler<T> where T : IAsakiEvent
	{
		void OnEvent(T e);
	}

}
