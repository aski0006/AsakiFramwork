using Asaki.Core;
using Asaki.Core.Broker;
using Asaki.Core.Configuration;
using Asaki.Core.Context;
using Asaki.Unity.Services.Configuration;
using System.Threading.Tasks;

namespace Asaki.Unity.Modules
{
	[AsakiModule(150, typeof(AsakiEventBusModule))]
	public class AsakiConfigModule : IAsakiModule
	{
		private IAsakiConfigService _configService;
		public void OnInit()
		{
			IAsakiEventService eventService = AsakiContext.Get<IAsakiEventService>();
			_configService = new AsakiConfigService(eventService);
			AsakiContext.Register(_configService);
			_configService.OnInit();
		}
		public async Task OnInitAsync()
		{
			await _configService.OnInitAsync();
		}
		public void OnDispose()
		{
			_configService.OnDispose();
		}
	}


}
