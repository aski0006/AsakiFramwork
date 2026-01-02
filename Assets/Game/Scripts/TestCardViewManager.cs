using Asaki.Core.Configuration;
using Asaki.Core.Context;
using Asaki.Core.Pooling;
using Asaki.Core.Resources;
using Asaki.Core.Tasks;
using Game.Scripts.Data;
using Game.Scripts.View;
using UnityEngine;

namespace Game.Scripts
{
	public class TestCardViewManager : MonoBehaviour
	{
		public string CardViewPrefabPath = "CardView";
		private IAsakiPoolService _asakiPoolService;
		private IAsakiConfigService _asakiConfigService;
		private IAsakiResourceService _asakiResourceService;
		private void Start()
		{
			_asakiPoolService = AsakiContext.Get<IAsakiPoolService>();
			_asakiConfigService = AsakiContext.Get<IAsakiConfigService>();
			_asakiResourceService = AsakiContext.Get<IAsakiResourceService>();	
			Prewarn().Forget();
		}

		private async AsakiTaskVoid Prewarn()
		{
			await _asakiPoolService.PrewarmAsync(CardViewPrefabPath, 8);
		}

		[ContextMenu("SpawnCardView")]
		public void SpawnCardView()
		{
			var cvgo = _asakiPoolService.Spawn(CardViewPrefabPath);
			var cv = cvgo.GetComponent<CardView>(); 
			cv.LoadCardData(_asakiConfigService.Get<CardData>(0), _asakiResourceService).Forget();
		}
	}
}
