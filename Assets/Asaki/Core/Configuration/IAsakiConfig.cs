using Asaki.Core.Serialization;

namespace Asaki.Core.Configuration
{
	public interface IAsakiConfig : IAsakiSavable
	{
		/// <summary>
		/// 配置表主键 ID
		/// </summary>
		int Id { get; }

		void AllowConfigSerialization(string permissionKey);
	}
}
