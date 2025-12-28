using Asaki.Core.Context;
using Asaki.Core.Logging;
using Asaki.Editor.Utilities.Extensions;
using Asaki.Unity.Services.Logging;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Asaki.Editor.Debugging
{
	public class AsakiLogDashboard : EditorWindow
	{
		private enum DashboardMode { Live, Local }

		// UI Elements
		private ListView _listView;
		private ScrollView _stackScrollView;
		private VisualElement _toolbar;
		private Label _statusLabel;

		// Data
		private AsakiLogAggregator _liveAggregator;
		private List<AsakiLogModel> _localLogs = new List<AsakiLogModel>();
		private DashboardMode _mode = DashboardMode.Local;

		// Logic
		private double _lastRefreshTime;
		private const double REFRESH_INTERVAL = 0.1f;

		[MenuItem("Asaki/Log Dashboard V3", false, 0)]
		public static void ShowWindow() => GetWindow<AsakiLogDashboard>("Asaki Logs").Show();

		private void OnEnable()
		{
			EditorApplication.update += OnEditorUpdate;
			// 监听 PlayMode 变化，实现无缝衔接
			EditorApplication.playModeStateChanged += OnPlayModeChanged;
		}

		private void OnDisable()
		{
			EditorApplication.update -= OnEditorUpdate;
			EditorApplication.playModeStateChanged -= OnPlayModeChanged;
		}

		// --- 核心逻辑：模式切换与数据保持 ---

		private void OnPlayModeChanged(PlayModeStateChange state)
		{
			// [修复] 核心防护：如果 UI 还没构建好 (CreateGUI 未执行)，直接跳过。
			// 这通常发生在 Editor 刚启动或布局加载时。
			if (_statusLabel == null || _listView == null) return;

			if (state == PlayModeStateChange.ExitingPlayMode)
			{
				// 游戏即将停止：赶紧把内存里的日志"快照"下来
				if (_liveAggregator != null)
				{
					// [优化] 安全地获取快照，避免 null 访问
					var snapshot = _liveAggregator.GetSnapshot();
					if (snapshot != null && snapshot.Count > 0)
					{
						SnapshotLiveLogs();
					}
				}
				_mode = DashboardMode.Local;
				_statusLabel.text = "Status: Local Snapshot (Play Stopped)";
				_statusLabel.style.color = Color.yellow;
			}
			else if (state == PlayModeStateChange.EnteredPlayMode)
			{
				// 进入游戏：切回 Live 模式
				_mode = DashboardMode.Live;
				
				// [修复] 确保 _localLogs 不为 null
				if (_localLogs == null) _localLogs = new List<AsakiLogModel>();
				_localLogs.Clear();
				
				_liveAggregator = null; // 等待重连
				_statusLabel.text = "Status: Connecting...";
				_statusLabel.style.color = Color.gray;
			}
		}

		private void SnapshotLiveLogs()
		{
			if (_liveAggregator == null || _listView == null || _statusLabel == null) return;

			// 1. 获取线程安全的快照
			List<AsakiLogModel> tempSnapshot = _liveAggregator.GetSnapshot();

			// 2. 序列化克隆 (Deep Copy)
			_localLogs.Clear();

			// 预分配容量优化性能
			if (tempSnapshot.Count > 0)
			{
				var wrapper = new LogListWrapper { List = tempSnapshot };
				string json = JsonUtility.ToJson(wrapper);
				var deserialized = JsonUtility.FromJson<LogListWrapper>(json);
				if (deserialized != null && deserialized.List != null)
				{
					_localLogs = deserialized.List;
				}
			}

			_listView.itemsSource = _localLogs;
			_listView.Rebuild();

			_statusLabel.text = "Status: Local Snapshot (Safe)";
			_statusLabel.style.color = Color.yellow;
		}

		// 用于 JsonUtility 序列化 List 的辅助类
		[System.Serializable]
		private class LogListWrapper
		{
			public List<AsakiLogModel> List;
		}

		private void OnEditorUpdate()
		{
			if (_mode == DashboardMode.Local) return; // 本地模式不需要刷新

			// Live 模式连接逻辑
			if (_liveAggregator == null)
			{
				var s = AsakiContext.Get<IAsakiLoggingService>() as AsakiLoggingService;
				if (s != null)
				{
					_liveAggregator = s.Aggregator;
					_listView.itemsSource = _liveAggregator.GetSnapshot();
					_listView.Rebuild();
					_statusLabel.text = "Status: ● Live Connected";
					_statusLabel.style.color = new Color(0.4f, 1f, 0.4f);
				}
			}

			// 节流刷新
			if (EditorApplication.timeSinceStartup - _lastRefreshTime > REFRESH_INTERVAL)
			{
				_lastRefreshTime = EditorApplication.timeSinceStartup;
				if (_listView != null && _liveAggregator != null && _liveAggregator.GetSnapshot().Count > 0)
				{
					_listView.RefreshItems();
				}
			}
		}

		// --- UI 构建 ---

		public void CreateGUI()
		{
			var root = rootVisualElement;

			// 1. Toolbar
			DrawToolbar(root);

			// 2. Split View
			var split = new TwoPaneSplitView(0, 350, TwoPaneSplitViewOrientation.Horizontal);
			root.Add(split);

			// 3. Left: Log List
			_listView = new ListView();
			_listView.fixedItemHeight = 24;
			_listView.makeItem = () => new Label { style = { paddingLeft = 6, unityTextAlign = TextAnchor.MiddleLeft, fontSize = 12 } };
			_listView.bindItem = BindLogItem;
			_listView.selectionChanged += OnLogSelected;
			split.Add(_listView);

			// 4. Right: Details
			var rightPane = new VisualElement { style = { flexGrow = 1, backgroundColor = new Color(0.16f, 0.16f, 0.16f) } };
			_stackScrollView = new ScrollView { style = { paddingLeft = 15, paddingTop = 15, paddingRight = 10 } };
			rightPane.Add(_stackScrollView);
			split.Add(rightPane);

			// Initial State
			if (Application.isPlaying) _mode = DashboardMode.Live;
			else _mode = DashboardMode.Local;
		}

		private void DrawToolbar(VisualElement root)
		{
			_toolbar = new VisualElement
			{
				style = { flexDirection = FlexDirection.Row, height = 30, backgroundColor = new Color(0.22f, 0.22f, 0.22f), alignItems = Align.Center, paddingLeft = 5, borderBottomWidth = 1, borderBottomColor = new Color(0.1f, 0.1f, 0.1f) }
			};

			// Clear Button
			_toolbar.Add(new Button(() =>
			{
				_liveAggregator?.Clear();
				_localLogs.Clear();
				_listView.RefreshItems();
				_stackScrollView.Clear();
			}) { text = "Clear", style = { width = 50 } });

			// Load History Button
			_toolbar.Add(new Button(OpenLogFile) { text = "📂 Load History", style = { width = 100 } });

			// Open Folder Button
			_toolbar.Add(new Button(() =>
			{
				string path = Path.Combine(Application.persistentDataPath, "Logs");
				EditorUtility.RevealInFinder(path);
			}) { text = "Show Folder", style = { width = 90 } });

			// Status Label
			_statusLabel = new Label("Status: Idle") { style = { marginLeft = 10, unityFontStyleAndWeight = FontStyle.Bold } };
			_toolbar.Add(_statusLabel);

			root.Add(_toolbar);
		}

		private void BindLogItem(VisualElement e, int i)
		{
			var list = _mode == DashboardMode.Live ? _liveAggregator?.GetSnapshot() : _localLogs;
			if (list == null || i >= list.Count) return;

			var log = list[i];
			var label = e as Label;

			string countBadge = log.Count > 1 ? $" [{log.Count}]" : "";
			label.text = $"[{log.DisplayTime}]{countBadge} {log.Message}";

			// Modern Colors
			switch (log.Level)
			{
				case AsakiLogLevel.Error:   label.style.color = new Color(1f, 0.5f, 0.5f); break;
				case AsakiLogLevel.Warning: label.style.color = new Color(1f, 0.9f, 0.4f); break;
				case AsakiLogLevel.Fatal:   label.style.color = new Color(1f, 0.2f, 0.2f); break;
				default:                    label.style.color = new Color(0.9f, 0.9f, 0.9f); break;
			}
		}

		// --- 现代化堆栈渲染 (Waterfall Visualization) ---

		private void OnLogSelected(IEnumerable<object> selection)
		{
			var log = selection.FirstOrDefault() as AsakiLogModel;
			if (log == null) return;

			_stackScrollView.Clear();

			// 1. Message Header (Copyable)
			var msgContainer = new VisualElement { style = { marginBottom = 15, borderLeftWidth = 3, borderLeftColor = GetColorByLevel(log.Level), paddingLeft = 10 } };
			var msgLabel = new Label(log.Message) { style = { fontSize = 14, whiteSpace = WhiteSpace.Normal, unityFontStyleAndWeight = FontStyle.Bold, color = new Color(0.95f, 0.95f, 0.95f) } };
			// 允许选中复制
			msgLabel.RegisterCallback<MouseDownEvent>(evt =>
			{
				if (evt.button == 1) // Right click
				{
					var menu = new GenericMenu();
					menu.AddItem(new GUIContent("Copy Message"), false, () => EditorGUIUtility.systemCopyBuffer = log.Message);
					menu.AddItem(new GUIContent("Copy Full Log (JSON)"), false, () => EditorGUIUtility.systemCopyBuffer = JsonUtility.ToJson(log, true));
					menu.ShowAsContext();
				}
			});
			msgContainer.Add(msgLabel);
			_stackScrollView.Add(msgContainer);

			// 2. Payload
			if (!string.IsNullOrEmpty(log.PayloadJson))
			{
				var pBox = new VisualElement { style = { backgroundColor = new Color(0.2f, 0.2f, 0.25f), marginBottom = 15 } };
				pBox.style.SetPadding(8);
				pBox.style.SetBorderRadius(4);
				pBox.Add(new Label("PAYLOAD") { style = { fontSize = 10, color = new Color(0.5f, 0.5f, 0.6f), marginBottom = 4 } });
				pBox.Add(new Label(log.PayloadJson) { style = { color = new Color(0.6f, 0.8f, 1f) } });
				_stackScrollView.Add(pBox);
			}

			// 3. Stack Trace (Visual Timeline)
			_stackScrollView.Add(new Label("STACK TRACE") { style = { fontSize = 10, color = Color.gray, marginBottom = 8, marginLeft = 2 } });

			if (log.StackFrames != null)
			{
				for (int i = 0; i < log.StackFrames.Count; i++)
				{
					var frame = log.StackFrames[i];
					RenderStackRow(frame, i == log.StackFrames.Count - 1);
				}
			}
		}

		private void RenderStackRow(StackFrameModel frame, bool isLast)
		{
			var row = new VisualElement { style = { flexDirection = FlexDirection.Row, minHeight = 24 } };

			// A. Timeline Graphic
			var timeline = new VisualElement { style = { width = 20, alignItems = Align.Center } };
			// Vertical Line
			if (!isLast)
			{
				timeline.Add(new VisualElement { style = { position = Position.Absolute, top = 10, bottom = -14, width = 1, backgroundColor = new Color(0.3f, 0.3f, 0.3f) } });
			}
			// Dot
			var dot = new VisualElement { style = { width = 8, height = 8, marginTop = 8 } };
			dot.style.SetBorderRadius(4);
			dot.style.backgroundColor = frame.IsUserCode ? new Color(0.3f, 0.6f, 1f) : new Color(0.4f, 0.4f, 0.4f);
			timeline.Add(dot);
			row.Add(timeline);

			// B. Content Button
			var btn = new Button(() =>
			{
				if (!string.IsNullOrEmpty(frame.FilePath))
				{
					string sysPath = frame.FilePath.Replace('/', System.IO.Path.DirectorySeparatorChar);
					UnityEditorInternal
						.InternalEditorUtility
						.OpenFileAtLineExternal(sysPath, frame.LineNumber);
				}
			})
			{
				style =
				{
					flexGrow = 1, flexDirection = FlexDirection.Column, justifyContent = Justify.Center,
					backgroundColor = Color.clear, borderTopWidth = 0, borderBottomWidth = 0, borderLeftWidth = 0, borderRightWidth = 0,
					paddingTop = 4, paddingBottom = 4, marginLeft = 5
				}
			};

			// Method Name
			var methodLabel = new Label($"{frame.DeclaringType}.{frame.MethodName}")
			{
				style =
				{
					fontSize = 12,
					color = frame.IsUserCode ? new Color(0.9f, 0.9f, 0.9f) : new Color(0.6f, 0.6f, 0.6f),
					unityFontStyleAndWeight = frame.IsUserCode ? FontStyle.Bold : FontStyle.Normal
				}
			};
			btn.Add(methodLabel);

			// File Path (Small)
			if (frame.IsUserCode)
			{
				var fileLabel = new Label($"{Path.GetFileName(frame.FilePath)}:{frame.LineNumber}") { style = { fontSize = 10, color = new Color(0.4f, 0.4f, 0.4f) } };
				btn.Add(fileLabel);
			}

			row.Add(btn);
			_stackScrollView.Add(row);
		}

		// --- 辅助功能 ---

		private void OpenLogFile()
		{
			string dir = Path.Combine(Application.persistentDataPath, "Logs");
			string path = EditorUtility.OpenFilePanel("Load Asaki Log", dir, "asakilog");
			if (!string.IsNullOrEmpty(path))
			{
				_localLogs = AsakiLogFileReader.LoadFile(path);
				_mode = DashboardMode.Local;
				_listView.itemsSource = _localLogs;
				_listView.Rebuild();
				_statusLabel.text = $"Status: Loaded {Path.GetFileName(path)}";
				_statusLabel.style.color = new Color(0.4f, 0.8f, 1f);
			}
		}

		private Color GetColorByLevel(AsakiLogLevel level)
		{
			switch (level)
			{
				case AsakiLogLevel.Error:   return new Color(1f, 0.4f, 0.4f);
				case AsakiLogLevel.Warning: return Color.yellow;
				default:                    return Color.white;
			}
		}
	}
}
