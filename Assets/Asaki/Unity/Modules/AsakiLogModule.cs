using System.Threading.Tasks;
using Asaki.Core;
using Asaki.Core.Configs;
using Asaki.Core.Context;
using Asaki.Core.Logging;
using Asaki.Unity.Services.Logging;

namespace Asaki.Unity.Modules
{
	[AsakiModule(priority: -100)] // 极高优先级，确保在业务模块之前运行
	public class AsakiLogModule : IAsakiModule
	{
		private IAsakiLoggingService _service;

		public void OnInit()
		{
			if (!AsakiContext.TryGet(out IAsakiLoggingService iService))
			{
				_service = new AsakiLoggingService();
				AsakiContext.Register<IAsakiLoggingService>(_service);
			}
			else
			{
				_service = iService as AsakiLoggingService;
			}
			if (_service != null && AsakiContext.TryGet(out AsakiConfig config))
			{
				// [关键] 激活配置
				if (config.LogConfig != null)
				{
					_service.ApplyConfig(config.LogConfig);
				}
			}
		}

		public Task OnInitAsync()
		{
			return Task.CompletedTask;
		}

		public void OnDispose()
		{
			_service = null;
		}
	}
}
