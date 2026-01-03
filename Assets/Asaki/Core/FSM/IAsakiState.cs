namespace Asaki.Core.FSM
{
	public interface IAsakiState
	{
		void OnEnter();
		void OnUpdate(float deltaTime);
		void OnFixedUpdate(float fixedDeltaTime);
		void OnExit();
	}

}
