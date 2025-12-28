using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Asaki.Core.Logging;
using UnityEngine;

namespace Asaki.Unity.Services.Logging
{
    public class AsakiLogFileWriter : IDisposable
    {
        private readonly AsakiLogAggregator _aggregator; // 仅用于 Swap
        private readonly string _logPath;
        private readonly Thread _workerThread;
        private readonly AutoResetEvent _signal = new AutoResetEvent(false);
        private volatile bool _isRunning;
        private readonly StringBuilder _sb = new StringBuilder(4096);
        
        // 引用计数/状态防止 Dispose 竞态
        private volatile bool _stopRequested = false;

        public AsakiLogFileWriter(AsakiLogAggregator aggregator)
        {
            _aggregator = aggregator;
            
            // 路径创建代码
            string dir = Path.Combine(Application.persistentDataPath, "Logs");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            
            // 每次启动生成新文件
            _logPath = Path.Combine(dir, $"Log_{DateTime.Now:yyyyMMdd_HHmmss}.asakilog");
            
            // 写入文件头，版本更新为2.3
            File.WriteAllText(_logPath, $"#VERSION:2.3\n#SESSION:{DateTime.Now}\n", Encoding.UTF8);

            _isRunning = true;
            _workerThread = new Thread(WriteLoop) 
            { 
                IsBackground = true, 
                Priority = System.Threading.ThreadPriority.BelowNormal 
            };
            _workerThread.Start();
        }

        private void WriteLoop()
        {
            try 
            {
                // 看门狗机制：如果发生非致命异常，尝试恢复
                while (_isRunning) 
                {
                    _signal.WaitOne(500); // 0.5s 刷新一次，或者被 Dispose 唤醒
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
        }

        private void FlushBuffer()
        {
            // [优化] 1. 极速交换 (仅持有锁 0.001ms)
            List<LogWriteCommand> buffer = _aggregator.SwapIOBuffer();
            
            if (buffer == null || buffer.Count == 0) return;

            try
            {
                // [优化] 2. 慢速 IO (完全无锁)
                _sb.Clear();
                
                foreach (var cmd in buffer)
                {
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
                    
                    // [优化] 3. 用完即回收
                    LogCommandPool.Return(cmd);
                }

                if (_sb.Length > 0)
                {
                    // 使用 FileShare.Read 允许外部工具查看日志
                    using (var fs = new FileStream(_logPath, FileMode.Append, FileAccess.Write, FileShare.Read))
                    using (var sw = new StreamWriter(fs, Encoding.UTF8))
                    {
                        sw.Write(_sb.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AsakiWriter] Write failed: {ex.Message}");
            }
            finally
            {
                // 4. 清空 List，以便下次 Aggregator 复用
                buffer.Clear();
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