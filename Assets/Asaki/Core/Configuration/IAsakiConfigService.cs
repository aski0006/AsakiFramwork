using Asaki.Core.Context;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Asaki.Core.Configuration
{
	/// <summary>
	/// 配置管理服务的核心接口，提供配置表的加载、查询、缓存和生命周期管理功能。
	/// </summary>
	/// <remarks>
	/// <para>核心职责：</para>
	/// <list type="bullet">
	///     <item>配置表的热加载与热重载</item>
	///     <item>基于内存压力的自动卸载</item>
	///     <item>多策略加载（预加载、按需加载、手动加载）</item>
	///     <item>配置数据缓存与线程安全访问</item>
	///     <item>配置变更事件发布</item>
	/// </list>
	/// <para>设计原则：</para>
	/// <list type="number">
	///     <item><b>延迟加载：</b>非预加载配置在首次访问时加载</item>
	///     <item><b>透明缓存：</b>调用者无需关心配置是否已加载</item>
	///     <item><b>线程安全：</b>所有公共方法支持多线程并发调用</item>
	///     <item><b>性能优先：</b>使用哈希表索引，O(1)时间复杂度查询</item>
	/// </list>
	/// <para>使用示例：</para>
	/// <code>
	/// // 1. 获取单个配置
	/// var itemConfig = _configService.Get&lt;ItemConfig&gt;(1001);
	/// Debug.Log($"Item Name: {itemConfig.Name}");
	/// 
	/// // 2. 批量查询
	/// var allRareItems = _configService.Where&lt;ItemConfig&gt;(c => c.Rarity == Rarity.Legendary);
	/// 
	/// // 3. 异步预加载
	/// await _configService.PreloadAsync&lt;LevelConfig&gt;();
	/// 
	/// // 4. 热重载支持
	/// await _configService.ReloadAsync&lt;GameBalanceConfig&gt;();
	/// </code>
	/// </remarks>
	public interface IAsakiConfigService : IAsakiModule
	{
		/// <summary>
		/// 异步加载所有标记为<see cref="AsakiConfigLoadStrategy.Preload"/>的配置表。
		/// </summary>
		/// <returns>表示异步加载操作的<see cref="Task"/>。</returns>
		/// <remarks>
		/// <para>加载流程：</para>
		/// <list type="number">
		///     <item>扫描所有实现<see cref="IAsakiConfig"/>的类型</item>
		///     <item>过滤策略为<see cref="AsakiConfigLoadStrategy.Preload"/>的配置</item>
		///     <item>按<see cref="AsakiConfigLoadInfo.Priority"/>排序</item>
		///     <item>并行加载（受限于<see cref="TaskScheduler"/>和I/O带宽）</item>
		/// </list>
		/// <para>调用时机：</para>
		/// 通常在<see cref="IAsakiModule.OnInitAsync"/>中自动调用，或在游戏初始化流程中手动调用。
		/// 不应在游戏进行中调用，以免引起不必要的I/O和CPU开销。
		/// </remarks>
		Task LoadAllAsync();

		/// <summary>
		/// 获取指定ID的配置对象，若尚未加载则触发按需加载。
		/// </summary>
		/// <typeparam name="T">配置类型，必须实现<see cref="IAsakiConfig"/>并拥有无参构造函数。</typeparam>
		/// <param name="id">配置的主键ID。</param>
		/// <returns>与ID关联的配置对象实例。</returns>
		/// <exception cref="KeyNotFoundException">配置不存在于数据源中。</exception>
		/// <exception cref="InvalidOperationException">
		/// 加载策略为<see cref="AsakiConfigLoadStrategy.Manual"/>且未手动加载。
		/// </exception>
		/// <remarks>
		/// <para>加载行为：</para>
		/// <list type="bullet">
		///     <item>若配置已加载，直接返回缓存实例</item>
		///     <item>若配置未加载且策略为<see cref="AsakiConfigLoadStrategy.OnDemand"/>，触发异步加载并阻塞直至完成</item>
		///     <item>更新<see cref="AsakiConfigLoadInfo.AccessCount"/>和<see cref="AsakiConfigLoadInfo.LastAccessTime"/></item>
		/// </list>
		/// <para>性能注意：</para>
		/// 首次访问<see cref="AsakiConfigLoadStrategy.OnDemand"/>配置可能导致主线程阻塞，建议在加载画面预加载关键配置。
		/// </remarks>
		T Get<T>(int id) where T : class, IAsakiConfig, new();
		/// <summary>
		/// 获取指定类型的所有配置对象，触发按需加载（若未加载）。
		/// </summary>
		/// <typeparam name="T">配置类型。</typeparam>
		/// <returns>包含所有配置对象的只读列表。</returns>
		/// <remarks>
		/// <para>返回数据为快照视图，后续加载或重载不会更新此列表内容。</para>
		/// <para>对于大数据量配置（>10000行），考虑使用<see cref="GetAllStreamAsync{T}"/>避免内存峰值。</para>
		/// </remarks>
		IReadOnlyList<T> GetAll<T>() where T : class, IAsakiConfig, new();
		/// <summary>
		/// 异步流式获取所有配置对象，支持在低内存环境下处理超大型配置表。
		/// </summary>
		/// <typeparam name="T">配置类型。</typeparam>
		/// <returns>异步枚举器，每次迭代产生一个配置对象。</returns>
		/// <remarks>
		/// <para>性能优势：</para>
		/// <list type="bullet">
		///     <item>内存占用恒定，不一次性加载整个列表</item>
		///     <item>支持取消操作（通过<see cref="CancellationToken"/>）</item>
		///     <item>可与其他异步流操作（如<see cref="System.Linq.AsyncEnumerable"/>）组合</item>
		/// </list>
		/// <para>适用场景：</para>
		/// 配置表行数超过10000行，或需要在加载过程中进行筛选、转换操作。
		/// </remarks>
		IAsyncEnumerable<T> GetAllStreamAsync<T>() where T : class, IAsakiConfig, new();
		/// <summary>
		/// 异步重载指定类型的配置，从数据源重新读取并更新缓存。
		/// </summary>
		/// <typeparam name="T">要重载的配置类型。</typeparam>
		/// <returns>表示异步重载操作的<see cref="Task"/>。</returns>
		/// <remarks>
		/// <para>重载流程：</para>
		/// <list type="number">
		///     <item>从数据源（文件、远程服务器）读取原始数据</item>
		///     <item>反序列化为新的配置对象集合</item>
		///     <item>数据验证（如主键唯一性、字段范围检查）</item>
		///     <item>原子性替换内部缓存引用</item>
		///     <item>发布<see cref="AsakiConfigReloadedEvent"/>事件</item>
		/// </list>
		/// <para>线程安全：重载期间读取操作可能获取旧数据，但绝不会获得不一致的混合状态。</para>
		/// </remarks>
		Task ReloadAsync<T>() where T : class, IAsakiConfig, new();

		/// <summary>
		/// 查找满足条件的第一个配置对象。
		/// </summary>
		/// <typeparam name="T">配置类型。</typeparam>
		/// <param name="predicate">条件谓词函数。</param>
		/// <returns>满足条件的第一个配置对象，若无则返回<c>null</c>。</returns>
		/// <remarks>
		/// <para>若配置未加载，此操作会触发按需加载。</para>
		/// <para>实现采用短路求值，找到匹配项后立即停止遍历。</para>
		/// </remarks>
		T Find<T>(Predicate<T> predicate) where T : class, IAsakiConfig, new();
		/// <summary>
		/// 筛选满足条件的所有配置对象。
		/// </summary>
		/// <typeparam name="T">配置类型。</typeparam>
		/// <param name="predicate">条件谓词函数。</param>
		/// <returns>包含所有匹配配置对象的只读列表。</returns>
		/// <remarks>
		/// <para>返回新集合，修改不影响原始缓存数据。</para>
		/// <para>对于高频调用，建议在服务层缓存筛选结果。</para>
		/// </remarks>
		IReadOnlyList<T> Where<T>(Func<T, bool> predicate) where T : class, IAsakiConfig, new();
		/// <summary>
		/// 检查是否存在满足条件的配置对象。
		/// </summary>
		/// <typeparam name="T">配置类型。</typeparam>
		/// <param name="predicate">条件谓词函数。</param>
		/// <returns>若至少存在一个匹配项则返回<c>true</c>，否则返回<c>false</c>。</returns>
		bool Exists<T>(Predicate<T> predicate) where T : class, IAsakiConfig, new();

		// Batch Op
		/// <summary>
		/// 批量获取指定ID集合的配置对象。
		/// </summary>
		/// <typeparam name="T">配置类型。</typeparam>
		/// <param name="ids">要查询的配置ID集合。</param>
		/// <returns>配置对象列表，顺序与<paramref name="ids"/>一致，缺失的ID对应位置为<c>null</c>。</returns>
		/// <remarks>
		/// <para>性能优化：此接口可能实现为批量IO操作，减少多次单ID查询的开销。</para>
		/// <para>数据一致性：返回列表是请求时刻的快照，不反映后续重载变化。</para>
		/// </remarks>
		IReadOnlyList<T> GetBatch<T>(IEnumerable<int> ids) where T : class, IAsakiConfig, new();

		// Config Meta
		/// <summary>
		/// 获取指定类型的配置总数。
		/// </summary>
		/// <typeparam name="T">配置类型。</typeparam>
		/// <returns>配置表中的记录总数。</returns>
		/// <remarks>
		/// 若配置未加载，此方法不会触发加载，返回0。
		/// 需要准确计数的场景应先调用<see cref="IsLoaded{T}"/>或<see cref="PreloadAsync{T}"/>。
		/// </remarks>
		int GetCount<T>() where T : class, IAsakiConfig, new();
		/// <summary>
		/// 检查指定类型的配置是否已加载。
		/// </summary>
		/// <typeparam name="T">配置类型。</typeparam>
		/// <returns>若已加载返回<c>true</c>，否则返回<c>false</c>。</returns>
		bool IsLoaded<T>() where T : class, IAsakiConfig, new();
		/// <summary>
		/// 检查指定类型的配置是否已加载。
		/// </summary>
		/// <param name="type">配置的类型对象。</param>
		/// <returns>若已加载返回<c>true</c>，否则返回<c>false</c>。</returns>
		/// <exception cref="ArgumentNullException"><paramref name="type"/>为<c>null</c>。</exception>
		/// <exception cref="ArgumentException"><paramref name="type"/>未实现<see cref="IAsakiConfig"/>接口。</exception>
		bool IsLoaded(Type type);

		/// <summary>
		/// 获取配置数据源的原始路径（如文件路径或URI）。
		/// </summary>
		/// <typeparam name="T">配置类型。</typeparam>
		/// <returns>数据源路径字符串，若配置未注册返回<c>null</c>。</returns>
		/// <remarks>
		/// 用于调试、日志记录或手动检查原始数据文件。
		/// 路径格式取决于实现（如"Assets/Configs/ItemConfig.json"或"http://cdn.example.com/configs/item.xml"）。
		/// </remarks>
		string GetSourcePath<T>() where T : class, IAsakiConfig, new();
		/// <summary>
		/// 获取配置数据源的最后修改时间（UTC）。
		/// </summary>
		/// <typeparam name="T">配置类型。</typeparam>
		/// <returns>最后修改时间，若文件不存在或无法访问返回<see cref="DateTime.MinValue"/>。</returns>
		/// <remarks>
		/// 用于热重载系统判断配置是否需要更新。
		/// 对于远程数据源，可能返回上次成功获取的时间戳。
		/// </remarks>
		DateTime GetLastModifiedTime<T>() where T : class, IAsakiConfig, new();
		/// <summary>
		/// 异步获取配置对象，支持非阻塞的按需加载。
		/// </summary>
		/// <typeparam name="T">配置类型。</typeparam>
		/// <param name="id">配置主键ID。</param>
		/// <returns>异步任务的配置对象结果。</returns>
		/// <remarks>
		/// <para>与<see cref="Get{T}"/>的区别：</para>
		/// <list type="bullet">
		///     <item>此方法为异步，不会阻塞调用线程</item>
		///     <item>适合在异步上下文中使用（如<see cref="Task.WhenAll"/>组合）</item>
		///     <item>加载期间可执行其他不依赖此配置的异步操作</item>
		/// </list>
		/// <para>示例：</para>
		/// <code>
		/// // 并行加载多个按需配置
		/// var itemTask = configService.GetAsync&lt;ItemConfig&gt;(1001);
		/// var npcTask = configService.GetAsync&lt;NpcConfig&gt;(2001);
		/// var (item, npc) = await Task.WhenAll(itemTask, npcTask);
		/// </code>
		/// </remarks>
		Task<T> GetAsync<T>(int id) where T : class, IAsakiConfig, new();
		/// <summary>
		/// 异步预加载指定类型的配置，即使其策略为<see cref="AsakiConfigLoadStrategy.OnDemand"/>或<see cref="AsakiConfigLoadStrategy.Manual"/>。
		/// </summary>
		/// <typeparam name="T">配置类型。</typeparam>
		/// <returns>表示异步预加载操作的<see cref="Task"/>。</returns>
		/// <remarks>
		/// <para>此方法强制加载配置，忽略其加载策略设置。</para>
		/// <para>适用场景：</para>
		/// <list type="bullet">
		///     <item>加载画面预加载下一关卡所需配置</item>
		///     <item>玩家进入新区域前预加载相关配置</item>
		///     <item>后台线程空闲时预加载预测需要的配置</item>
		/// </list>
		/// <para>内存影响：预加载后配置将常驻内存，直到显式调用<see cref="Unload{T}"/>或应用退出。</para>
		/// </remarks>
		Task PreloadAsync<T>() where T : class, IAsakiConfig, new();
		/// <summary>
		/// 异步预加载指定类型的配置。
		/// </summary>
		/// <param name="type">配置的类型对象。</param>
		/// <returns>表示异步预加载操作的<see cref="Task"/>。</returns>
		/// <exception cref="ArgumentNullException"><paramref name="type"/>为<c>null</c>。</exception>
		/// <seealso cref="PreloadAsync{T}"/>
		Task PreloadAsync(Type type);
		/// <summary>
		/// 批量异步预加载多个配置类型。
		/// </summary>
		/// <param name="configTypes">要预加载的配置类型数组。</param>
		/// <returns>表示异步批量加载操作的<see cref="Task"/>。</returns>
		/// <remarks>
		/// <para>优化策略：</para>
		/// <list type="bullet">
		///     <item>并行加载以最小化总耗时</item>
		///     <item>共享I/O调度器避免磁盘争用</item>
		///     <item>统一的异常聚合（若部分失败）</item>
		/// </list>
		/// <para>调用示例：</para>
		/// <code>
		/// await configService.PreloadBatchAsync(
		///     typeof(ItemConfig),
		///     typeof(NpcConfig),
		///     typeof(QuestConfig)
		/// );
		/// </code>
		/// </remarks>
		Task PreloadBatchAsync(params Type[] configTypes);
		/// <summary>
		/// 卸载指定类型的配置，释放内存并移除缓存。
		/// </summary>
		/// <typeparam name="T">配置类型。</typeparam>
		/// <remarks>
		/// <para>卸载条件：</para>
		/// <list type="bullet">
		///     <item>配置必须已加载（<see cref="IsLoaded{T}"/>为<c>true</c>）</item>
		///     <item><see cref="AsakiConfigLoadInfo.Unloadable"/>必须为<c>true</c></item>
		///     <item>不存在未完成的加载任务</item>
		/// </list>
		/// <para>副作用：</para>
		/// 卸载后再次调用<see cref="Get{T}"/>会触发重新加载（若策略允许）。
		/// 此方法不会清理<see cref="AsakiConfigLoadInfo"/>统计信息（AccessCount、LastAccessTime）。
		/// </remarks>
		void Unload<T>() where T : class, IAsakiConfig, new();
		/// <summary>
		/// 卸载指定类型的配置。
		/// </summary>
		/// <param name="type">要卸载的配置类型。</param>
		/// <exception cref="ArgumentNullException"><paramref name="type"/>为<c>null</c>。</exception>
		/// <seealso cref="Unload{T}"/>
		void Unload(Type type);
		/// <summary>
		/// 获取指定配置类型的加载信息和元数据。
		/// </summary>
		/// <typeparam name="T">配置类型。</typeparam>
		/// <returns><see cref="AsakiConfigLoadInfo"/>结构体，包含加载状态、策略、统计信息等。</returns>
		/// <remarks>
		/// 此方法不会触发加载操作，即使配置未加载也会返回结构体（其中<see cref="AsakiConfigLoadInfo.IsLoaded"/>为<c>false</c>）。
		/// 用于监控UI、调试工具和自动化管理系统。
		/// </remarks>
		AsakiConfigLoadInfo GetLoadInfo<T>() where T : class, IAsakiConfig, new();
	}
}
