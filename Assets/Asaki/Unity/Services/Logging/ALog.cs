using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Asaki.Core.Logging;
using Asaki.Core.Serialization;
using Asaki.Unity.Services.Serialization;

namespace Asaki.Unity.Services.Logging
{
    /// <summary>
    /// Asaki Logger 静态门面 - 统一API设计
    /// 设计理念: 开发期全功能，发布期轻量级，主线程路径零GC
    /// </summary>
    public static class ALog
    {
        private static IAsakiLoggingService _backend;
        
        // ThreadLocal优化：每个线程独立构建器，彻底消除GC和线程竞争
        private static readonly ThreadLocal<StringBuilder> _tlsBuilder 
            = new ThreadLocal<StringBuilder>(() => new StringBuilder(1024));

        internal static void RegisterBackend(IAsakiLoggingService backend) 
            => _backend = backend;
        
        internal static void ConfigureSampling(int sampleInterval, int invocationThreshold) 
            => AsakiHotPathDetector.Configure(sampleInterval, invocationThreshold);
        internal static void Reset() => _backend = null;

        // ========================================================================
        // 1. 开发期专用日志 (编辑器和开发构建)
        // ========================================================================

        // ========================================================================
        // 6. 热路径专用API（发布构建完全剥离）
        // ========================================================================

        #if UNITY_EDITOR || DEVELOPMENT_BUILD

        /// <summary>
        /// 高频采样日志：自动按配置间隔记录
        /// <para>适用于Update/FixedUpdate等每帧调用场景</para>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DebugSampled(string message, object payload = null,
                                        [CallerFilePath] string file = "", [CallerLineNumber] int line = 0, [CallerMemberName] string member = "")
        {
            if (_backend == null) return;
    
            // 强制记录调用统计，但不立即序列化
            AsakiHotPathDetector.RecordInvocation();
            if (!AsakiHotPathDetector.ShouldSample()) return;
    
            // 惰性序列化：仅在通过采样后才执行
            string pJson = payload != null ? FastSerialize(payload) : null;
            _backend.Enqueue(AsakiLogLevel.Debug, $"[SAMPLED] {message}", default, pJson, null, file, line, member);
        }

        /// <summary>
        /// 条件采样日志：仅在高频场景下按条件记录
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DebugSampledIf(bool condition, string message, object payload = null,
                                          [CallerFilePath] string file = "", [CallerLineNumber] int line = 0, [CallerMemberName] string member = "")
        {
            if (!condition) return;
            DebugSampled(message, payload, file, line, member);
        }

        #endif // UNITY_EDITOR || DEVELOPMENT_BUILD
        
        #if UNITY_EDITOR || DEVELOPMENT_BUILD
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Debug(string message, object payload = null,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0, [CallerMemberName] string member = "")
        {
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_backend == null) return;
    
            // === 热路径优化：采样决策 ===
            AsakiHotPathDetector.RecordInvocation();
            if (!AsakiHotPathDetector.ShouldSample()) return; // 高频调用快速退出
    
            string pJson = FastSerialize(payload);
            _backend.Enqueue(AsakiLogLevel.Debug, message, default, pJson, null, file, line, member);
            #endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Info(string message, object payload = null,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0, [CallerMemberName] string member = "")
        {
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_backend == null) return;
    
            // === 热路径优化：采样决策 ===
            AsakiHotPathDetector.RecordInvocation();
            if (!AsakiHotPathDetector.ShouldSample()) return; // 高频调用快速退出
    
