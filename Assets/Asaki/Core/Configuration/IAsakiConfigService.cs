using Asaki.Core.Context;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Asaki.Core.Configuration
{
	public interface IAsakiConfigService : IAsakiModule
	{
		Task LoadAllAsync();
		T Get<T>(int id) where T : class, IAsakiConfig, new();
		IReadOnlyList<T> GetAll<T>() where T : class, IAsakiConfig, new();
		Task ReloadAsync<T>() where T : class, IAsakiConfig, new();
	}
}
