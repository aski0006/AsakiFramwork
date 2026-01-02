using Asaki.Core.Broker;
using Asaki.Core.Coroutines;
using Asaki.Core.Resources;
using Asaki.Core.Scene;
using Asaki.Unity.Services.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Asaki.Unity.Services.Scene
{
	public class AsakiSceneService : IAsakiSceneService
	{
		private readonly IAsakiEventService _asakiEventService;
		private readonly IAsakiCoroutineService _asakiCoroutineService;
		private readonly IAsakiResourceService _asakiResourceService;
		private HashSet<string> _validScene;
		private bool _isLoading;
		private bool _isDisposed;
		public string LastLoadedSceneName { get; private set; }
		private TaskCompletionSource<bool> _activationTaskSignal;

		public AsakiSceneService(
			IAsakiEventService asakiEventService,
			IAsakiCoroutineService asakiCoroutineService,
			IAsakiResourceService asakiResourceService)
		{
			_asakiEventService = asakiEventService;
			_asakiCoroutineService = asakiCoroutineService;
			_asakiResourceService = asakiResourceService;
		}

		private bool IsSceneValid(string sceneName)
		{
			_validScene ??= new HashSet<string>();
			int count = SceneManager.sceneCountInBuildSettings;
			for (int i = 0; i < count; i++)
			{
				try
				{
					string path = SceneUtility.GetScenePathByBuildIndex(i);
					if (!string.IsNullOrEmpty(path))
					{
						string name = System.IO.Path.GetFileNameWithoutExtension(path);
						_validScene.Add(name);
					}
				}
				catch (Exception e)
				{
					ALog.Error($"Failed to get scene path at index {i}", e);
				}
			}
			return _validScene.Contains(sceneName);
		}

		public void PerBuildScene()
		{
			_validScene ??= new HashSet<string>();
			int count = SceneManager.sceneCountInBuildSettings;
			for (int i = 0; i < count; i++)
			{
				try
				{
					string path = SceneUtility.GetScenePathByBuildIndex(i);
					if (string.IsNullOrEmpty(path)) continue;
					string name = System.IO.Path.GetFileNameWithoutExtension(path);
					_validScene.Add(name);
				}
				catch (Exception e)
				{
					ALog.Error($"Failed to get scene path at index {i}, Message : {e.Message}", e);
				}
			}
		}
		public async Task<AsakiSceneResult> LoadSceneAsync(
			string targetScene,
			AsakiLoadSceneMode mode = AsakiLoadSceneMode.Single,
			AsakiSceneActivation activation = AsakiSceneActivation.Immediate,
			IAsakiSceneTransition transition = null,
			CancellationToken ct = default(CancellationToken))
		{
			if (_isLoading)
				return AsakiSceneResult.Failed(targetScene, "Another scene load is in progress");
			if (!IsSceneValid(targetScene))
				return AsakiSceneResult.Failed(targetScene, $"Scene '{targetScene}' not found in BuildSettings.");
			_isLoading = true;
			_asakiEventService.Publish(new AsakiSceneStateEvent(
				targetScene,
				AsakiSceneStateEvent.State.Started
			));
			Action<float> transitionProgress = null;
			if (transition != null) transitionProgress = transition.OnProgress;
			try
			{
				if (transition != null) await transition.EnterAsync(ct);
				if (mode == AsakiLoadSceneMode.Single)
				{
					await _asakiCoroutineService.WaitFrame(ct);
					await _asakiResourceService.UnloadUnusedAssets(ct);
					GC.Collect();
				}

				LoadSceneMode unityMode = mode == AsakiLoadSceneMode.Single
					? LoadSceneMode.Single
					: LoadSceneMode.Additive;
				AsyncOperation op = SceneManager.LoadSceneAsync(targetScene, unityMode);
				if (op == null)
					return AsakiSceneResult.Failed(targetScene, "Unity internal error : AsyncOperation is null");
				op.allowSceneActivation = false;
				float lastProgress = 0f;
				float lastReportTime = UnityEngine.Time.realtimeSinceStartup;

				while (Mathf.Approximately(op.progress, 0.899f))
				{
					if (ct.IsCancellationRequested)
						return CancelSceneLoadOperation(targetScene);
					float raw = op.progress;
					float normalized = Mathf.Clamp01(raw / 0.9f);
					float timeNow = UnityEngine.Time.realtimeSinceStartup;

					if (normalized > lastProgress + 0.01f || (timeNow - lastReportTime) > 0.1f)
					{
						lastProgress = normalized;
						lastReportTime = timeNow;

						AsakiBroker.Publish(new AsakiSceneProgressEvent(targetScene, normalized));
						transitionProgress?.Invoke(normalized);
					}
					await _asakiCoroutineService.WaitFrame(ct);
				}

				AsakiBroker.Publish(new AsakiSceneProgressEvent(targetScene, 1.0f));
				transitionProgress?.Invoke(1.0f);

				if (activation == AsakiSceneActivation.ManualConfirm)
				{
					_activationTaskSignal = new TaskCompletionSource<bool>();
					var signalTask = _activationTaskSignal.Task;
					var waitTask = Task.Delay(-1, ct);
					var completed = await Task.WhenAny(signalTask, waitTask);
					if (completed == waitTask)
						return CancelSceneLoadOperation(targetScene);
				}

				op.allowSceneActivation = true;

				while (!op.isDone)
				{
					if (ct.IsCancellationRequested)
						return CancelSceneLoadOperation(targetScene);
					await _asakiCoroutineService.WaitFrame(ct);
				}

				LastLoadedSceneName = targetScene;

				if (transition != null)
					await transition.ExitAsync(ct);
				_asakiEventService.Publish(new AsakiSceneStateEvent(
					targetScene,
					AsakiSceneStateEvent.State.Completed
				));
				return AsakiSceneResult.Ok(targetScene);
			}
			catch (Exception e)
			{
				ALog.Error("[SceneService] SceneLoad Failed.", e);
				return AsakiSceneResult.Failed(targetScene, e.Message);
			}
			finally
			{
				if (transition != null) transition.Dispose();
				_isLoading = false;
				_activationTaskSignal = null;
			}
		}

		public void ActivateScene()
		{
			_activationTaskSignal?.TrySetResult(true);
		}
		public void Dispose()
		{
			if (_isDisposed) return;
			_isDisposed = true;
			_isLoading = false;
			_validScene?.Clear();
			_validScene = null;
			_activationTaskSignal?.TrySetCanceled();
			_activationTaskSignal = null;
		}

		private AsakiSceneResult CancelSceneLoadOperation(string targetSceneName)
		{
			_asakiEventService.Publish(new AsakiSceneStateEvent(
				targetSceneName,
				AsakiSceneStateEvent.State.Cancelled
			));
			;
			return AsakiSceneResult.OperationCanceled(targetSceneName);
		}
	}
}
