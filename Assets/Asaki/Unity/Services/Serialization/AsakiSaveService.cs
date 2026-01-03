using Asaki.Core.Broker;
using Asaki.Core.Logging;
using Asaki.Core.Serialization;
using Asaki.Unity.Utils;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using AsakiBroker = Asaki.Core.Broker.AsakiBroker;

namespace Asaki.Unity.Services.Serialization
{
	public class AsakiSaveService : IAsakiSaveService
	{
		private string _rootPath;
		private bool _isDebug;
		private IAsakiEventService _eventService;
		public AsakiSaveService(IAsakiEventService eventService)
		{
			_eventService = eventService;
		}

		public void OnInit()
		{
			_rootPath = Path.Combine(Application.persistentDataPath, "Saves");
			_isDebug = Application.isEditor || Debug.isDebugBuild;
			if (!Directory.Exists(_rootPath)) Directory.CreateDirectory(_rootPath);
		}

		public Task OnInitAsync()
		{
			return Task.CompletedTask;
		}
		public void OnDispose() { }

		// =========================================================
		// 路径策略 (Encapsulation)
		// =========================================================
		private string GetSlotDir(int id)
		{
			return Path.Combine(_rootPath, $"Slot_{id}");
		}
		private string GetDataPath(int id)
		{
			return Path.Combine(GetSlotDir(id), "data.bin");
		}
		private string GetMetaPath(int id)
		{
			return Path.Combine(GetSlotDir(id), "meta.json");
		}

		// =========================================================
		// 核心 Slot 逻辑
		// =========================================================

		public async Task SaveSlotAsync<TMeta, TData>(int slotId, TMeta meta, TData data)
			where TMeta : IAsakiSlotMeta where TData : IAsakiSavable
		{
			string dir = GetSlotDir(slotId);
			if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

			// 自动填充基础元数据
			meta.SlotId = slotId;
			meta.LastSaveTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

			_eventService.Publish(new AsakiSaveBeginEvent { Filename = $"Slot_{slotId}" });

			try
			{
				// Step 1: [主线程] 内存快照 (Snapshot)
				// 必须在主线程执行 Serialize，防止访问 UnityEngine 对象或数据在写入时被逻辑修改
				byte[] dataBuffer;
				using (MemoryStream ms = new MemoryStream())
				{
					AsakiBinaryWriter writer = new AsakiBinaryWriter(ms);
					data.Serialize(writer);
					dataBuffer = ms.ToArray();
				}

				// Step 2: [后台线程] 异步 IO
				#if ASAKI_USE_UNITASK
				await UniTask.SwitchToThreadPool();
				#endif
				await File.WriteAllBytesAsync(GetDataPath(slotId), dataBuffer);

				// Meta 数据的持久化 (JSON 方便外部查阅)
				if (_isDebug)
				{
					// 复用 AsakiStringBuilderPool 减少 GC
					StringBuilder sb = AsakiStringBuilderPool.Rent();
					try
					{
						AsakiJsonWriter jsonWriter = new AsakiJsonWriter(sb);
						meta.Serialize(jsonWriter);
						await File.WriteAllTextAsync(GetMetaPath(slotId), jsonWriter.GetResult());
					}
					finally
					{
						AsakiStringBuilderPool.Return(sb);
					}
				}

				#if ASAKI_USE_UNITASK
				await UniTask.SwitchToMainThread();
				#endif
				_eventService.Publish(new AsakiSaveSuccessEvent { Filename = $"Slot_{slotId}" });
			}
			catch (Exception ex)
			{
				ALog.Error($"[AsakiSave] Slot {slotId} Save Failed: {ex.Message}", ex);
				_eventService.Publish(new AsakiSaveFailedEvent { Filename = $"Slot_{slotId}", ErrorMessage = ex.Message });
				throw;
			}
		}

		public async Task<(TMeta Meta, TData Data)> LoadSlotAsync<TMeta, TData>(int slotId)
			where TMeta : IAsakiSlotMeta, new() where TData : IAsakiSavable, new()
		{
			if (!SlotExists(slotId)) throw new FileNotFoundException($"Slot {slotId} not found.");

			try
			{
				// Step 1: [后台线程] 并行读取
				#if ASAKI_USE_UNITASK
				await UniTask.SwitchToThreadPool();
				#endif
        
				// 并行读取 IO，进一步提升速度
				var dataTask = File.ReadAllBytesAsync(GetDataPath(slotId));
				var metaTask = File.ReadAllTextAsync(GetMetaPath(slotId));
				await Task.WhenAll(dataTask, metaTask);
        
				byte[] dataBuffer = dataTask.Result;
				string metaJson = metaTask.Result;

				#if ASAKI_USE_UNITASK
				await UniTask.SwitchToMainThread();
				#endif

				// Step 2: [主线程] 反序列化 Data (Binary)
				TData data = new TData();
				using (MemoryStream ms = new MemoryStream(dataBuffer))
				{
					AsakiBinaryReader reader = new AsakiBinaryReader(ms);
					data.Deserialize(reader);
				}

				// Step 3: [主线程] 反序列化 Meta (JSON) - [已修复]
				TMeta meta = new TMeta();
				// 利用你实现的 FromJson 静态方法
				AsakiJsonReader jsonReader = AsakiJsonReader.FromJson(metaJson);
				// 因为 Meta 本身就是一个 Object，我们需要让 Reader 认为它处于 Root 上下文
				// 你的 Generated Code 可能会调用 BeginObject("GameSlotMeta")
				// 但 AsakiTinyJsonParser 解析出的 Root 是 Dictionary
				// 所以直接调用 Deserialize 即可，Reader 的 GetValue 会在 Root 字典中查找
				meta.Deserialize(jsonReader);

				return (meta, data);
			}
			catch (Exception ex)
			{
				ALog.Error($"[AsakiSave] Slot {slotId} Load Failed: {ex.Message}", ex);
				throw;
			}
		}

		// =========================================================
		// Slot 管理工具
		// =========================================================

		public List<int> GetUsedSlots()
		{
			if (!Directory.Exists(_rootPath)) return new List<int>();
			return Directory.GetDirectories(_rootPath, "Slot_*")
			                .Select(d => Path.GetFileName(d).Replace("Slot_", ""))
			                .Where(s => int.TryParse(s, out _))
			                .Select(int.Parse)
			                .ToList();
		}

		public bool SlotExists(int slotId)
		{
			return File.Exists(GetDataPath(slotId));
		}

		public bool DeleteSlot(int slotId)
		{
			string dir = GetSlotDir(slotId);
			if (Directory.Exists(dir))
			{
				Directory.Delete(dir, true);
				return true;
			}
			return false;
		}
	}
}
