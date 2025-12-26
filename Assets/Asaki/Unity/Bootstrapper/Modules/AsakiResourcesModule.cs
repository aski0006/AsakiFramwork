using Asaki.Core;
using Asaki.Core.Broker;
using Asaki.Core.Context;
using Asaki.Core.Coroutines;
using Asaki.Core.Resources;
using Asaki.Unity.Configuration;
using Asaki.Unity.Services.Resources;
using System.Threading.Tasks;
using UnityEngine;

namespace Asaki.Unity.Bootstrapper.Modules
{
	// 优先级 200，且显式依赖 RoutineModule
	[AsakiModule(125,
		typeof(AsakiRoutineModule),
		typeof(AsakiEventBusModule))]
	public class AsakiResourcesModule : IAsakiModule
	{
		private IAsakiResService _resService;

		public void OnInit()
		{
			AsakiConfig config = AsakiContext.Get<AsakiConfig>();
			IAsakiCoroutineService routine = AsakiContext.Get<IAsakiCoroutineService>();
			IAsakiEventService eventService = AsakiContext.Get<IAsakiEventService>();
			// 2. 创建工厂
			AsakiResKitMode mode = config ? config.AsakiResKitMode : AsakiResKitMode.Resources;
			int timeoutSeconds = config.ResourcesTimeoutSeconds;
			_resService = AsakiResKitFactory.Create(mode, routine, eventService);
			_resService.SetTimeoutSeconds(timeoutSeconds);
			// 3. 注册服务
			AsakiContext.Register(_resService);

			Debug.Log($"[Asaki] Resources initialized in {mode} mode.");
		}

		public async Task OnInitAsync()
		{
			// 4. 执行异步初始化 (加载 Manifest 等)
			if (_resService != null)
			{
				await _resService.OnInitAsync();
			}
		}

		public void OnDispose() { }
	}
}
