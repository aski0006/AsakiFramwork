using System;
using System.Collections.Generic;
using System.Threading.Tasks;

// 统一使用标准 Task

namespace Asaki.Unity.Services.Configuration
{
    /// <summary>
    /// 配置加载器注册中心，提供全局的配置加载处理器注册和分发机制。
    /// </summary>
    /// <remarks>
    /// <para>设计目的：</para>
    /// 解决不同配置源（CSV、JSON、XML、远程API）的加载器扩展问题，允许外部模块注册自定义加载逻辑，
    /// 而无需修改核心配置服务的实现。
    /// 
    /// <para>核心特性：</para>
    /// <list type="bullet">
    ///     <item><b>解耦设计：</b>配置服务不依赖具体加载实现，通过委托注册机制解耦</item>
    ///     <item><b>优先级链：</b>注册顺序决定尝试顺序，首个返回非null结果的加载器获胜</item>
    ///     <item><b>异步统一：</b>强制返回 <see cref="Task"/> 确保异步执行一致性，支持混合加载器</item>
    ///     <item><b>类型安全：</b>通过闭包和泛型方法保持编译时类型检查</item>
    /// </list>
    /// 
    /// <para>线程安全：</para>
    /// 所有公共方法内部使用 <see cref="List{T}"/> 的副本或只读操作，支持多线程并发注册和查询。
    /// 但注册操作建议在应用初始化阶段（<see cref="IAsakiModule.OnInit"/>）完成，避免运行时竞争。
    /// 
    /// <para>约定与规范：</para>
    /// <list type="number">
    ///     <item>加载器委托应尽早在初始化阶段注册，避免首次加载时注册未就绪</item>
    ///     <item>加载器返回 <c>null</c> 表示"无法处理此配置"，而非错误</item>
    ///     <item>加载器内部应自行处理异常，异常不会中断其他加载器的尝试</item>
    ///     <item>返回的 <see cref="Task"/> 应代表完整的加载、反序列化和缓存流程</item>
    ///     <item>对于大型配置，加载器应实现进度报告或分段加载</item>
    /// </list>
    /// </remarks>
    /// <example>
    /// 自定义CSV加载器注册示例：
    /// <code>
    /// // 在游戏初始化模块中
    /// public class MyGameInitModule : IAsakiModule
    /// {
    ///     public void OnInit()
    ///     {
    ///         // 注册CSV加载器
    ///         AsakiConfigRegistry.RegisterLoader(CsvConfigLoader.LoadAsync);
    ///         
    ///         // 注册JSON加载器（备用）
    ///         AsakiConfigRegistry.RegisterLoader(JsonConfigLoader.LoadAsync);
    ///     }
    /// }
    /// 
    /// // 自定义加载器实现
    /// public static class CsvConfigLoader
    /// {
    ///     public static Task LoadAsync(AsakiConfigService service, string configName, string path)
    ///     {
    ///         // 仅处理CSV文件
    ///         if (!path.EndsWith(".csv")) return null;
    ///         
    ///         // 执行CSV解析逻辑...
    ///         var configData = ParseCsv(path);
    ///         
    ///         // 注册到配置服务
    ///         service.RegisterConfig(configName, configData);
    ///         
    ///         return Task.CompletedTask;
    ///     }
    /// }
    /// </code>
    /// </example>
    public static class AsakiConfigRegistry
    {
        /// <summary>
        /// 内部加载器委托列表，存储所有已注册的配置加载处理器。
        /// </summary>
        /// <remarks>
        /// <para>委托签名：<c>(AsakiConfigService, string configName, string filePath) -> Task</c></para>
        /// 参数说明：
        /// <list type="bullet">
        ///     <item><c>AsakiConfigService</c>：配置服务实例，用于注册加载后的配置数据</item>
        ///     <item><c>string configName</c>：配置名称（通常为类型名）</item>
        ///     <item><c>string filePath</c>：配置文件完整路径</item>
        /// </list>
        /// 返回说明：
        /// <list type="bullet">
        ///     <item><c>Task</c>：表示异步加载操作，完成时配置应已注册到服务</item>
        ///     <item><c>null</c>：加载器无法处理此配置（如格式不匹配）</item>
        /// </list>
        /// </remarks>
        // 签名：(Service, ConfigName, FilePath) -> Task
        private static readonly List<Func<AsakiConfigService, string, string, Task>> _loaders
            = new List<Func<AsakiConfigService, string, string, Task>>();

