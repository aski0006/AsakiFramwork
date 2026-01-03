using Asaki.Core;
using Asaki.Core.Broker;
using Asaki.Core.Context;
using Asaki.Core.Async;
using Asaki.Core.Network;
using Asaki.Unity.Services.Network;
using System.Threading.Tasks;

namespace Asaki.Unity.Modules
{
	[AsakiModule(125,
		typeof(AsakiEventBusModule),
		typeof(AsakiRoutineModule))]
	public class AsakiDownloadModule : IAsakiModule
	{
		private AsakiDownloadService _asakiDownloadService;
		public void OnInit()
		{
			IAsakiEventService eventsService = AsakiContext.Get<IAsakiEventService>();
			IAsakiAsyncService asyncService = AsakiContext.Get<IAsakiAsyncService>();
			_asakiDownloadService = new AsakiDownloadService(
				asyncService,
				eventsService
			);
			AsakiContext.Register<IAsakiDownloadService>(_asakiDownloadService);
		}
		public Task OnInitAsync()
		{
			return Task.CompletedTask;
		}
		public void OnDispose() { }
	}
}
