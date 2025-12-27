using Asaki.Core.Context;
using System;

namespace Asaki.Core.Logging
{
	public interface IAsakiLoggingService : IAsakiService, IDisposable
	{
		void SetLevel(AsakiLogLevel level);
        
		// V3 接口：接收全安全的数据
		void Enqueue(AsakiLogLevel level, string message, ExceptionSnapshot exSnap, 
		             string payloadJson, string stackTrace, 
		             string file, int line, string member);
                     
		void FlushSync();
	}
}
