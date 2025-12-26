namespace Asaki.Core.Serialization
{

	public interface IAsakiSavable
	{
		void Serialize(IAsakiWriter writer);
		void Deserialize(IAsakiReader reader);
	}
}
