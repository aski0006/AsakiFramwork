namespace Asaki.Core.Configuration
{
	/// <summary>
	/// 配置加载策略 
	/// </summary>
	public enum AsakiConfigLoadStrategy
	{
		/// <summary>
		/// 自动决策：框架根据配置大小和使用频率自动选择
		/// </summary>
		Auto = 0,
    
		/// <summary>
		/// 预加载：启动时立即加载（推荐用于核心配置）
		/// </summary>
		Preload = 1,
    
		/// <summary>
		/// 按需加载：首次使用时加载（推荐用于场景配置）
		/// </summary>
		OnDemand = 2,
    
		/// <summary>
		/// 手动加载：需要显式调用 LoadAsync (推荐用于 DLC)
		/// </summary>
		Manual = 3
	}
}
