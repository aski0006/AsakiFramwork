using Asaki.Core;
using Asaki.Core.Context;
using Asaki.Core.Network;
using Asaki.Unity.Configuration;
using Asaki.Unity.Services.Network;
using System.Threading.Tasks;

namespace Asaki.Unity.Modules
{
	[AsakiModule(priority: 100)]
	public class AsakiWebModule : IAsakiModule
	{

		private AsakiWebService _asakiWebService;

		public void OnInit()
		{
			AsakiConfig asakiConfig = AsakiContext.Get<AsakiConfig>();
			_asakiWebService = new AsakiWebService();
			_asakiWebService.Setup(asakiConfig.AsakiWebConfig);
			AsakiContext.Register<IAsakiWebService>(_asakiWebService);
		}
		public Task OnInitAsync()
		{
			return Task.CompletedTask;
		}
		public void OnDispose()
		{
			_asakiWebService?.OnDispose();
		}
	}
}
