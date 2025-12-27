using Asaki.Core.Logging;
using System;
using UnityEngine;

namespace Asaki.Unity.Configuration
{
	[Serializable]
	public class AsakiLogConfig
	{
		[Header("Runtime Settings")]
		public AsakiLogLevel MinLogLevel = AsakiLogLevel.Debug;
		public bool EnableConsoleSync = true; // 是否同步输出到 Unity Console

		[Header("Storage Settings")]
		[Tooltip("单个日志文件最大尺寸 (KB)")]
		public int MaxFileSizeKb = 2048;
		[Tooltip("保留的历史 Session 数量")]
		public int MaxHistorySessions = 10;
        
		[Header("Editor Integration")]
		[Tooltip("是否为 Debug/Info 级别记录调用位置 (支持跳转)")]
		public bool WriteCallerInfoForLowLevel = true; 
		
		[Header("Hot Path Sampling")]
		[Tooltip("高频日志采样间隔（帧）: 0=禁用采样, 1=每帧记录, N=每N帧记录一次")]
		public int SampleIntervalFrames = 0;
		[Tooltip("热路径自动检测: 当单帧日志调用超过此阈值时自动启用采样")]
		public int HotPathInvocationThreshold = 100;
		[Tooltip("为Verbose/Debug/Info级别启用采样（Warning+级别始终全量记录）")]
		public bool EnableSamplingForLowLevels = true;
	}
}
