using Asaki.Core.Network;
using System;
using UnityEngine;
using UnityEngine.Networking;

namespace Asaki.Unity.Services.Network.Interceptors
{
	public class AsakiLogInterceptor : IAsakiWebInterceptor
	{
		public void OnRequest(UnityWebRequest uwr) { }
		public bool OnResponse(UnityWebRequest uwr) { return true; }
		public void OnError(UnityWebRequest uwr, Exception ex)
		{
			Debug.LogError($"[Web Error] {uwr.url}: {ex.Message}");
		}
	}
}
