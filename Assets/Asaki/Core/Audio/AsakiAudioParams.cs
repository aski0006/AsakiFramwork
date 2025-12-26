using UnityEngine;

namespace Asaki.Core.Audio
{

	/// <summary>
	/// 标准化音频播放参数包 (纯配置数据)
	/// </summary>
	public readonly struct AsakiAudioParams
	{
		// public readonly int AssetId; // [已删除] ID 由 Play 方法的参数决定

		public readonly Vector3 Position;   // 3D 空间坐标
		public readonly float Volume;       // 音量 (0-1)
		public readonly float Pitch;        // 音调
		public readonly float SpatialBlend; // 2D/3D 混合 (0=2D, 1=3D)
		public readonly bool IsLoop;        // 是否循环
		public readonly int Priority;       // 优先级 (0=最高)

		// [新增] 默认参数实例 (方便复用)
		public static readonly AsakiAudioParams Default = new AsakiAudioParams(
			Vector3.zero, 1.0f, 1.0f, 0f, false, 128
		);

		// 私有全参构造
		private AsakiAudioParams(Vector3 pos, float volume, float pitch, float spatialBlend, bool isLoop, int priority)
		{
			Position = pos;
			Volume = volume;
			Pitch = pitch;
			SpatialBlend = spatialBlend;
			IsLoop = isLoop;
			Priority = priority;
		}

		// ==========================================
		// Fluent API (每次修改返回新结构体)
		// ==========================================

		public AsakiAudioParams Set3D(Vector3 pos)
		{
			return new AsakiAudioParams(pos, Volume, Pitch, 1.0f, IsLoop, Priority);
			// Set3D 隐含 SpatialBlend=1
		}

		public AsakiAudioParams SetVolume(float vol)
		{
			return new AsakiAudioParams(Position, vol, Pitch, SpatialBlend, IsLoop, Priority);
		}

		public AsakiAudioParams SetPitch(float pitch)
		{
			return new AsakiAudioParams(Position, Volume, pitch, SpatialBlend, IsLoop, Priority);
		}

		public AsakiAudioParams SetLoop(bool loop)
		{
			return new AsakiAudioParams(Position, Volume, Pitch, SpatialBlend, loop, Priority);
		}

		public AsakiAudioParams SetPriority(int priority)
		{
			return new AsakiAudioParams(Position, Volume, Pitch, SpatialBlend, IsLoop, priority);
		}
	}
}
