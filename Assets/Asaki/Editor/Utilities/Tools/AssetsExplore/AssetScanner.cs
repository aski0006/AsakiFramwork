using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Asaki.Editor.Utilities.Tools.AssetsExplore
{
	/// <summary>
	/// 异步资源扫描器 - 支持多线程、取消、进度报告和智能缓存
	/// </summary>
	public class AssetScanner
	{
		// ========== 公共属性与事件 ==========

		public bool IsScanning { get; private set; }
		public float Progress { get; private set; }
		public string CurrentScanningPath { get; private set; }
		public IReadOnlyDictionary<string, AssetInfo> CachedAssets => _cachedAssets;

		public event Action<AssetInfo> OnAssetFound;
		public event Action OnScanComplete;
		public event Action<string> OnError;

		// ========== 私有字段 ==========

		private CancellationTokenSource _cancellationTokenSource;
		private readonly HashSet<string> _excludedFolders;
		private readonly List<string> _scanPaths;

		// 线程安全的数据结构
		private readonly ConcurrentBag<AssetInfo> _scanResults = new ConcurrentBag<AssetInfo>();
		private readonly SemaphoreSlim _maxConcurrencySemaphore;

		// 缓存机制
		private Dictionary<string, AssetInfo> _cachedAssets = new Dictionary<string, AssetInfo>();
		private readonly string _cacheFilePath;
		private DateTime _lastFullScanTime;
		private const double CACHE_VALID_HOURS = 24.0; // 缓存有效期24小时

		// ========== 构造函数 ==========

		public AssetScanner()
		{
			_excludedFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
			{
				"Library", "Temp", "obj", "bin", "Logs", "Packages",
				"Assets/ThirdParty", "Assets/Plugins", "Assets/Editor",
			};

			_scanPaths = new List<string> { "Assets" };
			_cacheFilePath = Path.Combine(Application.temporaryCachePath, "AssetExplorerCache_v2.json");

			// 根据CPU核心数限制并发数，保留1个核心给主线程
			int maxThreads = Mathf.Max(1, SystemInfo.processorCount - 1);
			_maxConcurrencySemaphore = new SemaphoreSlim(maxThreads, maxThreads);

			LoadCache();
		}

		// ========== 公共方法 ==========

		public void SetExcludedFolders(IEnumerable<string> folders)
		{
			_excludedFolders.Clear();
			foreach (string folder in folders)
			{
				_excludedFolders.Add(folder.Replace("\\", "/").TrimEnd('/'));
			}
		}

		/// <summary>
		/// 排除文件夹集合（供外部访问）
		/// </summary>
		public IReadOnlyCollection<string> ExcludedFolders => _excludedFolders;

		/// <summary>
		/// 检查缓存是否有效
		/// </summary>
		public bool IsCacheValid()
		{
			if (!File.Exists(_cacheFilePath)) return false;
			if (_cachedAssets.Count == 0) return false;

			TimeSpan cacheAge = DateTime.UtcNow - _lastFullScanTime;
			return cacheAge.TotalHours < CACHE_VALID_HOURS;
		}

		/// <summary>
		/// 智能启动扫描（仅在需要时）
		/// </summary>
		public async Task StartScanIfNeededAsync()
		{
			if (IsCacheValid())
			{
				Debug.Log("[AssetExplorer] 使用有效缓存，跳过扫描");
				OnScanComplete?.Invoke();
				return;
			}

			await StartScanAsync(incremental: false);
		}

		/// <summary>
		/// 主扫描方法 - 多线程优化
		/// </summary>
		public async Task StartScanAsync(bool incremental = false)
		{
			if (IsScanning) return;

			IsScanning = true;
			Progress = 0f;
			_scanResults.Clear();

			_cancellationTokenSource = new CancellationTokenSource();
			CancellationToken token = _cancellationTokenSource.Token;

			try
			{
				var allFiles = new List<string>();

				// 第一阶段：收集文件路径（单线程快速收集）
				await Task.Run(() =>
				{
					foreach (string scanPath in _scanPaths)
					{
						if (Directory.Exists(scanPath))
						{
							CollectFiles(scanPath, allFiles, token);
						}
					}
				}, token);

				// 第二阶段：按类型分组并行处理
				int totalCount = allFiles.Count;
				int processedCount = 0;
				var newCache = new Dictionary<string, AssetInfo>();

				// 按扩展名分组，避免线程竞争
				var filesByExtension = allFiles
				                       .GroupBy(f => Path.GetExtension(f).ToLowerInvariant())
				                       .ToDictionary(g => g.Key, g => g.ToList());

				// 创建处理任务
				var processingTasks = filesByExtension.Select(async kvp =>
				{
					var files = kvp.Value;

					await Task.WhenAll(files.Select(async filePath =>
					{
						if (token.IsCancellationRequested) return;

						// 限制并发数
						await _maxConcurrencySemaphore.WaitAsync(token);

						try
						{
							// 增量更新检查
							if (incremental && _cachedAssets.TryGetValue(filePath, out AssetInfo cachedInfo))
							{
								DateTime lastWriteTime = File.GetLastWriteTimeUtc(filePath);
								if (lastWriteTime == cachedInfo.lastModified)
								{
									lock (newCache)
									{
										newCache[filePath] = cachedInfo;
									}
									// 优化：降低回调频率，减少UI刷新
									lock (this)
									{
										OnAssetFound?.Invoke(cachedInfo);
									}
									return;
								}
							}

							AssetInfo assetInfo = await ProcessFileAsync(filePath);
							if (assetInfo != null)
							{
								lock (newCache)
								{
									newCache[filePath] = assetInfo;
								}
								// 优化：降低回调频率
								lock (this)
								{
									OnAssetFound?.Invoke(assetInfo);
								}
							}
						}
						finally
						{
							_maxConcurrencySemaphore.Release();

							// 原子操作更新进度
							int currentCount = Interlocked.Increment(ref processedCount);
							Progress = (float)currentCount / totalCount;
							CurrentScanningPath = Path.GetFileName(filePath);
						}
					}));
				});

				await Task.WhenAll(processingTasks);

				_cachedAssets = newCache;
				_lastFullScanTime = DateTime.UtcNow;
				SaveCache();

				Progress = 1f;
				OnScanComplete?.Invoke();
			}
			catch (OperationCanceledException)
			{
				Debug.Log("[AssetExplorer] 扫描已取消");
			}
			catch (Exception ex)
			{
				OnError?.Invoke($"扫描失败: {ex.Message}\n{ex.StackTrace}");
			}
			finally
			{
				IsScanning = false;
				CurrentScanningPath = "";
			}
		}

		public void CancelScan()
		{
			_cancellationTokenSource?.Cancel();
		}

		// ========== 私有方法 ==========

		/// <summary>
		/// 递归收集文件（支持取消）
		/// </summary>
		private void CollectFiles(string directory, List<string> files, CancellationToken token)
		{
			try
			{
				// 检查取消
				if (token.IsCancellationRequested) return;

				string relativePath = GetRelativePath(directory);
				if (_excludedFolders.Any(excluded =>
					relativePath.StartsWith(excluded, StringComparison.OrdinalIgnoreCase)))
					return;

				// 获取文件
				foreach (string file in Directory.GetFiles(directory))
				{
					if (token.IsCancellationRequested) return;

					string ext = Path.GetExtension(file);
					if (IsValidAssetFile(ext) && !file.EndsWith(".meta"))
					{
						files.Add(file);
					}
				}

				// 递归子目录
				foreach (string subDir in Directory.GetDirectories(directory))
				{
					CollectFiles(subDir, files, token);
				}
			}
			catch (Exception ex)
			{
				OnError?.Invoke($"访问目录失败: {directory}\n{ex.Message}");
			}
		}

		private async Task<AssetInfo> ProcessFileAsync(string filePath)
		{
			await Task.Yield(); // 确保异步

			try
			{
				FileInfo fileInfo = new FileInfo(filePath);
				string relativePath = GetRelativePath(filePath);
				string guid = AssetDatabase.AssetPathToGUID(relativePath);

				if (string.IsNullOrEmpty(guid))
				{
					AssetDatabase.ImportAsset(relativePath, ImportAssetOptions.ForceSynchronousImport);
					guid = AssetDatabase.AssetPathToGUID(relativePath);
				}

				if (string.IsNullOrEmpty(guid))
					return null;

				return new AssetInfo
				{
					guid = guid,
					name = Path.GetFileNameWithoutExtension(filePath),
					path = relativePath,
					extension = Path.GetExtension(filePath).ToLower(),
					fileSize = fileInfo.Length,
					lastModified = fileInfo.LastWriteTimeUtc,
					category = CategoryManager.GetCategory(Path.GetExtension(filePath)),
				};
			}
			catch
			{
				return null;
			}
		}

		private bool IsValidAssetFile(string extension)
		{
			if (string.IsNullOrEmpty(extension)) return false;
			string[] invalidExtensions = new[] { ".meta", ".tmp", ".temp", ".log", ".pid" };
			return !invalidExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
		}

		private string GetRelativePath(string fullPath)
		{
			string projectPath = Directory.GetParent(Application.dataPath).FullName;
			return fullPath.Replace(projectPath + Path.DirectorySeparatorChar, "")
			               .Replace('\\', '/');
		}

		// ========== 缓存持久化 ==========

		#region 缓存持久化

		private void LoadCache()
		{
			if (!File.Exists(_cacheFilePath)) return;

			try
			{
				string json = File.ReadAllText(_cacheFilePath);
				CacheWrapper wrapper = JsonUtility.FromJson<CacheWrapper>(json);
				if (wrapper?.assets != null && wrapper.timestamp > 0)
				{
					_cachedAssets = wrapper.assets.ToDictionary(a => a.path);
					_lastFullScanTime = DateTime.FromFileTimeUtc(wrapper.timestamp);
					Debug.Log($"[AssetExplorer] 已加载 {wrapper.assets.Length} 条缓存记录");
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"[AssetExplorer] 加载缓存失败: {ex.Message}");
				_cachedAssets.Clear();
			}
		}

		private void SaveCache()
		{
			try
			{
				CacheWrapper wrapper = new CacheWrapper
				{
					assets = _cachedAssets.Values.ToArray(),
					timestamp = DateTime.UtcNow.ToFileTimeUtc(),
				};

				string json = JsonUtility.ToJson(wrapper, true);
				Directory.CreateDirectory(Path.GetDirectoryName(_cacheFilePath));
				File.WriteAllText(_cacheFilePath, json);

				Debug.Log($"[AssetExplorer] 已保存 {wrapper.assets.Length} 条缓存记录");
			}
			catch (Exception ex)
			{
				Debug.LogWarning($"[AssetExplorer] 保存缓存失败: {ex.Message}");
			}
		}

		[Serializable]
		private class CacheWrapper
		{
			public AssetInfo[] assets;
			public long timestamp;
		}

		#endregion
	}
}
