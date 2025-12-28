using System;
using UnityEngine; // 仅用于 Update 驱动
using Asaki.Core.Logging;
using Asaki.Unity.Configuration; // IAsakiLoggingService, LogAggregator

namespace Asaki.Unity.Services.Logging
{
    /// <summary>
    /// V2 日志服务实现 (纯净版)
    /// </summary>
    public class AsakiLoggingService : IAsakiLoggingService
    {
        public AsakiLogAggregator Aggregator { get; }
        private AsakiLogFileWriter _writer;
        private LogUpdateDriver _driver;
        private AsakiLogLevel _minLevel = AsakiLogLevel.Debug;
        private bool _isDisposed;

        public AsakiLoggingService()
        {
            Aggregator = new AsakiLogAggregator();
            _writer = new AsakiLogFileWriter(Aggregator);
            CreateDriver();
        }
        
        public void ApplyConfig(AsakiLogConfig config)
        {
            if (config == null) return;

            // 1. 设置运行时过滤等级
            _minLevel = config?.MinLogLevel ?? AsakiLogLevel.Error;

            // 2. 将配置下发给 Writer (处理轮转和前缀)
            _writer?.ApplyConfig(config);
            
            // 3. 记录一条系统日志
            ALog.Info($"[AsakiLog] Config Applied: Level={config.MinLogLevel}, MaxSize={config.MaxFileSizeKB}KB");
        }
        
        public void LogTrace(AsakiLogLevel level, string message, string payloadJson, string file, int line)
        {
            if (_isDisposed || level < _minLevel) return;
            Aggregator.Log(level, message, payloadJson, file, line, null);
        }

        public void LogException(string message, Exception ex, string file, int line)
        {
            if (_isDisposed) return;
            Aggregator.Log(AsakiLogLevel.Error, message, null, file, line, ex);
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            if (_driver != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(_driver.gameObject);
                else UnityEngine.Object.DestroyImmediate(_driver.gameObject);
            }

            _writer?.Dispose();
            Aggregator.Clear();
        }

        private void CreateDriver()
        {
            var go = new GameObject("AsakiLogDriver");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            _driver = go.AddComponent<LogUpdateDriver>();
            _driver.Aggregator = Aggregator;
        }

        private class LogUpdateDriver : MonoBehaviour
        {
            public AsakiLogAggregator Aggregator;
            private void LateUpdate()
            {
                Aggregator?.Sync();
            }
        }
    }
}