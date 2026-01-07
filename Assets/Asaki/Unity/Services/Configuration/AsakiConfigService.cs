using Asaki.Core.Broker;
using Asaki.Core.Configuration;
using Asaki.Core.Logging;
using Asaki.Unity.Services.Logging;
using Asaki.Unity.Services.Serialization;
using Asaki.Unity.Utils;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
// 核心引用
using Object = UnityEngine.Object;

namespace Asaki.Unity.Services.Configuration
{
	public class AsakiConfigService : IAsakiConfigService
	{
		// [Security] 系统级序列化口令
		// 必须与 AsakiSaveGenerator 中的定义保持完全一致
		// 只有持有此 Key 的服务才有权将 Config 写入二进制流
		public const string SYSTEM_PERMISSION_KEY = "ASAKI_SYS_KEY_9482_ACCESS";

		private readonly Dictionary<Type, Dictionary<int, IAsakiConfig>> _configStore = new Dictionary<Type, Dictionary<int, IAsakiConfig>>();
		private readonly Dictionary<Type, object> _listStore = new Dictionary<Type, object>();

		private string _csvRootPath;
		private string _binaryCachePath;
		private bool _isEditor;
		private IAsakiEventService _asakiEventService;
		public AsakiConfigService(IAsakiEventService asakiEventService)
		{
			_asakiEventService = asakiEventService;
		}

		public void OnInit()
		{
			_csvRootPath = Path.Combine(Application.streamingAssetsPath, "Configs");
			_binaryCachePath = Path.Combine(Application.persistentDataPath, "ConfigCache");
			_isEditor = Application.isEditor;

			if (!Directory.Exists(_binaryCachePath)) Directory.CreateDirectory(_binaryCachePath);

			if (_isEditor && Application.isPlaying)
			{
				GameObject go = new GameObject("[AsakiConfigHotReloader]");
				go.AddComponent<AsakiConfigHotReloader>();
				Object.DontDestroyOnLoad(go);
			}
		}

		public async Task OnInitAsync()
		{
			await LoadAllAsync();
			ALog.Info($"[AsakiConfig] Service Ready. Loaded {_configStore.Count} tables.");
		}

		public void OnDispose()
		{
			_configStore.Clear();
			_listStore.Clear();
		}

		// =========================================================
		// IAsakiConfigService 接口实现
		// =========================================================

		public Task LoadAllAsync()
		{
			return LoadAllInternal();
		}

		public Task ReloadAsync<T>() where T : class, IAsakiConfig, new()
		{
			return ReloadInternal<T>();
		}

		public T Get<T>(int id) where T : class, IAsakiConfig, new()
		{
			if (_configStore.TryGetValue(typeof(T), out var dict))
			{
				if (dict.TryGetValue(id, out IAsakiConfig val)) return (T)val.CloneConfig();
			}
			return null;
		}
		public IReadOnlyList<T> GetAll<T>() where T : class, IAsakiConfig, new()
		{
			if (_listStore.TryGetValue(typeof(T), out object list))
			{
				return (IReadOnlyList<T>)list;
			}
			return Array.Empty<T>();
		}

		public async IAsyncEnumerable<T> GetAllStreamAsync<T>() where T : class, IAsakiConfig, new()
		{
			if (!IsLoaded<T>())
			{
				string csvPath = Path.Combine(_csvRootPath, typeof(T).Name + ".csv");
				if (File.Exists(csvPath))
				{
					await LoadInternalAsync<T>(csvPath);
				}
				else
				{
					yield break;
				}
			}
			if (!_listStore.TryGetValue(typeof(T), out object list)) yield break;
			if (list is not List<T> typedList) yield break;
			foreach (T item in typedList)
			{
				yield return item;
			}
		}

		// =========================================================
		// 条件查询 (Link)
		// =========================================================

