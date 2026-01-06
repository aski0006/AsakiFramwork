using Asaki.Core.Attributes;
using Asaki.Core.Network;
using System;
using UnityEngine;

namespace Asaki.Core.Configs
{
	[Serializable]
	public class AsakiWebConfig
	{
		[field: SerializeField] public string BaseUrl { get; set; }
		[field: SerializeField] public int TimeoutSeconds { get; set; }
		[SerializeReference]
		[AsakiInterface(typeof(IAsakiWebInterceptor))]
		public IAsakiWebInterceptor[] InitialInterceptors;
	}
}
