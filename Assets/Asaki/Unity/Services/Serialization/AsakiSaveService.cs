using Asaki.Core.Broker;
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
				Debug.LogError($"[AsakiSave] Slot {slotId} Save Failed: {ex.Message}"); // TODO: [Asaki] -> Asaki.ALog.Error
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
				// Step 1: [后台线程] 并行读取二进制和元数据
				#if ASAKI_USE_UNITASK
				await UniTask.SwitchToThreadPool();
				#endif
				byte[] dataBuffer = await File.ReadAllBytesAsync(GetDataPath(slotId));
				string metaJson = await File.ReadAllTextAsync(GetMetaPath(slotId));

				#if ASAKI_USE_UNITASK
				await UniTask.SwitchToMainThread();
				#endif
				// Step 2: [主线程] 反序列化并触发 UI 绑定
				TData data = new TData();
				using (MemoryStream ms = new MemoryStream(dataBuffer))
				{
					AsakiBinaryReader reader = new AsakiBinaryReader(ms);
					data.Deserialize(reader);
				}

				// Meta 加载 (此处示例简化，实际应使用 AsakiJsonReader)
				TMeta meta = new TMeta();
				// 暂时使用简单逻辑或 JsonReader 实现...

				return (meta, data);
			}
			catch (Exception ex)
			{
				Debug.LogError($"[AsakiSave] Slot {slotId} Load Failed: {ex.Message}"); // TODO: [Asaki] -> Asaki.ALog.Error
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
