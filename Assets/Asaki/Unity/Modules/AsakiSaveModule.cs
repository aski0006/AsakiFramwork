using Asaki.Core.Attributes;
using Asaki.Core.Broker;
using Asaki.Core.Context;
using Asaki.Core.Serialization;
using Asaki.Unity.Services.Serialization;
using System.Threading.Tasks;

namespace Asaki.Unity.Modules
{
	[AsakiModule(175,
		typeof(AsakiEventBusModule))]
	public class AsakiSaveModule : IAsakiModule
	{
		private IAsakiSaveService _asakiSaveService;
		private IAsakiEventService eventService;

		[AsakiInject]
		public void Init(IAsakiEventService eventService)
		{
			this.eventService = eventService;
		}

		public void OnInit()
		{
			_asakiSaveService = new AsakiSaveService(eventService);
			_asakiSaveService.OnInit();
			AsakiContext.Register(_asakiSaveService);
		}
		public async Task OnInitAsync()
		{
			await _asakiSaveService.OnInitAsync();
		}
		public void OnDispose()
		{
			_asakiSaveService.OnDispose();
		}
	}
}