		public T Find<T>(Predicate<T> predicate) where T : class, IAsakiConfig, new()
		{
			if (predicate == null)
			{
				ALog.Warn("[AsakiConfig] Find predicate cannot be null");
				return null;
			}

			if (!_listStore.TryGetValue(typeof(T), out object list))
			{
				return null; // 配置未加载
			}

			var typedList = list as List<T>;
			if (typedList == null) return null;

			// 遍历查找第一个匹配项
			foreach (T item in typedList)
			{
				if (predicate(item))
				{
					return item;
				}
			}

			return null;
		}

		public IReadOnlyList<T> Where<T>(Func<T, bool> predicate) where T : class, IAsakiConfig, new()
		{
			if (predicate == null)
			{
				ALog.Warn("[AsakiConfig] Where predicate cannot be null");
				return Array.Empty<T>();
			}

			if (!_listStore.TryGetValue(typeof(T), out object list))
			{
				return Array.Empty<T>(); // 配置未加载
			}

			var typedList = list as List<T>;
			if (typedList == null) return Array.Empty<T>();

			// 构建结果列表（避免返回原集合引用，保证数据安全）
			var result = new List<T>();
			foreach (T item in typedList)
			{
				if (predicate(item))
				{
					result.Add(item);
				}
			}

			return result;
		}

		public bool Exists<T>(Predicate<T> predicate) where T : class, IAsakiConfig, new()
		{
			if (predicate == null)
			{
				ALog.Warn("[AsakiConfig] Exists predicate cannot be null");
				return false;
			}

			if (!_listStore.TryGetValue(typeof(T), out object list))
			{
				return false; // 配置未加载视为不存在
			}

			var typedList = list as List<T>;
			if (typedList == null) return false;

			// 只要找到一个匹配项就返回
			foreach (T item in typedList)
			{
				if (predicate(item))
				{
					return true;
				}
			}

			return false;
		}

		// =========================================================
		// 批量操作 (Batch Op)
		// =========================================================

		public IReadOnlyList<T> GetBatch<T>(IEnumerable<int> ids) where T : class, IAsakiConfig, new()
		{
			if (ids == null)
			{
				ALog.Warn("[AsakiConfig] GetBatch ids cannot be null");
				return Array.Empty<T>();
			}

			if (!_configStore.TryGetValue(typeof(T), out var dict))
			{
				return Array.Empty<T>(); // 配置未加载
			}

			var result = new List<T>();
			foreach (int id in ids)
			{
				if (dict.TryGetValue(id, out IAsakiConfig config))
				{
					result.Add((T)config);
				}
				else
				{
					// 记录无效ID但不中断流程
					ALog.Warn($"[AsakiConfig] ID {id} not found in {typeof(T).Name}");
				}
			}

			return result;
		}

		// =========================================================
		// 配置元数据 (Config Meta)
		// =========================================================

		public int GetCount<T>() where T : class, IAsakiConfig, new()
		{
			if (_listStore.TryGetValue(typeof(T), out object list))
			{
				return (list as List<T>)?.Count ?? 0;
			}
			return 0; // 未加载返回0
		}

		public bool IsLoaded<T>() where T : class, IAsakiConfig, new()
		{
			return _configStore.ContainsKey(typeof(T));
		}

		public string GetSourcePath<T>() where T : class, IAsakiConfig, new()
		{
			string fileName = typeof(T).Name + ".csv";
			return Path.Combine(_csvRootPath, fileName);
		}

		public DateTime GetLastModifiedTime<T>() where T : class, IAsakiConfig, new()
		{
			string sourcePath = GetSourcePath<T>();
			try
			{
				return File.Exists(sourcePath)
					? File.GetLastWriteTime(sourcePath)
					: DateTime.MinValue;
			}
			catch (Exception ex)
			{
				ALog.Error($"[AsakiConfig] Failed to get modified time for {typeof(T).Name}: {ex.Message}", ex);
				return DateTime.MinValue;
			}
		}

		// =========================================================
		// 核心加载逻辑
		// =========================================================

