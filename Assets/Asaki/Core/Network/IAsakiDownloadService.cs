using Asaki.Core.Context;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Asaki.Core.Network
{
	public interface IAsakiDownloadService : IAsakiService
	{
		/// <summary>
		/// 下载文件 (支持大文件流式写入)
		/// </summary>
		/// <param name="url">资源地址</param>
		/// <param name="localPath">本地保存路径 (绝对路径)</param>
		/// <param name="progress">进度回调 (0.0 ~ 1.0)</param>
		/// <param name="token">取消令牌</param>
		Task DownloadAsync(string url, string localPath, IProgress<float> progress = null, CancellationToken token = default(CancellationToken));

		/// <summary>
		/// 获取文件大小 (HEAD 请求)
		/// 用于显示 "需要下载 x MB" 或校验文件
		/// </summary>
		Task<long> GetFileSizeAsync(string url);
	}
}