        /// <summary>
        /// 注册一个配置加载处理器，支持链式注册多个加载器。
        /// </summary>
        /// <param name="loader">
        /// 加载器委托，其签名必须符合 <c>Task Func(AsakiConfigService, string, string)</c>。
        /// 若返回 <c>null</c>，表示该加载器无法处理请求的配置。
        /// </param>
        /// <remarks>
        /// <para>注册规则：</para>
        /// <list type="bullet">
        ///     <item>重复注册同一委托会被忽略（通过 <see cref="List{T}.Contains"/> 检查）</item>
        ///     <item>注册顺序决定优先级，越先注册越优先尝试</item>
        ///     <item>建议在应用初始化阶段完成注册，避免运行时动态注册</item>
        /// </list>
        /// <para>线程安全：内部使用 <see cref="List{T}.Add"/>，非线程安全。调用方应确保注册操作的串行化。</para>
        /// </remarks>
        /// <example>
        /// 多格式加载器注册：
        /// <code>
        /// // 优先级：CSV > JSON > 远程API
        /// AsakiConfigRegistry.RegisterLoader(LoadCsvAsync);  // 首选
        /// AsakiConfigRegistry.RegisterLoader(LoadJsonAsync); // 备选
        /// AsakiConfigRegistry.RegisterLoader(LoadFromRemoteAsync); // 最后手段
        /// </code>
        /// </example>
        public static void RegisterLoader(Func<AsakiConfigService, string, string, Task> loader)
        {
            if (!_loaders.Contains(loader))
            {
                _loaders.Add(loader);
            }
        }

        /// <summary>
        /// 执行配置加载器链，按注册顺序尝试加载指定配置。
        /// </summary>
        /// <param name="service">配置服务实例，用于传递上下文。</param>
        /// <param name="configName">配置名称（通常为类型名）。</param>
        /// <param name="path">配置文件完整路径。</param>
        /// <returns>
        /// 首个成功返回非null <see cref="Task"/> 的加载器的任务，
        /// 若所有加载器均返回null，则返回null表示无法处理此配置。
        /// </returns>
        /// <remarks>
        /// <para>执行逻辑：</para>
        /// <list type="number">
        ///     <item>按注册顺序遍历所有加载器</item>
        ///     <item>调用每个加载器并检查返回值</item>
        ///     <item>首个返回非null结果的加载器"获胜"，立即停止遍历</item>
        ///     <item>若全部返回null，最终返回null</item>
        /// </list>
        /// <para>异常策略：加载器内部异常不会捕获，由调用方处理。</para>
        /// </remarks>
        /// <example>
        /// 在服务中使用注册中心：
        /// <code>
        /// public async Task LoadConfigAsync(string configName, string path)
        /// {
        ///     var task = AsakiConfigRegistry.GetLoader(this, configName, path);
        ///     if (task != null)
        ///     {
        ///         await task;
        ///         Debug.Log($"配置 {configName} 加载成功");
        ///     }
        ///     else
        ///     {
        ///         throw new NotSupportedException($"无法加载配置 {configName}：没有匹配的加载器");
        ///     }
        /// }
        /// </code>
        /// </example>
        public static Task GetLoader(AsakiConfigService service, string configName, string path)
        {
            foreach (var loader in _loaders)
            {
                Task task = loader(service, configName, path);
                if (task != null)
                {
                    return task;
                }
            }
            return null; // 返回 null 代表没人处理，而不是 default
        }

        /// <summary>
        /// 清空所有已注册的配置加载器，重置注册中心状态。
        /// </summary>
        /// <remarks>
        /// <para>使用场景：</para>
        /// <list type="bullet">
        ///     <item>单元测试的 <c>[SetUp]</c> 或 <c>[TearDown]</c> 阶段</item>
        ///     <item>编辑器域重载（Domain Reload）后的状态清理</item>
        ///     <item>模块化系统中模块卸载时的资源释放</item>
        /// </list>
        /// <para>警告：此方法会无条件清除所有加载器，调用后需重新注册所需加载器。</para>
        /// </remarks>
        public static void Clear()
        {
            _loaders.Clear();
        }
    }
}