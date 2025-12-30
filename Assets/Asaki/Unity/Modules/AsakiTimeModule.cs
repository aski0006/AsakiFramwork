using Asaki.Core;
using Asaki.Core.Context;
using Asaki.Core.Simulation;
using Asaki.Core.Time;
using Asaki.Unity.Services.Time;
using System.Threading.Tasks;

namespace Asaki.Unity.Modules
{
	[AsakiModule(priority: 200)]
	public class AsakiTimeModule : IAsakiModule
	{
		private IAsakiTimerService _asakiTimerService;

		public void OnInit()
		{
			var simulation = AsakiContext.Get<AsakiSimulationManager>();
			_asakiTimerService = new AsakiTimerService();
			simulation.Register(_asakiTimerService);

		}
		public Task OnInitAsync()
		{
			return Task.CompletedTask;
		}
		public void OnDispose()
		{
			var simulation = AsakiContext.Get<AsakiSimulationManager>();
			simulation?.Unregister(_asakiTimerService);
			_asakiTimerService.Dispose();
		}
	}
}
