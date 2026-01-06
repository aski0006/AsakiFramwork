using Asaki.Core;
using Asaki.Core.Attributes;
using Asaki.Core.Context;
using Asaki.Core.Simulation;
using Asaki.Core.Time;
using Asaki.Unity.Services.Time;
using System.Threading.Tasks;

namespace Asaki.Unity.Modules
{
	[AsakiModule(priority: 200, typeof(AsakiSimulationModule))]
	public class AsakiTimeModule : IAsakiModule
	{
		private IAsakiTimerService _asakiTimerService;
		private IAsakiSimulationService simulation;
		
		[AsakiInject]
		public void Init(IAsakiSimulationService simulation)
		{
			this.simulation = simulation;
		}
		public void OnInit()
		{
			_asakiTimerService = new AsakiTimerService();
			simulation.Register(_asakiTimerService);

		}
		public Task OnInitAsync()
		{
			return Task.CompletedTask;
		}
		public void OnDispose()
		{
			simulation?.Unregister(_asakiTimerService);
			_asakiTimerService.Dispose();
			simulation = null;
			_asakiTimerService = null;
		}
	}
}
