using System;

namespace Asaki.Core.Logging
{
	public struct AsakiLogEntry
	{
		public long Timestamp;
		public int ThreadId;
		public AsakiLogLevel Level;
		public string Message;
		public string PayloadJson;              // 预序列化的安全数据
		public ExceptionSnapshot ExceptionData; // 异常快照
		public string StackTrace;               // 调试堆栈(无异常时)
        
		// 编译期/调用点信息
		public string CallerPath;
		public int CallerLine;
		public string CallerMember;
	}

	public struct ExceptionSnapshot
	{
		public bool HasValue;
		public string TypeName;
		public string Message;
		public string StackTrace;
		public string InnerExceptionMsg;

		public static ExceptionSnapshot Capture(Exception ex)
		{
			if (ex == null) return default;
			return new ExceptionSnapshot
			{
				HasValue = true,
				TypeName = ex.GetType().Name,
				Message = ex.Message,
				StackTrace = ex.StackTrace,
				InnerExceptionMsg = ex.InnerException?.Message
			};
		}
	}
}
