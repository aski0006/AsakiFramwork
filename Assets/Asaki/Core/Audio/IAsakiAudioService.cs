using Asaki.Core.Context;
using System.Threading;
using UnityEngine;

namespace Asaki.Core.Audio
{
	/// <summary>
	/// 纯粹的音频播放行为接口
	/// 资源生命周期由实现层通过 AsakiResKit 自动管理
	/// </summary>
	public interface IAsakiAudioService : IAsakiModule
	{
		// ==========================================================
		// 1. 核心行为 (Core Behavior)
		// ==========================================================

		// 播放音频
		// 实现层逻辑：解析 AssetId -> 调用 Resources.LoadAsync -> 播放 -> 结束时 Release
		public AsakiAudioHandle Play(int assetId, AsakiAudioParams p = default(AsakiAudioParams), CancellationToken token = default(CancellationToken));

		// 停止音频
		// 实现层逻辑：淡出 -> 停止 -> Release Resources Handle
		void Stop(AsakiAudioHandle handle, float fadeDuration = 0.2f);

		// ==========================================================
		// 2. 运行时控制 (Runtime Control)
		// ==========================================================

		void Pause(AsakiAudioHandle handle);
		void Resume(AsakiAudioHandle handle);

		// 动态参数调整
		void SetVolume(AsakiAudioHandle handle, float volume);
		void SetPitch(AsakiAudioHandle handle, float pitch);
		void SetSpatialBlend(AsakiAudioHandle handle, float spatialBlend);

		// 3D 位置更新 (用于 Core 层驱动移动物体)
		void SetPosition(AsakiAudioHandle handle, Vector3 position);

		// 效果控制
		void SetLoop(AsakiAudioHandle handle, bool isLoop);
		void SetMuted(AsakiAudioHandle handle, bool isMuted);
		void SetPriority(AsakiAudioHandle handle, int priority);

		// ==========================================================
		// 3. 全局与分组控制 (Global & Groups)
		// ==========================================================

		// 全局控制
		void SetGlobalVolume(float volume);
		void StopAll(float fadeDuration = 0.5f);
		void PauseAll();
		void ResumeAll();

		bool IsPlaying(AsakiAudioHandle handle);
		bool IsPaused(AsakiAudioHandle handle); // <--- 关键补充

		// 分组控制 (Music, SFX, Voice)
		// 建议：groupId 对应 Unity 的 AudioMixerGroup
		void SetGroupVolume(int groupId, float volume);
		void SetGroupMuted(int groupId, bool isMuted);
		void StopGroup(int groupId, float fadeDuration = 0.2f);
		void PauseGroup(int groupId);
		void ResumeGroup(int groupId);
	}
}
