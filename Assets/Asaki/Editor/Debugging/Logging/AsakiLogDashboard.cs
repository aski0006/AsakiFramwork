using Asaki.Core.Logging;
using Asaki.Editor.Utilities.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Asaki.Editor.Debugging.Logging
{
    public class AsakiLogDashboard : EditorWindow
    {
        // =========================================================
        // 1. 状态与逻辑
        // =========================================================
        
        private readonly AsakiLogFileWatcher _logWatcher = new AsakiLogFileWatcher();
        private List<AsakiEditorLogEntry> _allLogs;
        private List<AsakiEditorLogEntry> _filteredLogs;
        private AsakiEditorLogEntry _selectedEntry;
        
        // 模式控制
        private bool _isHistoryMode = false;
        private string _viewingFile = ""; // 初始化空字符串

        // 智能刷新控制
        private double _lastPollTime;
        private const double POLL_INTERVAL = 0.5f;

        // 正则 (甘特图解析) - 优化版
        private static readonly Regex _stackRegex = new Regex(
            @"at\s+(.+?)\s*(?:\[.*?\])?\s+(?:in|\(at)\s+(.+?):(\d+)\)?\s*$",
            RegexOptions.Compiled | RegexOptions.Multiline);

        // 新增：项目文件缓存，提高查找性能
        private static Dictionary<string, string> _filePathCache = new Dictionary<string, string>();
        private static double _lastCacheRefreshTime;

        // UI State
        [SerializeField] private string _searchText = "";
        [SerializeField] private bool _showInfo = true;
        [SerializeField] private bool _showWarn = true;
        [SerializeField] private bool _showErr = true;
        [SerializeField] private bool _autoScroll = true;

        // UI Elements
        private ListView _listView;
        private ScrollView _stackTraceView;
        private Label _payloadLabel;
        private Label _detailHeaderLabel;
        private Label _statusLabel; // 底部状态栏
        private string _lastWatchedPath;
        
        [MenuItem("Asaki/Debugger/Log Dashboard (V5)", false, 0)]
        public static void ShowWindow()
        {
            var wnd = GetWindow<AsakiLogDashboard>();
            wnd.titleContent = new GUIContent("Asaki Logs");
            wnd.minSize = new Vector2(900, 600);
            wnd.Show();
        }

        private void OnEnable()
        {
            _allLogs = new List<AsakiEditorLogEntry>(2000);
            _filteredLogs = new List<AsakiEditorLogEntry>(2000);
            
            // 启动时默认为 Live 模式
            SwitchToLiveMode();
            
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            // 如果处于历史查看模式，停止轮询
            if (_isHistoryMode) return;

            if (EditorApplication.timeSinceStartup - _lastPollTime > POLL_INTERVAL)
            {
                PollLiveLogs();
                _lastPollTime = EditorApplication.timeSinceStartup;
            }
        }

        private void PollLiveLogs()
        {
            // 先执行 Poll，让 Watcher 内部更新路径
            var newLogs = _logWatcher.Poll();
            string currentPath = _logWatcher.GetCurrentPath();

            // 检测 Session 切换 (路径变了)
            if (!string.IsNullOrEmpty(currentPath) && currentPath != _lastWatchedPath)
            {
                // 如果路径变了，且之前已经有路径（说明是运行时切换，而非首次启动），则清空
                if (!string.IsNullOrEmpty(_lastWatchedPath))
                {
                    _allLogs.Clear();
                    _filteredLogs.Clear();
                    _selectedEntry = null;
                    // 清空详情面板
                    ClearDetail();
                }
                
                _lastWatchedPath = currentPath;
                
                // 更新状态栏
                UpdateStatusUI();
            }
            // 如果路径为空（还没生成日志），更新状态
            else if (string.IsNullOrEmpty(currentPath))
            {
                UpdateStatusUI();
            }

            // 2. 追加新日志
            if (newLogs != null && newLogs.Count > 0)
            {
                _allLogs.AddRange(newLogs);
                RefreshFilterList(newLogs);
            }
        }

        private void SwitchToLiveMode()
        {
            _isHistoryMode = false;
            _viewingFile = ""; // 清空历史文件名
            _allLogs.Clear();
            _filteredLogs.Clear();
            _lastWatchedPath = null; // 重置路径追踪
            
            _logWatcher.ForceRefresh();
            PollLiveLogs(); 
            
            if (_listView != null) _listView.RefreshItems();
            ClearDetail();
            
            UpdateStatusUI(); // 立即刷新状态栏
        }

        private void LoadHistoryFile()
        {
            string path = EditorUtility.OpenFilePanel("Load Log File", Application.persistentDataPath, "log");
            if (string.IsNullOrEmpty(path)) return;

            _isHistoryMode = true;
            _viewingFile = Path.GetFileName(path); // 记录文件名
            _allLogs.Clear();
            _filteredLogs.Clear();
            ClearDetail();

            // 调用 Watcher 的加载功能
            var historyLogs = _logWatcher.LoadHistoryFile(path);
            _allLogs.AddRange(historyLogs);
            
            // 全量刷新过滤
            RefreshFilterList(_allLogs);
            
            // 更新状态UI
            UpdateStatusUI();
            _autoScroll = false; // 查看历史时通常不需要自动滚动
        }

        // 统一的状态栏更新方法
        private void UpdateStatusUI()
        {
            if (_statusLabel == null) return;

            if (_isHistoryMode)
            {
                // 历史模式：灰色背景，静态文字
                _statusLabel.text = $"HISTORY MODE: {_viewingFile}";
                _statusLabel.style.color = new Color(0.7f, 0.7f, 0.7f); // Gray
                _statusLabel.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
            }
            else
            {
                // Live 模式：根据连接状态变色
                string path = _logWatcher.GetCurrentPath();
                bool isConnected = !string.IsNullOrEmpty(path);
                
                if (isConnected)
                {
                    // 绿色高亮，模拟 LED 呼吸灯效果
                    _statusLabel.text = $"● LIVE MONITORING: {Path.GetFileName(path)}";
                    _statusLabel.style.color = new Color(0.4f, 1f, 0.4f); // Bright Green
                    _statusLabel.style.backgroundColor = new Color(0.1f, 0.3f, 0.1f); // Dark Green Background
                }
                else
                {
                    // 等待连接：橙色
                    _statusLabel.text = "○ LIVE: Waiting for logs...";
                    _statusLabel.style.color = new Color(1f, 0.8f, 0.4f); // Orange
                    _statusLabel.style.backgroundColor = new Color(0.3f, 0.2f, 0.1f);
                }
            }
        }

        private void RefreshFilterList(IEnumerable<AsakiEditorLogEntry> newEntries)
        {
            if (_isHistoryMode) _filteredLogs.Clear();

            bool anyAdded = false;
            foreach (var log in newEntries)
            {
                if (IsMatch(log))
                {
                    _filteredLogs.Add(log);
                    anyAdded = true;
                }
            }

            if (!_isHistoryMode && !anyAdded) return;

            // 检查 ListView
            if (_listView != null)
            {
                _listView.RefreshItems();
                if (_autoScroll && _filteredLogs.Count > 0)
                    _listView.ScrollToItem(_filteredLogs.Count - 1);
            }
        }

        private bool IsMatch(AsakiEditorLogEntry entry)
        {
            if (entry.Level <= AsakiLogLevel.Info && !_showInfo) return false;
            if (entry.Level == AsakiLogLevel.Warning && !_showWarn) return false;
            if (entry.Level >= AsakiLogLevel.Error && !_showErr) return false;

            if (!string.IsNullOrEmpty(_searchText))
            {
                string search = _searchText.ToLower();
                return (entry.Message != null && entry.Message.ToLower().Contains(search)) ||
                       (entry.CallerMember != null && entry.CallerMember.ToLower().Contains(search));
            }
            return true;
        }

        // =========================================================
        // 2. UI 构建
        // =========================================================

        public void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f);

            // --- Toolbar ---
            var toolbar = new VisualElement { style = { flexDirection = FlexDirection.Row, height = 32, backgroundColor = new Color(0.25f, 0.25f, 0.25f), alignItems = Align.Center, paddingLeft = 5, borderBottomWidth = 1, borderBottomColor = Color.black } };

            // 模式控制区
            toolbar.Add(CreateButton("Live", SwitchToLiveMode, 50));
            toolbar.Add(CreateButton("Load File...", LoadHistoryFile, 80));
            toolbar.Add(new VisualElement { style = { width = 10 } }); // Divider

            // Clear 按钮 - 添加确认对话框
            toolbar.Add(CreateButton("Clear", () => { 
                // 显示确认对话框
                if (EditorUtility.DisplayDialog("Clear Logs", 
                    "This will clear all log entries from memory" + (_isHistoryMode ? " (history mode)" : " and delete the current log file (live mode)") + 
                    ".\nAre you sure?", "Yes, Clear", "Cancel"))
                {
                    ClearAllLogs();
                }
            }, 50));
            
            toolbar.Add(CreateButton("Open Dir", () => EditorUtility.RevealInFinder(Application.persistentDataPath), 70));
            toolbar.Add(CreateButton("Copy Select", CopySelectedLog, 80));
            toolbar.Add(CreateButton("Export All", ExportAllLogs, 70));

            toolbar.Add(new VisualElement { style = { flexGrow = 1 } }); 
            
            var autoScroll = new Toggle("Auto") { value = _autoScroll, style = { marginRight = 10 } };
            autoScroll.RegisterValueChangedCallback(e => _autoScroll = e.newValue);
            toolbar.Add(autoScroll);

            var searchField = new TextField { style = { width = 200, marginRight = 10 } };
            searchField.RegisterValueChangedCallback(e => { _searchText = e.newValue; RebuildFilter(); });
            toolbar.Add(searchField);

            toolbar.Add(CreateFilterToggle("I", _showInfo, v => _showInfo = v, new Color(0.4f, 0.8f, 1f)));
            toolbar.Add(CreateFilterToggle("W", _showWarn, v => _showWarn = v, Color.yellow));
            toolbar.Add(CreateFilterToggle("E", _showErr, v => _showErr = v, new Color(1f, 0.4f, 0.4f)));

            root.Add(toolbar);

            // --- Status Bar (视觉增强版) ---
            _statusLabel = new Label("Ready") { style = { 
                backgroundColor = new Color(0.12f, 0.12f, 0.12f), 
                color = Color.gray, 
                fontSize = 11, 
                paddingLeft = 8, 
                paddingTop = 4, 
                paddingBottom = 4,
                borderTopWidth = 1,
                borderTopColor = new Color(0.1f, 0.1f, 0.1f)
            }};
            root.Add(_statusLabel);

            // 初始更新状态栏
            EditorApplication.delayCall += () => UpdateStatusUI();

            // --- Main Split ---
            var split = new TwoPaneSplitView(0, 400, TwoPaneSplitViewOrientation.Vertical);
            root.Add(split);

            // --- List View ---
            _listView = new ListView();
            _listView.style.flexGrow = 1;
            _listView.fixedItemHeight = 24;
            _listView.makeItem = () => new Label { style = { paddingLeft = 5, fontSize = 12, unityTextAlign = TextAnchor.MiddleLeft } };
            _listView.bindItem = (e, i) =>
            {
                if (i >= _filteredLogs.Count) return;
                var log = _filteredLogs[i];
                var label = e as Label;
                label.text = $"[{log.DisplayTime}] {log.Message}";
                label.style.color = GetLogColor(log.Level);
            };
            _listView.itemsSource = _filteredLogs;
            _listView.selectionChanged += OnLogSelected;
            split.Add(_listView);

            // --- Detail Panel ---
            var bottomPanel = new TwoPaneSplitView(1, 450, TwoPaneSplitViewOrientation.Horizontal);

            // Stack Trace
            var stackContainer = new VisualElement { style = { flexGrow = 1 } };
            var stackHeader = new VisualElement { style = { flexDirection = FlexDirection.Row, backgroundColor = new Color(0.22f, 0.22f, 0.22f), height = 24, alignItems = Align.Center, paddingLeft = 5 } };
            _detailHeaderLabel = new Label("Call Stack") { style = { unityFontStyleAndWeight = FontStyle.Bold } };
            stackHeader.Add(_detailHeaderLabel);
            stackContainer.Add(stackHeader);

            _stackTraceView = new ScrollView(ScrollViewMode.Vertical);
            _stackTraceView.contentContainer.style.paddingTop = 5;
            _stackTraceView.contentContainer.style.paddingLeft = 5;
            stackContainer.Add(_stackTraceView);
            bottomPanel.Add(stackContainer);

            // Payload
            var payloadContainer = new VisualElement { style = { flexGrow = 1, borderLeftWidth = 1, borderLeftColor = new Color(0.1f, 0.1f, 0.1f) } };
            var payloadHeader = new VisualElement { style = { flexDirection = FlexDirection.Row, backgroundColor = new Color(0.22f, 0.22f, 0.22f), height = 24, alignItems = Align.Center, paddingLeft = 5, paddingRight = 5 } };
            payloadHeader.Add(new Label("Payload (JSON)") { style = { unityFontStyleAndWeight = FontStyle.Bold, flexGrow = 1 } });
            
            var copyPayloadBtn = new Button(() => CopyPayload()) { text = "Copy", style = { height = 18, fontSize = 10, width = 50 } };
            payloadHeader.Add(copyPayloadBtn);
            payloadContainer.Add(payloadHeader);

            var pScroll = new ScrollView();
            _payloadLabel = new Label {
                style = { whiteSpace = WhiteSpace.Normal, paddingLeft = 5, paddingTop = 5, color = new Color(0.7f, 1f, 0.7f) }
            };
            _payloadLabel.RegisterCallback<MouseDownEvent>(e => { if (e.clickCount == 2) CopyPayload(); });
            pScroll.Add(_payloadLabel);
            payloadContainer.Add(pScroll);
            bottomPanel.Add(payloadContainer);

            split.Add(bottomPanel);
        }

        // =========================================================
        // 3. 渲染逻辑 - 修改：支持虚拟堆栈
        // =========================================================

        private struct StackFrameInfo
        {
            public string Method;
            public string File;
            public int Line;
            public bool IsValidFile;
            public FrameType Type;
        }

        private enum FrameType { User, Framework, Engine, Exception, Message }

        private void OnLogSelected(IEnumerable<object> selection)
{
    var obj = selection.FirstOrDefault();
    if (obj == null) { ClearDetail(); return; }

    var entry = (AsakiEditorLogEntry)obj;
    _selectedEntry = entry;

    // 1. 显示 Payload
    if (_payloadLabel != null) 
        _payloadLabel.text = FormatPayload(entry.PayloadJson);

    // 2. 准备渲染列表
    if (_stackTraceView != null) 
        _stackTraceView.Clear();
        
    List<StackFrameInfo> frames = new List<StackFrameInfo>();
    
    // ========================================================================
    // 【核心修复】 第一阶段：强制渲染 "Source Location" (虚拟帧)
    // 只要日志头部解析出了 CallerPath，无论后面有没有堆栈，都必须显示这个按钮
    // ========================================================================
    bool hasVirtualFrame = false;
    if (!string.IsNullOrEmpty(entry.CallerPath))
    {
        hasVirtualFrame = true;
        
        // 智能查找文件路径
        string fullPath = FindFileInProject(entry.CallerPath);
        bool isValid = !string.IsNullOrEmpty(fullPath) && File.Exists(fullPath);

        frames.Add(new StackFrameInfo
        {
            // 按钮名字：如果有具体的 Member 就显示 Member，否则显示 "Source Location"
            Method = string.IsNullOrEmpty(entry.CallerMember) 
                     ? "➜ Source Location" 
                     : $"➜ {entry.CallerMember} (Call Site)",
            
            File = fullPath,
            Line = entry.CallerLine,
            IsValidFile = isValid,
            Type = FrameType.User // 标记为用户代码（通常显示为蓝色或高亮色）
        });
    }

    // ========================================================================
    // 【核心修复】 第二阶段：追加堆栈信息 (Additive Mode)
    // 不再使用 else if，而是追加到列表后面
    // ========================================================================
    
    // 确定标题颜色和内容
    if (entry.IsException)
    {
        _detailHeaderLabel.text = $"Exception: {entry.ExceptionType}";
        _detailHeaderLabel.style.color = new Color(1f, 0.4f, 0.4f); // 红色警告
        
        // 如果前面有虚拟帧，加一个分割线，视觉上区分开
        if (hasVirtualFrame) 
            frames.Add(new StackFrameInfo { Method = "--- Exception Trace ---", Type = FrameType.Message });
            
        // 添加异常消息
        frames.Add(new StackFrameInfo { Method = entry.ExceptionMessage, Type = FrameType.Message });
        
        // 解析异常堆栈
        if (!string.IsNullOrEmpty(entry.StackTrace))
            frames.AddRange(ParseStackString(entry.StackTrace, isException: true));
    }
    else if (!string.IsNullOrEmpty(entry.StackTrace))
    {
        // 普通堆栈模式 (WRN / ERR / 开启了 StackTrace 的 INFO)
        if (!hasVirtualFrame)
        {
            _detailHeaderLabel.text = "Call Stack";
            _detailHeaderLabel.style.color = Color.white;
        }
        else
        {
            // 如果已经有了虚拟帧，这里加个分割线
            frames.Add(new StackFrameInfo { Method = "--- Stack Trace ---", Type = FrameType.Message });
        }

        frames.AddRange(ParseStackString(entry.StackTrace, isException: false));
    }
    else if (!hasVirtualFrame)
    {
        // 既没虚拟帧也没堆栈
        _detailHeaderLabel.text = "No Location Info";
        _detailHeaderLabel.style.color = Color.gray;
    }
    else
    {
        // 只有虚拟帧
        _detailHeaderLabel.text = "Source Location";
        _detailHeaderLabel.style.color = new Color(0.4f, 1f, 0.4f); // 绿色高亮
    }

    // 3. 渲染所有帧
    for (int i = 0; i < frames.Count; i++) RenderFrame(frames[i], i);
}

        private void RenderFrame(StackFrameInfo frame, int index)
        {
            VisualElement ele;
            if (frame.Type == FrameType.Message || !frame.IsValidFile)
            {
                var box = new VisualElement();
                box.style.borderTopWidth = 1; box.style.borderBottomWidth = 1; box.style.borderLeftWidth = 1; box.style.borderRightWidth = 1;
                box.style.SetBorderColor(new Color(0.1f, 0.1f, 0.1f));
                ele = box;
            }
            else
            {
                var btn = new Button(() => {
                    UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(frame.File, frame.Line);
                });
                ele = btn;
            }

            ele.style.flexDirection = FlexDirection.Row;
            ele.style.height = 22;
            ele.style.marginBottom = 1;
            ele.style.alignSelf = Align.FlexStart;
            ele.style.paddingLeft = 6;
            ele.style.paddingRight = 8;
            ele.style.borderTopLeftRadius = 0; ele.style.borderBottomLeftRadius = 0;
            ele.style.borderTopRightRadius = 4; ele.style.borderBottomRightRadius = 4;
            ele.style.marginLeft = Mathf.Min(index * 12, 200);

            Color bgColor = new Color(0.3f, 0.3f, 0.3f);
            if (frame.Type == FrameType.Message) bgColor = new Color(0.5f, 0.1f, 0.1f);
            else if (frame.Type == FrameType.Exception) bgColor = new Color(0.7f, 0.2f, 0.2f);
            else if (frame.Type == FrameType.User) bgColor = new Color(0.2f, 0.4f, 0.6f);
            else if (frame.Type == FrameType.Framework) bgColor = new Color(0.7f, 0.5f, 0.2f);

            if (frame.Type != FrameType.Message && !frame.IsValidFile) 
                bgColor = new Color(0.25f, 0.25f, 0.25f);

            ele.style.backgroundColor = bgColor;

            var lblMethod = new Label(frame.Method) { style = { color = Color.white, unityFontStyleAndWeight = FontStyle.Bold } };
            ele.Add(lblMethod);

            if (frame.IsValidFile && frame.Line > 0)
            {
                var lblLine = new Label($":{frame.Line}") { style = { color = new Color(1, 1, 1, 0.6f), marginLeft = 4, fontSize = 10, paddingTop = 2 } };
                ele.Add(lblLine);
            }

            _stackTraceView.Add(ele);
        }

        // =========================================================
        // 4. 辅助逻辑 - 新增：文件查找器
        // =========================================================

        // 【新增】简单的文件查找器 - 带缓存优化
        private string FindFileInProject(string fileNameOrPath)
        {
            if (string.IsNullOrEmpty(fileNameOrPath)) return "";
            if (fileNameOrPath.IndexOf('<') >= 0 || fileNameOrPath.IndexOf('>') >= 0 || 
                fileNameOrPath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            {
                return fileNameOrPath;
            }
            try
            {
                // 2. 正常路径检查
                if (File.Exists(fileNameOrPath)) return fileNameOrPath;

                // 3. 检查缓存
                if (EditorApplication.timeSinceStartup - _lastCacheRefreshTime > 30.0)
                {
                    _filePathCache.Clear();
                    _lastCacheRefreshTime = EditorApplication.timeSinceStartup;
                }

                if (_filePathCache.TryGetValue(fileNameOrPath, out string cached))
                    return cached;

                // 4. 智能查找
                string fileName = Path.GetFileName(fileNameOrPath); // 这里现在安全了
                string resultPath = fileNameOrPath;

                // 尝试通过 AssetDatabase 查找
                string[] guids = AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension(fileName));
                foreach (var guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (path.EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
                    {
                        resultPath = path;
                        break;
                    }
                }

                _filePathCache[fileNameOrPath] = resultPath;
                return resultPath;
            }
            catch (Exception)
            {
                return fileNameOrPath;
            }
        }

        private List<StackFrameInfo> ParseStackString(string rawStack, bool isException)
        {
            var list = new List<StackFrameInfo>();
            var lines = rawStack.Split('\n');

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var match = _stackRegex.Match(line);
                if (match.Success)
                {
                    string method = match.Groups[1].Value.Trim();
                    string file = match.Groups[2].Value.Trim();
                    string lineStr = match.Groups[3].Value;
                    int lineNum = int.TryParse(lineStr, out var l) ? l : 0;
                    
                    // 优化：查找文件的完整路径
                    string fullPath = !string.IsNullOrEmpty(file) ? FindFileInProject(file) : file;
                    bool fileExists = !string.IsNullOrEmpty(fullPath) && File.Exists(fullPath);
                    
                    list.Add(new StackFrameInfo
                    {
                        Method = method,
                        File = fullPath,
                        Line = lineNum,
                        IsValidFile = fileExists,
                        Type = DetermineFrameType(fullPath, isException)
                    });
                }
                else
                {
                    list.Add(new StackFrameInfo { Method = line.Trim(), Type = FrameType.Message });
                }
            }
            return list;
        }

        private FrameType DetermineFrameType(string path, bool isException)
        {
            if (isException) return FrameType.Exception;
            if (string.IsNullOrEmpty(path)) return FrameType.Engine;
            if (path.StartsWith("<") || path.Contains(">")) return FrameType.Engine;
            string normPath = path.Replace("\\", "/");
            if (normPath.Contains("Asaki/")) return FrameType.Framework;
            if (normPath.Contains("Assets/")) return FrameType.User;
            return FrameType.Engine;
        }

        private string FormatPayload(string json)
        {
            if (string.IsNullOrEmpty(json)) return "No Payload";
            return json.Replace(",", ",\n").Replace("{", "{\n").Replace("}", "\n}");
        }

        private void CopyPayload()
        {
            if (_selectedEntry != null && !string.IsNullOrEmpty(_selectedEntry.PayloadJson))
            {
                EditorGUIUtility.systemCopyBuffer = _selectedEntry.PayloadJson;
                ShowNotification(new GUIContent("Payload Copied!"));
            }
        }

        private void CopySelectedLog()
        {
            if (_selectedEntry == null) return;
            var e = _selectedEntry;
            var sb = new StringBuilder();
            sb.AppendLine($"[{e.Level}] {e.DisplayTime}");
            sb.AppendLine($"Msg: {e.Message}");
            if (!string.IsNullOrEmpty(e.StackTrace)) sb.AppendLine(e.StackTrace);
            if(!string.IsNullOrEmpty(e.PayloadJson)) sb.AppendLine($"Payload: {e.PayloadJson}");
            EditorGUIUtility.systemCopyBuffer = sb.ToString();
            ShowNotification(new GUIContent("Log Copied!"));
        }

        private void ExportAllLogs()
        {
            if (_filteredLogs == null || _filteredLogs.Count == 0) return;
            var sb = new StringBuilder();
            foreach(var e in _filteredLogs)
                sb.AppendLine($"[{e.Level}] {e.Message} | {e.PayloadJson}");
            EditorGUIUtility.systemCopyBuffer = sb.ToString();
            ShowNotification(new GUIContent($"{_filteredLogs.Count} Logs Exported!"));
        }

        // 清除所有日志的完整逻辑
        private void ClearAllLogs()
        {
            // 1. 清除内存中的日志
            _allLogs.Clear();
            _filteredLogs.Clear();
            ClearDetail();
            
            // 2. 如果是 Live 模式，删除当前日志文件
            if (!_isHistoryMode)
            {
                try
                {
                    string currentPath = _logWatcher.GetCurrentPath();
                    if (!string.IsNullOrEmpty(currentPath) && File.Exists(currentPath))
                    {
                        File.Delete(currentPath);
                        Debug.Log($"Log file deleted: {currentPath}");
                    }
                    
                    // 重置 Watcher 状态
                    _logWatcher.ForceRefresh();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to delete log file: {ex.Message}");
                }
            }
            
            // 3. 刷新 UI
            if (_listView != null) _listView.RefreshItems();
            UpdateStatusUI();
            
            ShowNotification(new GUIContent("Logs cleared!"));
        }

        private Button CreateButton(string text, Action onClick, float width)
        {
            return new Button(onClick) { text = text, style = { width = width, height = 20, backgroundColor = new Color(0.35f, 0.35f, 0.35f) } };
        }

        private Toggle CreateFilterToggle(string txt, bool val, Action<bool> setter, Color c)
        {
            var t = new Toggle(txt) { value = val, style = { marginLeft = 10 } };
            var label = t.Q<Label>();
            if (label != null) label.style.color = c;
            t.RegisterValueChangedCallback(e => { setter(e.newValue); RebuildFilter(); });
            return t;
        }

        private void ClearDetail()
        {
            if (_stackTraceView != null) _stackTraceView.Clear();
            if (_payloadLabel != null) _payloadLabel.text = "";
            if (_detailHeaderLabel != null) _detailHeaderLabel.text = "Stack Trace";
            _selectedEntry = null;
        }

        private void RebuildFilter()
        {
            if (_allLogs == null) return;
            _filteredLogs.Clear();
            foreach (var log in _allLogs)
                if (IsMatch(log)) _filteredLogs.Add(log);
            _listView?.RefreshItems();
        }

        private Color GetLogColor(AsakiLogLevel level)
        {
            switch (level)
            {
                case AsakiLogLevel.Info: return Color.white;
                case AsakiLogLevel.Warning: return Color.yellow;
                case AsakiLogLevel.Error: return new Color(1f, 0.5f, 0.5f);
                case AsakiLogLevel.Fatal: return Color.red;
                default: return Color.gray;
            }
        }
    }
}