using Asaki.Core;
using Asaki.Core.Context;
using Asaki.Core.Network;
using Asaki.Unity.Configuration;
using Asaki.Unity.Services.Network;
using System.Threading.Tasks;

namespace Asaki.Unity.Bootstrapper.Modules
{
	[AsakiModule(priority: 100)]
	public class AsakiWebModule : IAsakiModule
	{

		private AsakiWebService _asakiWebService;

		public void OnInit()
		{
			AsakiConfig asakiConfig = AsakiContext.Get<AsakiConfig>();
			_asakiWebService = new AsakiWebService();
			_asakiWebService.OnInit();
			_asakiWebService.SetBaseUrl(asakiConfig.BaseUrt);
			_asakiWebService.SetTimeout(asakiConfig.WebTimeoutSeconds);
			AsakiContext.Register<IAsakiWebService>(_asakiWebService);
		}
		public async Task OnInitAsync()
		{
			await _asakiWebService.OnInitAsync();
		}
		public void OnDispose()
		{
			_asakiWebService?.OnDispose();
		}
	}
}
