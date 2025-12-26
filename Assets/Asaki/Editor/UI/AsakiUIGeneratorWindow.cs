using Asaki.Core.UI;
using Asaki.Unity;
using Asaki.Unity.Configuration;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Asaki.Editor.UI
{
	public class AsakiUIGeneratorWindow : EditorWindow
	{
		// ================= 配置区域 =================
		private const string CODE_GEN_PATH = "Assets/Asaki/Generated/UIID.cs";
		private const string CONFIG_ASSET_PATH = "Assets/Resources/Asaki/Configuration/AsakiUIConfig.asset";
		// ===========================================

		[System.Serializable]
		private class UIItem
		{
			public GameObject Prefab;
			public AsakiUILayer Layer = AsakiUILayer.Normal;
			public string EnumName;

			// [新增] 自定义加载路径 (Addressable Key 或 Resources Path)
			public string LoadPath;

			public bool HasConflict;

			public UIItem(GameObject prefab, string overridePath = null)
			{
				Prefab = prefab;
				RefreshName();

				if (!string.IsNullOrEmpty(overridePath))
				{
					LoadPath = overridePath;
				}
				else
				{
					// 默认智能路径生成逻辑
					string rawPath = AssetDatabase.GetAssetPath(prefab);
					if (rawPath.Contains("/Resources/"))
					{
						// 如果在 Resources 下，自动裁剪为 Resources 加载路径
						string ext = Path.GetExtension(rawPath);
						int resIndex = rawPath.IndexOf("/Resources/") + 11;
						LoadPath = rawPath.Substring(resIndex).Replace(ext, "");
					}
					else
					{
						// 否则使用 AssetPath (Addressable 默认通常是路径，用户可手动改)
						LoadPath = rawPath;
					}
				}
			}

			public void RefreshName()
			{
				if (Prefab != null)
					EnumName = SanitizeName(Prefab.name);
			}
		}

		private List<UIItem> _items = new List<UIItem>();
		private Vector2 _scrollPos;
		private bool _hasGlobalConflict = false;

		[MenuItem("Asaki/UI/UI Generator Window")]
		public static void OpenWindow()
		{
			AsakiUIGeneratorWindow window = GetWindow<AsakiUIGeneratorWindow>("Asaki UI Gen");
			window.minSize = new Vector2(600, 400); // 加宽窗口以容纳路径编辑
			window.Show();
			window.LoadCurrentConfig();
		}

		private void OnGUI()
		{
			DrawToolbar();
			DrawDragDropArea();
			DrawList();
			DrawFooter();
		}

		private void DrawToolbar()
		{
			EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
			if (GUILayout.Button("Load From Configuration", EditorStyles.toolbarButton))
			{
				LoadCurrentConfig();
			}
			if (GUILayout.Button("Clear All", EditorStyles.toolbarButton))
			{
				_items.Clear();
				_hasGlobalConflict = false;
			}
			GUILayout.FlexibleSpace();
			EditorGUILayout.EndHorizontal();
		}

		private void DrawDragDropArea()
		{
			Event evt = Event.current;
			Rect dropArea = GUILayoutUtility.GetRect(0.0f, 50.0f, GUILayout.ExpandWidth(true));
			GUI.Box(dropArea, "Drag UI Prefabs or Folders Here", EditorStyles.helpBox);

			if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
			{
				if (!dropArea.Contains(evt.mousePosition)) return;

				DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

				if (evt.type == EventType.DragPerform)
				{
					DragAndDrop.AcceptDrag();
					foreach (Object draggedTask in DragAndDrop.objectReferences)
					{
						AddObject(draggedTask);
					}
					ValidateConflicts();
				}
				Event.current.Use();
			}
		}

		private void AddObject(Object obj)
		{
			string path = AssetDatabase.GetAssetPath(obj);

			if (Directory.Exists(path))
			{
				string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { path });
				foreach (string guid in guids)
				{
					string assetPath = AssetDatabase.GUIDToAssetPath(guid);
					GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
					if (go != null) AddSingleItem(go);
				}
			}
			else if (obj is GameObject go)
			{
				if (PrefabUtility.IsPartOfPrefabAsset(obj))
				{
					AddSingleItem(go);
				}
			}
		}

		private void AddSingleItem(GameObject go)
		{
			if (_items.Any(x => x.Prefab == go)) return;
			_items.Add(new UIItem(go));
		}

		private void DrawList()
		{
			EditorGUILayout.LabelField($"Total Items: {_items.Count}", EditorStyles.boldLabel);

			// 表头
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField("Prefab", EditorStyles.boldLabel, GUILayout.Width(150));
			EditorGUILayout.LabelField("Generated Enum", EditorStyles.boldLabel, GUILayout.Width(180));
			EditorGUILayout.LabelField("Load Path (Key)", EditorStyles.boldLabel, GUILayout.Width(200)); // [新增]
			EditorGUILayout.LabelField("Layer", EditorStyles.boldLabel, GUILayout.Width(80));
			EditorGUILayout.EndHorizontal();

			_scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

			for (int i = 0; i < _items.Count; i++)
			{
				UIItem item = _items[i];
				if (item.Prefab == null) continue;

				GUI.backgroundColor = item.HasConflict ? new Color(1f, 0.5f, 0.5f) : Color.white;

				EditorGUILayout.BeginHorizontal("box");

				// 1. Prefab 对象引用
				EditorGUI.BeginChangeCheck();
				GameObject newPrefab = (GameObject)EditorGUILayout.ObjectField(item.Prefab, typeof(GameObject), false, GUILayout.Width(150));
				if (EditorGUI.EndChangeCheck())
				{
					item.Prefab = newPrefab;
					item.RefreshName();
					ValidateConflicts();
				}

				// 2. Enum 名称显示
				EditorGUILayout.LabelField(item.EnumName, GUILayout.Width(180));

				// 3. [新增] Load Path 编辑框 (支持 Addressable Key 修改)
				item.LoadPath = EditorGUILayout.TextField(item.LoadPath, GUILayout.Width(200));

				// 4. Layer 选择
				item.Layer = (AsakiUILayer)EditorGUILayout.EnumPopup(item.Layer, GUILayout.Width(80));

				// 5. 删除按钮
				GUI.backgroundColor = Color.red;
				if (GUILayout.Button("X", GUILayout.Width(20)))
				{
					_items.RemoveAt(i);
					ValidateConflicts();
					i--;
				}
				GUI.backgroundColor = Color.white;

				EditorGUILayout.EndHorizontal();
			}

			EditorGUILayout.EndScrollView();

			if (_hasGlobalConflict)
			{
				EditorGUILayout.HelpBox("Duplicate Enum Names detected! Please rename your prefabs or remove duplicates.", MessageType.Error);
			}
		}

		private void DrawFooter()
		{
			GUILayout.Space(10);
			GUI.enabled = !_hasGlobalConflict && _items.Count > 0;

			// 按钮文字改为 "Sync & Generate" 以体现同步功能
			if (GUILayout.Button("Sync Configuration & Generate Code", GUILayout.Height(40)))
			{
				SyncAndGenerate();
			}
			GUI.enabled = true;
		}

		// ================= 逻辑区域 =================

		private void ValidateConflicts()
		{
			_hasGlobalConflict = false;
			var nameCount = new Dictionary<string, int>();

			foreach (UIItem item in _items)
			{
				if (item.Prefab == null) continue;
				item.RefreshName();
				if (!nameCount.ContainsKey(item.EnumName)) nameCount[item.EnumName] = 0;
				nameCount[item.EnumName]++;
			}

			foreach (UIItem item in _items)
			{
				if (item.Prefab == null) continue;
				bool isConflict = nameCount[item.EnumName] > 1;
				item.HasConflict = isConflict;
				if (isConflict) _hasGlobalConflict = true;
			}
		}

		private void SyncAndGenerate()
		{
			try
			{
				EditorUtility.DisplayProgressBar("Asaki UI Gen", "Processing...", 0.5f);

				// 1. 获取或创建配置
				AsakiUIConfig config = LoadOrCreateConfig();

				// 2. 生成 C# 枚举代码
				GenerateCode(_items);

				// 3. 同步数据到 ScriptableObject (包含手动修改后的 Path)
				UpdateConfigData(config, _items);

				// 4. 刷新 AssetDatabase
				AssetDatabase.Refresh();
				EditorUtility.DisplayDialog("Success", $"Synced {_items.Count} items to Configuration & UIID.cs", "OK");
			}
			catch (System.Exception e)
			{
				Debug.LogError($"[AsakiUI] Failed: {e.Message}");
			}
			finally
			{
				EditorUtility.ClearProgressBar();
			}
		}

		private static void GenerateCode(List<UIItem> items)
		{
			StringBuilder sb = new StringBuilder();
			sb.AppendLine("// <auto-generated/>");
			sb.AppendLine("// This file is generated by AsakiUIGeneratorWindow.");
			sb.AppendLine();
			sb.AppendLine("namespace Asaki.Generated");
			sb.AppendLine("{");
			sb.AppendLine("    public enum UIID");
			sb.AppendLine("    {");
			sb.AppendLine("        None = 0,");

			foreach (UIItem item in items.OrderBy(x => x.EnumName))
			{
				int id = Animator.StringToHash(item.EnumName);
				sb.AppendLine($"        {item.EnumName} = {id},");
			}

			sb.AppendLine("    }");
			sb.AppendLine("}");

			WriteFile(CODE_GEN_PATH, sb.ToString());
		}

		private static void UpdateConfigData(AsakiUIConfig config, List<UIItem> items)
		{
			config.UIList.Clear();
			foreach (UIItem item in items.OrderBy(x => x.EnumName))
			{
				config.UIList.Add(new UIInfo
				{
					Name = item.EnumName,
					ID = Animator.StringToHash(item.EnumName),
					Layer = item.Layer,
					AssetPath = item.LoadPath, // [关键] 保存用户编辑过的路径
				});
			}
			EditorUtility.SetDirty(config);
			AssetDatabase.SaveAssets();
		}

		private static AsakiUIConfig LoadOrCreateConfig()
		{
			AsakiUIConfig config = AssetDatabase.LoadAssetAtPath<AsakiUIConfig>(CONFIG_ASSET_PATH);
			if (config == null)
			{
				config = CreateInstance<AsakiUIConfig>();
				string dir = Path.GetDirectoryName(CONFIG_ASSET_PATH);
				if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
				AssetDatabase.CreateAsset(config, CONFIG_ASSET_PATH);
			}
			return config;
		}

		private void LoadCurrentConfig()
		{
			AsakiUIConfig config = AssetDatabase.LoadAssetAtPath<AsakiUIConfig>(CONFIG_ASSET_PATH);
			if (config == null) return;

			_items.Clear();
			foreach (UIInfo info in config.UIList)
			{
				// 尝试反向查找 Prefab，用于编辑器显示
				// 逻辑：如果 info.AssetPath 是完整路径，直接加载
				// 如果是 Addressable Key，我们可能找不到 Prefab，
				// 但为了列表不丢失数据，我们尽量尝试在项目中按名称搜索

				GameObject prefab = null;
				string searchPath = info.AssetPath;

				// 1. 尝试作为绝对路径加载
				prefab = AssetDatabase.LoadAssetAtPath<GameObject>(searchPath);

				// 2. 如果是 Resources 短路径，尝试还原
				if (prefab == null)
				{
					prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/" + searchPath + ".prefab");
				}

				// 3. 如果还是找不到 (比如是自定义 Key "Inventory"), 尝试全局搜索同名 Prefab
				if (prefab == null && !searchPath.Contains("/"))
				{
					string[] guids = AssetDatabase.FindAssets(searchPath + " t:Prefab");
					if (guids.Length > 0)
					{
						// 取第一个匹配的，风险是可能匹配错，但好过丢失
						prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[0]));
					}
				}

				if (prefab != null)
				{
					_items.Add(new UIItem(prefab, info.AssetPath) // 传入配置中保存的 Path
					{
						Layer = info.Layer,
						// EnumName 会在构造函数中刷新
					});
				}
				else
				{
					Debug.LogWarning($"[AsakiUI] Could not find prefab for config ID: {info.Name} (Path: {info.AssetPath}). Item skipped in editor list.");
				}
			}
			ValidateConflicts();
		}

		private static string SanitizeName(string rawName)
		{
			string name = rawName.Replace(" ", "_")
			                     .Replace("-", "_")
			                     .Replace(".", "_")
			                     .Replace("(", "")
			                     .Replace(")", "");
			if (char.IsDigit(name[0])) name = "UI_" + name;
			return name;
		}

		private static void WriteFile(string path, string content)
		{
			string dir = Path.GetDirectoryName(path);
			if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
			File.WriteAllText(path, content, Encoding.UTF8);
		}
	}
}
