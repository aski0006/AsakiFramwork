using Asaki.Core.Context;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;
// 用于 [Conditional]
// 假设使用 AsakiContext 获取服务

namespace Asaki.Core.Logging
{
    /// <summary>
    /// Asaki Log V2 静态门面
    /// <para>提供极简的日志入口，自动处理上下文捕获和聚合转发</para>
    /// </summary>
    public static class ALog
    {
        // 缓存服务实例，避免每次调用都去 Context 查找
        private static IAsakiLoggingService _cachedService;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Init()
        {
            _cachedService = null;
        }

        /// <summary>
        /// 重置缓存 (当 Service 销毁或重启时调用)
        /// </summary>
        public static void Reset()
        {
            _cachedService = null;
        }

        private static IAsakiLoggingService Service
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                // [优化] 移除 _isServiceMissing 这种永久失败标志
                // 如果缓存为空，或者 Context 中有更新版本的服务（Context如果支持版本号的话），这里重新获取
                
                if (_cachedService != null) return _cachedService;

                // 每次都尝试获取 (Context.TryGet 是无锁极速的，可以承受高频失败)
                if (AsakiContext.TryGet(out _cachedService))
                {
                    return _cachedService;
                }
                
                return null;
            }
        }

        // ========================================================================
        // 1. 高频追踪 (Trace) - Update/FixedUpdate 专用
        // ========================================================================

        /// <summary>
        /// [V2核心] 高频追踪日志
        /// <para>专为 Update/循环 设计。自动聚合，无 GC，Release 模式下自动剔除。</para>
        /// </summary>
        /// <param name="message">日志消息</param>
        /// <param name="payload">附带数据 (建议传基础类型或 struct)</param>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Trace(string message, object payload = null,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            // 如果服务没准备好，直接跳过 (Fail-Fast)
            var s = Service;
            if (s == null) return;

            // 序列化 Payload (V2.1 可以优化为零GC的 BufferWriter)
            // 目前简单用 ToString 或 JsonUtility，聚合器会缓存这个结果，开销可控
            string pJson = FormatPayload(payload);

            // 转发给 Service -> Aggregator
            s.LogTrace(AsakiLogLevel.Debug, message, pJson, file, line);
        }

        // ========================================================================
        // 2. 逻辑节点 (Info/Warning) - 关键流程流转
        // ========================================================================

        /// <summary>
        /// 关键信息日志
        /// <para>用于记录流程状态变化 (如：初始化完成、玩家登录)</para>
        /// </summary>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Info(string message, object payload = null,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            var s = Service;
            if (s == null) return;

            string pJson = FormatPayload(payload);
            s.LogTrace(AsakiLogLevel.Info, message, pJson, file, line);
        }

        /// <summary>
        /// 警告日志
        /// <para>用于记录非预期的状态，但程序仍可运行</para>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Warn(string message, object payload = null,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            var s = Service;
            if (s == null) return;

            string pJson = FormatPayload(payload);
            s.LogTrace(AsakiLogLevel.Warning, message, pJson, file, line);
        }

        // ========================================================================
        // 3. 异常处理 (Error) - 强制记录
        // ========================================================================

        /// <summary>
        /// 错误日志
        /// <para>发生严重错误。如果传入 Exception 对象，将记录完整堆栈。</para>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Error(string message, Exception ex,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            var s = Service;
            if (s == null) return;

            if (ex != null)
            {
                // 走异常专用通道 (会从 ex 中提取堆栈)
                s.LogException(message, ex, file, line);
            }
            else
            {
                // 普通错误 (无异常对象)，作为高优先级的 Trace 处理
                s.LogTrace(AsakiLogLevel.Error, message, null, file, line);
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Error(string message, object payload = null,
                                 [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            var s = Service;
            // 走 Trace 通道，但级别为 Error
            s?.LogTrace(AsakiLogLevel.Error, message, FormatPayload(payload), file, line);
        }
        
        /// <summary>
        /// 致命错误 (通常会导致程序崩溃或无法继续)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Fatal(string message, Exception ex = null,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            var s = Service;
            if (s == null) return;

            if (ex != null)
                s.LogException(message, ex, file, line);
            else
                s.LogTrace(AsakiLogLevel.Fatal, message, null, file, line);
        }

        // ========================================================================
        // 4. 辅助方法
        // ========================================================================

        /// <summary>
        /// 简单的 Payload 格式化
        /// </summary>
        private static string FormatPayload(object payload)
        {
            if (payload == null) return null;

            // 1. 基础类型直接转
            if (payload is string s) return s;
            if (payload is int || payload is float || payload is bool) return payload.ToString();
            
            // 2. Unity 向量类型特化 (常用)
            if (payload is UnityEngine.Vector3 v3) return $"({v3.x:F2}, {v3.y:F2}, {v3.z:F2})";
            if (payload is UnityEngine.Vector2 v2) return $"({v2.x:F2}, {v2.y:F2})";

            // 3. 复杂对象转 JSON (在 Editor 下为了方便调试，允许产生 GC)
            try
            {
                return UnityEngine.JsonUtility.ToJson(payload);
            }
            catch
            {
                return payload.ToString();
            }
        }
    }
}