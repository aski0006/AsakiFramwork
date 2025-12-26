using Asaki.Core.Broker;
using System;

namespace Asaki.Core.Configuration
{
	public struct AsakiConfigReloadedEvent : IAsakiEvent
	{
		public Type ConfigType { get; set; }
	}
}