		private async Task LoadAllInternal()
		{
			if (!Directory.Exists(_csvRootPath)) return;
			string[] files = Directory.GetFiles(_csvRootPath, "*.csv");
			var tasks = new List<Task>();

			foreach (string file in files)
			{
				string fileName = Path.GetFileNameWithoutExtension(file);

				// 此时 GetLoader 返回的是标准的 Task
				Task loadTask = AsakiConfigRegistry.GetLoader(this, fileName, file);

				if (loadTask != null)
				{
					tasks.Add(loadTask);
				}
				else
				{
					ALog.Warn($"[AsakiConfig] Skip loading '{fileName}'. No registry entry found.");
				}
			}

			await Task.WhenAll(tasks);
		}

		// =========================================================
		// 公开给 Registry 调用的方法 (签名必须返回 Task)
		// =========================================================

		public async Task LoadInternalAsync<T>(string csvPath) where T : class, IAsakiConfig, new()
		{
			string fileName = Path.GetFileNameWithoutExtension(csvPath);
			string binaryPath = Path.Combine(_binaryCachePath, fileName + ".bin");
			List<T> results = null;
			bool shouldLoadBinary = false;


			if (File.Exists(binaryPath))
			{
				if (_isEditor)
				{
					DateTime binTime = File.GetLastWriteTime(binaryPath);
					DateTime csvTime = File.GetLastWriteTime(csvPath);
					if (binTime >= csvTime)
					{
						shouldLoadBinary = true;
					}
					else
					{
						ALog.Warn($"[AsakiConfig] Detected stale binary for '{fileName}'. Re-baking from CSV...");
					}
				}
				else
				{
					shouldLoadBinary = true;
				}
			}

			if (shouldLoadBinary)
			{
				try
				{
					results = await LoadFromBinaryAsync<T>(binaryPath);
				}
				catch (Exception ex)
				{
					ALog.Error($"[AsakiConfig] Failed to load binary '{fileName}', falling back to CSV. Error : {ex.Message}", ex);
					results = null;
				}
			}

			if (results == null)
			{
				#if ASAKI_USE_UNITASK
				await UniTask.SwitchToThreadPool();
				string csvContent = await File.ReadAllTextAsync(csvPath);
				await UniTask.SwitchToMainThread();
				#else
                string csvContent = await System.Threading.Tasks.Task.Run(() => File.ReadAllTextAsync(csvPath));
				#endif
				results = await ParseCsvAsync<T>(csvContent);

				// 3. 自动烘焙 (Auto Bake)
				// 只要读了 CSV，就顺手更新一下 Bin，这样下次启动就能快了
				await SaveToBinaryAsync(binaryPath, results);
			}
			BuildIndex(results);
		}

		// =========================================================
		// 内部实现 (Internal)
		// =========================================================

		private async Task<List<T>> ParseCsvAsync<T>(string csvContent) where T : class, IAsakiConfig, new()
		{
			return await Task.Run(() =>
			{
				string[] lines = csvContent.Replace("\r\n", "\n").Split('\n');
				if (lines.Length < 2) return Task.FromResult(new List<T>());

				string[] headers = AsakiCsvUtils.ParseLine(lines[0]);
				var headerMap = new Dictionary<string, int>();
				for (int i = 0; i < headers.Length; i++) headerMap[headers[i].Trim()] = i;

				var result = new List<T>(lines.Length);
				for (int i = 1; i < lines.Length; i++)
				{
					if (string.IsNullOrWhiteSpace(lines[i])) continue;

					string[] rowData = AsakiCsvUtils.ParseLine(lines[i]);
					AsakiCsvReader reader = new AsakiCsvReader(rowData, headerMap);
					T obj = new T();
					obj.Deserialize(reader);
					result.Add(obj);
				}
				return Task.FromResult(result);
			});
		}

		private async Task<List<T>> LoadFromBinaryAsync<T>(string path) where T : class, IAsakiConfig, new()
		{
			#if ASAKI_USE_UNITASK
			await UniTask.SwitchToThreadPool();
			byte[] bytes = await File.ReadAllBytesAsync(path);
			await UniTask.SwitchToMainThread();
			#else
            byte[] bytes = await Task.Run(() => File.ReadAllBytesAsync(path));
			#endif
			return DeserializeBytes<T>(bytes);
		}

