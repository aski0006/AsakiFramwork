using Asaki.Core.Attributes;
using Asaki.Core.Broker;
using Asaki.Core.Configuration;
using Asaki.Core.Logging;
using Asaki.Unity.Services.Serialization;
using Asaki.Unity.Utils;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
// 核心引用
using Object = UnityEngine.Object;

namespace Asaki.Unity.Services.Configuration
{
	public class AsakiConfigService : IAsakiConfigService
	{
		private const string SYSTEM_PERMISSION_KEY = "ASAKI_SYS_KEY_9482_ACCESS";

		private readonly Dictionary<Type, Dictionary<int, IAsakiConfig>> _configStore = new Dictionary<Type, Dictionary<int, IAsakiConfig>>();
		private readonly Dictionary<Type, object> _listStore = new Dictionary<Type, object>();

		private readonly SemaphoreSlim _loadSemaphore = new SemaphoreSlim(1, 1);
		private string _csvRootPath;
		private string _binaryCachePath;
		private bool _isEditor;
		private readonly IAsakiEventService _asakiEventService;

		private readonly Dictionary<Type, ConfigMetadata> _metadataCache = new();
		private readonly Dictionary<Type, Task> _loadingTasks = new(); // 加载任务
		private readonly Dictionary<Type, ConfigStats> _statsCache = new();
		private class ConfigMetadata
		{
			public Type ConfigType;
			public AsakiConfigLoadStrategy Strategy;
			public int Priority;
			public bool Unloadable;
			public Type[] Dependencies;
			public long EstimatedSize; // 预估大小（字节）
		}

		private class ConfigStats
		{
			public int AccessCount;         // 访问次数
			public DateTime LastAccessTime; // 最后访问时间
			public DateTime LoadTime;       // 加载时间
		}
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

			ScanConfigTypes();

			if (!_isEditor || !Application.isPlaying) return;
			GameObject go = new GameObject("[AsakiConfigHotReloader]");
			go.AddComponent<AsakiConfigHotReloader>();
			Object.DontDestroyOnLoad(go);
		}


		public async Task OnInitAsync()
		{
			var preloadTypes = _metadataCache
			                   .Where(kvp => kvp.Value.Strategy == AsakiConfigLoadStrategy.Preload)
			                   .OrderByDescending(kvp => kvp.Value.Priority) // 按优先级排序
			                   .Select(kvp => kvp.Key)
			                   .ToList();
			if (preloadTypes.Count > 0)
			{
				ALog.Info($"[AsakiConfig] Preloading {preloadTypes.Count} core configs...");

				var tasks = preloadTypes.Select(LoadConfigInternalAsync).ToList();

				await Task.WhenAll(tasks);
			}
			ALog.Info($"[AsakiConfig] Service Ready.  Preloaded {_configStore.Count} tables.");
			#if UNITY_EDITOR
			if (_isEditor)
			{
				await ValidateAllConfigsAsync();
			}
			#endif
		}

