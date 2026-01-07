using Asaki.Core.Attributes;
using Asaki.Core.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Asaki.Core.Context.Resolvers
{
	[DefaultExecutionOrder(-100)]
	public class AsakiSceneContext : MonoBehaviour, IAsakiResolver
	{
		[Header("Local Services Configuration")]
		[Tooltip("在此处配置纯 C# 场景服务 (Non-MonoBehaviour)。\n它们将在 Awake 时被实例化并注册。")]
		[SerializeReference]
		[AsakiInterface(typeof(IAsakiSceneContextService))]
		private List<IAsakiSceneContextService> _preconfiguredServices = new List<IAsakiSceneContextService>();
		private readonly Dictionary<Type, IAsakiService> _localServices = new Dictionary<Type, IAsakiService>();
		#if UNITY_EDITOR
		public Dictionary<Type, IAsakiService> GetRuntimeServices()
		{
			return _localServices;
		}
		#endif

		private void Awake()
		{
			if (_preconfiguredServices == null) return;
			foreach (IAsakiSceneContextService service in _preconfiguredServices.Where(service => service != null))
			{
				// 注册
				RegisterInternal(service.GetType(), service);
			}
		}
		public void Register<T>(T service) where T : class, IAsakiService
		{
			_localServices[typeof(T)] = service;
		}

		private void RegisterInternal(Type type, IAsakiService service)
		{
			if (_localServices.ContainsKey(type))
			{
				ALog.Warn($"[AsakiSceneContext] Service {type.Name} is being overwritten.");
			}
			_localServices[type] = service;
		}
		public T Get<T>() where T : class, IAsakiService
		{
			// 1. 查本地
			if (_localServices.TryGetValue(typeof(T), out IAsakiService service)) return (T)service;

			// 2. 查全局 (降级)
			return AsakiContext.Get<T>();
		}

		public bool TryGet<T>(out T service) where T : class, IAsakiService
		{
			if (_localServices.TryGetValue(typeof(T), out IAsakiService s))
			{
				service = (T)s;
				return true;
			}
			return AsakiContext.TryGet(out service);
		}
		private void OnDestroy()
		{
			// 场景卸载时，自动清理本地服务
			foreach (IAsakiService service in _localServices.Values)
			{
				if (service is IDisposable d) d.Dispose();
			}
			_localServices.Clear();
		}
	}
}
