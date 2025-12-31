using Asaki.Core.Context;
using Asaki.Core.Scene;
using Asaki.Core.Tasks;
using Asaki.Unity.Services.Logging;
using JetBrains.Annotations;
using System;
using UnityEngine;

namespace Game.Examples
{
	public class AsakiSceneTest : MonoBehaviour
	{
		public string SceneName_1 = "Scene_1";
		public string SceneName_2 = "Scene_2";
		private IAsakiSceneService _asakiSceneService;
		private void Start()
		{
			_asakiSceneService = AsakiContext.Get<IAsakiSceneService>();
		}

		[ContextMenu("LoadScene_1_Add")]
		private async AsakiTaskVoid LoadScene_1_Add()
		{
			var sceneResult = await _asakiSceneService.LoadSceneAsync(SceneName_1, AsakiLoadSceneMode.Additive);
			ALog.Info(sceneResult.Success ? $"LoadScene_1_Add Success, SceneName: {SceneName_1}" : $"LoadScene_1_Add Failed, SceneName: {SceneName_1}");
		}

		[ContextMenu("LoadScene_2_Single")]
		private async AsakiTaskVoid LoadScene_2_Single()
		{
			var sceneResult = await _asakiSceneService.LoadSceneAsync(SceneName_2);
			ALog.Info(sceneResult.Success ? $"LoadScene_2_Single Success, SceneName: {SceneName_2}" : $"LoadScene_2_Single Failed, SceneName: {SceneName_2}");
		}
	}
}
