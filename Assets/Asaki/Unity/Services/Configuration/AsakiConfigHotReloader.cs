#if UNITY_EDITOR

using Asaki.Core.Configuration;
using Asaki.Core.Context;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

// 无论是否使用 UniTask，这里都引用 System.Threading.Tasks 以便处理反射返回的 Task

namespace Asaki.Unity.Services.Configuration
{
	/// <summary>
	/// [Editor Only] 配置热重载监听器
	/// </summary>
	public class AsakiConfigHotReloader : MonoBehaviour
	{
		private string _watchPath;
		private FileSystemWatcher _watcher;

		// 使用并发队列接收文件变更事件（FileSystemWatcher 在后台线程回调）
		private readonly ConcurrentQueue<string> _changedFiles = new ConcurrentQueue<string>();

		// 防抖字典
		private readonly Dictionary<string, float> _debounceMap = new Dictionary<string, float>();
		private const float DEBOUNCE_TIME = 0.5f;

		private void Start()
		{
			// 仅在编辑器下启用
			if (!Application.isEditor)
			{
				Destroy(this);
				return;
			}

			_watchPath = Path.Combine(Application.streamingAssetsPath, "Configs");
			if (!Directory.Exists(_watchPath)) return;

			InitWatcher();
			Debug.Log($"[AsakiConfig] Hot Reload Watcher Started: {_watchPath}");
		}

		private void InitWatcher()
		{
			_watcher = new FileSystemWatcher(_watchPath, "*.csv");
			_watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime;
			_watcher.Changed += OnFileChanged;
			_watcher.EnableRaisingEvents = true;
		}

		private void OnFileChanged(object sender, FileSystemEventArgs e)
		{
			_changedFiles.Enqueue(e.FullPath);
		}

		private void Update()
		{
			// 1. 处理队列
			while (_changedFiles.TryDequeue(out string filePath))
			{
				_debounceMap[filePath] = Time.realtimeSinceStartup + DEBOUNCE_TIME;
			}

			// 2. 检查防抖
			if (_debounceMap.Count > 0)
			{
				var toReload = new List<string>();
				var keys = new List<string>(_debounceMap.Keys);

				foreach (string key in keys)
				{
					if (Time.realtimeSinceStartup >= _debounceMap[key])
					{
						toReload.Add(key);
						_debounceMap.Remove(key);
					}
				}

				// 3. 执行重载
				foreach (string path in toReload)
				{
					ReloadConfig(path);
				}
			}
		}

		private void ReloadConfig(string filePath)
		{
			string fileName = Path.GetFileNameWithoutExtension(filePath);

			// 需要反射查找配置类型
			Type configType = FindConfigType(fileName);

			if (configType != null)
			{
				Debug.Log($"[AsakiConfig] File Changed: {fileName}, Reloading...");
				
				IAsakiConfigService service = AsakiContext.Get<IAsakiConfigService>();
				if (service != null)
				{
					MethodInfo method = service.GetType().GetMethod("ReloadAsync");
					if (method != null)
					{
						MethodInfo genericMethod = method.MakeGenericMethod(configType);
						object taskObj = genericMethod.Invoke(service, null);

						// 由于接口定义的 ReloadAsync 返回 Task，这里进行简单处理
						if (taskObj is Task task)
						{
							// Fire and forget, catch exceptions
							Task.Run(async () =>
							{
								try { await task; }
								catch (Exception ex) { Debug.LogError(ex.Message); }
							});
						}
					}
				}
			}
		}

		private Type FindConfigType(string typeName)
		{
			foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				if (assembly.FullName.StartsWith("System") || assembly.FullName.StartsWith("Unity")) continue;

				// 尝试常用命名空间规则，或者遍历
				Type type = assembly.GetType($"{assembly.GetName().Name}.{typeName}")
				            ?? assembly.GetType(typeName);

				if (type != null) return type;

				foreach (Type t in assembly.GetTypes())
				{
					if (t.Name == typeName && typeof(IAsakiConfig).IsAssignableFrom(t))
						return t;
				}
			}
			return null;
		}

		private void OnDestroy()
		{
			if (_watcher != null)
			{
				_watcher.EnableRaisingEvents = false;
				_watcher.Dispose();
				_watcher = null;
			}
		}
	}
}
#endif
