using Asaki.Core.Context;
using Asaki.Core.Coroutines;
using Asaki.Core.Network;
using Asaki.Core.Tasks;
using Asaki.Unity.Utils;
using System;
using System.IO;
using System.Threading;
using UnityEngine;

namespace Game.Examples
{
	public class AsakiDownloadTest : MonoBehaviour
	{
		private IAsakiDownloadService _downloader;
		private IAsakiCoroutineService _asakiCoroutineService;
		private CancellationTokenSource _cts;
		private void Start()
		{
			_downloader = AsakiContext.Get<IAsakiDownloadService>();
			_asakiCoroutineService = AsakiContext.Get<IAsakiCoroutineService>();
		}

		[ContextMenu("Test Download Image")]
		public async AsakiTaskVoid TestDownload()
		{
			string url = "https://image.baidu.com/search/down?&tn=download&word=download&ie=utf8&fr=home&url=https%3A%2F%2Fgips2.baidu.com%2Fit%2Fu%3D1651586290%2C17201034%26fm%3D3028%26app%3D3028%26f%3DJPEG%26fmt%3Dauto%26q%3D100%26size%3Df600_800&thumbUrl=https%3A%2F%2Fgips2.baidu.com%2Fit%2Fu%3D1651586290%2C17201034%26fm%3D3028%26app%3D3028%26f%3DJPEG%26fmt%3Dauto%26q%3D100%26size%3Df600_800&iswise=1";
			string savePath = Path.Combine(Application.persistentDataPath, "Downloads", "test_image.jpg");

			Debug.Log($"[Test] Start downloading to: {savePath}");
			var progress = new Progress<float>(p =>
			{
				// 打印进度 (0.1, 0.2 ...) 避免刷屏
				Debug.Log($"Downloading... {p:P0}");
			});

			_cts = new CancellationTokenSource();
			var linkedToken = _asakiCoroutineService.Link(this, _cts.Token);
			try
			{
				// 获取文件大小
				long size = await _downloader.GetFileSizeAsync(url);
				Debug.Log($"[Test] File Size: {size / 1024} KB");

				// 开始下载
				await _downloader.DownloadAsync(url, savePath, progress, linkedToken);

				Debug.Log("<color=green>[Test] Download Success!</color>");

				// 验证文件存在
				if (File.Exists(savePath))
				{
					Debug.Log($"File exists, size: {new FileInfo(savePath).Length} bytes");	
				}
			}
			catch (OperationCanceledException)
			{
				Debug.LogWarning("[Test] Download Canceled.");
			}
			catch (Exception ex)
			{
				Debug.LogError($"[Test] Failed: {ex.Message}");
			}
		}

		[ContextMenu("Cancel Download")]
		public void Cancel()
		{
			_cts?.Cancel();
		}

	}
}
