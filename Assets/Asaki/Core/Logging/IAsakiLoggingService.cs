using System;
using Asaki.Core.Context;

namespace Asaki.Core.Logging
{
	/// <summary>
	/// V2 日志服务接口
	/// <para>核心职责：连接 ALog 静态入口与 Aggregator 聚合引擎</para>
	/// </summary>
	public interface IAsakiLoggingService : IAsakiService, IDisposable
	{
		/// <summary>
		/// [V2核心] 获取聚合器实例
		/// <para>Editor Dashboard 将直接访问此属性以获取显示列表，实现零延迟调试</para>
		/// </summary>
		AsakiLogAggregator Aggregator { get; }

		/// <summary>
		/// 记录追踪日志 (Trace/Info/Warning/Error 无异常版)
		/// <para>适用于 Update/高频逻辑，会自动聚合</para>
		/// </summary>
		void LogTrace(AsakiLogLevel level, string message, string payloadJson, string file, int line);

		/// <summary>
		/// 记录异常日志 (Exception)
		/// <para>强制捕获堆栈，适用于 try-catch 块</para>
		/// </summary>
		void LogException(string message, Exception ex, string file, int line);
        
		/// <summary>
		/// 设置最小日志等级
		/// </summary>
		void SetLevel(AsakiLogLevel level);
	}
}
