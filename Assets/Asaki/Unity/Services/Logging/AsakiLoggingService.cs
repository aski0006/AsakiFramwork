using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Asaki.Core.Logging;
using System.Linq;
using UnityEngine;
using Asaki.Unity.Configuration;

namespace Asaki.Unity.Services.Logging
{
    public class AsakiLoggingService : IAsakiLoggingService
    {
        // 常量定义（基础不变）
        public const string LOG_ROOT_FOLDER = "Logs";
        public const string SESSION_PREFIX = "Session_";
        public const string LOG_FILE_PREFIX = "Log_";
        public const string DATE_FORMAT = "yyyyMMdd_HHmmss";
        private const string LOG_VERSION_HEADER = "#VERSION:5.1"; // 协议版本号

        // 配置字段（不再使用const）
        private readonly int _maxFileSize;
        private readonly int _maxHistorySessions;
        private readonly bool _writeCallerInfoForLowLevel;
        
        private readonly object _fileLock = new object();
        private readonly object _modeLock = new object(); 
        
        private AsakiLogLevel _currentLevel = AsakiLogLevel.Debug;
        private volatile bool _isRunning = false;
        
        private readonly ConcurrentQueue<AsakiLogEntry> _queue = new ConcurrentQueue<AsakiLogEntry>();
        private readonly AutoResetEvent _signal = new AutoResetEvent(false);
        
        private Thread _writerThread;
        private string _sessionDir;
        private string _currentLogPath;
        private int _fileIndex = 0;
        private readonly StringBuilder _writerBuffer = new StringBuilder(4096);

        private readonly List<AsakiLogEntry> _earlyBootLogs = new List<AsakiLogEntry>();
        private volatile bool _isEarlyBootMode = true;

        // 构造函数：接收配置
        public AsakiLoggingService(AsakiLogConfig config = null)
        {
            // 使用配置或默认值
            var cfg = config ?? new AsakiLogConfig();
            _maxFileSize = cfg.MaxFileSizeKb * 1024;
            _maxHistorySessions = cfg.MaxHistorySessions;
            _writeCallerInfoForLowLevel = cfg.WriteCallerInfoForLowLevel;
            _currentLevel = cfg.MinLogLevel;
            ALog.ConfigureSampling(cfg.SampleIntervalFrames, cfg.HotPathInvocationThreshold);
        }

        public void InitializeEarly()
        {
            SetupSession();
            
            lock (_modeLock)
            {
                _isEarlyBootMode = true;
                var entry = CreateEntry(AsakiLogLevel.Info, "===== ASAKI FRAMEWORK EARLY BOOT =====", default, null, null, null, 0, null);
                _earlyBootLogs.Add(entry);
                var pathEntry = CreateEntry(AsakiLogLevel.Info, $"Log Service Early Initialized. Session: {_sessionDir}", default, null, null, null, 0, null);
                _earlyBootLogs.Add(pathEntry);
            }
            
            ALog.RegisterBackend(this);
            RegisterCallbacks();
        }

        public void InitializeFull()
        {
            lock (_modeLock)
            {
                if (!_isEarlyBootMode) return;

                _isRunning = true;
                
                _writerThread = new Thread(WriteLoop)
                {
                    IsBackground = true,
                    Name = "AsakiLogWriter",
                    Priority = System.Threading.ThreadPriority.BelowNormal
                };
                _writerThread.Start();

                foreach (var entry in _earlyBootLogs)
                {
                    _queue.Enqueue(entry);
                }
                _earlyBootLogs.Clear();

                _isEarlyBootMode = false;
                _signal.Set();
            }
            EnqInternal(AsakiLogLevel.Info, "Log Service Fully Initialized - Writer thread started");
            
            
        }

        public void Dispose()
        {
            if (!_isRunning && !_isEarlyBootMode) return;

            UnregisterCallbacks();
            ALog.Reset();
            
            _isRunning = false;
            _signal.Set();
            
            if (_writerThread != null && _writerThread.IsAlive)
                _writerThread.Join(500);
                
            _signal.Dispose();
        }

        public void SetLevel(AsakiLogLevel level) => _currentLevel = level;

