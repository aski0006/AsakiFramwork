using Asaki.Core.Resources;
using System;
using UnityEngine;

namespace Asaki.Core.Configs
{
	[Serializable]
	public class AsakiResConfig
	{
		[Tooltip("资源加载策略模式")]
		public AsakiResKitMode Mode = AsakiResKitMode.Resources;

		[Tooltip("未使用的资源释放超时时间 (秒)")]
		public int TimeoutSeconds = 60;
	}
}
