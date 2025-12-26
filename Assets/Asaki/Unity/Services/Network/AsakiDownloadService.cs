using Asaki.Core.Broker;
using Asaki.Core.Coroutines;
using Asaki.Core.Network;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Asaki.Unity.Services.Network
{
	public struct AsakiDownloadSuccessEvent : IAsakiEvent { }
	public struct AsakiDownloadCancelledEvent : IAsakiEvent { }
	public struct AsakiDownloadErrorEvent : IAsakiEvent
	{
		public string ErrorMsg { get; set; }
	}

	public class AsakiDownloadService : IAsakiDownloadService
	{
		private IAsakiCoroutineService _asakiCoroutineService;
		private IAsakiEventService _asakiEventService;

		public AsakiDownloadService(IAsakiCoroutineService asakiCoroutineService, IAsakiEventService asakiEventService)
		{
			_asakiCoroutineService = asakiCoroutineService;
			_asakiEventService = asakiEventService;
		}

		public async Task DownloadAsync(string url, string localPath, IProgress<float> progress = null, CancellationToken token = default(CancellationToken))
		{
			string dir = Path.GetDirectoryName(localPath);
			if (string.IsNullOrEmpty(dir))
			{
				Debug.LogError("localPath 必须是绝对路径");
				return;
			}
			if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
			DownloadHandlerFile fileHandler = new DownloadHandlerFile(localPath) { removeFileOnAbort = false };
			using (UnityWebRequest uwr = new UnityWebRequest(url, "GET"))
			{
				uwr.downloadHandler = fileHandler;
				UnityWebRequestAsyncOperation op = uwr.SendWebRequest();
				while (!op.isDone)
				{
					if (token.IsCancellationRequested)
					{
						uwr.Abort();
						_asakiEventService.Publish(new AsakiDownloadCancelledEvent());
						if (File.Exists(localPath)) File.Delete(localPath);
						throw new OperationCanceledException(token);
					}
					progress?.Report(uwr.downloadProgress);
					await _asakiCoroutineService.WaitFrame(token);
				}
				if (uwr.result == UnityWebRequest.Result.ConnectionError ||
				    uwr.result == UnityWebRequest.Result.ProtocolError)
				{
					// 下载失败，清理文件
					if (File.Exists(localPath)) File.Delete(localPath);
					_asakiEventService.Publish(new AsakiDownloadErrorEvent { ErrorMsg = $"[Downloader] Failed: {uwr.error}\nURL: {url}" });
					throw new Exception($"[Downloader] Failed: {uwr.error}\nURL: {url}");
				}
				progress?.Report(1.0f);
				_asakiEventService.Publish(new AsakiDownloadSuccessEvent());
			}
		}
		public async Task<long> GetFileSizeAsync(string url)
		{
			using (UnityWebRequest uwr = UnityWebRequest.Head(url))
			{
				UnityWebRequestAsyncOperation op = uwr.SendWebRequest();
				while (!op.isDone)
				{
					// 获取文件头通常很快，不需要复杂的进度
					await _asakiCoroutineService.WaitFrame();
				}
				if (uwr.result == UnityWebRequest.Result.Success)
				{
					string lenHeader = uwr.GetResponseHeader("Content-Length");
					if (long.TryParse(lenHeader, out long size))
					{
						return size;
					}
				}
				return -1;
			}
		}
	}
}
