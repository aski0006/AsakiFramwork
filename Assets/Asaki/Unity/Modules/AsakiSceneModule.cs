using Asaki.Core;
using Asaki.Core.Broker;
using Asaki.Core.Context;
using Asaki.Core.Coroutines;
using Asaki.Core.Resources;
using Asaki.Core.Scene;
using Asaki.Unity.Services.Scene;
using System.Threading.Tasks;

namespace Asaki.Unity.Modules
{
	[AsakiModule(priority: 200,
		typeof(AsakiEventBusModule),
		typeof(AsakiRoutineModule),
		typeof(AsakiResourcesModule))]
	public class AsakiSceneModule : IAsakiModule
	{
		private IAsakiSceneService _asakiSceneService;
		public void OnInit()
		{
			var eventService = AsakiContext.Get<IAsakiEventService>();
			var resService = AsakiContext.Get<IAsakiResService>();
			var coroutineService = AsakiContext.Get<IAsakiCoroutineService>();
			_asakiSceneService = new AsakiSceneService(
				eventService,
				coroutineService,
				resService);
			_asakiSceneService.PerBuildScene();
			AsakiContext.Register<IAsakiSceneService>(_asakiSceneService);
		}
		public Task OnInitAsync()
		{
			return Task.CompletedTask;
		}
		public void OnDispose()
		{
			_asakiSceneService.Dispose();
		}
	}
}
