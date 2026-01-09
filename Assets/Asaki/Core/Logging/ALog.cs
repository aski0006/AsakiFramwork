using Asaki.Core.Context;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Asaki.Core.Logging
{
    /// <summary>
    /// Asaki Log V2 的静态门面类，为日志记录提供了极简的入口。
    /// 该类自动处理上下文捕获和聚合转发，简化了日志记录的操作。
    /// </summary>
    public static class ALog
    {
        /// <summary>
        /// 缓存的日志服务实例，避免每次调用日志方法时都去上下文查找服务。
        /// </summary>
        private static IAsakiLoggingService _cachedService;

        /// <summary>
        /// 在运行时子系统注册阶段调用的初始化方法。
        /// 将缓存的服务实例设置为 null，以便后续重新获取。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init()
        {
            _cachedService = null;
        }

        /// <summary>
        /// 重置缓存的服务实例。
        /// 当日志服务销毁或重启时调用此方法，以确保重新获取最新的服务实例。
        /// </summary>
        public static void Reset()
        {
            _cachedService = null;
        }

        /// <summary>
        /// 获取日志服务实例。
        /// 如果缓存的服务实例为空，则尝试从 AsakiContext 中获取。
        /// 此方法使用了 <see cref="MethodImplOptions.AggressiveInlining"/> 特性以提高性能。
        /// </summary>
        private static IAsakiLoggingService Service
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_cachedService != null || AsakiContext.TryGet(out _cachedService)) return _cachedService;

                return null;
            }
        }

        // ========================================================================
        // 1. 高频追踪 (Trace) - Update/FixedUpdate 专用
        // ========================================================================

        /// <summary>
        /// [V2核心] 用于高频追踪的日志方法。
        /// 专为 Update/循环 设计，自动聚合日志，无 GC 开销，在 Release 模式下自动剔除。
        /// </summary>
        /// <param name="message">要记录的日志消息。</param>
        /// <param name="payload">附带的数据，建议传递基础类型或 struct，默认为 null。</param>
        /// <param name="file">调用此方法的文件路径，由 <see cref="CallerFilePathAttribute"/> 自动填充。</param>
        /// <param name="line">调用此方法的行号，由 <see cref="CallerLineNumberAttribute"/> 自动填充。</param>
        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Trace(string message, object payload = null,
                                 [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            IAsakiLoggingService s = Service;
            if (s == null) return;

            string pJson = FormatPayload(payload);

            // 转发给 Service -> Aggregator
            s.LogTrace(AsakiLogLevel.Debug, message, pJson, file, line);
        }

        // ========================================================================
        // 2. 逻辑节点 (Info/Warning) - 关键流程流转
        // ========================================================================

        /// <summary>
        /// 用于记录关键信息的日志方法。
        /// 适用于记录流程状态变化，例如初始化完成、玩家登录等关键事件。
        /// </summary>
        /// <param name="message">要记录的日志消息。</param>
        /// <param name="payload">附带的数据，默认为 null。</param>
        /// <param name="file">调用此方法的文件路径，由 <see cref="CallerFilePathAttribute"/> 自动填充。</param>
        /// <param name="line">调用此方法的行号，由 <see cref="CallerLineNumberAttribute"/> 自动填充。</param>
        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Info(string message, object payload = null,
                                [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            IAsakiLoggingService s = Service;
            if (s == null) return;

            string pJson = FormatPayload(payload);
            s.LogTrace(AsakiLogLevel.Info, message, pJson, file, line);
        }

        /// <summary>
        /// 用于记录警告信息的日志方法。
        /// 适用于记录非预期的状态，但程序仍可继续运行的情况。
        /// </summary>
        /// <param name="message">要记录的日志消息。</param>
        /// <param name="payload">附带的数据，默认为 null。</param>
        /// <param name="file">调用此方法的文件路径，由 <see cref="CallerFilePathAttribute"/> 自动填充。</param>
        /// <param name="line">调用此方法的行号，由 <see cref="CallerLineNumberAttribute"/> 自动填充。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Warn(string message, object payload = null,
                                [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            IAsakiLoggingService s = Service;
            if (s == null) return;

            string pJson = FormatPayload(payload);
            s.LogTrace(AsakiLogLevel.Warning, message, pJson, file, line);
        }

        // ========================================================================
        // 3. 异常处理 (Error) - 强制记录
        // ========================================================================

        /// <summary>
        /// 用于记录错误信息的日志方法。
        /// 如果传入了 <see cref="Exception"/> 对象，将记录完整的堆栈信息。
        /// </summary>
        /// <param name="message">要记录的日志消息。</param>
        /// <param name="ex">异常对象，如果为 null，则作为普通错误处理。</param>
        /// <param name="file">调用此方法的文件路径，由 <see cref="CallerFilePathAttribute"/> 自动填充。</param>
        /// <param name="line">调用此方法的行号，由 <see cref="CallerLineNumberAttribute"/> 自动填充。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Error(string message, Exception ex,
                                 [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            IAsakiLoggingService s = Service;
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

        /// <summary>
        /// 用于记录错误信息的日志方法，不包含异常对象。
        /// 将错误信息作为高优先级的 Trace 处理，日志级别为 Error。
        /// </summary>
        /// <param name="message">要记录的日志消息。</param>
        /// <param name="payload">附带的数据，默认为 null。</param>
        /// <param name="file">调用此方法的文件路径，由 <see cref="CallerFilePathAttribute"/> 自动填充。</param>
        /// <param name="line">调用此方法的行号，由 <see cref="CallerLineNumberAttribute"/> 自动填充。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Error(string message, object payload = null,
                                 [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            IAsakiLoggingService s = Service;
            // 走 Trace 通道，但级别为 Error
            s?.LogTrace(AsakiLogLevel.Error, message, FormatPayload(payload), file, line);
        }

        /// <summary>
        /// 用于记录致命错误的日志方法。
        /// 致命错误通常会导致程序崩溃或无法继续运行。
        /// 如果传入了 <see cref="Exception"/> 对象，将记录完整的堆栈信息。
        /// </summary>
        /// <param name="message">要记录的日志消息。</param>
        /// <param name="ex">异常对象，如果为 null，则记录普通的致命错误信息。</param>
        /// <param name="file">调用此方法的文件路径，由 <see cref="CallerFilePathAttribute"/> 自动填充。</param>
        /// <param name="line">调用此方法的行号，由 <see cref="CallerLineNumberAttribute"/> 自动填充。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Fatal(string message, Exception ex = null,
                                 [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            IAsakiLoggingService s = Service;
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
        /// 简单的 Payload 格式化方法。
        /// 根据不同的数据类型将其转换为字符串格式。
        /// </summary>
        /// <param name="payload">要格式化的对象。</param>
        /// <returns>格式化后的字符串，如果对象为 null，则返回 null。</returns>
        private static string FormatPayload(object payload)
        {
            switch (payload)
            {
                case null:
                    return null;
                // 1. 基础类型直接转
                case string s:
                    return s;
                case int:
                case float:
                case bool:
                    return payload.ToString();
                // 2. Unity 向量类型特化 (常用)
                case Vector3 v3:
                    return $"({v3.x:F2}, {v3.y:F2}, {v3.z:F2})";
                case Vector2 v2:
                    return $"({v2.x:F2}, {v2.y:F2})";
                default:
                    try
                    {
                        #if UNITY_EDITOR
                        return JsonUtility.ToJson(payload);
                        #else
                        return null;
                        #endif
                    }
                    catch
                    {
                        return payload.ToString();
                    }
                    break;
            }

        }
    }
}