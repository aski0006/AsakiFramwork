using System;
using System.Collections.Generic;

namespace Asaki.Core.Logging
{
	/// <summary>
	/// V2 日志核心单元 (纯数据)
	/// </summary>
	[Serializable]
	public class AsakiLogModel
	{
		// === 动态状态 (会变) ===
		public volatile int Count = 1; // 聚合计数
		public long LastTimestamp;     // 最后一次发生时间
		public int FlushedCount = 0;
		// === 静态身份 (创建后不变) ===
		public int ID;              // 运行时唯一ID
		public AsakiLogLevel Level;
		public string Message;      
		public string PayloadJson;  
        
		// === 智能堆栈 (UI 渲染专用) ===
		public List<StackFrameModel> StackFrames; 
        
		// === 快速跳转源 ===
		public string CallerPath;   
		public int CallerLine;      

		public string DisplayTime => new DateTime(LastTimestamp).ToLocalTime().ToString("HH:mm:ss");
	}

	/// <summary>
	/// 单个堆栈帧的信息
	/// </summary>
	[Serializable]
	public struct StackFrameModel
	{
		public string DeclaringType; // 类名 (e.g. "PlayerController")
		public string MethodName;    // 方法名 (e.g. "Update")
		public string FilePath;      // 文件绝对路径
		public int LineNumber;       // 行号
		public bool IsUserCode;      // 是否是用户代码 (Assets下且非Asaki)
	}
}
