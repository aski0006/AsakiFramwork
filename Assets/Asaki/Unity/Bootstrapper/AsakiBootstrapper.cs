using Asaki.Core.Attributes;
using Asaki.Core.Broker;
using Asaki.Core.Configs;
using Asaki.Core.Context;
using Asaki.Core.Logging;
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
		[Header("Settings")]
		[Tooltip("是否自动扫描全场景？如果关闭，则需手动拖拽到 List 中 (最快)")]
		[SerializeField] private bool _autoScan = true;

		[Tooltip("手动列表 (高性能模式)")]
		[SerializeField] private MonoBehaviour[] _manualTargets;

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

				SceneInjector();
			}
			catch (Exception ex)
			{
				// 严重错误：V2 可以直接抓取异常堆栈
				ALog.Fatal("Framework boot failed!", ex);
				throw;
			}
		}
		private void SceneInjector()
		{
			ALog.Info("Scene Injector");

			if (_autoScan)
			{
				var targets = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
				foreach (var target in targets)
				{
					// 只处理实现了标记接口的对象
					if (target is IAsakiAutoInject)
					{
						ALog.Trace("Auto Inject: " + target.name);
						InjectTarget(target);
					}

				}
			}
			else
			{
				foreach (MonoBehaviour target in _manualTargets)
				{
					if (target != null) InjectTarget(target);
				}
			}
			
			ALog.Info(@"Scene Injector complete!");
		}
		private void InjectTarget(MonoBehaviour target)
		{
			AsakiGlobalInjector.Inject(target);
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
