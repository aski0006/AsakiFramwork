using Asaki.Core.Context;
using Asaki.Core.Serialization;
using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Asaki.Core.Network
{
	public interface IAsakiWebService : IAsakiService, IDisposable
	{
		void Setup(AsakiWebConfig config);
		void AddInterceptor(IAsakiWebInterceptor interceptor);
		void RemoveInterceptor(IAsakiWebInterceptor interceptor);

		/// <summary>
		/// GET 请求
		/// </summary>
		Task<TResponse> GetAsync<TResponse>(string apiPath)
			where TResponse : IAsakiSavable, new();

		Task<TResponse> PostAsync<TRequest, TResponse>(string apiPath, TRequest body)
			where TRequest : IAsakiSavable
			where TResponse : IAsakiSavable, new();

		Task<TResponse> PostFormAsync<TResponse>(string apiPath, WWWForm form)
			where TResponse : IAsakiSavable, new();
	}

	public interface IAsakiWebInterceptor
	{
		void OnRequest(UnityEngine.Networking.UnityWebRequest uwr);
		bool OnResponse(UnityEngine.Networking.UnityWebRequest uwr);
		void OnError(UnityEngine.Networking.UnityWebRequest uwr, System.Exception ex);
	}

}
