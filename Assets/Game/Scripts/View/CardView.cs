using Asaki.Core;
using Asaki.Core.MVVM;
using Asaki.Core.Resources;
using Asaki.Core.Tasks;
using Asaki.Unity.Services.UI.Observers;
using Game.Scripts.Data;
using System;
using TMPro;
using UnityEngine;

namespace Game.Scripts.View
{
	[Serializable]
	[AsakiBind]
	public partial class CardViewMVVM
	{
		[field: SerializeField] public AsakiProperty<int> CardCost { get; private set; } = new();
		[field: SerializeField] public AsakiProperty<int> CardAtk { get; private set; } = new();
		[field: SerializeField] public AsakiProperty<int> CardDef { get; private set; } = new();
	}

	public class CardView : MonoBehaviour
	{
		[SerializeField] private TMP_Text CardCostText;
		[SerializeField] private TMP_Text CardAtkText;
		[SerializeField] private TMP_Text CardDefText;
		[SerializeField] private SpriteRenderer CardSprite;
		[SerializeField] private TMP_Text CardDescription;
		public CardViewMVVM ViewModel { get; private set; } = new();
		private AsakiTMPTextIntObserver _cardCostObserver;
		private AsakiTMPTextIntObserver _cardAtkObserver;
		private AsakiTMPTextIntObserver _cardDefObserver;
		private IAsakiResourceService _asakiResourceService;
		private void Awake()
		{
			_cardCostObserver = new AsakiTMPTextIntObserver(CardCostText);
			_cardAtkObserver = new AsakiTMPTextIntObserver(CardAtkText);
			_cardDefObserver = new AsakiTMPTextIntObserver(CardDefText);
		}

		public async AsakiTaskVoid LoadCardData(CardData cardData,
		                                        IAsakiResourceService asakiResourceService)
		{
			ViewModel.CardCost.Value = cardData.Cost;
			ViewModel.CardAtk.Value = cardData.Atk;
			ViewModel.CardDef.Value = cardData.Def;
			CardSprite.sprite =  await asakiResourceService.LoadAsync<Sprite>(cardData.CardSpriteAssetKey, destroyCancellationToken);
			CardDescription.text = cardData.CardDescription;
		}

		private void OnEnable()
		{
			ViewModel.CardCost.Bind(_cardCostObserver);
			ViewModel.CardAtk.Bind(_cardAtkObserver);
			ViewModel.CardDef.Bind(_cardDefObserver);
		}

		private void OnDisable()
		{
			ViewModel.CardCost.Unbind(_cardCostObserver);
			ViewModel.CardAtk.Unbind(_cardAtkObserver);
			ViewModel.CardDef.Unbind(_cardDefObserver);
		}
	}
}
