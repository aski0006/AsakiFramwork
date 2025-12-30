using System;
using UnityEngine;

namespace Asaki.Core.Network
{
	[Serializable]
	public class AsakiWebConfig
	{
		[field: SerializeField] public string BaseUrl { get; set; }
		[field: SerializeField] public int TimeoutSeconds { get; set; }
		public IAsakiWebInterceptor[] InitialInterceptors;
	}
}
