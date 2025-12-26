using Asaki.Core.Broker;

namespace Asaki.Core.Serialization
{
	public struct AsakiSaveBeginEvent : IAsakiEvent
	{
		public string Filename;
	}

	// 保存成功
	public struct AsakiSaveSuccessEvent : IAsakiEvent
	{
		public string Filename;
	}

	// 保存失败（用于弹出错误提示）
	public struct AsakiSaveFailedEvent : IAsakiEvent
	{
		public string Filename;
		public string ErrorMessage;
	}
}
