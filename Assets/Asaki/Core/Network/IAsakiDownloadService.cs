using Asaki.Core.Context;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Asaki.Core.Network
{
	/// <summary>
	/// [M-005] 详细下载进度结构体 (值类型，零GC)
	/// </summary>
	public readonly struct AsakiDownloadProgress
	{
		public readonly float Progress;        // 0.0 - 1.0
		public readonly ulong DownloadedBytes; // 已下载字节
		public readonly ulong TotalBytes;      // 总字节 (如果未知则为 0)
		public readonly float Speed;           // 下载速度 (Bytes/s)

		public AsakiDownloadProgress(float progress, ulong downloaded, ulong total, float speed)
		{
			Progress = progress;
			DownloadedBytes = downloaded;
			TotalBytes = total;
			Speed = speed;
		}
	}

	public interface IAsakiDownloadService : IAsakiService
	{
		/// <summary>
		/// 下载文件 (支持断点续传预留，支持流式写入)
		/// </summary>
		/// <param name="progress">进度回调 (使用 AsakiDownloadProgress)</param>
		Task DownloadAsync(string url, string localPath, IProgress<AsakiDownloadProgress> progress = null, CancellationToken token = default(CancellationToken));

		Task<long> GetFileSizeAsync(string url);
	}
}
