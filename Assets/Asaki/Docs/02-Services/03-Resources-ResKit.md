# Resources-ResKit
没有历史版本

## 1. 概述

### 核心职责
Resources-ResKit是Asaki Framework的资源管理模块，提供统一、高效的资源加载、缓存和释放机制，支持多种资源加载策略（Resources/Addressables）。

### 设计哲学
- **策略模式**：通过接口抽象实现不同资源加载方式的无缝切换
- **引用计数**：自动管理资源生命周期，防止内存泄漏
- **异步优先**：全面支持异步加载，提高游戏运行流畅度
- **依赖管理**：自动处理资源间的依赖关系
- **线程安全**：支持多线程调用，保证并发安全性

### 适用场景
- 游戏场景加载：异步加载场景资源，显示加载进度
- 资源预加载：在游戏启动或场景切换时预加载关键资源
- 动态资源管理：根据游戏需求动态加载和释放资源，优化内存使用
- 多模式支持：在不同开发阶段（开发/测试/发布）使用不同的资源加载策略
- 批量资源操作：一次性加载或释放多个资源，提高操作效率

### 依赖关系
- 核心服务：IAsakiContext、IAsakiRoutineService
- 异步支持：Task、CancellationToken
- 条件依赖：Addressables（仅在定义ASAKI_USE_ADDRESSABLE宏时）、UniTask（根据宏定义可选）

## 2. 核心组件

### IAsakiResService
**职责**：资源服务主接口，定义资源加载、释放的核心方法
**生命周期**：模块初始化时创建，应用退出时销毁
**关键API**：

```csharp
/// <summary>
/// 异步加载单个资源（带进度回调）
/// </summary>
/// <typeparam name="T">资源类型</typeparam>
/// <param name="location">资源位置标识符</param>
/// <param name="onProgress">进度回调（0.0~1.0）</param>
/// <param name="token">取消令牌，用于取消加载操作</param>
/// <returns>资源句柄，包含加载的资源实例</returns>
/// <exception cref="InvalidCastException">资源类型不匹配时抛出</exception>
/// <exception cref="OperationCanceledException">加载被取消时抛出</exception>
/// <exception cref="TimeoutException">依赖加载超时时抛出</exception>
/// <remarks>线程安全，支持并发调用</remarks>
Task<ResHandle<T>> LoadAsync<T>(string location, Action<float> onProgress, CancellationToken token) where T : class;
```

```csharp
/// <summary>
/// 异步加载单个资源（不带进度回调）
/// </summary>
/// <typeparam name="T">资源类型</typeparam>
/// <param name="location">资源位置标识符</param>
/// <param name="token">取消令牌，用于取消加载操作</param>
/// <returns>资源句柄，包含加载的资源实例</returns>
/// <exception cref="InvalidCastException">资源类型不匹配时抛出</exception>
/// <exception cref="OperationCanceledException">加载被取消时抛出</exception>
/// <exception cref="TimeoutException">依赖加载超时时抛出</exception>
/// <remarks>线程安全，支持并发调用</remarks>
Task<ResHandle<T>> LoadAsync<T>(string location, CancellationToken token) where T : class;
```

```csharp
/// <summary>
/// 释放单个资源
/// </summary>
/// <param name="location">资源位置标识符</param>
/// <remarks>线程安全，支持并发调用</remarks>
/// <remarks>内部使用引用计数机制，当引用计数为0时实际释放资源</remarks>
void Release(string location);
```

```csharp
/// <summary>
/// 异步批量加载资源（带进度回调）
/// </summary>
/// <typeparam name="T">资源类型</typeparam>
/// <param name="locations">资源位置标识符列表</param>
/// <param name="onProgress">总进度回调（0.0~1.0）</param>
/// <param name="token">取消令牌，用于取消加载操作</param>
/// <returns>资源句柄列表，包含所有加载的资源实例</returns>
/// <exception cref="InvalidCastException">任何资源类型不匹配时抛出</exception>
/// <exception cref="OperationCanceledException">加载被取消时抛出</exception>
/// <exception cref="TimeoutException">任何依赖加载超时时抛出</exception>
/// <remarks>线程安全，支持并发调用</remarks>
/// <remarks>内部计算所有资源的平均进度</remarks>
Task<List<ResHandle<T>>> LoadBatchAsync<T>(IEnumerable<string> locations, Action<float> onProgress, CancellationToken token) where T : class;
```

