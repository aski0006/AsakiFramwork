using Asaki.Core.Configs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Asaki.Core.Logging;
using UnityEngine;

namespace Asaki.Unity.Services.Logging
{
    public class AsakiLogFileWriter : IDisposable
    {
        private readonly AsakiLogAggregator _aggregator;
        private readonly StringBuilder _sb = new StringBuilder(4096);
        
        // 线程控制
        private readonly Thread _workerThread;
        private readonly AutoResetEvent _signal = new AutoResetEvent(false);
        private volatile bool _isRunning;
        private volatile bool _stopRequested = false; // 防止多次 Dispose
        
        // 文件状态
        private string _logDir;
        private string _currentFilePath;
        private FileStream _fileStream;
        private StreamWriter _streamWriter;
        private long _currentWrittenBytes;

        // 配置 (默认值)
        private int _maxFileSize = 2 * 1024 * 1024; // 2MB
        private int _maxHistoryFiles = 10;
        private string _filePrefix = "Log";

        public AsakiLogFileWriter(AsakiLogAggregator aggregator)
        {
            _aggregator = aggregator;
            
            // 1. 准备目录
            _logDir = Path.Combine(Application.persistentDataPath, "Logs");
            if (!Directory.Exists(_logDir)) Directory.CreateDirectory(_logDir);

            // 2. 初始创建一个日志文件
            OpenNewFile();

            // 3. 启动线程
            _isRunning = true;
            _workerThread = new Thread(WriteLoop) 
            { 
                IsBackground = true, 
                Priority = System.Threading.ThreadPriority.BelowNormal 
            };
            _workerThread.Start();
        }

        /// <summary>
        /// [新增] 动态应用配置
        /// </summary>
        public void ApplyConfig(AsakiLogConfig config)
        {
            if (config == null) return;
            
            _maxFileSize = config.MaxFileSizeKB * 1024;
            _maxHistoryFiles = config.MaxHistoryFiles;
            _filePrefix = string.IsNullOrEmpty(config.FilePrefix) ? "Log" : config.FilePrefix;

            // 异步触发一次历史清理
            // 不要在 Writer 线程做 IO 删除操作，避免阻塞写入
            ThreadPool.QueueUserWorkItem(_ => CleanupHistory());
        }

        private void OpenNewFile()
        {
            try
            {
                // 关闭旧的
                _streamWriter?.Dispose();
                _fileStream?.Dispose();

                // 创建新的
                string fileName = $"{_filePrefix}_{DateTime.Now:yyyyMMdd_HHmmss}.asakilog";
                _currentFilePath = Path.Combine(_logDir, fileName);
                
                // 使用 FileShare.Read 允许外部查看
                _fileStream = new FileStream(_currentFilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
                _streamWriter = new StreamWriter(_fileStream, Encoding.UTF8);
                _streamWriter.AutoFlush = false; // 手动 Flush 提高性能

                // 写入头信息
                _streamWriter.Write($"#VERSION:2.3\n#SESSION:{DateTime.Now}\n");
                _streamWriter.Flush();
                _currentWrittenBytes = _fileStream.Length;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AsakiWriter] Failed to create log file: {ex.Message}");
            }
        }

        private void WriteLoop()
        {
            try
            {
                // 看门狗机制：如果发生非致命异常，尝试恢复
                while (_isRunning)
                {
                    _signal.WaitOne(500); // 0.5秒刷新，或者被 Dispose 唤醒
                    if (!_isRunning) break;
                    FlushBuffer();
                }
                
                // 退出前最后一次 Flush
                FlushBuffer();
            }
            catch (ThreadAbortException)
            {
                // 线程被强杀
            }
            catch (Exception e)
            {
                // 甚至可以把自己的错误写到 stderr 或者独立的 fallback 文件
                Debug.LogError($"[AsakiWriter] Crash: {e.Message}");
            }
            finally
            {
                // 退出时清理资源
                _streamWriter?.Dispose();
                _fileStream?.Dispose();
            }
        }

        private void FlushBuffer()
        {
            // 1. 获取数据 (双缓冲交换)
            List<LogWriteCommand> buffer = _aggregator.SwapIOBuffer();
            if (buffer == null || buffer.Count == 0) return;

            try
            {
                _sb.Clear();
                
                foreach (var cmd in buffer)
                {
                    // 序列化逻辑
                    if (cmd.Type == LogWriteCommand.CmdType.Def)
                    {
                        // 直接使用预处理好的数据
                        _sb.Append("$DEF|").Append(cmd.Id).Append('|')
                           .Append(cmd.LevelInt).Append('|')
                           .Append(cmd.Timestamp).Append('|')
                           .Append(Sanitize(cmd.Message)).Append('|')
                           .Append(Sanitize(cmd.Payload)).Append('|')
                           .Append(cmd.Path).Append(':').Append(cmd.Line).Append('|')
                           .Append(Sanitize(cmd.StackJson)).AppendLine(); // 注意：也需要对 StackJson 进行清理
                    }
                    else // Inc
                    {
                        _sb.Append("$INC|").Append(cmd.Id).Append('|').Append(cmd.IncAmount).AppendLine();
                    }
                    
                    // 用完即回收
                    LogCommandPool.Return(cmd);
                }

                if (_sb.Length > 0 && _streamWriter != null)
                {
                    // 2. 写入文件
                    _streamWriter.Write(_sb.ToString());
                    _streamWriter.Flush(); // 确保刷入磁盘
                    
                    // 3. 更新长度并检查轮转
                    _currentWrittenBytes = _fileStream.Length; // 获取真实文件大小
                    if (_currentWrittenBytes > _maxFileSize)
                    {
                        OpenNewFile(); // 触发轮转
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AsakiWriter] Write error: {ex.Message}");
            }
            finally
            {
                buffer.Clear();
            }
        }

        /// <summary>
        /// 清理旧日志 (保留最近的 N 个)
        /// </summary>
        private void CleanupHistory()
        {
            try
            {
                var dirInfo = new DirectoryInfo(_logDir);
                var files = dirInfo.GetFiles("*.asakilog")
                                   .OrderByDescending(f => f.CreationTime) // 最新的在前
                                   .ToList();

                if (files.Count > _maxHistoryFiles)
                {
                    // 删除多余的 (从列表尾部开始删)
                    for (int i = _maxHistoryFiles; i < files.Count; i++)
                    {
                        try { files[i].Delete(); } catch { }
                    }
                }
            }
            catch 
            { 
                /* 忽略清理错误 */ 
            }
        }

        private string Sanitize(string input) 
        {
            return string.IsNullOrEmpty(input) ? "" : input.Replace("|", "¦").Replace("\n", " ");
        }

        public void Dispose()
        {
            if (_stopRequested) return;
            _stopRequested = true;
            
            _isRunning = false;
            _signal.Set(); // 唤醒线程
            
            // 安全等待线程结束 (最多等 1 秒)
            if (_workerThread.IsAlive)
            {
                _workerThread.Join(1000);
            }
        }
    }
}