using Asaki.Core.Context;
using Asaki.Core.Logging;
using Asaki.Unity.Services.Resources;
using System;
using UnityEngine;

// [新增] 引用 UI 命名空间

namespace Asaki.Unity.Configuration
{
	[CreateAssetMenu(fileName = "AsakiConfig", menuName = "Asaki/AsakiConfig")]
	public class AsakiConfig : ScriptableObject, IAsakiService
	{
		[Header("Simulation Settings")]
		[Range(30, 120)]
		[SerializeField] private int tickRate = 60;
		public int TickRate => tickRate;

		[Header("Performance")]
		[SerializeField] private int defaultPoolSize = 128;
		public int DefaultPoolSize => defaultPoolSize;

		[Header("Resources Strategies")]
		[SerializeField] private AsakiResKitMode asakiResKitMode = AsakiResKitMode.Resources;
		[SerializeField] private int resourcesTimeoutSeconds;
		public AsakiResKitMode AsakiResKitMode => asakiResKitMode;
		public int ResourcesTimeoutSeconds => resourcesTimeoutSeconds;

		[Header("Audio")]
		[SerializeField] private AsakiAudioConfig asakiAudioConfig;
		[SerializeField] private string soundAgentPrefabAssetKey;
		[SerializeField] private int initialAudioPoolSize = 16;
		public AsakiAudioConfig AsakiAudioConfig => asakiAudioConfig;
		public string SoundAgentPrefabAssetKey => soundAgentPrefabAssetKey;
		public int InitialAudioPoolSize => initialAudioPoolSize;

		// [新增] UI 配置区域 ============================================
		[Header("UI")]
		[SerializeField] private AsakiUIConfig uiConfig;
		[SerializeField] private Vector2 referenceResolution = new Vector2(1920, 1080);
		[Range(0f, 1f)]
		[SerializeField] private float matchWidthOrHeight = 0.5f;
		public AsakiUIConfig UIConfig => uiConfig;
		public Vector2 ReferenceResolution => referenceResolution;
		public float MatchWidthOrHeight => matchWidthOrHeight;
		// =============================================================

		[Header("Web")]
		[SerializeField] private string baseUrl;
		[SerializeField] private int webTimeoutSeconds;
		public string BaseUrt => baseUrl;
		public int WebTimeoutSeconds => webTimeoutSeconds;
		
		[Header("Logging System")]
		[SerializeField] private AsakiLogConfig logConfig;
		public AsakiLogConfig LogConfig => logConfig;
		
	}
	
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
	}
}
