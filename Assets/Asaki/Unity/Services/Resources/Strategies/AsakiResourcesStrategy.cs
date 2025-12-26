using Asaki.Core.Coroutines;
using Asaki.Core.Resources;
using Asaki.Unity.Utils;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Asaki.Unity.Services.Resources.Strategies
{
	public class AsakiResourcesStrategy : IAsakiResStrategy
	{
		public string StrategyName => "Resources (Native)";
		private IAsakiCoroutineService _coroutine;
		public AsakiResourcesStrategy(IAsakiCoroutineService coroutine)
		{
			_coroutine = coroutine;
		}
		public Task InitializeAsync()
		{
			return Task.CompletedTask;
		}
		public async Task<Object> LoadAssetInternalAsync(string location, Type type, Action<float> onProgress, CancellationToken token)
		{
			ResourceRequest request = UnityEngine.Resources.LoadAsync(location, type);

			// 如果没有进度回调，直接使用之前的 Bridge
			if (onProgress == null)
			{
				return await request.ToTask(token);
			}

			// === 进度轮询模式 ===
			// 只要没完成，就每帧报告一次进度
			while (!request.isDone)
			{
				// 响应取消
				if (token.IsCancellationRequested)
				{
					// Resources.LoadAsync 无法真正取消底层 IO，但我们可以停止等待
					throw new OperationCanceledException(token);
				}

				// 报告进度
				onProgress.Invoke(request.progress);

				// 等待下一帧
				await _coroutine.WaitFrame(token);
			}

			// 完成
			onProgress.Invoke(1f);
			return request.asset;
		}
		public void UnloadAssetInternal(string location, Object asset)
		{
			if (asset is not GameObject)
				UnityEngine.Resources.UnloadAsset(asset);
		}
	}
}
