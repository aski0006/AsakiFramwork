using System;
using System.Collections.Generic;
using UnityEngine;

namespace Asaki.Core.Context.Resolvers
{
	public class AsakiSceneContext : MonoBehaviour, IAsakiResolver
	{
		private readonly Dictionary<Type, IAsakiService> _localServices = new Dictionary<Type, IAsakiService>();
		public void Register<T>(T service) where T : class, IAsakiService
		{
			_localServices[typeof(T)] = service;
		}
		public T Get<T>() where T : class, IAsakiService
		{
			// 1. 查本地
			if (_localServices.TryGetValue(typeof(T), out var service)) return (T)service;
        
			// 2. 查全局 (降级)
			return AsakiContext.Get<T>();
		}
		
		public bool TryGet<T>(out T service) where T : class, IAsakiService
		{
			if (_localServices.TryGetValue(typeof(T), out var s))
			{
				service = (T)s;
				return true;
			}
			return AsakiContext.TryGet(out service);
		}
		private void OnDestroy()
		{
			// 场景卸载时，自动清理本地服务
			foreach(var service in _localServices.Values)
			{
				if(service is IDisposable d) d.Dispose();
			}
			_localServices.Clear();
		}
	}
}
