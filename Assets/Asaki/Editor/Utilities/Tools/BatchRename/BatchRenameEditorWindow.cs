using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Asaki.Editor.Utilities.Tools.BatchRename
{
	/// <summary>
	/// 场景对象批量重命名工具（IMGUI优化版）
	/// 架构特点：零依赖、高性能、Undo安全、生产级健壮性
	/// 快捷键：Alt + F2
	/// </summary>
	public class BatchRenameEditorWindow : EditorWindow
	{
		// 序列化状态：窗口关闭后保留输入
		[SerializeField] private string _prefix = "";
		[SerializeField] private string _suffix = "";
		[SerializeField] private bool _showPreview = true;
		[SerializeField] private Vector2 _scrollPos;

		// 业务数据
		private RenameOperation[] _previewOps = new RenameOperation[0];
		private Dictionary<int, string> _conflicts = new Dictionary<int, string>();

		// 服务层（纯逻辑，可单元测试）
		private readonly BatchRenameService _service = new BatchRenameService();

		// GUIStyle缓存：避免每帧创建，消除GC
		private GUIStyle _conflictStyle;
		private GUIStyle _normalStyle;
		private GUIStyle _infoBoxStyle;
		private bool _stylesInitialized;

		// 常量配置：集中管理，便于维护
		private const string WINDOW_TITLE = "批量重命名";
		private const string PREVIEW_FOLDOUT_FORMAT = "预览 ({0} 对象)";
		private const string NO_SELECTION_TIP = "请先选中至少一个GameObject";
		private const string CONFLICT_TIP_FORMAT = "检测到 {0} 个命名冲突，请调整规则";
		private const string UNDO_GROUP_NAME_FORMAT = "批量重命名 {0} 个对象";

		[MenuItem("Asaki/Tools/Batch Rename &F2")]
		public static void ShowWindow()
		{
			BatchRenameEditorWindow window = GetWindow<BatchRenameEditorWindow>();
			window.titleContent = new GUIContent(WINDOW_TITLE);
			window.minSize = new Vector2(450, 300);
			window.Show();
		}

		private void OnEnable()
		{
			// 立即刷新，确保窗口打开时显示当前选择
			RefreshPreview();
		}

		private void OnGUI()
		{
			// 懒初始化GUIStyle：只在首次OnGUI时创建
			if (!_stylesInitialized)
			{
				InitializeStyles();
				_stylesInitialized = true;
			}

			// 主布局：垂直分组
			EditorGUILayout.BeginVertical(Styles.MainContainer);

			DrawInputSection();
			DrawPreviewSection();
			DrawActionButtons();

			EditorGUILayout.EndVertical();
		}

		#region 输入区域

		private void DrawInputSection()
		{
			EditorGUILayout.LabelField("命名规则", Styles.SectionTitle);

			EditorGUI.BeginChangeCheck();
			_prefix = EditorGUILayout.TextField("前缀", _prefix);
			_suffix = EditorGUILayout.TextField("后缀", _suffix);

			if (EditorGUI.EndChangeCheck())
			{
				RefreshPreview(); // 实时刷新，防抖由IMGUI自动处理
			}
		}

		#endregion

		#region 预览区域

		private void DrawPreviewSection()
		{
			EditorGUILayout.Space(4f);

			string previewHeader = string.Format(PREVIEW_FOLDOUT_FORMAT, _previewOps.Length);
			_showPreview = EditorGUILayout.Foldout(_showPreview, previewHeader, true);

			if (!_showPreview) return;

			if (_previewOps.Length == 0)
			{
				EditorGUILayout.HelpBox(NO_SELECTION_TIP, MessageType.Info);
				return;
			}

			// 滚动视图：支持大量对象
			using EditorGUILayout.ScrollViewScope scrollScope = new EditorGUILayout.ScrollViewScope(_scrollPos, Styles.PreviewScroll);
			_scrollPos = scrollScope.scrollPosition;

			// 使用Box容器提升视觉层次
			EditorGUILayout.BeginVertical(Styles.PreviewBox);

			for (int i = 0; i < _previewOps.Length; i++)
			{
				DrawPreviewItem(_previewOps[i], i);
				// 分隔线：提升可读性
				if (i < _previewOps.Length - 1)
					GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(1f));
			}

			EditorGUILayout.EndVertical();

			// 冲突提示：独立区域，红色警示
			DrawConflictWarning();
		}

		private void DrawPreviewItem(RenameOperation op, int index)
		{
			bool hasConflict = _conflicts.ContainsKey(op.InstanceId);
			GUIStyle style = hasConflict ? _conflictStyle : _normalStyle;

			EditorGUILayout.BeginHorizontal(Styles.PreviewItem);

			// 索引：灰色小字体
			EditorGUILayout.LabelField($"#{index + 1}", Styles.IndexLabel, GUILayout.Width(30f));

			// 图标：固定宽度
			EditorGUILayout.LabelField(EditorGUIUtility.IconContent("GameObject Icon"),
				GUILayout.Width(20f), GUILayout.Height(20f));

			// 原始名称：受限宽度，过长截断
			EditorGUILayout.LabelField(op.OriginalName, style, GUILayout.Width(150f));

			// 箭头：居中
			EditorGUILayout.LabelField("→", Styles.ArrowLabel, GUILayout.Width(20f));

			// 新名称：弹性宽度
			string newNameLabel = hasConflict ? $"{op.NewName} (冲突!)" : op.NewName;
			EditorGUILayout.LabelField(newNameLabel, style, GUILayout.ExpandWidth(true));

			EditorGUILayout.EndHorizontal();
		}

		private void DrawConflictWarning()
		{
			if (_conflicts.Count == 0) return;

			EditorGUILayout.Space(4f);
			string message = string.Format(CONFLICT_TIP_FORMAT, _conflicts.Count);
			EditorGUILayout.HelpBox(message, MessageType.Error);
		}

		#endregion

		#region 操作按钮

		private void DrawActionButtons()
		{
			EditorGUILayout.Space(8f);

			EditorGUILayout.BeginHorizontal();

			// 应用按钮：冲突时禁用
			bool canApply = _conflicts.Count == 0 && _previewOps.Length > 0;
			using (new EditorGUI.DisabledScope(!canApply))
			{
				if (GUILayout.Button("应用", Styles.ApplyButton, GUILayout.Height(28f)))
				{
					OnApply();
				}
			}

			if (GUILayout.Button("取消", Styles.CancelButton, GUILayout.Height(28f)))
			{
				Close();
			}

			EditorGUILayout.EndHorizontal();
		}

		#endregion

		#region 业务逻辑

		private void RefreshPreview()
		{
			var selected = Selection.GetFiltered<GameObject>(SelectionMode.TopLevel);
			_previewOps = _service.GeneratePreview(selected, _prefix, _suffix);
			_conflicts = _service.DetectConflicts(_previewOps);
		}

		private void OnApply()
		{
			if (_previewOps.Length == 0) return;

			string undoName = string.Format(UNDO_GROUP_NAME_FORMAT, _previewOps.Length);
			using UndoScope undoScope = new UndoScope(undoName);

			foreach (RenameOperation op in _previewOps)
			{
				if (!op.TryGetGameObject(out GameObject go)) continue;

				Undo.RegisterCompleteObjectUndo(go, "Rename GameObject");
				op.Apply();
			}

			Close();
		}

		private void OnSelectionChange()
		{
			RefreshPreview();
			Repaint(); // 强制重绘，更新预览
		}

		#endregion

		#region 样式初始化

		private void InitializeStyles()
		{
			_normalStyle = new GUIStyle(EditorStyles.label)
			{
				richText = false,
			};

			_conflictStyle = new GUIStyle(EditorStyles.label)
			{
				normal = { textColor = Color.red },
				fontStyle = FontStyle.Bold,
			};

			_infoBoxStyle = new GUIStyle(EditorStyles.helpBox)
			{
				margin = new RectOffset(4, 4, 4, 4),
			};
		}

		// 静态样式类：便于复用和主题切换
		private static class Styles
		{
			public static readonly GUIStyle MainContainer = new GUIStyle
			{
				padding = new RectOffset(8, 8, 8, 8),
			};

			public static readonly GUIStyle SectionTitle = new GUIStyle(EditorStyles.boldLabel)
			{
				fontSize = 14,
				margin = new RectOffset(0, 0, 8, 8),
			};

			public static readonly GUIStyle PreviewScroll = new GUIStyle
			{
				padding = new RectOffset(2, 2, 2, 2),
			};

			public static readonly GUIStyle PreviewBox = new GUIStyle(EditorStyles.helpBox)
			{
				padding = new RectOffset(4, 4, 4, 4),
			};

			public static readonly GUIStyle PreviewItem = new GUIStyle
			{
				margin = new RectOffset(0, 0, 2, 2),
			};

			public static readonly GUIStyle IndexLabel = new GUIStyle(EditorStyles.miniLabel)
			{
				alignment = TextAnchor.MiddleRight,
				normal = { textColor = Color.gray },
			};

			public static readonly GUIStyle ArrowLabel = new GUIStyle(EditorStyles.label)
			{
				alignment = TextAnchor.MiddleCenter,
				normal = { textColor = new Color(0.5f, 0.5f, 0.5f, 1f) },
			};

			public static readonly GUIStyle ApplyButton = new GUIStyle(GUI.skin.button)
			{
				fontSize = 13,
				fontStyle = FontStyle.Bold,
			};

			public static readonly GUIStyle CancelButton = new GUIStyle(GUI.skin.button)
			{
				fontSize = 13,
			};
		}

		#endregion
	}
}
