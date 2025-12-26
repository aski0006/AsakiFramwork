using Asaki.Core.Broker;
using Asaki.Core.Context;
using Asaki.Core.Simulation;
using Asaki.Unity.Bridge;
using Asaki.Unity.Configuration;
using System;
using System.Collections.Generic;
using UnityEngine;
using AsakiBroker = Asaki.Core.Broker.AsakiBroker;

namespace Asaki.Unity.Bootstrapper
{
	public struct FrameworkReadyEvent : IAsakiEvent { }

	[DefaultExecutionOrder(-9999)]
	public class AsakiBootstrapper : MonoBehaviour
	{
		[Header("Configuration")]
		[SerializeField] private AsakiConfig _config;

		[Header("Module Discovery")]
		[Tooltip("指定要扫描模块的程序集名称，为空则扫描所有")]
		[SerializeField] private List<string> _scanAssemblies = new List<string>
		{
			"Asaki.Unity",
			"Game.Logic",
			"Game.View",
		};

		private static AsakiBootstrapper _instance;

		private void Awake()
		{
			if (_instance != null)
			{
				Destroy(gameObject);
				return;
			}
			_instance = this;
			DontDestroyOnLoad(gameObject);

			Debug.Log("== Asaki Framework Booting (DAG System) ==");

			AsakiContext.ClearAll();
			Application.targetFrameRate = _config ? _config.TickRate : 60;

			// 1. 注册全局配置 (第0号服务)
			if (_config != null) AsakiContext.Register(_config);
			else Debug.LogError("[Asaki] Configuration is null!");

			// 2. 初始化核心驱动 (Simulation & Driver)
			SetupCoreDriver();
		}

		private async void Start()
		{
			try
			{
				Debug.Log("[Asaki] Freezing Context...");

				// 创建模块发现器实例
				AsakiStaticModuleDiscovery discovery = new AsakiStaticModuleDiscovery();

				await AsakiModuleLoader.Startup(discovery);

				Debug.Log("[Asaki] Freezing Context...");
				AsakiContext.Freeze();

				// 启动完成，发送框架就绪事件
				AsakiBroker.Publish(new FrameworkReadyEvent());
				Debug.Log("[Asaki] Framework boot complete.");
			}
			catch (Exception ex)
			{
				// 移到这里，ex变量在catch块中已定义
				Debug.LogError($"[Asaki] Boot Failed: {ex}");
			}
		}

		private void SetupCoreDriver()
		{
			AsakiSimulationManager simManager = new AsakiSimulationManager();
			AsakiContext.Register(simManager);

			GameObject driverGo = new GameObject("[Asaki.Driver]");
			DontDestroyOnLoad(driverGo);
			AsakiMonoDriver driver = driverGo.AddComponent<AsakiMonoDriver>();
			driver.Initialize(simManager);
		}

		private void OnDestroy()
		{
			if (_instance == this)
			{
				AsakiContext.ClearAll(); // 会触发所有 Module 的 OnDispose
				_instance = null;
			}
		}
	}
}
