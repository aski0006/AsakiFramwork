using Asaki.Core;
using Asaki.Core.Context;
using Asaki.Core.Logging;
using Asaki.Unity.Configuration;
using Asaki.Unity.Services.Logging;
using System.Threading.Tasks;

namespace Asaki.Unity.Modules
{
	[AsakiModule(priority: 25)]
	public class AsakiLogModule : IAsakiModule
	{
		private IAsakiLoggingService _service;
		
		public void OnInit()
		{
			// 1. 获取全局配置
			var config = AsakiContext.Get<AsakiConfig>();
			var logConfig = config != null ? config.LogConfig : new AsakiLogConfig(); // 默认回滚

			_service = AsakiContext.Get<IAsakiLoggingService>();

			if (_service == null)
			{
				// 2. 注入配置到 Service (构造函数或属性注入)
				// 这里我们修改 Service 构造函数来接收配置
				var impl = new AsakiLoggingService(logConfig);
				_service = impl;
				AsakiContext.Register<IAsakiLoggingService>(_service);
				impl.InitializeEarly();
			}

			if (_service is AsakiLoggingService concreteService)
			{
				concreteService.InitializeFull();
			}
		}
		
		public Task OnInitAsync()
		{
			// 现在可以安全地使用 ALog 了
			ALog.Info("AsakiLogModule fully initialized - Writer thread active");
			return Task.CompletedTask;
		}
		
		public void OnDispose()
		{
			_service?.Dispose();
		}
	}
}
