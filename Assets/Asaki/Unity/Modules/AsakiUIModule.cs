using Asaki.Core;
using Asaki.Core.Broker;
using Asaki.Core.Context;
using Asaki.Core.Pooling;
using Asaki.Core.Resources;
using Asaki.Core.UI;
using Asaki.Unity.Configuration;
using Asaki.Unity.Services.UI;
using System.Threading.Tasks;

namespace Asaki.Unity.Modules
{
	// 优先级 300，依赖 Resources 加载预制体
	[AsakiModule(225,
		typeof(AsakiResourcesModule),
		typeof(AsakiPoolModule),
		typeof(AsakiEventBusModule),
		typeof(AsakiSimulationModule))]
	public class AsakiUIModule : IAsakiModule
	{
		private AsakiUIManageService _uiManageService;

		public void OnInit()
		{
			AsakiConfig config = AsakiContext.Get<AsakiConfig>();
			IAsakiEventService eventService = AsakiContext.Get<IAsakiEventService>();
			IAsakiResourceService resourceService = AsakiContext.Get<IAsakiResourceService>();
			IAsakiPoolService poolService = AsakiContext.Get<IAsakiPoolService>();
			// 如果没配置 UI，直接跳过
			if (!config) return;

			_uiManageService = new AsakiUIManageService(
				config.UIConfig,
				config.UIConfig.ReferenceResolution,
				config.UIConfig.MatchWidthOrHeight,
				eventService,
				resourceService,
				poolService
			);

			// 内部 OnInit 会调用 Resources 接口，此时 Resources 已注册
			_uiManageService.OnInit();

			AsakiContext.Register<IAsakiUIService>(_uiManageService);
		}

		public async Task OnInitAsync()
		{
			if (_uiManageService != null)
			{
				await _uiManageService.OnInitAsync();
			}
		}

		public void OnDispose() { }
	}
}
