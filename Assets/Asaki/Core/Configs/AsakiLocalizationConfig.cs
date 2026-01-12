using System;
using UnityEngine;

namespace Asaki.Core.Configs
{
	[Serializable]
	public class AsakiLocalizationConfig
	{
		[field: SerializeField] public string FallbackLanguage { get; private set; } = "en";
	}
}
