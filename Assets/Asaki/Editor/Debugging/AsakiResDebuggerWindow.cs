using Asaki.Core.Resources;
using Asaki.Editor.Utilities.Extensions;
using Asaki.Unity;
using Asaki.Unity.Services.Resources;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Asaki.Editor.Debugging
{
	public class AsakiResDebuggerWindow : EditorWindow
	{
		// ==================== 反射字段缓存 ====================
		private FieldInfo cacheField;
		private FieldInfo lockField;
		private FieldInfo strategyField;
		private FieldInfo locationField;
		private FieldInfo assetField;
		private FieldInfo refCountField;
		private FieldInfo dependencyLocationsField;
		private FieldInfo loadingTcsField;
		private FieldInfo progressCallbacksField;
		private PropertyInfo taskProperty;
		private PropertyInfo strategyNameProperty;

		// AsakiContext 反射相关
		private Type asakiContextType;
		private MethodInfo getServiceMethod;

		// ==================== 窗口状态 ====================
		private IAsakiResService targetService;
		private Vector2 leftScrollPos;
		private Vector2 rightScrollPos;
		private string selectedLocation;
		private float splitterPos = 300f;
		private bool isReflectionInitialized = false;
		private double lastRefreshTime;
		private const float AUTO_REFRESH_INTERVAL = 1f;

		// 自动获取服务状态
		private bool isAutoFetching = true;
		private string fetchStatusMessage = "等待获取服务...";
		private MessageType fetchStatusType = MessageType.Info;

		// ==================== 尺寸限制 ====================
		private const float MIN_LEFT_WIDTH = 200f;
		private const float MIN_RIGHT_WIDTH = 300f;

		[MenuItem("Asaki/Debugger/Resources Debugger")]
		public static void ShowWindow()
		{
			AsakiResDebuggerWindow window = GetWindow<AsakiResDebuggerWindow>("Resources Debugger");
			window.minSize = new Vector2(700, 500);
			window.Show();
		}

		private void OnEnable()
		{
			InitializeReflection();
			EditorApplication.update += OnEditorUpdate;
			isAutoFetching = true;
			fetchStatusMessage = "正在尝试从 AsakiContext 获取服务...";
		}

		private void OnDisable()
		{
			EditorApplication.update -= OnEditorUpdate;
		}

		private void OnEditorUpdate()
		{
			double currentTime = EditorApplication.timeSinceStartup;

			// 每1秒尝试获取一次服务
			if (isAutoFetching && currentTime - lastRefreshTime > AUTO_REFRESH_INTERVAL)
			{
				TryFetchServiceFromContext();
				lastRefreshTime = currentTime;
			}

			// 每秒刷新UI
			if (currentTime - lastRefreshTime > AUTO_REFRESH_INTERVAL * 0.5f)
			{
				Repaint();
			}
		}

		private void InitializeReflection()
		{
			try
			{
				// 初始化 AsakiContext 反射
				InitializeContextReflection();

				// 初始化 AsakiResService 反射
				InitializeServiceReflection();

				isReflectionInitialized = true;
			}
			catch (Exception ex)
			{
				Debug.LogError($"反射初始化失败: {ex.Message}\n{ex.StackTrace}");
				fetchStatusMessage = $"反射初始化失败: {ex.Message}";
				fetchStatusType = MessageType.Error;
				isAutoFetching = false;
				isReflectionInitialized = false;
			}
		}

		private void InitializeContextReflection()
		{
			// 尝试多种方式查找AsakiContext类型
			string[] possibleTypeNames = new[]
			{
				"Asaki.Core.AsakiContext, Asaki.Core",
				"Asaki.Core.AsakiContext",
				"AsakiContext, Asaki.Core",
				"AsakiContext",
			};

			foreach (string typeName in possibleTypeNames)
			{
				asakiContextType = Type.GetType(typeName);
				if (asakiContextType != null) break;
			}

			// 如果还找不到，扫描所有程序集
			if (asakiContextType == null)
			{
				foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
				{
					var types = assembly.GetTypes().Where(t => t.Name == "AsakiContext" && t.IsClass && t.IsPublic);
					if (types.Any())
					{
						asakiContextType = types.First();
						break;
					}
				}
			}

			if (asakiContextType == null)
			{
				throw new Exception("无法在任何程序集中找到 AsakiContext 类型。请确保包含 Asaki.Core 命名空间。");
			}

			// 获取 Get<T> 方法
			MethodInfo genericGetMethod = asakiContextType.GetMethod("Get", BindingFlags.Public | BindingFlags.Static);
			if (genericGetMethod == null)
			{
				throw new Exception($"AsakiContext 中未找到 Get<T> 方法。");
			}

			getServiceMethod = genericGetMethod.MakeGenericMethod(typeof(IAsakiResService));
		}

		private void InitializeServiceReflection()
		{
			Type serviceType = typeof(AsakiResService);

			// 获取私有字段
			cacheField = serviceType.GetField("_cache", BindingFlags.NonPublic | BindingFlags.Instance);
			lockField = serviceType.GetField("_lock", BindingFlags.NonPublic | BindingFlags.Instance);
			strategyField = serviceType.GetField("_strategy", BindingFlags.NonPublic | BindingFlags.Instance);

			if (cacheField == null)
			{
				throw new Exception("未找到 '_cache' 字段。请检查 AsakiResService 定义。");
			}

			// 获取ResRecord嵌套类型
			Type recordType = serviceType.GetNestedType("ResRecord", BindingFlags.NonPublic);
			if (recordType == null)
			{
				throw new Exception("未找到 ResRecord 嵌套类型。");
			}

			// 获取记录字段
			locationField = recordType.GetField("Location");
			assetField = recordType.GetField("Asset");
			refCountField = recordType.GetField("RefCount");
			dependencyLocationsField = recordType.GetField("DependencyLocations");
			loadingTcsField = recordType.GetField("LoadingTcs");
			progressCallbacksField = recordType.GetField("ProgressCallbacks");

			// 获取Task属性
			Type tcsType = loadingTcsField.FieldType;
			taskProperty = tcsType.GetProperty("Task");

			// 获取策略名称属性
			if (strategyField != null)
			{
				Type strategyType = strategyField.FieldType;
				strategyNameProperty = strategyType.GetProperty("StrategyName");
			}
		}

		private void OnGUI()
		{
			if (!isReflectionInitialized)
			{
				EditorGUILayout.HelpBox("反射初始化失败！", MessageType.Error);
				return;
			}

			DrawToolbar();

			if (targetService == null)
			{
				DrawServiceSelector();
			}
			else
			{
				DrawSplitterLayout();
			}
		}

		private void DrawToolbar()
		{
			EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

			if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
			{
				Repaint();
			}

			if (targetService != null && GUILayout.Button("Clear Selection", EditorStyles.toolbarButton, GUILayout.Width(90)))
			{
				selectedLocation = null;
			}

			if (targetService != null)
			{
				GUILayout.Label($"<color=green>{GetStrategyName()}</color>", EditorStyles.toolbarButton);
			}

			GUILayout.FlexibleSpace();

			// 停止/开始自动获取按钮
			if (GUILayout.Button(isAutoFetching ? "Stop Auto Fetch" : "Start Auto Fetch",
				EditorStyles.toolbarButton, GUILayout.Width(120)))
			{
				isAutoFetching = !isAutoFetching;
				if (isAutoFetching)
				{
					fetchStatusMessage = "正在尝试从 AsakiContext 获取服务...";
					fetchStatusType = MessageType.Info;
				}
			}

			if (targetService != null && GUILayout.Button("Release All", EditorStyles.toolbarButton, GUILayout.Width(80)))
			{
				IDictionary cache = GetCacheDictionary();
				if (EditorUtility.DisplayDialog("Release All Resources",
					$"释放所有 {cache?.Count ?? 0} 个资源？", "Yes", "No"))
				{
					ReleaseAllResources();
				}
			}

			EditorGUILayout.EndHorizontal();
		}

		private void DrawServiceSelector()
		{
			EditorGUILayout.BeginVertical();
			GUILayout.FlexibleSpace();

			if (isAutoFetching)
			{
				EditorGUILayout.HelpBox(fetchStatusMessage, fetchStatusType);
			}
			else
			{
				EditorGUILayout.HelpBox("无法自动从 AsakiContext 获取服务。\n\n" +
				                        "可能的原因：\n" +
				                        "1. AsakiContext 未初始化\n" +
				                        "2. IAsakiResService 未注册到上下文\n" +
				                        "3. 命名空间或程序集名称不匹配\n\n" +
				                        "临时方案：手动拖拽服务实例到下方字段", MessageType.Warning);
			}

			// 备用手动赋值方案
			EditorGUILayout.Space();
			Object obj = EditorGUILayout.ObjectField("手动指定服务", null, typeof(Object), true);
			if (obj != null)
			{
				if (obj is IAsakiResService service && service.GetType() == typeof(AsakiResService))
				{
					targetService = service;
					isAutoFetching = false;
					selectedLocation = null;
				}
				else if (obj is IAsakiResService)
				{
					EditorUtility.DisplayDialog("类型错误",
						$"服务类型不匹配: {obj.GetType().Name}\n需要: AsakiResService", "确定");
				}
				else
				{
					EditorUtility.DisplayDialog("类型错误",
						"对象未实现 IAsakiResService 接口", "确定");
				}
			}

			if (!isAutoFetching && GUILayout.Button("重试自动获取"))
			{
				isAutoFetching = true;
				fetchStatusMessage = "正在尝试从 AsakiContext 获取服务...";
			}

			GUILayout.FlexibleSpace();
			EditorGUILayout.EndVertical();
		}

		private void TryFetchServiceFromContext()
		{
			if (getServiceMethod == null)
			{
				fetchStatusMessage = "AsakiContext.Get<T> 方法未找到";
				fetchStatusType = MessageType.Error;
				isAutoFetching = false;
				return;
			}

			try
			{
				object result = getServiceMethod.Invoke(null, null);
				if (result is IAsakiResService service && service.GetType() == typeof(AsakiResService))
				{
					targetService = service;
					fetchStatusMessage = "✓ 服务获取成功";
					fetchStatusType = MessageType.Info;
					isAutoFetching = false;
				}
				else if (result != null)
				{
					fetchStatusMessage = $"服务类型不匹配: {result.GetType().Name}";
					fetchStatusType = MessageType.Warning;
				}
			}
			catch (TargetInvocationException targetEx)
			{
				Exception innerEx = targetEx.InnerException;
				fetchStatusMessage = $"获取失败: {innerEx?.Message ?? targetEx.Message}";
				fetchStatusType = MessageType.Error;
			}
			catch (Exception ex)
			{
				fetchStatusMessage = $"获取异常: {ex.Message}";
				fetchStatusType = MessageType.Error;
				isAutoFetching = false;
			}
		}

		private void DrawSplitterLayout()
		{
			EditorGUILayout.BeginHorizontal();

			// 左侧面板
			GUILayout.BeginVertical(GUILayout.Width(splitterPos));
			DrawLeftPanel();
			GUILayout.EndVertical();

			// 使用分隔条
			GUILayoutExtensions.Splitter(ref splitterPos, MIN_LEFT_WIDTH, position.width - MIN_RIGHT_WIDTH);

			// 右侧面板
			GUILayout.BeginVertical();
			DrawRightPanel();
			GUILayout.EndVertical();

			EditorGUILayout.EndHorizontal();
		}

		private void DrawLeftPanel()
		{
			EditorGUILayout.LabelField("Loaded Resources", EditorStyles.boldLabel);

			leftScrollPos = EditorGUILayout.BeginScrollView(leftScrollPos);

			try
			{
				IDictionary cache = GetCacheDictionary();
				if (cache != null && cache.Count > 0)
				{
					// 正确遍历非泛型IDictionary
					var entries = new List<DictionaryEntry>();
					foreach (DictionaryEntry entry in cache)
					{
						entries.Add(entry);
					}

					// 按引用计数排序
					foreach (DictionaryEntry entry in entries.OrderByDescending(e => GetRefCount(e.Value)))
					{
						DrawResourceButton((string)entry.Key, entry.Value);
					}
				}
				else
				{
					EditorGUILayout.HelpBox("No resources loaded.", MessageType.Info);
				}
			}
			catch (Exception ex)
			{
				EditorGUILayout.HelpBox($"渲染资源列表失败: {ex.Message}", MessageType.Error);
			}

			EditorGUILayout.EndScrollView();
		}

		private void DrawResourceButton(string location, object record)
		{
			int refCount = (int)refCountField.GetValue(record);
			Object asset = assetField.GetValue(record) as Object;

			// 创建按钮内容
			GUIContent content = new GUIContent
			{
				text = $"{location} [{refCount}]",
				tooltip = asset != null ? asset.GetType().Name : "Loading...",
			};

			// 根据状态设置样式
			GUIStyle style = selectedLocation == location ? EditorStyles.boldLabel : EditorStyles.label;
			Color originalColor = GUI.color;

			// 状态颜色
			if (asset == null)
			{
				GUI.color = Color.yellow; // 加载中
			}
			else if (refCount == 0)
			{
				GUI.color = Color.gray; // 待释放
			}

			if (GUILayout.Button(content, style, GUILayout.ExpandWidth(true)))
			{
				selectedLocation = location;
			}

			GUI.color = originalColor;
		}

		private void DrawRightPanel()
		{
			if (string.IsNullOrEmpty(selectedLocation))
			{
				EditorGUILayout.HelpBox("Select a resource from the left panel to view details.", MessageType.Info);
				return;
			}

			rightScrollPos = EditorGUILayout.BeginScrollView(rightScrollPos);

			try
			{
				IDictionary cache = GetCacheDictionary();
				if (cache != null)
				{
					// 查找选中的记录
					foreach (DictionaryEntry entry in cache)
					{
						if ((string)entry.Key == selectedLocation)
						{
							DrawRecordDetails(entry.Value);
							break;
						}
					}
				}
			}
			catch (Exception ex)
			{
				EditorGUILayout.HelpBox($"渲染资源详情失败: {ex.Message}", MessageType.Error);
			}

			EditorGUILayout.EndScrollView();
		}

		private void DrawRecordDetails(object record)
		{
			DrawBasicInfo(record);
			DrawDependencies(record);
			DrawProgressCallbacks(record);
			DrawLoadingStatus(record);
			DrawActionButtons(record);
		}

		private void DrawBasicInfo(object record)
		{
			EditorGUILayout.LabelField("Resource Details", EditorStyles.boldLabel);
			EditorGUILayout.BeginVertical(EditorStyles.helpBox);

			string location = locationField.GetValue(record) as string;
			EditorGUILayout.LabelField("Location", location ?? "Null");

			Object asset = assetField.GetValue(record) as Object;
			EditorGUILayout.ObjectField("Asset", asset, typeof(Object), false);

			if (asset != null)
			{
				EditorGUILayout.LabelField("Type", asset.GetType().Name);

				// 显示资源路径
				string path = AssetDatabase.GetAssetPath(asset);
				if (!string.IsNullOrEmpty(path))
				{
					EditorGUILayout.LabelField("Asset Path", path);
				}
			}

			int refCount = (int)refCountField.GetValue(record);
			EditorGUILayout.LabelField("Reference Count", refCount.ToString());

			// 策略信息
			string strategyName = GetStrategyName();
			if (!string.IsNullOrEmpty(strategyName))
			{
				EditorGUILayout.LabelField("Strategy", strategyName);
			}

			EditorGUILayout.EndVertical();
		}

		private void DrawDependencies(object record)
		{
			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Dependencies", EditorStyles.boldLabel);

			IEnumerable dependencies = dependencyLocationsField.GetValue(record) as IEnumerable;
			if (dependencies != null)
			{
				EditorGUILayout.BeginVertical(EditorStyles.helpBox);

				bool hasDeps = false;
				foreach (object dep in dependencies)
				{
					if (dep is string depStr)
					{
						hasDeps = true;
						GUIStyle buttonStyle = new GUIStyle(EditorStyles.label);
						buttonStyle.normal.textColor = Color.cyan;

						if (GUILayout.Button($"• {depStr}", buttonStyle))
						{
							selectedLocation = depStr;
						}
					}
				}

				if (!hasDeps)
				{
					EditorGUILayout.LabelField("No dependencies");
				}

				EditorGUILayout.EndVertical();
			}
			else
			{
				EditorGUILayout.HelpBox("No dependencies", MessageType.Info);
			}
		}

		private void DrawProgressCallbacks(object record)
		{
			var progressCallbacks = progressCallbacksField.GetValue(record) as Action<float>;
			if (progressCallbacks != null)
			{
				var invocationList = progressCallbacks.GetInvocationList();
				EditorGUILayout.Space();
				EditorGUILayout.LabelField("Progress Callbacks", $"{invocationList.Length} subscriber(s)");

				if (invocationList.Length > 0)
				{
					EditorGUILayout.BeginVertical(EditorStyles.helpBox);
					foreach (Delegate callback in invocationList)
					{
						string targetName = callback.Target?.ToString() ?? "Null";
						if (targetName.Length > 50) targetName = targetName.Substring(0, 50) + "...";

						string methodName = callback.Method.Name;
						EditorGUILayout.LabelField($"• {targetName}.{methodName}");
					}
					EditorGUILayout.EndVertical();
				}
			}
		}

		private void DrawLoadingStatus(object record)
		{
			object loadingTcs = loadingTcsField.GetValue(record);
			if (loadingTcs != null && taskProperty != null)
			{
				Task task = taskProperty.GetValue(loadingTcs) as Task;
				if (task != null)
				{
					EditorGUILayout.Space();
					EditorGUILayout.LabelField("Loading Status", EditorStyles.boldLabel);
					EditorGUILayout.BeginVertical(EditorStyles.helpBox);

					EditorGUILayout.LabelField("Status", task.Status.ToString());

					if (task.IsFaulted)
					{
						string errorMsg = task.Exception?.InnerException?.Message ?? task.Exception?.Message ?? "Unknown error";
						EditorGUILayout.HelpBox($"Error: {errorMsg}", MessageType.Error);
					}
					else if (task.IsCanceled)
					{
						EditorGUILayout.HelpBox("Loading was canceled.", MessageType.Warning);
					}
					else if (task.IsCompletedSuccessfully)
					{
						EditorGUILayout.HelpBox("Loading completed successfully.", MessageType.Info);
					}

					EditorGUILayout.EndVertical();
				}
			}
		}

		private void DrawActionButtons(object record)
		{
			EditorGUILayout.Space();
			EditorGUILayout.BeginHorizontal();

			if (GUILayout.Button("Release This", GUILayout.Height(30)))
			{
				if (EditorUtility.DisplayDialog("Release Resource", $"Release '{selectedLocation}'?", "Yes", "No"))
				{
					targetService.Release(selectedLocation);
					selectedLocation = null;
				}
			}

			GUI.backgroundColor = Color.red;
			if (GUILayout.Button("Force Unload", GUILayout.Height(30)))
			{
				if (EditorUtility.DisplayDialog("Force Unload",
					$"Force unload '{selectedLocation}'? This bypasses reference counting and may cause issues!", "Yes", "Cancel"))
				{
					ForceUnloadResource(record);
				}
			}
			GUI.backgroundColor = Color.white;

			EditorGUILayout.EndHorizontal();
		}

		private void ForceUnloadResource(object record)
		{
			Object asset = assetField.GetValue(record) as Object;
			string location = locationField.GetValue(record) as string;

			if (asset == null)
			{
				EditorUtility.DisplayDialog("Force Unload", "Asset is null, cannot unload.", "OK");
				return;
			}

			object strategy = strategyField?.GetValue(targetService);
			if (strategy == null)
			{
				EditorUtility.DisplayDialog("Error", "Strategy is null.", "OK");
				return;
			}

			MethodInfo unloadMethod = strategy.GetType().GetMethod("UnloadAssetInternal");
			if (unloadMethod != null)
			{
				try
				{
					unloadMethod.Invoke(strategy, new object[] { location, asset });
					Debug.Log($"[AsakiResDebugger] Force unloaded: {location}");
					Repaint();
				}
				catch (Exception ex)
				{
					Debug.LogError($"[AsakiResDebugger] Force unload failed: {ex.Message}");
					EditorUtility.DisplayDialog("Error", $"Force unload failed: {ex.Message}", "OK");
				}
			}
			else
			{
				EditorUtility.DisplayDialog("Error", "未找到 UnloadAssetInternal 方法", "OK");
			}
		}

		private void ReleaseAllResources()
		{
			IDictionary cache = GetCacheDictionary();
			if (cache != null && cache.Count > 0)
			{
				int count = cache.Count;
				var locations = new List<string>();
				foreach (DictionaryEntry entry in cache)
				{
					locations.Add((string)entry.Key);
				}

				foreach (string location in locations)
				{
					targetService.Release(location);
				}
				Debug.Log($"[AsakiResDebugger] Released all {count} resources.");
				selectedLocation = null;
				Repaint();
			}
		}

		private string GetStrategyName()
		{
			if (strategyField == null || targetService == null) return "Unknown";

			try
			{
				object strategy = strategyField.GetValue(targetService);
				return strategyNameProperty?.GetValue(strategy) as string ?? "Unknown Strategy";
			}
			catch
			{
				return "Unknown";
			}
		}

		// 关键修复：返回 IDictionary 而不是 Dictionary<string, object>
		private IDictionary GetCacheDictionary()
		{
			if (targetService == null) return null;

			object lockObj = GetLockObject();
			if (lockObj == null) return null;

			lock (lockObj)
			{
				try
				{
					object cache = cacheField.GetValue(targetService);
					return cache as IDictionary;
				}
				catch (Exception ex)
				{
					Debug.LogError($"[AsakiResDebugger] Failed to get cache: {ex.Message}");
					return null;
				}
			}
		}

		private object GetLockObject()
		{
			try
			{
				return lockField?.GetValue(targetService) ?? new object();
			}
			catch
			{
				return new object();
			}
		}

		private int GetRefCount(object record)
		{
			try
			{
				return (int)refCountField.GetValue(record);
			}
			catch
			{
				return 0;
			}
		}
	}
}
