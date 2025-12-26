using Asaki.Core.Network;
using Asaki.Core.Serialization;
using Asaki.Unity.Extensions;
using Asaki.Unity.Services.Network.Interceptors;
using Asaki.Unity.Services.Serialization;
using Asaki.Unity.Utils;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Asaki.Unity.Services.Network
{
	public class AsakiWebService : IAsakiWebService
	{
		private string _baseUrl = "";
		private int _timeout = 10;
		private readonly List<IAsakiWebInterceptor> _interceptors = new List<IAsakiWebInterceptor>();
		public void OnInit()
		{
			AddInterceptor(new AsakiLogInterceptor());
		}

		public Task OnInitAsync()
		{
			return Task.CompletedTask;
		}
		public void OnDispose()
		{
			_interceptors.Clear();
		}

		public void SetBaseUrl(string url)
		{
			_baseUrl = url.TrimEnd('/');
		}
		public void SetTimeout(int seconds)
		{
			_timeout = seconds;
		}
		public void AddInterceptor(IAsakiWebInterceptor interceptor)
		{
			if (!_interceptors.Contains(interceptor)) _interceptors.Add(interceptor);
		}
		public void RemoveInterceptor(IAsakiWebInterceptor interceptor)
		{
			_interceptors.Remove(interceptor);
		}

		public async Task<TResponse> GetAsync<TResponse>(string apiPath)
			where TResponse : IAsakiSavable, new()
		{
			string url = BuildUrl(apiPath);
			using (UnityWebRequest uwr = UnityWebRequest.Get(url))
			{
				ConfigureRequest(uwr);
				foreach (IAsakiWebInterceptor i in _interceptors) i.OnRequest(uwr);

				// 使用扩展方法桥接 UnityWebRequest 到 Task
				await uwr.SendWebRequestAsTask();

				return ProcessResponse<TResponse>(uwr);
			}
		}

		public async Task<TResponse> PostAsync<TRequest, TResponse>(string apiPath, TRequest body) where TRequest : IAsakiSavable where TResponse : IAsakiSavable, new()
		{
			string url = BuildUrl(apiPath);
			string jsonBody = SerializeRequest(body);
			byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

			using (UnityWebRequest uwr = new UnityWebRequest(url, "POST"))
			{
				uwr.uploadHandler = new UploadHandlerRaw(bodyRaw);
				uwr.downloadHandler = new DownloadHandlerBuffer();
				uwr.SetRequestHeader("Content-Type", "application/json");

				ConfigureRequest(uwr);
				foreach (IAsakiWebInterceptor i in _interceptors) i.OnRequest(uwr);

				await uwr.SendWebRequestAsTask();

				return ProcessResponse<TResponse>(uwr);
			}
		}

		public async Task<TResponse> PostFormAsync<TResponse>(string apiPath, WWWForm form) where TResponse : IAsakiSavable, new()
		{
			string url = BuildUrl(apiPath);
			using (UnityWebRequest uwr = UnityWebRequest.Post(url, form))
			{
				ConfigureRequest(uwr);
				foreach (IAsakiWebInterceptor i in _interceptors) i.OnRequest(uwr);

				await uwr.SendWebRequestAsTask();

				return ProcessResponse<TResponse>(uwr);
			}
		}

		// =========================================================
		// 内部逻辑
		// =========================================================

		private string BuildUrl(string apiPath)
		{
			if (apiPath.StartsWith("http") || string.IsNullOrEmpty(_baseUrl)) return apiPath;
			return $"{_baseUrl}/{apiPath.TrimStart('/')}";
		}

		private void ConfigureRequest(UnityWebRequest uwr)
		{
			uwr.timeout = _timeout;
		}

		private string SerializeRequest<T>(T body) where T : IAsakiSavable
		{
			StringBuilder sb = AsakiStringBuilderPool.Rent();
			try
			{
				AsakiJsonWriter writer = new AsakiJsonWriter(sb);
				body.Serialize(writer);
				return writer.GetResult();
			}
			finally { AsakiStringBuilderPool.Return(sb); }
		}

		private T ProcessResponse<T>(UnityWebRequest uwr) where T : IAsakiSavable, new()
		{
			// 错误检查
			if (uwr.result == UnityWebRequest.Result.ConnectionError ||
			    uwr.result == UnityWebRequest.Result.ProtocolError)
			{
				AsakiWebException ex = new AsakiWebException(uwr.error, uwr.responseCode, uwr.url);
				foreach (IAsakiWebInterceptor i in _interceptors) i.OnError(uwr, ex);
				throw ex;
			}

			// 业务拦截
			foreach (IAsakiWebInterceptor i in _interceptors)
			{
				if (!i.OnResponse(uwr))
					throw new AsakiWebException("Intercepted", uwr.responseCode, uwr.url);
			}

			// 反序列化
			string json = uwr.downloadHandler.text;
			if (string.IsNullOrEmpty(json)) return default(T);

			try
			{
				AsakiJsonReader reader = AsakiJsonReader.FromJson(json);
				T response = new T();
				response.Deserialize(reader);
				return response;
			}
			catch (Exception ex)
			{
				Debug.LogError($"[AsakiWeb] Parse Error: {ex.Message}\n{json}"); // TODO: [Asaki] -> Asaki.ALog.Error
				throw;
			}
		}
	}
}
