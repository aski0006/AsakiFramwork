using Asaki.Core;
using Asaki.Core.Broker;
using Asaki.Core.Context;
using Asaki.Core.Async;
using Asaki.Core.Attributes;
using Asaki.Core.Resources;
using Asaki.Core.Scene;
using Asaki.Unity.Services.Scene;
using System.Threading.Tasks;

namespace Asaki.Unity.Modules
{
	[AsakiModule(200,
		typeof(AsakiEventBusModule),
		typeof(AsakiAsyncModule),
		typeof(AsakiResourcesModule))]
	public class AsakiSceneModule : IAsakiModule
	{
		private IAsakiSceneService _asakiSceneService;
		private IAsakiEventService eventService;
		private IAsakiResourceService resService;
		private IAsakiAsyncService asyncService;

		[AsakiInject]
		public void Init(IAsakiEventService eventService, IAsakiResourceService resService, IAsakiAsyncService asyncService)
		{
			this.eventService = eventService;
			this.resService = resService;
			this.asyncService = asyncService;
		}
		public void OnInit()
		{

			_asakiSceneService = new AsakiSceneService(
				eventService,
				asyncService,
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
