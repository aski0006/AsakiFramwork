using Asaki.Core.Context;

namespace Asaki.Core.Broker
{
	/// <summary>
	/// [Facade] 静态入口外观
	/// 负责连接静态调用 (Roslyn) 与 动态实例 (EventBus)。
	/// </summary>
	public static class AsakiBroker
	{

		// 懒加载获取 Bus
		private static IAsakiEventService GetOrRegisterBus()
		{
			// 1. 尝试获取现有实例
			if (AsakiContext.TryGet<IAsakiEventService>(out IAsakiEventService bus))
			{
				return bus;
			}

			// 2. [Auto-Fix] 如果没找到，主动创建一个并注册！
			// 这解决了脚本执行顺序导致的 "Subscribe too early" 问题
			// 确保总线在第一次使用时必定存在。

			// 注意：使用 double-check locking 防止并发创建 (虽然 Unity 主线程通常没这个问题)
			return AsakiContext.GetOrRegister<IAsakiEventService>(() => new AsakiEventService());
		}

		public static void Publish<T>(T e) where T : IAsakiEvent
		{
			GetOrRegisterBus().Publish(e);
		}

		public static void Subscribe<T>(IAsakiHandler<T> handler) where T : IAsakiEvent
		{
			GetOrRegisterBus().Subscribe(handler);
		}

		public static void Unsubscribe<T>(IAsakiHandler<T> handler) where T : IAsakiEvent
		{
			// Unsubscribe 时如果 Bus 不存在，那肯定没订阅过，直接忽略即可
			if (AsakiContext.TryGet<IAsakiEventService>(out IAsakiEventService bus))
			{
				bus.Unsubscribe(handler);
			}
		}
	}

	public static class AsakiBrokerExtensions
	{
		public static void AsakiRegister<T>(this IAsakiHandler<T> handler) where T : IAsakiEvent
		{
			AsakiBroker.Subscribe(handler);
		}

		public static void AsakiUnregister<T>(this IAsakiHandler<T> handler) where T : IAsakiEvent
		{
			AsakiBroker.Unsubscribe(handler);
		}
	}
}
