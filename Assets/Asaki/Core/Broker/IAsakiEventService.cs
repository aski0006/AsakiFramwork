using Asaki.Core.Context;
using System;

namespace Asaki.Core.Broker
{
	public interface IAsakiEventService : IAsakiService, IDisposable
	{
		void Subscribe<T>(IAsakiHandler<T> handler) where T : IAsakiEvent;
		void Unsubscribe<T>(IAsakiHandler<T> handler) where T : IAsakiEvent;
		void Publish<T>(T e) where T : IAsakiEvent;
	}
}
