using Asaki.Core.Context;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Asaki.Core.Serialization
{
	public interface IAsakiSaveService : IAsakiModule
	{
		// 基于 Slot 的异步保存
		Task SaveSlotAsync<TMeta, TData>(int slotId, TMeta meta, TData data)
			where TMeta : IAsakiSlotMeta where TData : IAsakiSavable;

		// 基于 Slot 的异步加载
		Task<(TMeta Meta, TData Data)> LoadSlotAsync<TMeta, TData>(int slotId)
			where TMeta : IAsakiSlotMeta, new() where TData : IAsakiSavable, new();

		// 存档工具 API
		List<int> GetUsedSlots();
		bool DeleteSlot(int slotId);
		bool SlotExists(int slotId);
	}

}
