using UnityEngine;

namespace Asaki.Unity.Services.Coroutines
{
	[AddComponentMenu("")]
	internal class AsakiCoroutineRunner : MonoBehaviour
	{
		private void Awake()
		{
			DontDestroyOnLoad(gameObject);
		}
	}
}
