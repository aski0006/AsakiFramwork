using Asaki.Core.Broker;
using Asaki.Core.Context;
using Asaki.Core.Logging;
using Asaki.Core.Simulation;
using Asaki.Unity.Bridge;
using Asaki.Unity.Configuration;
using Asaki.Unity.Services.Logging;
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
		
		// 新增：早期启动的日志服务实例
		private IAsakiLoggingService _earlyLogService;

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
			// 第0阶段：极早期初始化 - 只做绝对必要的设置
			// ============================================
			
			// 清空全局上下文
			AsakiContext.ClearAll();
			
			// 设置目标帧率
			Application.targetFrameRate = _config ? _config.TickRate : 60;

			// ============================================
			// 第1阶段：日志服务早期启动
			// ============================================
			
			// 创建日志服务实例（但不会启动写入线程）
			_earlyLogService = new AsakiLoggingService();
			AsakiContext.Register<IAsakiLoggingService>(_earlyLogService);
			
			// 早期初始化（只初始化文件系统，不启动后台线程）
			if (_earlyLogService is AsakiLoggingService loggingService)
			{
				loggingService.InitializeEarly();
			}
			
			// ============================================
			// 第2阶段：使用日志服务记录启动过程
			// ============================================
			
			ALog.Info("=======================================");
			ALog.Info("== ASAKI FRAMEWORK BOOTSTRAP START ==");
			ALog.Info("=======================================");
			ALog.Info($"Bootstrapper initialized at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
			ALog.Info($"Unity Version: {Application.unityVersion}");
			ALog.Info($"Platform: {Application.platform}");
			ALog.Info($"Persistent Data Path: {Application.persistentDataPath}");

			// 注册全局配置
			if (_config != null)
			{
				AsakiContext.Register(_config);
				ALog.Info($"Configuration registered: {_config.name}");
				ALog.Info($"Target FPS: {_config.TickRate}");
			}
			else
			{
				ALog.Error("Configuration is null! Using default settings.");
			}

			// ============================================
			// 第3阶段：核心驱动初始化
			// ============================================
			
			SetupCoreDriver();
		}

		private async void Start()
		{
			try
			{
				ALog.Info("Starting module discovery and initialization...");

				// 创建模块发现器实例
				AsakiStaticModuleDiscovery discovery = new AsakiStaticModuleDiscovery();

				ALog.Info("Beginning DAG-based module initialization...");
				await AsakiModuleLoader.Startup(discovery);

				ALog.Info("Freezing global context...");
				AsakiContext.Freeze();

				// 启动完成，发送框架就绪事件
				AsakiBroker.Publish(new FrameworkReadyEvent());
				ALog.Info("=======================================");
				ALog.Info("== ASAKI FRAMEWORK BOOT COMPLETE ==");
				ALog.Info("=======================================");
			}
			catch (Exception ex)
			{
				ALog.Fatal("Framework boot failed!", ex);
				
				// 紧急情况：立即刷新所有日志到文件
				_earlyLogService?.FlushSync();
				
				// 再次抛出，让Unity可以捕获
				throw;
			}
		}

		private void SetupCoreDriver()
		{
			ALog.Info("Initializing core simulation driver...");
			
			AsakiSimulationManager simManager = new AsakiSimulationManager();
			AsakiContext.Register(simManager);

			GameObject driverGo = new GameObject("[Asaki.Driver]");
			DontDestroyOnLoad(driverGo);
			AsakiMonoDriver driver = driverGo.AddComponent<AsakiMonoDriver>();
			driver.Initialize(simManager);
			
			ALog.Info("Core simulation driver initialized.");
		}

		private void OnDestroy()
		{
			if (_instance == this)
			{
				ALog.Info("Asaki Framework shutting down...");
				
				AsakiContext.ClearAll(); // 会触发所有 Module 的 OnDispose
				_instance = null;
				
				// 确保所有日志都写入文件
				_earlyLogService?.FlushSync();
			}
		}
	}
}
