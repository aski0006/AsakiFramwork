using Asaki.Core.Context;
using System.Threading;
using System.Threading.Tasks;

namespace Asaki.Core.UI
{
	public interface IAsakiUIService : IAsakiModule
	{
		Task<T> OpenAsync<T>(int uiId, object args = null, CancellationToken token = default(CancellationToken))
			where T : class, IAsakiWindow;

		void Close<T>() where T : IAsakiWindow;
		void Close(IAsakiWindow window);
		void Back();
	}
}
