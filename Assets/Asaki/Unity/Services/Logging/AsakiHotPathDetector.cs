using UnityEngine;

namespace Asaki.Unity.Services.Logging
{
	internal class AsakiHotPathDetector
	{
		private static int _frameInvocationCount;
		private static int _lastCheckedFrame = -1;
		private static volatile bool _isHotPathDetected;
		private static int _sampleInterval;
		private static int _frameCounter;
		
		internal static void Configure(int sampleInterval, int invocationThreshold)
		{
			_sampleInterval = Mathf.Max(0, sampleInterval);
			// 阈值设为0表示禁用自动检测
			_isHotPathDetected = invocationThreshold == 0; 
		}
		
		[System.Runtime.CompilerServices.MethodImpl(
			System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
		internal static void RecordInvocation()
		{
			int currentFrame = Time.frameCount;
			if (currentFrame != _lastCheckedFrame)
			{
				// 新帧开始：检查上一帧是否超标
				_isHotPathDetected = _frameInvocationCount >= 100; // 可配置化
				_frameInvocationCount = 0;
				_lastCheckedFrame = currentFrame;
				_frameCounter++;
			}
			_frameInvocationCount++;
		}
		
		[System.Runtime.CompilerServices.MethodImpl(
			System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
		internal static bool ShouldSample()
		{
			// 未启用采样
			if (_sampleInterval <= 0) return true; 
            
			// 热路径未触发时全量记录
			if (!_isHotPathDetected) return true;
            
			// 采样决策：基于帧计数器
			return (_frameCounter % _sampleInterval) == 0;
		}
	}
}
