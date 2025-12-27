using Asaki.Core.Logging;
using System;

namespace Asaki.Editor.Debugging.Logging
{
	/// <summary>
	/// 编辑器专用日志条目 (File Parsing Model)
	/// <para>独立于运行时 AsakiLogEntry，专门用于文本解析</para>
	/// </summary>
	[Serializable]
	public class AsakiEditorLogEntry
	{
		public long Timestamp;
		public int ThreadId;
		public AsakiLogLevel Level;
		public string Message;
        
		// 详情部分
		public string PayloadJson;
		public string StackTrace;
		public string ExceptionType;
		public string ExceptionMessage;
		public bool IsException;
        
		// 调用定位
		public string CallerPath;
		public int CallerLine;
		public string CallerMember;

		// 辅助属性
		public string DisplayTime => new DateTime(Timestamp).ToLocalTime().ToString("HH:mm:ss");
	}
}
