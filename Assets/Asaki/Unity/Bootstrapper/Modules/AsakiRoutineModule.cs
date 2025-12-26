using Asaki.Core;
using Asaki.Core.Context;
using Asaki.Core.Coroutines;
using System.Threading.Tasks;

namespace Asaki.Unity.Bootstrapper.Modules
{
	[AsakiModule(100)]
	public class AsakiRoutineModule : IAsakiModule
	{
		private Services.Coroutines.AsakiCoroutineProvider _provider;
		public void OnInit()
		{
			// 1. 创建具体服务实现
			_provider = new Services.Coroutines.AsakiCoroutineProvider();

			// 2. 注册服务接口 (供其他模块通过 Get<IAsakiCoroutineService> 获取)
			AsakiContext.Register<IAsakiCoroutineService>(_provider);
		}
		public Task OnInitAsync()
		{
			// Coroutines 服务本身无需异步初始化
			return Task.CompletedTask;
		}

		public void OnDispose() { }
	}
}
