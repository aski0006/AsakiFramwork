using Asaki.Core.Context;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Asaki.Core.Scene
{
	public interface IAsakiSceneService : IAsakiService, IDisposable
	{
		string LastLoadedSceneName { get; }
		void PerBuildScene();
		Task<AsakiSceneResult> LoadSceneAsync(
			string sceneName,
			AsakiLoadSceneMode mode = AsakiLoadSceneMode.Single,
			AsakiSceneActivation activation = AsakiSceneActivation.Immediate,
			IAsakiSceneTransition transition = null,
			CancellationToken ct = default(CancellationToken)
		);

		void ActivateScene();
	}
}
