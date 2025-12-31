using Asaki.Core.Broker;
using Asaki.Core.Context;
using Asaki.Core.Logging;
using Asaki.Core.Simulation;
using Asaki.Unity.Bridge;
using Asaki.Unity.Configuration;
using Asaki.Unity.Services.Logging;
using System;
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
		private static AsakiBootstrapper _instance;
		
		// 引用日志服务，用于生命周期管理
		private IAsakiLoggingService _logService;

		private void Awake()
		{
			if (_instance != null)
			{
				Destroy(gameObject);
				return;
			}
			_instance = this;
			DontDestroyOnLoad(gameObject);

			// ============================================
			// 第0阶段：极早期初始化 - 上下文清理
			// ============================================
			
			AsakiContext.ClearAll();
			Application.targetFrameRate = _config ? _config.TickRate : 60;

			// ============================================
			// 第1阶段：日志服务 V2 启动 (直连模式)
			// ============================================
			
			// 1. 创建服务 (V2 构造函数无参，立即就绪)
			_logService = new AsakiLoggingService();
			
			// 2. 注册到全局上下文 (ALog 依赖此步骤)
			AsakiContext.Register<IAsakiLoggingService>(_logService);
			
			// 3. 如果有配置，应用初始等级 (避免 Debug 刷屏)
			if (_config != null)
			{
				_logService.ApplyConfig(_config.LogConfig);
			}

			// ============================================
			// 第2阶段：核心驱动与自我诊断
			// ============================================

			// 此时 ALog 已可用，直接开始记录
			ALog.Info("=======================================");
			ALog.Info("== ASAKI FRAMEWORK V2 BOOT START ==");
			ALog.Info("=======================================");
			
			ALog.Info($"Bootstrapper ready. Platform: {Application.platform}");
			
			// 注册全局配置
			if (_config != null)
			{
				AsakiContext.Register(_config);
				ALog.Info($"Configuration loaded: {_config.name}");
			}
			else
			{
				ALog.Warn("No configuration assigned in inspector!");
			}
		}

		private async void Start()
		{
			try
			{
				ALog.Info("Starting module discovery...");
				AsakiStaticModuleDiscovery discovery = new AsakiStaticModuleDiscovery();

				ALog.Info("Initializing modules (DAG)...");
				await AsakiModuleLoader.Startup(discovery);

				ALog.Info("Freezing context...");
				AsakiContext.Freeze();

				AsakiBroker.Publish(new FrameworkReadyEvent());
				ALog.Info("== ASAKI FRAMEWORK READY ==");
			}
			catch (Exception ex)
			{
				// 严重错误：V2 可以直接抓取异常堆栈
				ALog.Fatal("Framework boot failed!", ex);
				throw;
			}
		}
		
		private void OnDestroy()
		{
			if (_instance == this)
			{
				ALog.Info("Asaki Framework shutting down...");
				AsakiContext.ClearAll();
				_instance = null;
			}
		}
	}
}