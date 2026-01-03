using Asaki.Core.Broker;
using Asaki.Core.Async;
using Asaki.Core.Logging;
using Asaki.Core.Network;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
namespace Asaki.Unity.Services.Network
{
    // 事件定义保持不变，建议未来优化为 ref struct 传递
    public struct AsakiDownloadSuccessEvent : IAsakiEvent { public string Url; }
    public struct AsakiDownloadCancelledEvent : IAsakiEvent { public string Url; }
    public struct AsakiDownloadErrorEvent : IAsakiEvent
    {
        public string ErrorMsg { get; set; }
        public string Url { get; set; }
    }

    public class AsakiDownloadService : IAsakiDownloadService
    {
        private readonly IAsakiAsyncService _asakiAsyncService;
        private readonly IAsakiEventService _asakiEventService;

        // [M-003] 构造函数依赖检查
        public AsakiDownloadService(IAsakiAsyncService asakiAsyncService, IAsakiEventService asakiEventService)
        {
            _asakiAsyncService = asakiAsyncService ?? throw new ArgumentNullException(nameof(asakiAsyncService));
            _asakiEventService = asakiEventService ?? throw new ArgumentNullException(nameof(asakiEventService));
        }

        public async Task DownloadAsync(string url, string localPath, IProgress<AsakiDownloadProgress> progress = null, CancellationToken token = default(CancellationToken))
        {
            // [W-006] 路径安全校验
            ValidatePath(localPath);

            // [W-006] 异步创建目录，防止IO阻塞主线程
            string dir = Path.GetDirectoryName(localPath);
            if (!string.IsNullOrEmpty(dir))
            {
                await Task.Run(() =>
                {
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                });
            }

            // 使用 DownloadHandlerFile (Stream) 避免内存爆涨
            DownloadHandlerFile fileHandler = new DownloadHandlerFile(localPath) { removeFileOnAbort = false };

            using (UnityWebRequest uwr = new UnityWebRequest(url, "GET"))
            {
                uwr.downloadHandler = fileHandler;
                UnityWebRequestAsyncOperation op = uwr.SendWebRequest();

                // 速度计算变量
                float lastTime = UnityEngine.Time.realtimeSinceStartup;
                ulong lastBytes = 0;

                while (!op.isDone)
                {
                    // 1. 处理取消
                    if (token.IsCancellationRequested)
                    {
                        uwr.Abort();
                        // [W-006] 异步删除残余文件
                        await DeleteFileAsync(localPath);
                        _asakiEventService.Publish(new AsakiDownloadCancelledEvent { Url = url });
                        throw new OperationCanceledException(token);
                    }

                    // 2. 计算进度与速度 (每帧更新)
                    if (progress != null)
                    {
                        float currentTime =UnityEngine.Time.realtimeSinceStartup;
                        float deltaTime = currentTime - lastTime;
                        
                        // 限制更新频率，避免除以零或过于敏感
                        if (deltaTime > 0.1f) 
                        {
                            ulong currentBytes = uwr.downloadedBytes;
                            ulong totalBytes = 0; // 尝试获取 Content-Length (未必每帧都准确，通常在 header 收到后固定)
                            
                            // 尝试解析总大小 (Unity 有时在下载中途 downloadProgress 为 0-1)
                             // 注意：uwr.downloadHandler.data 不可用，因为是 Stream 模式
                            
                            // 简单速度计算
                            float speed = (currentBytes - lastBytes) / deltaTime; // Bytes per second

                            // 估算 Total
                            if (uwr.downloadProgress > 0)
                            {
                                totalBytes = (ulong)(currentBytes / uwr.downloadProgress);
                            }

                            progress.Report(new AsakiDownloadProgress(
                                uwr.downloadProgress, 
                                currentBytes, 
                                totalBytes, 
                                speed
                            ));

                            lastBytes = currentBytes;
                            lastTime = currentTime;
                        }
                    }

                    await _asakiAsyncService.WaitFrame(token);
                }

                // 3. 处理结果
                if (uwr.result == UnityWebRequest.Result.ConnectionError ||
                    uwr.result == UnityWebRequest.Result.ProtocolError)
                {
                    await DeleteFileAsync(localPath);
                    string errorMsg = $"[Downloader] Failed: {uwr.error}\nURL: {url}";
                    _asakiEventService.Publish(new AsakiDownloadErrorEvent { ErrorMsg = errorMsg, Url = url });
                    throw new Exception(errorMsg);
                }

                // 4. 完成
                progress?.Report(new AsakiDownloadProgress(1.0f, uwr.downloadedBytes, uwr.downloadedBytes, 0));
                _asakiEventService.Publish(new AsakiDownloadSuccessEvent { Url = url });
            }
        }

        public async Task<long> GetFileSizeAsync(string url)
        {
            using (UnityWebRequest uwr = UnityWebRequest.Head(url))
            {
                await uwr.SendWebRequest(); // 这里使用扩展方法桥接，如果已引入 AsakiUnityWebRequestExtensions
                
                // 或者保持原样循环，但必须支持 token
                // 建议使用 SendWebRequestAsTask 扩展方法更简洁

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

        // =========================================================
        // 内部工具 (安全与IO)
        // =========================================================

        /// <summary>
        /// [W-006] 路径白名单校验
        /// </summary>
        private void ValidatePath(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("Path cannot be empty");
            
            // 允许 persistentDataPath 和 temporaryCachePath
            bool isSafe = path.StartsWith(Application.persistentDataPath) || 
                          path.StartsWith(Application.temporaryCachePath);

            if (!isSafe)
            {
                // 在编辑器下为了测试方便，可能允许 Project 目录，但发布必须严格
                if (Application.isEditor) return; 
                
                throw new UnauthorizedAccessException($"[Security] Access denied. Path must be within persistentDataPath or temporaryCachePath. Target: {path}");
            }
        }

        /// <summary>
        /// [W-006] 异步删除文件 (防卡顿)
        /// </summary>
        private async Task DeleteFileAsync(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            
            // 再次校验，双重保险
            try 
            {
                ValidatePath(path); 
            }
            catch { return; }

            await Task.Run(() =>
            {
                try
                {
                    if (File.Exists(path)) File.Delete(path);
                }
                catch (Exception ex)
                {
                    ALog.Warn($"[Downloader] Delete file failed: {ex.Message}");
                }
            });
        }
    }
}