using UnityEngine;

namespace Asaki.Core.Context
{
	public interface IAsakiInit<in T1>
	{
		void Init(T1 args);
	}
	public interface IAsakiInit<in T1, in T2>
	{
		void Init(T1 args1, T2 args2);
	}
	public interface IAsakiInit<in T1, in T2, in T3>
	{
		void Init(T1 args1, T2 args2, T3 args3);
	}
	public interface IAsakiInit<in T1, in T2, in T3, in T4>
	{
		void Init(T1 args1, T2 args2, T3 args3, T4 args4);
	}
	public interface IAsakiInit<in T1, in T2, in T3, in T4, in T5>
	{
		void Init(T1 args1, T2 args2, T3 args3, T4 args4, T5 args5);
	}

	public static class AsakiInitFactory
	{
		public static T Instantiate<T, TArg1>(T prefab, TArg1 arg1, Transform parent = null)
			where T : MonoBehaviour, IAsakiInit<TArg1>
		{
			T instance = Object.Instantiate(prefab, parent);
			instance.Init(arg1);
			return instance;
		}

		public static T Instantiate<T, TArg1>(T prefab, Vector3 position, Quaternion rotation, TArg1 arg1, Transform parent = null)
			where T : MonoBehaviour, IAsakiInit<TArg1>
		{
			T instance = Object.Instantiate(prefab, position, rotation, parent);
			instance.Init(arg1);
			return instance;
		}

		public static T Instantiate<T, TArg1, TArg2>(T prefab, TArg1 arg1, TArg2 arg2, Transform parent = null)
			where T : MonoBehaviour, IAsakiInit<TArg1, TArg2>
		{
			T instance = Object.Instantiate(prefab, parent);
			instance.Init(arg1, arg2);
			return instance;
		}

		public static T Instantiate<T, TArg1, TArg2>(T prefab, Vector3 position, Quaternion rotation, TArg1 arg1, TArg2 arg2, Transform parent = null)
			where T : MonoBehaviour, IAsakiInit<TArg1, TArg2>
		{
			T instance = Object.Instantiate(prefab, position, rotation, parent);
			instance.Init(arg1, arg2);
			return instance;
		}

		public static T Instantiate<T, TArg1, TArg2, TArg3>(T prefab, TArg1 arg1, TArg2 arg2, TArg3 arg3, Transform parent = null)
			where T : MonoBehaviour, IAsakiInit<TArg1, TArg2, TArg3>
		{
			T instance = Object.Instantiate(prefab, parent);
			instance.Init(arg1, arg2, arg3);
			return instance;
		}

		public static T Instantiate<T, TArg1, TArg2, TArg3>(T prefab, Vector3 position, Quaternion rotation, TArg1 arg1, TArg2 arg2, TArg3 arg3, Transform parent = null)
			where T : MonoBehaviour, IAsakiInit<TArg1, TArg2, TArg3>
		{
			T instance = Object.Instantiate(prefab, position, rotation, parent);
			instance.Init(arg1, arg2, arg3);
			return instance;
		}

		public static T Instantiate<T, TArg1, TArg2, TArg3, TArg4>(T prefab, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, Transform parent = null)
			where T : MonoBehaviour, IAsakiInit<TArg1, TArg2, TArg3, TArg4>
		{
			T instance = Object.Instantiate(prefab, parent);
			instance.Init(arg1, arg2, arg3, arg4);
			return instance;
		}

		public static T Instantiate<T, TArg1, TArg2, TArg3, TArg4>(T prefab, Vector3 position, Quaternion rotation, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, Transform parent = null)
			where T : MonoBehaviour, IAsakiInit<TArg1, TArg2, TArg3, TArg4>
		{
			T instance = Object.Instantiate(prefab, position, rotation, parent);
			instance.Init(arg1, arg2, arg3, arg4);
			return instance;
		}
		public static T Instantiate<T, TArg1, TArg2, TArg3, TArg4, TArg5>(T prefab, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5, Transform parent = null)
			where T : MonoBehaviour, IAsakiInit<TArg1, TArg2, TArg3, TArg4, TArg5>
		{
			T instance = Object.Instantiate(prefab, parent);
			instance.Init(arg1, arg2, arg3, arg4, arg5);
			return instance;
		}
		public static T Instantiate<T, TArg1, TArg2, TArg3, TArg4, TArg5>(T prefab, Vector3 position, Quaternion rotation, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5, Transform parent = null)
			where T : MonoBehaviour, IAsakiInit<TArg1, TArg2, TArg3, TArg4, TArg5>
		{
			T instance = Object.Instantiate(prefab, position, rotation, parent);
			instance.Init(arg1, arg2, arg3, arg4, arg5);
			return instance;
		}
	}
}
