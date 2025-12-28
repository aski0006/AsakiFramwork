using System.Threading.Tasks;
using Asaki.Core;
using Asaki.Core.Context;
using Asaki.Core.Logging;
using Asaki.Unity.Configuration;
using Asaki.Unity.Services.Logging;

namespace Asaki.Unity.Modules
{
	[AsakiModule(priority: -100)] // 极高优先级，确保在业务模块之前运行
	public class AsakiLogModule : IAsakiModule
	{
		private IAsakiLoggingService _service;

		public void OnInit()
		{
			// 策略：检查是否已经有服务存在 (通常由 Bootstrapper 创建)
			// 如果没有 (例如单元测试环境或独立场景)，则创建一个。
			if (!AsakiContext.TryGet(out _service))
			{
				_service = new AsakiLoggingService();
				AsakiContext.Register<IAsakiLoggingService>(_service);
                
				// 这里我们不需要显式调用 ALog.Info，因为 ALog 内部会自动找到这个新注册的服务
			}

			// 应用配置
			// LogModule 的核心职责之一就是将 Config 映射到 Service
			if (AsakiContext.TryGet(out AsakiConfig config))
			{
				_service.SetLevel(config.LogConfig.MinLogLevel);
			}
		}

		public Task OnInitAsync()
		{
			return Task.CompletedTask;
		}

		public void OnDispose()
		{
			// 模块销毁时，如果该服务是由模块创建的，理论上应该负责销毁。
			// 但考虑到日志是基础设施，通常由 Context.ClearAll() 统一触发 Dispose，
			// 这里只需释放引用即可。
			_service = null;
		}
	}
}
