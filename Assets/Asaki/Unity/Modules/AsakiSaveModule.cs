using Asaki.Core;
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
		private AsakiSaveService _asakiSaveService;
		public void OnInit()
		{
			IAsakiEventService eventService = AsakiContext.Get<IAsakiEventService>();
			_asakiSaveService = new AsakiSaveService(eventService);
			_asakiSaveService.OnInit();
			AsakiContext.Register<IAsakiSaveService>(_asakiSaveService);
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
