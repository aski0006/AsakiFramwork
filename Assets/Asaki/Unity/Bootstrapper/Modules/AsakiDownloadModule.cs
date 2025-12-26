using Asaki.Core;
using Asaki.Core.Broker;
using Asaki.Core.Context;
using Asaki.Core.Coroutines;
using Asaki.Core.Network;
using Asaki.Unity.Services.Network;
using System.Threading.Tasks;

namespace Asaki.Unity.Bootstrapper.Modules
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
			IAsakiCoroutineService coroutineService = AsakiContext.Get<IAsakiCoroutineService>();
			_asakiDownloadService = new AsakiDownloadService(
				coroutineService,
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