            string pJson = FastSerialize(payload);
            _backend.Enqueue(AsakiLogLevel.Info, message, default, pJson, null, file, line, member);
            #endif
        }
        
        #else
        
        // 发布期：Debug/Info方法完全移除（减小IL2CPP包体积）
        // 不定义任何方法体，调用处代码在发布期会被完全移除
        
        #endif

        // ========================================================================
        // 2. 警告日志 (开发期全功能，发布期轻量级)
        // ========================================================================

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Warn(string message, object payload = null,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0, [CallerMemberName] string member = "")
        {
            if (_backend == null) return;
            string pJson = FastSerialize(payload);
            
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            // 开发期：包含调用堆栈
            string stack = new StackTrace(1, false).ToString();
            _backend.Enqueue(AsakiLogLevel.Warning, message, default, pJson, stack, file, line, member);
            #else
            // 发布期：不捕获堆栈
            _backend.Enqueue(AsakiLogLevel.Warning, message, default, pJson, null, file, line, member);
            #endif
        }

        // ========================================================================
        // 3. 错误与致命错误 (统一API，始终可用)
        // ========================================================================

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Error(string message, Exception ex = null, object payload = null,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0, [CallerMemberName] string member = "")
        {
            if (_backend == null) return;

            // 立即序列化 Payload（线程安全）
            string pJson = FastSerialize(payload);

            // 异常快照（立即捕获，避免线程问题）
            ExceptionSnapshot exSnapshot = ex != null ? ExceptionSnapshot.Capture(ex) : default;

            // 堆栈策略：优先使用异常堆栈，其次按需捕获
            string stack = null;
            
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            // 开发期：总是捕获堆栈（便于调试）
            if (ex != null)
            {
                // 使用异常自带的堆栈（更准确）
                stack = ex.StackTrace;
            }
            else
            {
                // 非异常错误：捕获调用堆栈
                stack = CaptureStackTrace(2, false); // 跳过2帧
            }
            #else
            // 发布期：只在有异常时记录堆栈
            if (ex != null)
            {
                stack = ex.StackTrace; // 使用现有堆栈，不额外捕获
            }
            #endif

            _backend.Enqueue(AsakiLogLevel.Error, message, exSnapshot, pJson, stack, file, line, member);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Fatal(string message, Exception ex = null, object payload = null,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0, [CallerMemberName] string member = "")
        {
            if (_backend == null) return;
            
            string pJson = FastSerialize(payload);
            ExceptionSnapshot exSnapshot = ex != null ? ExceptionSnapshot.Capture(ex) : default;
            
            string stack = null;
            
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            // 开发期：完整堆栈
            stack = ex != null ? ex.StackTrace : CaptureStackTrace(2, true);
            #else
            // 发布期：异常堆栈或简化堆栈
            if (ex != null)
            {
                stack = GetCompactStackTrace(ex.StackTrace, 10); // 限制10帧
            }
            #endif

            _backend.Enqueue(AsakiLogLevel.Fatal, message, exSnapshot, pJson, stack, file, line, member);
        }

        // ========================================================================
        // 4. 性能优化辅助方法
        // ========================================================================

        /// <summary>
        /// 智能序列化路由器
        /// <para>策略：IAsakiSavable (零GC) > JsonUtility (有GC兜底)</para>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string FastSerialize(object payload)
        {
            if (payload == null) return "null";

            // 1. 基础类型快速通道 (避免任何对象分配)
            switch (payload)
            {
                case string s: return $"\"{s}\"";
                case int i: return i.ToString();
                case float f: return f.ToString("F2");
                case bool b: return b ? "true" : "false";
                // Asaki Math 扩展
                case UnityEngine.Vector3 v: return $"{{ \"x\":{v.x:F2}, \"y\":{v.y:F2}, \"z\":{v.z:F2} }}";
                case UnityEngine.Vector2 v: return $"{{ \"x\":{v.x:F2}, \"y\":{v.y:F2} }}";
            }

            var sb = _tlsBuilder.Value;
            sb.Clear();

            try
            {
                // 2. [核心复用] Asaki Native 序列化 (Zero Alloc)
                if (payload is IAsakiSavable savable)
                {
                    // 创建 Writer 包装现有的 StringBuilder (false 表示不从池中租借，直接用传入的)
                    var writer = new AsakiJsonWriter(sb, rentFromPool: false);
                    
                    // 手动构建一个伪造的 Root Object 闭包，保证格式合法
                    // 因为 AsakiJsonWriter 设计是流式的，我们需要帮它起个头
                    // writer.BeginObject(null) // 可选，看你是否需要外层 {}
                    
                    savable.Serialize(writer);
                    
                    return sb.ToString();
                }

                // 3. [兜底] Unity JsonUtility (仅在开发期使用，Release 下可能要考虑剥离)
                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                string json = UnityEngine.JsonUtility.ToJson(payload, true); // true = pretty print
                if (!string.IsNullOrEmpty(json) && json != "{}")
                {
                    return json;
                }
                #endif

                // 4. 最后的倔强
                return payload.ToString();
            }
            catch (Exception ex)
            {
                return $"<Serialize Error: {ex.GetType().Name}>";
            }
        }

        /// <summary>
        /// 智能堆栈捕获（开发期专用）
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)] // 防止被内联影响堆栈
        private static string CaptureStackTrace(int skipFrames, bool fullStack)
        {
            try
            {
                var trace = new StackTrace(skipFrames, fullStack);
                
                // 优化：限制堆栈深度
                var frames = trace.GetFrames();
                if (frames == null || frames.Length == 0) return string.Empty;
                
                var sb = _tlsBuilder.Value;
                sb.Clear();
                
                int maxFrames = fullStack ? 20 : 8; // 完整堆栈20帧，简化堆栈8帧
                int count = 0;
                
                foreach (var frame in frames)
                {
                    if (count >= maxFrames) break;
                    
                    var method = frame.GetMethod();
                    if (method == null) continue;
                    
                    var fileName = frame.GetFileName();
                    var lineNumber = frame.GetFileLineNumber();
                    
                    // 跳过框架内部调用（可选）
                    if (method.DeclaringType?.Namespace?.StartsWith("Asaki") == true && count > 0)
                        continue;
                    
                    sb.AppendFormat("  at {0}.{1}", 
                        method.DeclaringType?.Name ?? "Unknown", 
                        method.Name);
                    
                    if (!string.IsNullOrEmpty(fileName))
                    {
                        sb.AppendFormat(" in {0}:{1}", 
                            System.IO.Path.GetFileName(fileName), 
                            lineNumber);
                    }
                    
                    sb.AppendLine();
                    count++;
                }
                
                return sb.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// 获取压缩堆栈（发布期专用）
        /// </summary>
        private static string GetCompactStackTrace(string fullStack, int maxLines)
        {
            if (string.IsNullOrEmpty(fullStack)) return string.Empty;
            
            var lines = fullStack.Split('\n');
            var sb = _tlsBuilder.Value;
            sb.Clear();
            
            int count = 0;
            foreach (var line in lines)
            {
                if (count >= maxLines) break;
                if (string.IsNullOrWhiteSpace(line)) continue;
                
                // 只保留用户代码和Asaki框架代码
                if (line.Contains("Assets/") || line.Contains("Asaki"))
                {
                    sb.AppendLine(line.Trim());
                    count++;
                }
            }
            
            return sb.ToString();
        }

        // ========================================================================
        // 5. 开发期专用扩展方法
        // ========================================================================

        #if UNITY_EDITOR || DEVELOPMENT_BUILD
        
        /// <summary>
        /// 开发期：带条件判断的Debug日志
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DebugIf(bool condition, string message, object payload = null,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0, [CallerMemberName] string member = "")
        {
            if (condition) Debug(message, payload, file, line, member);
        }

        /// <summary>
        /// 开发期：性能测量辅助
        /// </summary>
        public struct ScopedProfiler : IDisposable
        {
            private readonly string _name;
            private readonly long _startTicks;
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public ScopedProfiler(string name)
            {
                _name = name;
                _startTicks = Stopwatch.GetTimestamp();
                ALog.Debug($"→ {_name}");
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose()
            {
                var elapsed = Stopwatch.GetTimestamp() - _startTicks;
                var ms = elapsed * 1000.0 / Stopwatch.Frequency;
                ALog.Debug($"← {_name} ({ms:F2}ms)");
            }
        }
        
        #endif
    }
}