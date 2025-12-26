using Asaki.Core.Simulation;
using UnityEngine;

namespace Asaki.Unity.Bridge
{
	public class AsakiMonoDriver : MonoBehaviour
	{
		private AsakiSimulationManager _simManager;

		public void Initialize(AsakiSimulationManager simManager)
		{
			_simManager = simManager;
		}

		private void Update()
		{
			if (_simManager == null) return;

			// [关键] 在这里获取 Unity 的时间，传给 Core
			// 这是整个框架唯一允许读取 Time.deltaTime 的地方
			_simManager.Tick(Time.deltaTime);
		}

		private void FixedUpdate()
		{
			if (_simManager == null) return;

			_simManager.FixedTick(Time.fixedDeltaTime);
		}
	}
}
