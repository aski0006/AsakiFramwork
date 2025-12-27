using Asaki.Core.Logging;
using Asaki.Unity.Services.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Asaki.Editor.Debugging.Logging
{
    public class AsakiLogFileWatcher
    {
        // 状态
        private long _lastFilePosition = 0;
        private string _currentFilePath;
        private string _currentFileVersion = "1.0"; // 默认版本
        
        // 缓存
        private readonly StringBuilder _entryBuffer = new StringBuilder();
        private AsakiEditorLogEntry _pendingEntry;

        // [优化] 正则只负责提取头部固定格式，内容部分(Group 4)后续手动解析
        // 格式: [Time][Level][ThreadId] Message...
        private static readonly Regex _headerRegex = new Regex(
            @"^\[(.*?)\]\[(.*?)\]\[(\d+)\]\s*(.*)$", 
            RegexOptions.Compiled);

        /// <summary>
        /// [模式1] 实时轮询
        /// </summary>
        public List<AsakiEditorLogEntry> Poll()
        {
            var newLogs = new List<AsakiEditorLogEntry>();
            string latestPath = FindLatestLogPath();
            
            if (latestPath != _currentFilePath)
            {
                _currentFilePath = latestPath;
                _lastFilePosition = 0;
                ResetParser();
            }

            if (string.IsNullOrEmpty(_currentFilePath) || !File.Exists(_currentFilePath)) 
                return newLogs;

            try
            {
                var fileInfo = new FileInfo(_currentFilePath);
                long fileLen = fileInfo.Length;

                if (fileLen <= _lastFilePosition) 
                {
                    if (fileLen < _lastFilePosition) _lastFilePosition = 0; 
                    else return newLogs; 
                }

                using (var fs = new FileStream(_currentFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    fs.Seek(_lastFilePosition, SeekOrigin.Begin);
                    using (var sr = new StreamReader(fs, Encoding.UTF8))
                    {
                        string line;
                        while ((line = sr.ReadLine()) != null)
                        {
                            ParseLine(line, newLogs);
                        }
                    }
                    
                    if (_pendingEntry != null)
                    {
                        FinalizeEntry(_pendingEntry, _entryBuffer);
                        newLogs.Add(_pendingEntry);
                        _pendingEntry = null;
                        _entryBuffer.Clear();
                    }

                    _lastFilePosition = fileLen; 
                }
            }
            catch { /* IO 冲突忽略 */ }

            return newLogs;
        }

        /// <summary>
        /// [模式2] 历史加载
        /// </summary>
        public List<AsakiEditorLogEntry> LoadHistoryFile(string path)
        {
            var logs = new List<AsakiEditorLogEntry>();
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return logs;

            ResetParser();
            _currentFilePath = path; // 记录当前路径以便查找同目录文件

            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sr = new StreamReader(fs, Encoding.UTF8))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        ParseLine(line, logs);
                    }
                }
                
                if (_pendingEntry != null)
                {
                    FinalizeEntry(_pendingEntry, _entryBuffer);
                    logs.Add(_pendingEntry);
                    _pendingEntry = null;
                    _entryBuffer.Clear();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load history: {ex.Message}");
            }

            return logs;
        }

        public string GetCurrentPath() => _currentFilePath;

        public void ForceRefresh()
        {
            _lastFilePosition = 0;
            ResetParser();
        }

        private void ResetParser()
        {
            _entryBuffer.Clear();
            _pendingEntry = null;
            _currentFileVersion = "1.0";
        }

        // =========================================================
        // 解析核心 (Core Parser) - 已优化
        // =========================================================

        private void ParseLine(string line, List<AsakiEditorLogEntry> results)
        {
            // 0. 版本头检测
            if (line.StartsWith("#VERSION:"))
            {
                _currentFileVersion = line.Substring(9).Trim();
                return;
            }

            var match = _headerRegex.Match(line);
            if (match.Success)
            {
                // 遇到新 Header -> 提交上一条
                if (_pendingEntry != null)
                {
                    FinalizeEntry(_pendingEntry, _entryBuffer);
                    results.Add(_pendingEntry);
                    _entryBuffer.Clear();
                }

                // 开始新条目
                _pendingEntry = new AsakiEditorLogEntry();
                if (DateTime.TryParse(match.Groups[1].Value, out DateTime time))
                    _pendingEntry.Timestamp = time.Ticks;
                
                _pendingEntry.Level = ParseLevel(match.Groups[2].Value);
                _pendingEntry.ThreadId = int.Parse(match.Groups[3].Value);

                // [关键修改] 解析消息体中的 " @ File:Line"
                string rawMsg = match.Groups[4].Value;
                ParseMessageAndCaller(rawMsg, _pendingEntry);
            }
            else if (_pendingEntry != null)
            {
                // 累积详情行
                _entryBuffer.AppendLine(line);
            }
        }

        /// <summary>
        /// 从消息文本中剥离调用位置信息
        /// </summary>
        private void ParseMessageAndCaller(string rawMsg, AsakiEditorLogEntry entry)
        {
            // 查找最后一个 " @ " 分隔符 (从右向左查，防止消息内容里也有 @)
            int atIndex = rawMsg.LastIndexOf(" @ ");
            
            if (atIndex > 0)
            {
                // 截取后缀部分，例如 "AsakiLogTest.cs:62"
                string tail = rawMsg.Substring(atIndex + 3);
                
                // 查找最后一个冒号 (文件名和行号的分隔符)
                int colonIndex = tail.LastIndexOf(':');
                
                // 简单的合法性检查：冒号存在，且冒号后面全是数字
                if (colonIndex > 0 && int.TryParse(tail.Substring(colonIndex + 1), out int lineNum))
                {
                    // 提取成功
                    entry.Message = rawMsg.Substring(0, atIndex).TrimEnd(); // 剥离后缀的消息
                    entry.CallerPath = tail.Substring(0, colonIndex);
                    entry.CallerLine = lineNum;
                    return;
                }
            }

            // 提取失败或无后缀，整个都是消息
            entry.Message = rawMsg;
        }

        private void FinalizeEntry(AsakiEditorLogEntry entry, StringBuilder buffer)
        {
            if (buffer.Length == 0) return;
            string details = buffer.ToString();
            
            // 1. Extract Payload
            int dataIndex = details.IndexOf("Data: ");
            if (dataIndex >= 0)
            {
                entry.PayloadJson = details.Substring(dataIndex + 6).Trim();
                details = details.Substring(0, dataIndex);
            }

            // 2. Extract Exception
            if (details.Contains("!!! EXCEPTION"))
            {
                entry.IsException = true;
                // ... (原有异常解析逻辑保持不变)
                int typeStart = details.IndexOf("EXCEPTION: ") + 11;
                int typeEnd = details.IndexOf(" !!!", typeStart);
                if (typeStart > 0 && typeEnd > typeStart)
                    entry.ExceptionType = details.Substring(typeStart, typeEnd - typeStart);
                
                int msgIndex = details.IndexOf("Message: ");
                if (msgIndex >= 0)
                {
                    int nextLine = details.IndexOf('\n', msgIndex);
                    if (nextLine < 0) nextLine = details.Length;
                    entry.ExceptionMessage = details.Substring(msgIndex + 9, nextLine - (msgIndex + 9)).Trim();
                    
                    if (nextLine < details.Length)
                        entry.StackTrace = details.Substring(nextLine).Trim();
                }
                else
                {
                    entry.StackTrace = details;
                }
            }
            // 3. Extract Caller (如果有多行堆栈)
            else
            {
                string stack = details.Trim();
                if (!string.IsNullOrEmpty(stack))
                {
                    entry.StackTrace = stack;
                }
            }
        }

        private string FindLatestLogPath()
        {
            string root = Path.Combine(Application.persistentDataPath, AsakiLoggingService.LOG_ROOT_FOLDER);
            if (!Directory.Exists(root)) return null;

            var latestSession = new DirectoryInfo(root).GetDirectories($"{AsakiLoggingService.SESSION_PREFIX}*")
                .OrderByDescending(d => d.CreationTime)
                .FirstOrDefault();

            if (latestSession == null) return null;

            var latestLog = latestSession.GetFiles($"{AsakiLoggingService.LOG_FILE_PREFIX}*.log")
                .OrderByDescending(f => f.Name)
                .FirstOrDefault();

            return latestLog?.FullName;
        }

        private AsakiLogLevel ParseLevel(string lvl)
        {
            switch (lvl) {
                case "INF": return AsakiLogLevel.Info;
                case "WRN": return AsakiLogLevel.Warning;
                case "ERR": return AsakiLogLevel.Error;
                case "FTL": return AsakiLogLevel.Fatal;
                case "DBG": return AsakiLogLevel.Debug;
                default: return AsakiLogLevel.Info;
            }
        }
    }
}