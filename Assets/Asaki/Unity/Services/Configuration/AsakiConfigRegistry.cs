using System;
using System.Collections.Generic;
using System.Threading.Tasks;

// 统一使用标准 Task

namespace Asaki.Unity.Services.Configuration
{
	/// <summary>
	/// [Asaki Core] 配置注册中心
	/// <para>统一返回 System.Threading.Tasks.Task，确保跨程序集兼容性。</para>
	/// </summary>
	public static class AsakiConfigRegistry
	{
		// 签名：(Service, ConfigName, FilePath) -> Task
		private static readonly List<Func<AsakiConfigService, string, string, Task>> _loaders
			= new List<Func<AsakiConfigService, string, string, Task>>();

		public static void RegisterLoader(Func<AsakiConfigService, string, string, Task> loader)
		{
			if (!_loaders.Contains(loader))
			{
				_loaders.Add(loader);
			}
		}

		public static Task GetLoader(AsakiConfigService service, string configName, string path)
		{
			foreach (var loader in _loaders)
			{
				Task task = loader(service, configName, path);
				if (task != null)
				{
					return task;
				}
			}
			return null; // 返回 null 代表没人处理，而不是 default
		}

		public static void Clear()
		{
			_loaders.Clear();
		}
	}
}