		private async Task SaveToBinaryAsync<T>(string path, List<T> data) where T : class, IAsakiConfig
		{
			byte[] bytes = SerializeBytes(data);
			#if ASAKI_USE_UNITASK
			await UniTask.SwitchToThreadPool();
			await File.WriteAllBytesAsync(path, bytes);
			await UniTask.SwitchToMainThread();
			#else
            await Task.Run(() => File.WriteAllBytesAsync(path, bytes));
			#endif
		}

		private async Task ReloadInternal<T>() where T : class, IAsakiConfig, new()
		{
			string csvPath = Path.Combine(_csvRootPath, typeof(T).Name + ".csv");
			if (File.Exists(csvPath))
			{
				ALog.Info($"[AsakiConfig] Hot Reloading: {typeof(T).Name}...");

				// 1. 读取最新的 CSV 内容
				#if ASAKI_USE_UNITASK
				await UniTask.SwitchToThreadPool();
				string content = await File.ReadAllTextAsync(csvPath);
				await UniTask.SwitchToMainThread();
				#else
                string content = await System.Threading.Tasks.Task.Run(() => File.ReadAllTextAsync(csvPath));
				#endif

				// 2. 解析
				var list = await ParseCsvAsync<T>(content);

				// 3. 更新内存索引
				BuildIndex(list);

				// 4. [关键] 立即更新二进制缓存
				string fileName = typeof(T).Name;
				string binaryPath = Path.Combine(_binaryCachePath, fileName + ".bin");
				await SaveToBinaryAsync(binaryPath, list);

				// 5. 发送事件
				_asakiEventService.Publish(new AsakiConfigReloadedEvent { ConfigType = typeof(T) });
			}
		}

		// =========================================================
		// 纯同步辅助方法
		// =========================================================

		private List<T> DeserializeBytes<T>(byte[] bytes) where T : class, IAsakiConfig, new()
		{
			using (MemoryStream ms = new MemoryStream(bytes))
			{
				AsakiBinaryReader reader = new AsakiBinaryReader(ms);
				int count = reader.ReadInt(null);
				var list = new List<T>(count);
				for (int i = 0; i < count; i++)
				{
					T obj = new T();
					obj.Deserialize(reader);
					list.Add(obj);
				}
				return list;
			}
		}

		private byte[] SerializeBytes<T>(List<T> data) where T : class, IAsakiConfig
		{
			using (MemoryStream ms = new MemoryStream())
			{
				AsakiBinaryWriter writer = new AsakiBinaryWriter(ms);
				writer.WriteInt(null, data.Count);

				// [Key Pattern Implementation]
				// 遍历所有对象，解锁权限，然后序列化
				foreach (T item in data)
				{
					// [Fix] 显式接口调用：直接调用生成器生成的 AllowConfigSerialization 方法
					// 传递硬编码的 System Key。如果 Key 不对，item 内部会报错并拒绝解锁。
					// 这种方式不需要反射，性能高，类型安全，且 IL2CPP 友好。

					// 注意：因为 T 已经约束为 IAsakiConfig，而我们在 IAsakiConfig 中新增了 AllowConfigSerialization
					// 所以这里可以直接调用，非常干净。
					item.AllowConfigSerialization(SYSTEM_PERMISSION_KEY);

					// 执行序列化 (此时 item 内部 _allowConfigSerialization 已经为 true)
					item.Serialize(writer);
				}

				return ms.ToArray();
			}
		}

		private void BuildIndex<T>(List<T> list) where T : class, IAsakiConfig
		{
			var dict = new Dictionary<int, IAsakiConfig>(list.Count);
			foreach (T item in list)
			{
				if (!dict.ContainsKey(item.Id)) dict.Add(item.Id, item);
			}
			_configStore[typeof(T)] = dict;
			_listStore[typeof(T)] = list;
		}
	}
}
