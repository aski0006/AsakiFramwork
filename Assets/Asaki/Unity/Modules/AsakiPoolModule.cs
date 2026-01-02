using Asaki.Core;
using Asaki.Core.Broker;
using Asaki.Core.Context;
using Asaki.Core.Coroutines;
using Asaki.Core.Pooling;
using Asaki.Core.Resources;
using Asaki.Unity.Services.Logging;
using Asaki.Unity.Services.Resources;
using System.Threading.Tasks;
using UnityEngine;

// 引用 Pooling 的命名空间

namespace Asaki.Unity.Modules
{
	// 优先级 150：必须在 Coroutines(100) 之后，但在 Resources(200) 之前
	// 因为 Resources 加载资源时可能需要从池中生成对象
	[AsakiModule(150,
		typeof(AsakiRoutineModule),
		typeof(AsakiResourcesModule),
		typeof(AsakiEventBusModule))]
	public class AsakiPoolModule : IAsakiModule
	{
		private IAsakiPoolService _poolService;
		public void OnInit()
		{
			// 1. 获取依赖
			IAsakiCoroutineService routine = AsakiContext.Get<IAsakiCoroutineService>();
			IAsakiResourceService resource = AsakiContext.Get<IAsakiResourceService>();
			IAsakiEventService eventService = AsakiContext.Get<IAsakiEventService>();
			_poolService = new AsakiPoolService(routine, resource, eventService);

			AsakiContext.Register(_poolService);

			ALog.Info("[Asaki] Pooling Service initialized (Async-Native Mode).");
		}

		public Task OnInitAsync()
		{
			return Task.CompletedTask;
		}

		public void OnDispose() { }
	}
}