        public void Enqueue(AsakiLogLevel level, string message, ExceptionSnapshot exSnap, 
                            string payloadJson, string stackTrace, 
                            string file, int line, string member)
        {
            if (level < _currentLevel) return;

            var entry = CreateEntry(level, message, exSnap, payloadJson, stackTrace, file, line, member);

            if (_isEarlyBootMode)
            {
                lock (_modeLock)
                {
                    if (_isEarlyBootMode)
                    {
                        _earlyBootLogs.Add(entry);
                        
                        if (level >= AsakiLogLevel.Error) FlushEarlyLogsToFile();
                        return;
                    }
                }
            }

            _queue.Enqueue(entry);
            _signal.Set();
        }
        
        private AsakiLogEntry CreateEntry(AsakiLogLevel level, string message, ExceptionSnapshot exSnap, 
            string payloadJson, string stackTrace, string file, int line, string member)
        {
            return new AsakiLogEntry
            {
                Timestamp = DateTime.UtcNow.Ticks,
                ThreadId = Thread.CurrentThread.ManagedThreadId,
                Level = level,
                Message = message,
                ExceptionData = exSnap,
                PayloadJson = payloadJson,
                StackTrace = stackTrace,
                CallerPath = file,
                CallerLine = line,
                CallerMember = member
            };
        }
        
        private void EnqInternal(AsakiLogLevel level, string msg) 
            => Enqueue(level, msg, default, null, null, null, 0, null);

        public void FlushSync()
        {
            if (_isEarlyBootMode)
            {
                lock (_modeLock)
                {
                    if (_isEarlyBootMode)
                    {
                        FlushEarlyLogsToFile();
                        return;
                    }
                }
            }
            ProcessQueueToFile(true);
        }

