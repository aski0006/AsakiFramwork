using Asaki.Core.Logging;
using System;
using UnityEngine;

namespace Asaki.Core.Configs
{
	[Serializable]
	public class AsakiLogConfig
	{
		[Tooltip("最低日志等级")]
		public AsakiLogLevel MinLogLevel = AsakiLogLevel.Debug;

		[Header("File Rotation")]
		[Tooltip("单个日志文件最大尺寸 (KB)")]
		public int MaxFileSizeKB = 2048; // 默认 2MB

		[Tooltip("保留的历史文件数量")]
		public int MaxHistoryFiles = 10; // 超过这个数量会自动删除最旧的

		[Tooltip("日志文件名前缀")]
		public string FilePrefix = "GameLog";
	}
}
