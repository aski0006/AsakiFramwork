using Asaki.Core;
using Asaki.Core.Broker;
using Asaki.Core.Context;
using Asaki.Core.Pooling;
using Asaki.Core.Resources;
using Asaki.Core.UI;
using Asaki.Unity.Configuration;
using Asaki.Unity.Services.UI;
using System.Threading.Tasks;

namespace Asaki.Unity.Bootstrapper.Modules
{
	// 优先级 300，依赖 Resources 加载预制体
	[AsakiModule(225,
		typeof(AsakiResourcesModule),
		typeof(AsakiPoolModule),
		typeof(AsakiEventBusModule))]
	public class AsakiUIModule : IAsakiModule
	{
		private AsakiUIManager _uiManager;

		public void OnInit()
		{
			AsakiConfig config = AsakiContext.Get<AsakiConfig>();
			IAsakiEventService eventService = AsakiContext.Get<IAsakiEventService>();
			IAsakiResService resService = AsakiContext.Get<IAsakiResService>();
			IAsakiPoolService poolService = AsakiContext.Get<IAsakiPoolService>();
			// 如果没配置 UI，直接跳过
			if (!config || !config.UIConfig) return;

			_uiManager = new AsakiUIManager(
				config.UIConfig,
				config.ReferenceResolution,
				config.MatchWidthOrHeight,
				eventService,
				resService,
				poolService
			);

			// 内部 OnInit 会调用 Resources 接口，此时 Resources 已注册
			_uiManager.OnInit();

			AsakiContext.Register<IAsakiUIService>(_uiManager);
		}

		public async Task OnInitAsync()
		{
			if (_uiManager != null)
			{
				await _uiManager.OnInitAsync();
			}
		}

		public void OnDispose() { }
	}
}