```csharp
/// <summary>
/// 异步批量加载资源（不带进度回调）
/// </summary>
/// <typeparam name="T">资源类型</typeparam>
/// <param name="locations">资源位置标识符列表</param>
/// <param name="token">取消令牌，用于取消加载操作</param>
/// <returns>资源句柄列表，包含所有加载的资源实例</returns>
/// <exception cref="InvalidCastException">任何资源类型不匹配时抛出</exception>
/// <exception cref="OperationCanceledException">加载被取消时抛出</exception>
/// <exception cref="TimeoutException">任何依赖加载超时时抛出</exception>
/// <remarks>线程安全，支持并发调用</remarks>
Task<List<ResHandle<T>>> LoadBatchAsync<T>(IEnumerable<string> locations, CancellationToken token) where T : class;
```

```csharp
/// <summary>
/// 批量释放资源
/// </summary>
/// <param name="locations">资源位置标识符列表</param>
/// <remarks>线程安全，支持并发调用</remarks>
/// <remarks>内部逐个调用Release方法</remarks>
void ReleaseBatch(IEnumerable<string> locations);
```

### ResHandle<T>
**职责**：资源句柄，用于自动管理资源的生命周期
**生命周期**：创建时关联资源，Dispose时自动释放资源
**关键API**：

```csharp
/// <summary>
/// 资源位置标识符
/// </summary>
public readonly string Location;
```

```csharp
/// <summary>
/// 加载的资源实例
/// </summary>
public readonly T Asset;
```

```csharp
/// <summary>
/// 资源是否有效（Asset不为null）
/// </summary>
public bool IsValid { get; }
```

```csharp
/// <summary>
/// 构造函数
/// </summary>
/// <param name="location">资源位置标识符</param>
/// <param name="asset">资源实例</param>
/// <param name="service">资源服务实例</param>
public ResHandle(string location, T asset, IAsakiResService service);
```

```csharp
/// <summary>
/// 释放资源
/// </summary>
/// <remarks>自动调用资源服务的Release方法</remarks>
/// <remarks>实现IDisposable接口，建议使用using语句管理</remarks>
public void Dispose();
```

```csharp
/// <summary>
/// 隐式转换为资源类型
/// </summary>
/// <param name="handle">资源句柄</param>
/// <returns>资源实例</returns>
public static implicit operator T(ResHandle<T> handle);
```

### IAsakiResStrategy
**职责**：资源加载策略接口，定义不同资源加载方式的统一方法
**生命周期**：资源服务初始化时创建，资源服务销毁时销毁
**关键API**：

```csharp
/// <summary>
/// 策略名称
/// </summary>
string StrategyName { get; }
```

```csharp
/// <summary>
/// 初始化策略
/// </summary>
/// <returns>初始化完成的Task</returns>
Task InitializeAsync();
```

```csharp
/// <summary>
/// 内部加载资源（带进度回调）
/// </summary>
/// <param name="location">资源位置标识符</param>
/// <param name="type">资源类型</param>
/// <param name="onProgress">进度回调（0.0~1.0）</param>
/// <param name="token">取消令牌</param>
/// <returns>加载的资源实例</returns>
Task<UnityEngine.Object> LoadAssetInternalAsync(string location, Type type, Action<float> onProgress, CancellationToken token);
```

```csharp
/// <summary>
/// 内部卸载资源
/// </summary>
/// <param name="location">资源位置标识符</param>
/// <param name="asset">资源实例</param>
void UnloadAssetInternal(string location, UnityEngine.Object asset);
```

### AsakiResService
**职责**：IAsakiResService的具体实现，管理资源的加载、缓存和释放
**生命周期**：模块初始化时创建，应用退出时销毁
**核心实现**：
- 资源缓存管理：使用Dictionary存储资源记录
- 引用计数机制：通过Interlocked原子操作管理引用计数
- 依赖关系管理：自动加载和释放依赖资源
- 线程安全：使用lock和Interlocked保证并发安全性
- 批量操作：优化批量加载和释放的性能

## 3. 资源加载策略

### AsakiResourcesStrategy
**职责**：基于Unity原生Resources API的资源加载策略
**适用场景**：开发阶段快速迭代，资源量较小的项目
**核心特点**：
- 无需额外配置，使用简单
- 支持进度回调和取消操作
- 适用于小到中型项目