        private void FlushEarlyLogsToFile()
        {
            lock (_fileLock)
            {
                try
                {
                    if (string.IsNullOrEmpty(_currentLogPath)) return;
    
                    using (var fs = new FileStream(_currentLogPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                    using (var sw = new StreamWriter(fs, Encoding.UTF8))
                    {
                        // 注意：这里复制列表以避免迭代时修改的问题
                        var logsCopy = new List<AsakiLogEntry>(_earlyBootLogs);
                        foreach (var entry in logsCopy)
                        {
                            _writerBuffer.Clear();
                            var entryCopy = entry; // 创建副本用于ref参数
                            FormatLogEntry(_writerBuffer, ref entryCopy);
                            sw.Write(_writerBuffer.ToString());
                        }
                        sw.Flush();
                        fs.Flush(true);
                    }
                }
                catch { /* Ignored */ }
            }
        }
        
        private void WriteLoop()
        {
            try
            {
                while (_isRunning)
                {
                    _signal.WaitOne();
                    ProcessQueueToFile(false);
                }
                ProcessQueueToFile(true);
            }
            catch (Exception e)
            {
                Debug.LogError($"[AsakiLogger Fatal] Writer thread crashed: {e.Message}");
            }
        }

        private void ProcessQueueToFile(bool forceFlush)
        {
            if (string.IsNullOrEmpty(_currentLogPath)) return;

            lock (_fileLock) 
            {
                try
                {
                    using (var fs = new FileStream(_currentLogPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                    using (var sw = new StreamWriter(fs, Encoding.UTF8))
                    {
                        while (_queue.TryDequeue(out var entry))
                        {
                            _writerBuffer.Clear();
                            FormatLogEntry(_writerBuffer, ref entry);
                            sw.Write(_writerBuffer.ToString());

                            if (fs.Length > _maxFileSize)
                            {
                                sw.Flush();
                                fs.Flush(true);
                                CreateNewLogFile();
                                break; 
                            }
                        }
                        sw.Flush();
                        if (forceFlush) fs.Flush(true);
                    }
                }
                catch { /* Ignored */ }
            }
        }

        private void FormatLogEntry(StringBuilder sb, ref AsakiLogEntry entry)
        {
            var time = new DateTime(entry.Timestamp, DateTimeKind.Utc).ToLocalTime();
            sb.Append('[').Append(time.ToString("HH:mm:ss.fff")).Append(']')
              .Append('[').Append(GetLevelString(entry.Level)).Append(']')
              .Append('[').Append(entry.ThreadId).Append("] ");
            sb.Append(entry.Message);

            // 根据配置决定是否写入调用位置信息
            bool shouldWriteCaller = entry.Level >= AsakiLogLevel.Warning || _writeCallerInfoForLowLevel;
    
            if (shouldWriteCaller && !string.IsNullOrEmpty(entry.CallerPath))
            {
                sb.Append(" @ ").Append(Path.GetFileName(entry.CallerPath)).Append(':').Append(entry.CallerLine);
            }

            if (entry.ExceptionData.HasValue)
            {
                sb.AppendLine();
                sb.Append("!!! EXCEPTION: ").Append(entry.ExceptionData.TypeName).Append(" !!!");
                sb.AppendLine();
                sb.Append("Message: ").Append(entry.ExceptionData.Message);
                if (!string.IsNullOrEmpty(entry.ExceptionData.InnerExceptionMsg))
                {
                    sb.AppendLine();
                    sb.Append("Inner: ").Append(entry.ExceptionData.InnerExceptionMsg);
                }
                if (!string.IsNullOrEmpty(entry.ExceptionData.StackTrace))
                {
                    sb.AppendLine();
                    sb.Append(entry.ExceptionData.StackTrace);
                }
            }
            else if (!string.IsNullOrEmpty(entry.StackTrace))
            {
                sb.AppendLine();
                sb.AppendLine("Stack Trace:");
                sb.Append(entry.StackTrace);
            }

            if (!string.IsNullOrEmpty(entry.PayloadJson))
            {
                sb.AppendLine();
                sb.Append("Data: ").Append(entry.PayloadJson);
            }

            sb.AppendLine();
        }
        
        private string GetLevelString(AsakiLogLevel level)
        {
            switch (level)
            {
                case AsakiLogLevel.Debug: return "DBG";
                case AsakiLogLevel.Info: return "INF";
                case AsakiLogLevel.Warning: return "WRN";
                case AsakiLogLevel.Error: return "ERR";
                case AsakiLogLevel.Fatal: return "FTL";
                default: return "LOG";
            }
        }

        private void SetupSession()
        {
            string root = Path.Combine(Application.persistentDataPath, LOG_ROOT_FOLDER);
            if (!Directory.Exists(root)) Directory.CreateDirectory(root);
            CleanupOldSessions(root);
            _sessionDir = Path.Combine(root, $"{SESSION_PREFIX}{DateTime.Now.ToString(DATE_FORMAT)}");
            Directory.CreateDirectory(_sessionDir);
            _fileIndex = 0;
            CreateNewLogFile();
        }

        private void CleanupOldSessions(string root)
        {
            try
            {
                var dirs = new DirectoryInfo(root).GetDirectories($"{SESSION_PREFIX}*")
                    .OrderByDescending(d => d.CreationTime).ToList();
                if (dirs.Count >= _maxHistorySessions)
                {
                    for (int i = _maxHistorySessions - 1; i < dirs.Count; i++) dirs[i].Delete(true);
                }
            }
            catch { }
        }

        private void CreateNewLogFile()
        {
            _fileIndex++;
            _currentLogPath = Path.Combine(_sessionDir, $"{LOG_FILE_PREFIX}{_fileIndex:00}.log");
            
            // 写入版本头
            try
            {
                File.WriteAllText(_currentLogPath, $"{LOG_VERSION_HEADER}\n", Encoding.UTF8);
            }
            catch { /* 忽略文件创建错误 */ }
        }

        #region Crash Interception
        private void RegisterCallbacks()
        {
            Application.logMessageReceivedThreaded += OnUnityLogMessage;
            AppDomain.CurrentDomain.UnhandledException += OnUncaughtException;
        }

        private void UnregisterCallbacks()
        {
            Application.logMessageReceivedThreaded -= OnUnityLogMessage;
            AppDomain.CurrentDomain.UnhandledException -= OnUncaughtException;
        }

        private void OnUnityLogMessage(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Log || type == LogType.Warning) return;
            AsakiLogLevel level = type == LogType.Exception ? AsakiLogLevel.Fatal : AsakiLogLevel.Error;
            Enqueue(level, condition, default, null, stackTrace, "UnityNative", 0, null);
            if (level == AsakiLogLevel.Fatal) FlushSync();
        }

        private void OnUncaughtException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            var snap = ExceptionSnapshot.Capture(ex);
            Enqueue(AsakiLogLevel.Fatal, "Uncaught Application Exception", snap, null, null, "AppDomain", 0, "CrashHandler");
            FlushSync();
        }
        #endregion
    }
}