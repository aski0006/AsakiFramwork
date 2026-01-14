using UnityEngine;

namespace Asaki.Unity.Services.Coroutines
{
	/// <summary>
	/// Asaki异步服务的持久协程运行器
	/// 提供一个在场景切换时保持存活的GameObject，用于运行跨场景的协程操作。
	/// </summary>
	/// <remarks>
	/// 该类是Asaki异步服务的内部实现组件，不应该直接在项目中使用。
	/// 它通过DontDestroyOnLoad确保协程运行器在场景切换时不会被销毁，
	/// 从而保证异步操作能够在整个应用生命周期中持续执行。
	/// </remarks>
	[AddComponentMenu("")] // 不在Unity组件菜单中显示，避免用户直接添加
	internal class AsakiAsyncCoroutineRunner : MonoBehaviour
	{
		/// <summary>
		/// 初始化协程运行器，设置为场景切换时不销毁。
		/// </summary>
		/// <remarks>
		/// 该方法在GameObject被实例化时自动调用，
		/// 通过DontDestroyOnLoad使协程运行器成为持久化对象。
		/// </remarks>
		private void Awake()
		{
			DontDestroyOnLoad(gameObject);
		}
	}
}
