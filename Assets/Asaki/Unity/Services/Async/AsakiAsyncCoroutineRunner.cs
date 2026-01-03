using UnityEngine;

namespace Asaki.Unity.Services.Coroutines
{
	[AddComponentMenu("")]
	internal class AsakiAsyncCoroutineRunner : MonoBehaviour
	{
		private void Awake()
		{
			DontDestroyOnLoad(gameObject);
		}
	}
}
