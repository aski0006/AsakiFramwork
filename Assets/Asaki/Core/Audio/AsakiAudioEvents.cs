using Asaki.Core.Broker;

namespace Asaki.Core.Audio
{
	public struct AsakiPlayAudioEvent : IAsakiEvent
	{
		public AsakiAudioParams Params;
		public AsakiAudioHandle OutputHandle; // 用于回传句柄 (如果同步调用)
	}

	public struct AsakiStopAudioEvent : IAsakiEvent
	{
		public AsakiAudioHandle Handle;
		public float FadeOutDuration;
	}

	public struct AsakiAudioFinishedEvent : IAsakiEvent
	{
		public AsakiAudioHandle Handle;
		public int AssetId;
	}
}
