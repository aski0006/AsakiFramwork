namespace Asaki.Core.Serialization
{
	public interface IAsakiSlotMeta : IAsakiSavable
	{
		int SlotId { get; set; }
		long LastSaveTime { get; set; }
		string SaveName { get; set; }
	}
}