		public void OnDispose()
		{
			_configStore.Clear();
			_listStore.Clear();
			_loadSemaphore?.Dispose();
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
			// 同步版本：检查是否已加载
			if (!IsLoaded<T>())
			{
				var metadata = GetMetadata<T>();

				// 检查加载策略
				if (metadata.Strategy == AsakiConfigLoadStrategy.Manual)
				{
					ALog.Error($"[AsakiConfig] {typeof(T).Name} requires manual loading.  Call LoadAsync<{typeof(T).Name}>() first.");
					return null;
				}

				// 自动加载（阻塞警告）
				ALog.Warn($"[AsakiConfig] {typeof(T).Name} not loaded, blocking load on main thread.  Consider using GetAsync or Preload.");
				LoadConfigInternalAsync(typeof(T)).GetAwaiter().GetResult();
			}

			// 记录访问统计
			RecordAccess<T>();

			// 正常查询
			if (_configStore.TryGetValue(typeof(T), out var dict))
			{
				if (dict.TryGetValue(id, out IAsakiConfig val))
					return (T)val.CloneConfig();
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

		public bool IsLoaded(Type configType)
		{
			return _configStore.ContainsKey(configType);
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
		public async Task<T> GetAsync<T>(int id) where T : class, IAsakiConfig, new()
		{
			await EnsureLoadedAsync<T>();
			return Get<T>(id);
		}
		public async Task PreloadAsync<T>() where T : class, IAsakiConfig, new()
		{
			await LoadConfigInternalAsync(typeof(T));
		}
		public async Task PreloadAsync(Type configType)
		{
			// 直接调用核心加载逻辑，不做任何反射检查
			// 调用方需确保: configType != null 且实现了 IAsakiConfig
			await LoadConfigInternalAsync(configType);
		}
		public async Task PreloadBatchAsync(params Type[] configTypes)
		{
			var tasks = configTypes.Select(LoadConfigInternalAsync).ToArray();
			await Task.WhenAll(tasks);
		}
		public void Unload<T>() where T : class, IAsakiConfig, new()
		{
			var type = typeof(T);
			var metadata = GetMetadata<T>();

			if (!metadata.Unloadable)
			{
				ALog.Warn($"[AsakiConfig] {type.Name} is marked as non-unloadable.");
				return;
			}

			if (_configStore.Remove(type))
			{
				_listStore.Remove(type);
				ALog.Info($"[AsakiConfig] Unloaded {type.Name}");
			}
		}
		public void Unload(Type configType)
		{
			// 直接查字典，不做接口类型检查
			// 调用方需确保: configType != null 且实现了 IAsakiConfig
    
			var metadata = GetMetadata(configType); // 仅字典查找，无反射
    
			if (!metadata.Unloadable) 
			{
				ALog.Warn($"[AsakiConfig] {configType.Name} is non-unloadable.");
				return;
			}

			if (_configStore.Remove(configType))
			{
				_listStore.Remove(configType);
				ALog.Info($"[AsakiConfig] Unloaded {configType.Name}");
			}
		}
		public AsakiConfigLoadInfo GetLoadInfo<T>() where T : class, IAsakiConfig, new()
		{
			var type = typeof(T);
			var metadata = GetMetadata<T>();

			return new AsakiConfigLoadInfo
			{
				ConfigName = type.Name,
				IsLoaded = IsLoaded(type),
				Strategy = metadata.Strategy,
				Priority = metadata.Priority,
				Unloadable = metadata.Unloadable,
				EstimatedSize = metadata.EstimatedSize,
				AccessCount = _statsCache.TryGetValue(type, out var stats) ? stats.AccessCount : 0,
				LastAccessTime = stats?.LastAccessTime ?? DateTime.MinValue
			};
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

		private async Task EnsureLoadedAsync<T>() where T : class, IAsakiConfig, new()
		{
			if (IsLoaded<T>()) return;

			var metadata = GetMetadata<T>();

			// 检查策略
			if (metadata.Strategy == AsakiConfigLoadStrategy.Manual)
			{
				throw new InvalidOperationException(
					$"Config {typeof(T).Name} requires manual loading. Call LoadAsync<{typeof(T).Name}>() first.");
			}

			// 加载（包含依赖）
			await LoadConfigInternalAsync(typeof(T));
		}

		private async Task LoadConfigInternalAsync(Type configType)
		{
			// 防止重复加载
			if (IsLoaded(configType)) return;

			Task loadTask;

			await _loadSemaphore.WaitAsync();

			try
			{
				// 双重检查
				if (IsLoaded(configType)) return;

				// GetOrAdd 模式
				if (!_loadingTasks.TryGetValue(configType, out loadTask))
				{
					loadTask = LoadConfigCoreAsync(configType);
					_loadingTasks[configType] = loadTask;
				}
			}
			finally
			{
				_loadSemaphore.Release();
			}

			// 3. 等待加载完成（无锁）
			await loadTask;
		}

		private async Task LoadConfigCoreAsync(Type configType)
		{
			try
			{
				// 1. 获取元数据
				if (!_metadataCache.TryGetValue(configType, out var metadata))
				{
					throw new InvalidOperationException($"Config type {configType.Name} not registered.");
				}

				// 2. 加载依赖
				if (metadata.Dependencies is { Length: > 0 })
				{
					ALog.Info($"[AsakiConfig] Loading dependencies for {configType.Name}.. .");

					var depTasks = metadata.Dependencies
					                       .Select(LoadConfigInternalAsync) // 递归调用，自动防重复
					                       .ToArray();

					await Task.WhenAll(depTasks);
				}

				// 3. 加载配置文件
				string csvPath = Path.Combine(_csvRootPath, configType.Name + ".csv");
				if (!File.Exists(csvPath))
				{
					throw new FileNotFoundException($"Config file not found: {csvPath}");
				}

				// 4. 调用注册的加载器
				Task loadTask = AsakiConfigRegistry.GetLoader(this, configType.Name, csvPath);
				if (loadTask == null)
				{
					throw new InvalidOperationException($"No loader registered for {configType.Name}");
				}

				await loadTask;

				// 5. 记录统计信息
				if (!_statsCache.ContainsKey(configType))
				{
					_statsCache[configType] = new ConfigStats();
				}
				_statsCache[configType].LoadTime = DateTime.Now;

				ALog.Info($"[AsakiConfig] ✅ Loaded {configType.Name} ({metadata.Strategy})");
			}
			catch (Exception ex)
			{
				ALog.Error($"[AsakiConfig] ❌ Failed to load {configType.Name}: {ex.Message}", ex);
				throw; // 重新抛出，让等待的任务也能收到异常
			}
			finally
			{
				// 6. 清理任务记录
				await _loadSemaphore.WaitAsync();
				try
				{
					_loadingTasks.Remove(configType);
				}
				finally
				{
					_loadSemaphore.Release();
				}
			}
		}

		// =========================================================
		// 纯同步辅助方法
		// =========================================================

		private List<T> DeserializeBytes<T>(byte[] bytes) where T : class, IAsakiConfig, new()
		{
			using MemoryStream ms = new MemoryStream(bytes);
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

		private byte[] SerializeBytes<T>(List<T> data) where T : class, IAsakiConfig
		{
			using MemoryStream ms = new MemoryStream();
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

		private void BuildIndex<T>(List<T> list) where T : class, IAsakiConfig
		{
			var dict = new Dictionary<int, IAsakiConfig>(list.Count);
			foreach (T item in list)
			{
				dict.TryAdd(item.Id, item);
			}
			_configStore[typeof(T)] = dict;
			_listStore[typeof(T)] = list;
		}

		private void ScanConfigTypes()
		{
			var allTypes = TypeCache.GetTypesDerivedFrom<IAsakiConfig>()
			                        .Where(t => !t.IsAbstract && !t.IsInterface);

			foreach (var type in allTypes)
			{
				AsakiConfigAttribute attr = type.GetCustomAttribute<AsakiConfigAttribute>();

				ConfigMetadata metadata = new ConfigMetadata
				{
					ConfigType = type,
					Strategy = attr?.LoadStrategy ?? AsakiConfigLoadStrategy.Auto,
					Priority = attr?.Priority ?? 0,
					Unloadable = attr?.Unloadable ?? true,
					Dependencies = attr?.Dependencies ?? Array.Empty<Type>(),
					EstimatedSize = EstimateConfigSize(type)
				};

				// Auto 策略：根据大小自动决策
				if (metadata.Strategy == AsakiConfigLoadStrategy.Auto)
				{
					metadata.Strategy = metadata.EstimatedSize < 100 * 1024
						? AsakiConfigLoadStrategy.Preload   // < 100KB 预加载
						: AsakiConfigLoadStrategy.OnDemand; // >= 100KB 按需
				}

				_metadataCache[type] = metadata;
			}
		}

		private long EstimateConfigSize(Type type)
		{
			string csvPath = Path.Combine(_csvRootPath, type.Name + ".csv");
			if (File.Exists(csvPath))
				return new FileInfo(csvPath).Length;

			string binPath = Path.Combine(_binaryCachePath, type.Name + ".bin");
			if (File.Exists(binPath))
				return new FileInfo(binPath).Length;

			return 0;
		}

		private ConfigMetadata GetMetadata<T>() where T : IAsakiConfig
		{
			if (_metadataCache.TryGetValue(typeof(T), out var metadata))
				return metadata;

			// 未标记的配置：使用默认策略
			return new ConfigMetadata
			{
				ConfigType = typeof(T),
				Strategy = AsakiConfigLoadStrategy.OnDemand,
				Priority = 0,
				Unloadable = true,
				Dependencies = Array.Empty<Type>()
			};
		}

		private void RecordAccess<T>()
		{
			var type = typeof(T);
			if (!_statsCache.ContainsKey(type))
			{
				_statsCache[type] = new ConfigStats();
			}

			_statsCache[type].AccessCount++;
			_statsCache[type].LastAccessTime = DateTime.Now;
		}
		
		private async Task ValidateAllConfigsAsync()
		{
			var sw = Stopwatch.StartNew();
			ALog.Info("[AsakiConfig] 🔍 Validating all configs in editor mode...");

			var allTypes = _metadataCache.Keys.ToList();

			// 使用线程安全的集合收集错误
			var errors = new System.Collections.Concurrent.ConcurrentBag<string>();
			var warnings = new System.Collections.Concurrent.ConcurrentBag<string>();

			// 并行验证（充分利用多核 CPU）
			await Task.Run(() =>
			{
				Parallel.ForEach(allTypes, new ParallelOptions
				{
					MaxDegreeOfParallelism = Environment.ProcessorCount
				}, type =>
				{
					try
					{
						// 1. 检查 CSV 文件是否存在
						string csvPath = Path.Combine(_csvRootPath, type.Name + ".csv");
						if (!File.Exists(csvPath))
						{
							warnings.Add($"Missing CSV: {type.Name}.csv");
							return;
						}

						// 2. 检查文件大小（空文件警告）
						var fileInfo = new FileInfo(csvPath);
						if (fileInfo.Length < 10) // 小于 10 字节基本是空文件
						{
							warnings.Add($"{type.Name}. csv is too small ({fileInfo.Length} bytes), might be empty.");
						}

						// 3. 检查依赖
						if (_metadataCache.TryGetValue(type, out var metadata))
						{
							if (metadata.Dependencies != null && metadata.Dependencies.Length > 0)
							{
								foreach (var depType in metadata.Dependencies)
								{
									string depCsvPath = Path.Combine(_csvRootPath, depType.Name + ". csv");
									if (!File.Exists(depCsvPath))
									{
										errors.Add($"{type.Name} depends on {depType.Name}, but CSV not found!");
									}
								}
							}
						}
					}
					catch (Exception ex)
					{
						errors.Add($"Validation failed for {type.Name}: {ex.Message}");
					}
				});
			});

			sw.Stop();

			// 输出错误和警告
			foreach (var error in errors)
			{
				ALog.Error($"[AsakiConfig] ❌ {error}");
			}

			foreach (var warning in warnings)
			{
				ALog.Warn($"[AsakiConfig] ⚠️ {warning}");
			}

			// 输出验证结果
			if (errors.Count > 0)
			{
				ALog.Error($"[AsakiConfig] ❌ Validation completed with {errors.Count} errors and {warnings.Count} warnings in {sw.ElapsedMilliseconds}ms.");
			}
			else if (warnings.Count > 0)
			{
				ALog.Warn($"[AsakiConfig] ⚠️ Validation completed with {warnings.Count} warnings in {sw.ElapsedMilliseconds}ms.");
			}
			else
			{
				ALog.Info($"[AsakiConfig] ✅ All {allTypes.Count} configs validated successfully in {sw.ElapsedMilliseconds}ms.");
			}
		}
		
		private ConfigMetadata GetMetadata(Type configType)
		{
			// 只从缓存字典读取，未注册则返回默认策略
			return _metadataCache.TryGetValue(configType, out var metadata) 
				? metadata 
				: new ConfigMetadata
				{
					ConfigType = configType,
					Strategy = AsakiConfigLoadStrategy.OnDemand,
					Priority = 0,
					Unloadable = true,
					Dependencies = Array.Empty<Type>()
				};
		}
	}
}
