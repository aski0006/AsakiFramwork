using Asaki.Core;
using Asaki.Core.Context;
using Asaki.Core.Simulation;
using Asaki.Unity.Bridge;
using System.Threading.Tasks;
using UnityEngine;

namespace Asaki.Unity.Modules
{
	[AsakiModule(priority: 10)]
	public class AsakiSimulationModule : IAsakiModule
	{
		private AsakiSimulationManager _manager;
		private GameObject _driverGo;


		public void OnInit()
		{
			_manager = new AsakiSimulationManager();
			AsakiContext.Register(_manager);
			_driverGo = new GameObject("[Asaki.Driver]");
			Object.DontDestroyOnLoad(_driverGo);
			var driver = _driverGo.AddComponent<AsakiMonoDriver>();
			driver.Initialize(_manager);
		}
		public Task OnInitAsync()
		{
			return Task.CompletedTask;
		}
		public void OnDispose()
		{
			if (_driverGo != null)
			{
				Object.Destroy(_driverGo);
				_driverGo = null;
			}
            
			_manager = null;
		}
	}
}
