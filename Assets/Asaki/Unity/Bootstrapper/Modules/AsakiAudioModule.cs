using Asaki.Core;
using Asaki.Core.Audio;
using Asaki.Core.Context;
using Asaki.Core.Pooling;
using Asaki.Core.Resources;
using Asaki.Unity.Configuration;
using Asaki.Unity.Services.Audio;
using System.Threading.Tasks;

namespace Asaki.Unity.Bootstrapper.Modules
{
	// 优先级 400，通常依赖 Resources 加载音频
	[AsakiModule(400,
		typeof(AsakiResourcesModule),
		typeof(AsakiPoolModule),
		typeof(AsakiEventBusModule))]
	public class AsakiAudioModule : IAsakiModule
	{
		private IAsakiAudioService _audioService;

		public void OnInit()
		{
			AsakiConfig config = AsakiContext.Get<AsakiConfig>();
			IAsakiResService res = AsakiContext.Get<IAsakiResService>();
			IAsakiPoolService pool = AsakiContext.Get<IAsakiPoolService>();
			if (!config || !config.AsakiAudioConfig) return;

			_audioService = new AsakiAudioService(
				res,
				pool,
				config.AsakiAudioConfig,
				config.SoundAgentPrefabAssetKey,
				config.InitialAudioPoolSize
			);

			_audioService.OnInit();

			AsakiContext.Register(_audioService);
		}

		public async Task OnInitAsync()
		{
			await _audioService.OnInitAsync();
		}

		public void OnDispose()
		{
			_audioService.OnDispose();
		}
	}
}