### AsakiAddressablesStrategy
**职责**：基于Unity Addressables的资源加载策略
**适用场景**：大型项目，需要复杂资源管理（热更新、资源包管理）
**核心特点**：
- 支持热更新和资源包管理
- 更高效的资源内存管理
- 仅在定义ASAKI_USE_ADDRESSABLE宏时生效
- 支持进度回调和取消操作

## 4. 配置与初始化

### 配置文件
- **配置类**：`AsakiConfig`
- **配置资源路径**：`Resources/Asaki/AsakiConfig.asset`
- **关键配置项**：
  - `ResKitMode`：资源加载模式（Resources/Addressables/Custom）

### 模块初始化
模块通过`AsakiResKitModule`自动初始化：
1. 应用启动时，AsakiBootstrapper加载配置
2. 根据配置的`ResKitMode`，通过`AsakiResKitFactory`创建相应的资源服务实例
3. 将资源服务实例注册到`AsakiContext`中
4. 其他模块通过`AsakiContext`获取资源服务实例使用

## 5. 最佳实践

### 使用ResHandle<T>进行资源管理
```csharp
// 推荐：使用using语句自动释放资源
using var handle = await resService.LoadAsync<GameObject>("Prefabs/MyPrefab", CancellationToken.None);
gameObject.Instantiate(handle.Asset);

// 或使用隐式转换
using var handle = await resService.LoadAsync<Texture2D>("Textures/MyTexture", CancellationToken.None);
renderer.material.mainTexture = handle;
```

### 批量加载优化
```csharp
// 批量加载多个资源，使用进度回调
var locations = new[] { "Prefabs/Obj1", "Prefabs/Obj2", "Prefabs/Obj3" };
var handles = await resService.LoadBatchAsync<GameObject>(locations, progress => {
    loadingUI.SetProgress(progress);
}, CancellationToken.None);

// 使用资源
foreach (var handle in handles)
{
    Instantiate(handle.Asset);
}

// 批量释放
resService.ReleaseBatch(locations);
```

### 资源预加载
```csharp
// 在游戏启动时预加载关键资源
async Task PreloadResources()
{
    var preloadList = new[] { "Audio/BGM", "Textures/UI/Common", "Prefabs/UI/Loading" };
    await resService.LoadBatchAsync<Object>(preloadList, CancellationToken.None);
}
```

### 取消加载操作
```csharp
// 使用CancellationTokenSource取消加载
var cts = new CancellationTokenSource();

// 10秒后自动取消
cts.CancelAfter(TimeSpan.FromSeconds(10));

try
{
    var handle = await resService.LoadAsync<GameObject>("LargeAsset", cts.Token);
    // 使用资源
}
catch (OperationCanceledException)
{
    Debug.Log("资源加载已取消");
}
```

## 6. 常见问题与解决方案

### 1. 资源加载超时
**现象**：加载资源时抛出TimeoutException
**原因**：可能存在循环依赖或资源加载过慢
**解决方案**：
- 检查资源间的依赖关系，避免循环依赖
- 优化资源大小，提高加载速度
- 考虑使用异步加载，避免阻塞主线程

### 2. 资源类型不匹配
**现象**：加载资源时抛出InvalidCastException
**原因**：请求的资源类型与实际资源类型不匹配
**解决方案**：
- 确保请求的资源类型与实际资源类型一致
- 使用Object类型加载，然后手动转换

### 3. 资源泄漏
**现象**：游戏运行时内存占用持续增加
**原因**：资源没有正确释放，引用计数没有减到0
**解决方案**：
- 始终使用using语句或手动调用Dispose方法释放ResHandle
- 避免长期持有ResHandle实例
- 使用资源调试器（AsakiResDebuggerWindow）检查资源使用情况

### 4. 并发加载同一资源
**现象**：多次并发加载同一资源
**解决方案**：
- ResKit内部自动处理并发加载，多次加载同一资源会共享引用计数
- 无需手动加锁，模块内部已保证线程安全

## 7. 架构设计与实现细节

### 资源缓存机制
- 使用Dictionary<string, ResRecord>存储资源记录
- 每个资源记录包含资源实例、引用计数、依赖关系等信息
- 线程安全设计，使用lock保证缓存操作的原子性

