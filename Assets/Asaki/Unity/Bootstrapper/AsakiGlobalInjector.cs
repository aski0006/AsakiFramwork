using Asaki.Core.Context;
using Asaki.Core.Logging;
using System.Collections.Generic;

namespace Asaki.Unity.Bootstrapper
{
	/// <summary>
	/// [Asaki V5] 全局注入总线
	/// <para>负责聚合所有程序集生成的注入器。</para>
	/// </summary>
	public static class AsakiGlobalInjector
	{
		// 持有所有已注册的注入器 (Framework, Game, DLCs...)
		private static readonly List<IAsakiInjector> _injectors = new List<IAsakiInjector>();

		/// <summary>
		/// [由生成的代码调用] 注册一个新的程序集注入器
		/// </summary>
		public static void Register(IAsakiInjector injector)
		{
			if (injector != null && !_injectors.Contains(injector))
			{
				_injectors.Add(injector);
				ALog.Trace($"[Asaki] Registered injector: {injector.GetType().Name}");
			}
		}

		/// <summary>
		/// [核心入口] 对目标对象执行全量注入
		/// </summary>
		public static void Inject(object target)
		{
			if (target == null) return;

			// 遍历所有注入器。
			// 这种设计非常高效，因为通常整个游戏也就 2-3 个程序集 (Core, Unity, Game)
			// 每个 Injector 内部都是 O(1) 的类型检查，如果类型不匹配会立即 return。
			foreach (var injector in _injectors)
			{
				injector.Inject(target);
			}
		}
	}
}
