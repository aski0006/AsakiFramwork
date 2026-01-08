using Asaki.Core. Attributes;
using Asaki.Core.Context;
using Asaki.Core.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Asaki.Core.Context. Resolvers
{
    [DefaultExecutionOrder(-100)]
    public class AsakiSceneContext : MonoBehaviour, IAsakiResolver
    {
        // ========================================================================
        // 配置字段
        // ========================================================================
        
        [Header("Pure C# Services")]
        [Tooltip("纯 C# 场景服务（通过 SerializeReference 序列化）\n在 Awake 时实例化并注册")]
        [SerializeReference]
        [AsakiInterface(typeof(IAsakiSceneContextService))]
        private List<IAsakiSceneContextService> _pureCSharpServices = new List<IAsakiSceneContextService>();

        [Header("MonoBehaviour Services")]
        [Tooltip("MonoBehaviour 场景服务（通过 Unity 原生引用）\n仅作为服务注册，不会被注入（由 Bootstrapper 负责）")]
        [SerializeField]
        private List<MonoBehaviour> _behaviourServices = new List<MonoBehaviour>();

        // ========================================================================
        // 运行时数据
        // ========================================================================
        
        private readonly Dictionary<Type, IAsakiService> _localServices = new Dictionary<Type, IAsakiService>();

        #if UNITY_EDITOR
        public Dictionary<Type, IAsakiService> GetRuntimeServices() => _localServices;
        #endif

        // ========================================================================
        // 生命周期
        // ========================================================================

        private void Awake()
        {
            ALog.Info($"[AsakiSceneContext] Initializing in scene: {gameObject.scene.name}");

            // 1. 注册纯 C# 服务（立即创建并注册）
            RegisterPureCSharpServices();

            // 2. 注册 MonoBehaviour 服务（仅注册引用，不注入）
            RegisterBehaviourServices();

            ALog.Info($"[AsakiSceneContext] Registered {_localServices.Count} scene services");
        }

        private void OnDestroy()
        {
            ALog.Info($"[AsakiSceneContext] Cleaning up scene services.. .");

            // 清理本地服务（只清理纯 C# 服务，MonoBehaviour 由 Unity 管理）
            foreach (var kvp in _localServices)
            {
                // 只 Dispose 纯 C# 对象
                if (kvp.Value is IDisposable disposable && !(kvp.Value is MonoBehaviour))
                {
                    disposable.Dispose();
                }
            }

            _localServices. Clear();
        }

        // ========================================================================
        // 服务注册
        // ========================================================================

        /// <summary>
        /// 注册纯 C# 服务
        /// </summary>
        private void RegisterPureCSharpServices()
        {
            if (_pureCSharpServices == null || _pureCSharpServices.Count == 0)
                return;

            ALog.Info($"  Registering {_pureCSharpServices.Count} pure C# service(s)...");

            foreach (var service in _pureCSharpServices. Where(s => s != null))
            {
                RegisterServiceWithInterfaces(service.GetType(), service);
            }
        }

        /// <summary>
        /// 注册 MonoBehaviour 服务（仅注册，不注入）
        /// </summary>
        private void RegisterBehaviourServices()
        {
            if (_behaviourServices == null || _behaviourServices.Count == 0)
                return;

            ALog.Info($"  Registering {_behaviourServices.Count} MonoBehaviour service(s)...");

            foreach (var behaviour in _behaviourServices.Where(b => b != null))
            {
                // 验证接口实现
                if (behaviour is not IAsakiSceneContextService service)
                {
                    ALog.Error($"[SceneContext] {behaviour.GetType().Name} does not implement IAsakiSceneContextService!  Skipped.");
                    continue;
                }

                // 只注册，不注入
                // 注入由 AsakiBootstrapper 在场景加载后统一处理
                RegisterServiceWithInterfaces(behaviour.GetType(), service);
            }
        }

        /// <summary>
        /// 注册服务并自动注册所有服务接口
        /// </summary>
        private void RegisterServiceWithInterfaces(Type concreteType, IAsakiService service)
        {
            // 1. 注册具体类型
            RegisterInternal(concreteType, service);

            // 2. 注册所有服务接口（排除基础标记接口）
            foreach (var interfaceType in concreteType.GetInterfaces())
            {
                if (typeof(IAsakiService).IsAssignableFrom(interfaceType) &&
                    interfaceType != typeof(IAsakiService) &&
                    interfaceType != typeof(IAsakiSceneContextService) &&
                    interfaceType != typeof(IAsakiGlobalMonoBehaviourService))
                {
                    RegisterInternal(interfaceType, service);
                }
            }
        }

        /// <summary>
        /// 公共注册接口（允许运行时动态注册）
        /// </summary>
        public void Register<T>(T service) where T : class, IAsakiService
        {
            RegisterInternal(typeof(T), service);
        }

        /// <summary>
        /// 内部注册实现
        /// </summary>
        private void RegisterInternal(Type type, IAsakiService service)
        {
            if (_localServices.ContainsKey(type))
            {
                ALog. Warn($"[AsakiSceneContext] Service {type.Name} is being overwritten.");
            }

            _localServices[type] = service;
        }

        // ========================================================================
        // 服务解析（IAsakiResolver 实现）
        // ========================================================================

        public T Get<T>() where T : class, IAsakiService
        {
            // 1. 优先查找本地场景服务
            if (_localServices.TryGetValue(typeof(T), out IAsakiService service))
                return (T)service;

            // 2. 降级到全局服务
            return AsakiContext.Get<T>();
        }

        public bool TryGet<T>(out T service) where T : class, IAsakiService
        {
            // 1. 优先查找本地场景服务
            if (_localServices.TryGetValue(typeof(T), out IAsakiService s))
            {
                service = (T)s;
                return true;
            }

            // 2. 降级到全局服务
            return AsakiContext.TryGet(out service);
        }
    }
}