### 引用计数实现
- 使用Interlocked原子操作管理引用计数
- 加载资源时增加引用计数，释放资源时减少引用计数
- 当引用计数为0时，自动释放资源及其依赖

### 依赖管理
- 通过IAsakiResDependencyLookup接口查询资源依赖
- 自动加载依赖资源，记录依赖关系
- 释放资源时，递归释放所有依赖资源

### 异步加载流程
1. 调用LoadAsync方法，获取或创建资源记录
2. 增加引用计数，注册进度回调
3. 如果资源正在加载，等待加载完成
4. 如果资源未加载，启动加载任务
5. 加载任务先加载所有依赖资源
6. 然后加载自身资源，更新进度
7. 完成加载后，设置TaskCompletionSource结果
8. 返回ResHandle实例给调用者

### 错误处理机制
- 加载失败时，自动清理已加载的依赖资源
- 取消加载时，回滚引用计数
- 异常会被正确传递给调用者
- 提供详细的错误日志，便于调试

## 8. 性能优化建议

1. **批量操作优先**：使用批量加载和释放方法，减少频繁调用的开销
2. **资源预加载**：在合适的时机预加载关键资源，减少游戏运行时的加载延迟
3. **合理使用缓存**：根据资源使用频率，决定是否长期持有资源
4. **避免同步加载**：尽量使用异步加载方法，避免阻塞主线程
5. **优化资源大小**：减小资源文件大小，提高加载速度
6. **使用Addressables**：对于大型项目，考虑使用Addressables策略，获得更好的资源管理
7. **定期清理资源**：在合适的时机释放不再使用的资源，优化内存占用

## 9. 与其他模块的交互

### 与场景管理模块的交互
- 场景加载时，使用ResKit加载场景所需资源
- 场景切换时，释放当前场景不再使用的资源

### 与UI模块的交互
- UI组件使用ResKit加载UI资源（图片、预制体等）
- 加载UI资源时显示加载进度

### 与音频模块的交互
- 音频模块使用ResKit加载音频资源
- 支持异步加载音频资源，避免阻塞主线程

### 与配置模块的交互
- 从AsakiConfig读取资源加载模式配置
- 支持运行时动态切换资源加载模式

## 10. 扩展与自定义

### 自定义资源加载策略
1. 实现IAsakiResStrategy接口
2. 在AsakiResKitFactory中注册自定义策略
3. 修改配置文件，将ResKitMode设置为Custom

### 扩展资源服务功能
- 通过继承AsakiResService类，扩展资源服务功能
- 实现自定义的资源加载和释放逻辑

### 自定义依赖查询
1. 实现IAsakiResDependencyLookup接口
2. 在创建AsakiResService实例时传入自定义依赖查询实现

## 11. 调试与监控

### 资源调试器
- **编辑器窗口**：AsakiResDebuggerWindow
- **功能**：查看当前加载的资源、引用计数、依赖关系等
- **使用方法**：在Unity编辑器中，选择Window > Asaki > Res Debugger

### 日志与错误信息
- 资源加载和释放操作会输出详细的日志信息
- 错误信息包含资源位置、错误类型和堆栈跟踪
- 建议在开发阶段开启详细日志，便于调试

### 性能监控
- 监控资源加载时间和内存占用
- 分析资源加载的性能瓶颈
- 优化资源加载策略和资源大小

## 12. 版本兼容性

### Unity版本支持
- 支持Unity 2019.4及以上版本
- 支持不同Unity版本的Resources和Addressables API

### 平台支持
- 支持所有Unity支持的平台
- 不同平台的资源加载策略可能有所不同

### 脚本后端支持
- 支持Mono和IL2CPP脚本后端
- 支持AOT编译（如iOS平台）

## 13. 总结

Resources-ResKit是一个功能强大、设计灵活的资源管理模块，提供了统一的资源加载、缓存和释放机制。通过策略模式，支持多种资源加载方式的无缝切换，适应不同项目和开发阶段的需求。

模块的核心优势包括：
- 自动管理资源生命周期，防止内存泄漏
- 全面支持异步加载，提高游戏运行流畅度
- 线程安全设计，支持多线程调用
- 自动处理资源依赖关系
- 支持批量资源操作，提高效率
- 灵活的扩展机制，支持自定义资源加载策略

Resources-ResKit模块为游戏开发提供了可靠的资源管理解决方案，帮助开发者优化资源使用，提高游戏性能和开发效率。