using Asaki.Core;
using Asaki.Core.Broker;
using Asaki.Core.Context;
using Asaki.Core.Async;
using Asaki.Core.Resources;
using Asaki.Unity.Configuration;
using Asaki.Unity.Services.Logging;
using Asaki.Unity.Services.Resources;
using System.Threading.Tasks;
using UnityEngine;

namespace Asaki.Unity.Modules
{
	// 优先级 200，且显式依赖 RoutineModule
	[AsakiModule(125,
		typeof(AsakiRoutineModule),
		typeof(AsakiEventBusModule))]
	public class AsakiResourcesModule : IAsakiModule
	{
		private IAsakiResourceService _resourceService;

		public void OnInit()
		{
			AsakiConfig config = AsakiContext.Get<AsakiConfig>();
			IAsakiAsyncService routine = AsakiContext.Get<IAsakiAsyncService>();
			IAsakiEventService eventService = AsakiContext.Get<IAsakiEventService>();
			// 2. 创建工厂
			AsakiResKitMode mode = config ? config.ResConfig.Mode : AsakiResKitMode.Resources;
			int timeoutSeconds = config.ResConfig.TimeoutSeconds;
			_resourceService = AsakiResKitFactory.Create(mode, routine, eventService);
			_resourceService.SetTimeoutSeconds(timeoutSeconds);
			// 3. 注册服务
			AsakiContext.Register(_resourceService);

			ALog.Info($"[Asaki] Resources initialized in {mode} mode.");
		}

		public async Task OnInitAsync()
		{
			// 4. 执行异步初始化 (加载 Manifest 等)
			if (_resourceService != null)
			{
				await _resourceService.OnInitAsync();
			}
		}

		public void OnDispose() { }
	}
}
