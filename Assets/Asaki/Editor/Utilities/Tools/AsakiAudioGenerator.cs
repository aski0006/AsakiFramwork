using Asaki.Unity.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Asaki.Editor.Utilities.Tools
{
	public class AsakiAudioDashboard : EditorWindow
	{
		// === 路径配置 ===
		private const string CONFIG_PATH = "Assets/Resources/Asaki/Configuration/AsakiAudioConfig.asset";
		private const string CODE_GEN_PATH = "Assets/Asaki/Generated/AudioID.cs";

		private AsakiAudioConfig _config;
		private Vector2 _scrollPos;
		private string _searchFilter = "";

		// === 播放器状态 ===
		private AudioSource _previewSource;
		private AudioItem _currentPlayingItem; // 当前正在操作的 Item
		private bool _isPaused;                // 当前是否处于暂停状态

		[MenuItem("Asaki/Audio/Audio Dashboard")]
		public static void ShowWindow()
		{
			AsakiAudioDashboard wnd = GetWindow<AsakiAudioDashboard>("Audio Dashboard");
			wnd.minSize = new Vector2(850, 600); // 稍微加宽一点以容纳 Slider
			wnd.Show();
		}

		private void OnEnable()
		{
			LoadConfig();
			InitAudioSource();
		}

		private void OnDisable()
		{
			StopPreview();
			if (_previewSource != null)
				DestroyImmediate(_previewSource.gameObject);
		}

		private void InitAudioSource()
		{
			if (_previewSource == null)
			{
				GameObject go = EditorUtility.CreateGameObjectWithHideFlags("AsakiAudioPreview", HideFlags.HideAndDontSave, typeof(AudioSource));
				_previewSource = go.GetComponent<AudioSource>();
			}
		}

		private void OnGUI()
		{
			DrawHeader();

			if (_config == null)
			{
				DrawEmptyState();
				return;
			}

			DrawToolbar();
			DrawDropArea();
			DrawList();
			DrawFooter();
		}

		// ... (Header 和 EmptyState 保持不变，省略以节省篇幅，逻辑同上个版本) ...

		private void DrawHeader()
		{
			EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
			GUILayout.Label("Asaki Audio Dashboard V5.1", EditorStyles.boldLabel);
			GUILayout.FlexibleSpace();
			if (GUILayout.Button("Force Refresh", EditorStyles.toolbarButton)) LoadConfig();
			EditorGUILayout.EndHorizontal();
		}

		private void DrawEmptyState()
		{
			EditorGUILayout.HelpBox($"Config not found at: {CONFIG_PATH}", MessageType.Error);
			if (GUILayout.Button("Create Config Asset")) CreateConfigAsset();
		}

		private void DrawToolbar()
		{
			EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

			// 搜索
			GUILayout.Label("Search:", EditorStyles.miniLabel, GUILayout.Width(45));
			_searchFilter = EditorGUILayout.TextField(_searchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(200));
			if (GUILayout.Button("X", EditorStyles.toolbarButton, GUILayout.Width(20))) _searchFilter = "";

			GUILayout.FlexibleSpace();

			// 排序
			if (GUILayout.Button("Sort by Name", EditorStyles.toolbarButton))
			{
				Undo.RecordObject(_config, "Sort Audio Items");
				_config.Items = _config.Items.OrderBy(x => x.Key).ToList();
				EditorUtility.SetDirty(_config);
			}

			// 停止所有 (紧急按钮)
			if (GUILayout.Button("■ Stop All", EditorStyles.toolbarButton))
			{
				StopPreview();
			}

			EditorGUILayout.EndHorizontal();
		}

		private void DrawDropArea()
		{
			Event evt = Event.current;
			Rect dropArea = GUILayoutUtility.GetRect(0.0f, 40.0f, GUILayout.ExpandWidth(true));
			GUI.Box(dropArea, "Drag AudioClips Here (Batch Add)", EditorStyles.helpBox);

			if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
			{
				if (!dropArea.Contains(evt.mousePosition)) return;

				DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

				if (evt.type == EventType.DragPerform)
				{
					DragAndDrop.AcceptDrag();
					Undo.RecordObject(_config, "Add Audio Clips");
					foreach (Object draggedObj in DragAndDrop.objectReferences)
					{
						if (draggedObj is AudioClip clip) RegisterOrUpdateClip(clip);
					}
					EditorUtility.SetDirty(_config);
				}
				Event.current.Use();
			}
		}

		private void DrawList()
		{
			if (_config.Items == null) return;

			_scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

			for (int i = 0; i < _config.Items.Count; i++)
			{
				AudioItem item = _config.Items[i];
				if (!string.IsNullOrEmpty(_searchFilter) && !item.Key.ToLower().Contains(_searchFilter.ToLower())) continue;

				DrawAudioItem(item, i);
			}

			EditorGUILayout.EndScrollView();
		}

		// =========================================================
		// 核心绘制逻辑 (优化版)
		// =========================================================
		private void DrawAudioItem(AudioItem item, int index)
		{
			EditorGUILayout.BeginVertical(EditorStyles.helpBox);

			// === Top Row: Transport | Key | Clip | Group | Delete ===
			EditorGUILayout.BeginHorizontal();

			// [优化点 1] 播放控制区
			DrawTransportControls(item);

			// Key Name
			Color defaultColor = GUI.color;
			if (string.IsNullOrEmpty(item.Key)) GUI.color = Color.red;

			EditorGUI.BeginChangeCheck();
			string newKey = EditorGUILayout.TextField(item.Key, GUILayout.Width(180));
			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(_config, "Rename Key");
				item.Key = newKey;
			}
			GUI.color = defaultColor;

			// Clip Reference
			EditorGUI.BeginChangeCheck();
			AudioClip newClip = (AudioClip)EditorGUILayout.ObjectField(item.Clip, typeof(AudioClip), false);
			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(_config, "Change Clip");
				item.Clip = newClip;
				if (newClip != null) item.AssetPath = GetLoadPath(newClip);
			}

			// Group
			EditorGUI.BeginChangeCheck();
			AsakiAudioGroup newGroup = (AsakiAudioGroup)EditorGUILayout.EnumPopup(item.Group, GUILayout.Width(60));
			if (EditorGUI.EndChangeCheck())
			{
				Undo.RecordObject(_config, "Change Group");
				item.Group = newGroup;
			}

			// [优化点 3] 时长显示
			string durationStr = item.Clip ? FormatDuration(item.Clip.length) : "--:--";
			GUILayout.Label(durationStr, EditorStyles.miniLabel, GUILayout.Width(35));

			// Expand
			item._editorExpanded = EditorGUILayout.Foldout(item._editorExpanded, "Edit", true);

			// Delete
			if (GUILayout.Button("X", GUILayout.Width(20)))
			{
				StopPreview(); // 删除前停止播放防止报错
				Undo.RecordObject(_config, "Remove Item");
				_config.Items.RemoveAt(index);
				EditorUtility.SetDirty(_config);
				EditorGUILayout.EndHorizontal();
				EditorGUILayout.EndVertical();
				return;
			}

			EditorGUILayout.EndHorizontal();

			// === Detail Row: Sliders & Flags ===
			if (item._editorExpanded)
			{
				DrawDetailRow(item);
			}

			EditorGUILayout.EndVertical();
		}

		private void DrawTransportControls(AudioItem item)
		{
			// 逻辑：
			// 如果这个 Item 是当前正在播放的：
			//    - 如果 Source 在播放 -> 显示 [暂停] [停止]
			//    - 如果 Source 暂停中 -> 显示 [恢复] [停止]
			// 如果不是当前播放的 -> 显示 [播放]

			bool isCurrent = _currentPlayingItem == item;
			bool isSourcePlaying = _previewSource != null && _previewSource.isPlaying;

			if (isCurrent)
			{
				if (isSourcePlaying)
				{
					// 正在播放 -> 显示暂停
					if (GUILayout.Button(EditorGUIUtility.IconContent("PauseButton"), GUILayout.Width(25), GUILayout.Height(20)))
					{
						PausePreview();
					}
				}
				else if (_isPaused)
				{
					// 暂停中 -> 显示恢复
					if (GUILayout.Button(EditorGUIUtility.IconContent("PlayButton"), GUILayout.Width(25), GUILayout.Height(20)))
					{
						ResumePreview();
					}
				}
				else
				{
					// 异常状态 (停止了但标记还在)，重置为播放
					if (GUILayout.Button(EditorGUIUtility.IconContent("PlayButton"), GUILayout.Width(25), GUILayout.Height(20)))
					{
						PlayPreview(item);
					}
				}

				// 停止按钮 (总是显示)
				if (GUILayout.Button(EditorGUIUtility.IconContent("PreMatQuad"), GUILayout.Width(25), GUILayout.Height(20)))
				{
					StopPreview();
				}
			}
			else
			{
				// 未播放状态 -> 只有播放按钮
				if (GUILayout.Button(EditorGUIUtility.IconContent("PlayButton"), GUILayout.Width(25), GUILayout.Height(20)))
				{
					PlayPreview(item);
				}
				// 占位符保持对齐
				GUILayout.Space(29);
			}
		}

		private void DrawDetailRow(AudioItem item)
		{
			EditorGUI.indentLevel++;
			EditorGUILayout.BeginHorizontal();

			// [优化点 2] 使用 Slider

			// Volume Slider
			GUILayout.Label("Vol", GUILayout.Width(25));
			float newVol = EditorGUILayout.Slider(item.Volume, 0f, 1f, GUILayout.Width(120));
			if (newVol != item.Volume)
			{
				Undo.RecordObject(_config, "Change Volume");
				item.Volume = newVol;
				// 实时更新正在播放的音量
				if (_currentPlayingItem == item && _previewSource != null) _previewSource.volume = newVol;
			}

			GUILayout.Space(10);

			// Pitch Slider
			GUILayout.Label("Pitch", GUILayout.Width(35));
			float newPitch = EditorGUILayout.Slider(item.Pitch, 0.1f, 3f, GUILayout.Width(120));
			if (newPitch != item.Pitch)
			{
				Undo.RecordObject(_config, "Change Pitch");
				item.Pitch = newPitch;
				// 实时更新正在播放的音高
				if (_currentPlayingItem == item && _previewSource != null) _previewSource.pitch = newPitch;
			}

			GUILayout.Space(10);

			// Flags
			bool newLoop = EditorGUILayout.ToggleLeft("Loop", item.Loop, GUILayout.Width(50));
			if (newLoop != item.Loop)
			{
				Undo.RecordObject(_config, "Change Loop");
				item.Loop = newLoop;
			}

			bool newRnd = EditorGUILayout.ToggleLeft("Rnd Pitch", item.RandomPitch, GUILayout.Width(80));
			if (newRnd != item.RandomPitch)
			{
				Undo.RecordObject(_config, "Change RndPitch");
				item.RandomPitch = newRnd;
			}

			EditorGUILayout.EndHorizontal();

			// 显示路径 (灰色)
			EditorGUILayout.BeginHorizontal();
			GUI.enabled = false;
			EditorGUILayout.LabelField("Asset Path:", GUILayout.Width(70));
			EditorGUILayout.TextField(item.AssetPath);
			GUI.enabled = true;
			EditorGUILayout.EndHorizontal();

			EditorGUI.indentLevel--;
		}

		private void DrawFooter()
		{
			GUILayout.Space(10);
			GUI.backgroundColor = new Color(0.2f, 0.7f, 0.3f);
			if (GUILayout.Button("Sync & Generate Code", GUILayout.Height(40)))
			{
				SyncAndGenerate();
			}
			GUI.backgroundColor = Color.white;
		}

		// =========================================================
		// 逻辑处理
		// =========================================================

		private void LoadConfig()
		{
			_config = AssetDatabase.LoadAssetAtPath<AsakiAudioConfig>(CONFIG_PATH);
		}

		private void CreateConfigAsset()
		{
			string dir = Path.GetDirectoryName(CONFIG_PATH);
			if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

			_config = CreateInstance<AsakiAudioConfig>();
			AssetDatabase.CreateAsset(_config, CONFIG_PATH);
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
		}

		private void RegisterOrUpdateClip(AudioClip clip)
		{
			if (_config.Items.Any(x => x.Clip == clip)) return;
			string key = SanitizeName(clip.name);
			AudioItem existing = _config.Items.FirstOrDefault(x => x.Key == key);

			if (existing != null)
			{
				existing.Clip = clip;
				existing.AssetPath = GetLoadPath(clip);
				return;
			}

			AudioItem newItem = new AudioItem
			{
				Clip = clip,
				Key = key,
				Volume = 1.0f,
				Pitch = 1.0f,
				Group = AsakiAudioGroup.SFX,
				AssetPath = GetLoadPath(clip),
			};

			string lowerName = newItem.Key.ToLower();
			if (lowerName.Contains("bgm") || lowerName.Contains("music"))
			{
				newItem.Group = AsakiAudioGroup.BGM;
				newItem.Loop = true;
			}
			else if (lowerName.Contains("ui") || lowerName.Contains("click"))
			{
				newItem.Group = AsakiAudioGroup.UI;
			}

			_config.Items.Add(newItem);
		}

		// =========================================================
		// 播放控制逻辑 (核心修改)
		// =========================================================

		private void PlayPreview(AudioItem item)
		{
			if (item.Clip == null) return;
			InitAudioSource(); // 确保 Source 存在

			_previewSource.Stop();
			_previewSource.clip = item.Clip;
			_previewSource.volume = item.Volume;
			_previewSource.pitch = item.Pitch;
			// 预览时不应用 Random Pitch，方便听原声

			_previewSource.Play();

			_currentPlayingItem = item;
			_isPaused = false;
		}

		private void PausePreview()
		{
			if (_previewSource != null && _previewSource.isPlaying)
			{
				_previewSource.Pause();
				_isPaused = true;
			}
		}

		private void ResumePreview()
		{
			if (_previewSource != null && _isPaused)
			{
				_previewSource.UnPause();
				_isPaused = false;
			}
		}

		private void StopPreview()
		{
			if (_previewSource != null)
			{
				_previewSource.Stop();
				_previewSource.clip = null;
			}
			_currentPlayingItem = null;
			_isPaused = false;
		}

		// =========================================================
		// 辅助工具
		// =========================================================

		private string FormatDuration(float seconds)
		{
			System.TimeSpan ts = System.TimeSpan.FromSeconds(seconds);
			return $"{ts.Minutes:D1}:{ts.Seconds:D2}";
		}

		private string GetLoadPath(AudioClip clip)
		{
			string rawPath = AssetDatabase.GetAssetPath(clip);
			if (rawPath.Contains("/Resources/"))
			{
				string ext = Path.GetExtension(rawPath);
				int resIndex = rawPath.IndexOf("/Resources/") + 11;
				return rawPath.Substring(resIndex).Replace(ext, "");
			}
			return rawPath;
		}

		private string SanitizeName(string rawName)
		{
			string name = rawName.Replace(" ", "_").Replace("-", "_").Replace(".", "_");
			char[] arr = name.ToCharArray();
			for (int i = 0; i < arr.Length; i++)
			{
				if (!char.IsLetterOrDigit(arr[i]) && arr[i] != '_') arr[i] = '_';
			}
			name = new string(arr);
			if (char.IsDigit(name[0])) name = "Snd_" + name;
			return name;
		}

		private void SyncAndGenerate()
		{
			if (_config == null) return;
			foreach (AudioItem item in _config.Items)
			{
				if (string.IsNullOrEmpty(item.Key)) item.Key = "Unnamed_" + System.Guid.NewGuid().ToString().Substring(0, 4);
				item.ID = Animator.StringToHash(item.Key);
				if (item.Clip != null) item.AssetPath = GetLoadPath(item.Clip);
			}
			EditorUtility.SetDirty(_config);
			AssetDatabase.SaveAssets();

			StringBuilder sb = new StringBuilder();
			sb.AppendLine("// <auto-generated/>");
			sb.AppendLine("// Generated by AsakiAudioDashboard.");
			sb.AppendLine("using Asaki.Core;");
			sb.AppendLine("using Asaki.Core.Audio;");
			sb.AppendLine("using System.Threading;");
			sb.AppendLine();
			sb.AppendLine("namespace Asaki.Generated");
			sb.AppendLine("{");
			sb.AppendLine("    public enum AudioID");
			sb.AppendLine("    {");
			sb.AppendLine("        None = 0,");
			foreach (AudioItem item in _config.Items.OrderBy(x => x.Key))
			{
				sb.AppendLine($"        {item.Key} = {item.ID},");
			}
			sb.AppendLine("    }");
			sb.AppendLine();
			sb.AppendLine("    public static class AudioExtensions");
			sb.AppendLine("    {");
			sb.AppendLine("        public static AsakiAudioHandle Play(this IAsakiAudioService service, AudioID id, AsakiAudioParams p = default, CancellationToken token = default)");
			sb.AppendLine("        {");
			sb.AppendLine("            return service.Play((int)id, p, token);");
			sb.AppendLine("        }");
			sb.AppendLine("    }");
			sb.AppendLine("}");

			string dir = Path.GetDirectoryName(CODE_GEN_PATH);
			if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
			File.WriteAllText(CODE_GEN_PATH, sb.ToString(), Encoding.UTF8);

			AssetDatabase.Refresh();
			Debug.Log($"[AsakiAudio] Synced {_config.Items.Count} clips.");
		}
	}
}
