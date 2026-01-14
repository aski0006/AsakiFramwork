using Asaki.Core.Attributes;
using Asaki.Core.Broker;
using Asaki.Core.Configs;
using Asaki.Core.Configuration;
using Asaki.Core.Context;
using Asaki.Core.Logging;
using Asaki.Unity.Plugins.Localization;
using System.Threading.Tasks;

namespace Asaki.Unity.Modules
{
	[AsakiModule(400, typeof(AsakiEventBusModule), typeof(AsakiConfigModule))]
	public class AsakiLocalizationModule : IAsakiModule, IAsakiInit<IAsakiEventService, IAsakiConfigService>
	{
		private AsakiLocalizationService _asakiLocalizationService;
		private IAsakiEventService _asakiEventService;
		private IAsakiConfigService _asakiConfigService;

		[AsakiInject]
		public void Init(IAsakiEventService args1, IAsakiConfigService args2)
		{
			_asakiEventService = args1;
			_asakiConfigService = args2;
		}
		public void OnInit()
		{
			AsakiConfig cfg = AsakiContext.Get<AsakiConfig>();
			if (cfg == null)
			{
				ALog.Error("AsakiConfig is null. AsakiLocalization Can not Use !!");
				return;
			}
			_asakiLocalizationService = new AsakiLocalizationService(
				_asakiEventService,
				_asakiConfigService,
				cfg.LocalizationConfig
			);
			_asakiLocalizationService.OnInit();
			AsakiContext.Register(_asakiLocalizationService);
			ALog.Info("AsakiLocalizationModule Initialized.");
		}
		public Task OnInitAsync()
		{
			return Task.CompletedTask;
		}
		public void OnDispose()
		{
			_asakiLocalizationService.Dispose();
		}

	}
}
