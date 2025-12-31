using System;
using System.Threading;
using System.Threading.Tasks;

namespace Asaki.Core.Resources
{
	public interface IAsakiResStrategy
	{
		string StrategyName { get; }
		Task InitializeAsync();

		/// <summary>
		/// 加载资源 (支持进度回调)
		/// </summary>
		/// <param name="onProgress">进度回调 (0.0 ~ 1.0)</param>
		Task<UnityEngine.Object> LoadAssetInternalAsync(string location, Type type, Action<float> onProgress, CancellationToken token);

		void UnloadAssetInternal(string location, UnityEngine.Object asset);
		
		Task UnloadUnusedAssets(CancellationToken token);
	}
}
