using Asaki.Core.Logging;
using Asaki.Core.Network;
using Asaki.Unity.Services.Logging;
using System;
using UnityEngine.Networking;

namespace Asaki.Unity.Services.Network.Interceptors
{
	[Serializable]
	public class AsakiLogInterceptor : IAsakiWebInterceptor
	{
		public void OnRequest(UnityWebRequest uwr) { }
		public bool OnResponse(UnityWebRequest uwr) { return true; }
		public void OnError(UnityWebRequest uwr, Exception ex)
		{
			ALog.Error($"[Web Error] {uwr.url}: {ex.Message}", ex);
		}
	}
}
