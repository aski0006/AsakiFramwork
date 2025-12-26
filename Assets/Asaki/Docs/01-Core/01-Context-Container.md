# AsakiContext 服务容器

## 1. 概述
- **核心职责**：AsakiContext是一个线程安全的微内核服务容器，用于统一管理游戏中的各种服务和模块，提供服务注册、获取和生命周期管理功能。
- **设计哲学**：采用"多读少写"的设计模式，使用ReaderWriterLockSlim优化高并发场景，确保线程安全的同时保持高性能。
- **适用场景**：
  - 游戏服务管理：统一管理音频、UI、资源等核心服务
  - 模块生命周期管理：控制模块的初始化、异步加载和销毁
  - 依赖注入：实现模块间的解耦和依赖管理
  - 跨模块通信：提供统一的服务访问入口
  - 游戏状态管理：管理游戏全局状态和配置
- **依赖关系**：仅依赖.NET基础库（System、System.Collections.Generic、System.Threading、System.Threading.Tasks），无外部依赖。

## 2. 核心组件
### AsakiContext
**职责**：核心服务容器，负责服务的注册、获取和生命周期管理，提供线程安全的服务访问机制。
**生命周期**：静态类，随进程启动而存在，调用ClearAll或Dispose方法时销毁所有服务。
**关键API**：

```csharp
/// <summary>
/// 注册服务到容器
/// </summary>
/// <param name="type">服务类型，必须实现或继承IAsakiService</param>
/// <param name="service">服务实例，不能为空且必须可分配给type</param>
/// <exception cref="ArgumentNullException">当type或service为null时抛出</exception>
/// <exception cref="ArgumentException">当type未实现IAsakiService或service不可分配给type时抛出</exception>
/// <exception cref="Exception">当服务已注册时抛出</exception>
/// <remarks>线程安全：是</remarks>
/// <performance>O(1) 写入操作，需要获取写锁</performance>
public static void Register(Type type, IAsakiService service)

/// <summary>
/// 泛型注册服务到容器
/// </summary>
/// <typeparam name="T">服务类型，必须实现IAsakiService</typeparam>
/// <param name="service">服务实例，不能为空</param>
/// <exception cref="Exception">当服务已注册时抛出</exception>
/// <remarks>线程安全：是</remarks>
/// <performance>O(1) 写入操作，需要获取写锁</performance>
public static void Register<T>(T service) where T : IAsakiService

/// <summary>
/// 获取指定类型的服务
/// </summary>
/// <typeparam name="T">服务类型，必须实现IAsakiService</typeparam>
/// <returns>服务实例</returns>
/// <exception cref="Exception">当服务未注册时抛出</exception>
/// <remarks>线程安全：是</remarks>
/// <performance>O(1) 读取操作，需要获取读锁</performance>
public static T Get<T>() where T : IAsakiService

/// <summary>
/// 尝试获取指定类型的服务
/// </summary>
/// <typeparam name="T">服务类型，必须实现IAsakiService</typeparam>
/// <param name="service">输出参数，获取到的服务实例，未获取到时为默认值</param>
/// <returns>是否成功获取服务</returns>
/// <remarks>线程安全：是</remarks>
/// <performance>O(1) 读取操作，需要获取读锁</performance>
public static bool TryGet<T>(out T service) where T : IAsakiService

/// <summary>
/// 获取或注册服务
/// </summary>
/// <typeparam name="T">服务类型，必须实现IAsakiService</typeparam>
/// <param name="factory">服务工厂方法，用于创建新服务实例</param>
/// <returns>已存在的服务实例或新创建的服务实例</returns>
/// <remarks>线程安全：是</remarks>
/// <performance>O(1) 读取优先，未找到时升级为写锁创建服务</performance>
public static T GetOrRegister<T>(Func<T> factory) where T : IAsakiService

/// <summary>
/// 清空并销毁所有服务
/// </summary>
/// <remarks>
/// 线程安全：是
/// 销毁顺序：
/// 1. 如果是IAsakiModule实例，调用OnDispose方法
/// 2. 如果实现了IDisposable接口，调用Dispose方法
/// 3. 清除服务引用
/// </remarks>
/// <performance>O(n) 写入操作，需要获取写锁，n为服务数量</performance>
public static void ClearAll()

/// <summary>
/// 尝试获取可分配给指定类型的服务
/// </summary>
/// <typeparam name="T">目标类型，必须是引用类型</typeparam>
/// <param name="service">输出参数，获取到的服务实例，未获取到时为默认值</param>
/// <returns>是否成功获取服务</returns>
/// <remarks>线程安全：是</remarks>
/// <performance>O(n) 读取操作，需要遍历所有服务，n为服务数量</performance>
public static bool TryGetAssignable<T>(out T service) where T : class

/// <summary>
/// 释放资源
/// </summary>
/// <remarks>
/// 线程安全：是
/// 内部调用ClearAll方法销毁所有服务
/// </remarks>
/// <performance>O(n) 写入操作，需要获取写锁，n为服务数量</performance>
public static void Dispose()
```

### IAsakiService
**职责**：基础服务接口，所有注册到AsakiContext的服务必须实现此接口。
**生命周期**：无特定生命周期方法，仅作为服务标识。
**关键API**：
```csharp
/// <summary>
/// 基础服务接口，所有注册到AsakiContext的服务必须实现此接口
/// </summary>
public interface IAsakiService { }
```

### IAsakiModule
**职责**：模块生命周期契约，定义了模块的初始化、异步初始化和销毁阶段。
**生命周期**：
1. 实例创建 → 2. OnInit() → 3. OnInitAsync() → 4. 运行时 → 5. OnDispose() → 6. 实例销毁
**关键API**：

```csharp
/// <summary>
/// 同步初始化阶段
/// </summary>
/// <remarks>
/// 时机：模块实例被创建并注册到容器后立即调用
/// 职责：
/// - 获取配置
/// - 获取已就绪的依赖模块
/// - 注册此模块提供的额外子服务
/// 警告：严禁在此方法中再次注册模块自身
/// </remarks>
void OnInit();

/// <summary>
/// 异步初始化阶段
/// </summary>
/// <returns>异步任务</returns>
/// <remarks>
/// 时机：所有模块完成OnInit后，按DAG顺序依次调用
/// 职责：执行耗时的异步操作，如资源加载、网络连接、数据库预热
/// </remarks>
Task OnInitAsync();

/// <summary>
/// 销毁阶段
/// </summary>
/// <remarks>
/// 时机：游戏退出或重启时调用
/// 职责：清理非托管资源、断开连接
/// </remarks>
void OnDispose();
```

## 3. 设计模式与实现细节
### 3.1 线程安全实现
AsakiContext使用ReaderWriterLockSlim实现线程安全，优化了"多读少写"的高并发场景：
- 读操作（Get、TryGet、TryGetAssignable）获取读锁，允许多个线程同时读取
- 写操作（Register、ClearAll、Dispose）获取写锁，同一时间只允许一个线程写入
- GetOrRegister方法采用双重检查锁定模式，先获取读锁尝试获取服务，未找到时升级为可升级读锁，再检查一次，最后获取写锁创建服务

### 3.2 服务生命周期管理
AsakiContext提供了完整的服务生命周期管理：
- 注册阶段：将服务实例添加到容器中
- 运行阶段：通过Get、TryGet等方法访问服务
- 销毁阶段：调用ClearAll或Dispose方法时，依次执行：
  1. 调用IAsakiModule.OnDispose方法（如果服务实现了IAsakiModule）
  2. 调用IDisposable.Dispose方法（如果服务实现了IDisposable）
  3. 清除服务引用

### 3.3 模块依赖管理
AsakiContext支持模块间的依赖管理：
- 模块可以在OnInit方法中通过AsakiContext.Get<T>()获取已注册的依赖模块
- 模块加载器（外部实现）会根据模块间的依赖关系构建DAG图，按顺序初始化模块
- 异步初始化阶段（OnInitAsync）也会按DAG顺序依次调用

## 4. 使用示例
### 4.1 基本服务注册与获取
```csharp
// 定义服务接口和实现
public interface IGameService : IAsakiService {
    void StartGame();
}

public class GameServiceImpl : IGameService {
    public void StartGame() {
        Debug.Log("Game started!");
    }
}

// 注册服务
AsakiContext.Register<IGameService>(new GameServiceImpl());

// 获取服务
var gameService = AsakiContext.Get<IGameService>();
gameService.StartGame();
```

### 4.2 模块生命周期管理
```csharp
// 定义模块
[AsakiModule]
public class AudioModule : IAsakiModule {
    public void OnInit() {
        // 同步初始化：获取配置、注册子服务
        var config = AsakiContext.Get<AsakiConfig>();
        AsakiContext.Register<IAudioService>(new AudioServiceImpl(config));
    }

    public async Task OnInitAsync() {
        // 异步初始化：加载音频资源
        await LoadAudioResourcesAsync();
    }

    public void OnDispose() {
        // 销毁：释放音频资源
        ReleaseAudioResources();
    }

    private async Task LoadAudioResourcesAsync() {
        // 模拟异步加载
        await Task.Delay(1000);
    }

    private void ReleaseAudioResources() {
        // 释放资源
    }
}

// 模块会由外部加载器自动注册和初始化
```

### 4.3 使用GetOrRegister实现延迟加载
```csharp
// 延迟创建服务
var gameService = AsakiContext.GetOrRegister(() => new GameServiceImpl());
gameService.StartGame();
```

### 4.4 安全获取服务
```csharp
// 使用TryGet避免异常
if (AsakiContext.TryGet<IGameService>(out var gameService)) {
    gameService.StartGame();
} else {
    Debug.LogError("Game service not registered!");
}
```

## 5. 最佳实践
### 5.1 服务设计原则
- **单一职责**：每个服务只负责一个核心功能
- **接口抽象**：优先注册接口类型，而非具体实现类，便于后续替换实现
- **线程安全**：服务实现应考虑线程安全，特别是在多线程环境下使用的服务
- **资源管理**：实现IDisposable接口，妥善管理非托管资源

### 5.2 模块设计原则
- **明确依赖**：在OnInit方法中只获取已注册的依赖模块，避免循环依赖
- **异步优化**：耗时操作（如资源加载、网络请求）应放在OnInitAsync方法中执行
- **幂等设计**：Dispose方法应实现幂等性，避免多次调用导致错误
- **异常处理**：模块方法中应适当处理异常，避免影响整个系统

### 5.3 性能优化
- **减少写操作**：服务注册应集中在游戏启动阶段，避免运行时频繁注册
- **使用TryGet**：对于可能不存在的服务，使用TryGet而非Get，避免异常开销
- **合理使用GetOrRegister**：适用于延迟加载场景，避免不必要的初始化
- **避免在频繁调用的代码中使用TryGetAssignable**：TryGetAssignable需要遍历所有服务，性能开销较大

## 6. 常见问题与解决方案
### 6.1 服务已注册异常
**问题**：调用Register方法时抛出"Service of type {type} is already registered."异常
**解决方案**：
- 检查服务注册逻辑，避免重复注册
- 使用GetOrRegister方法替代Register，实现自动去重
- 使用TryGet方法先检查服务是否已注册

### 6.2 服务未找到异常
**问题**：调用Get方法时抛出"Service of type {type} is not registered."异常
**解决方案**：
- 检查服务注册顺序，确保依赖服务先注册
- 使用TryGet方法替代Get，避免异常
- 在模块OnInit方法中确保所有依赖服务已注册

### 6.3 循环依赖问题
**问题**：模块A依赖模块B，模块B又依赖模块A，导致初始化失败
**解决方案**：
- 重构模块设计，打破循环依赖
- 引入中间层服务，将共享功能提取到独立服务中
- 使用延迟加载，在运行时动态获取依赖，而非在初始化阶段

### 6.4 线程安全问题
**问题**：在多线程环境下使用AsakiContext时出现数据竞争
**解决方案**：
- 确保服务实现本身是线程安全的
- 避免在服务方法中修改共享状态
- 使用适当的同步机制保护共享资源

## 7. 扩展与定制
### 7.1 自定义服务容器
AsakiContext设计为静态类，不支持继承扩展。如果需要定制服务容器行为，可以：
- 封装AsakiContext，添加自定义逻辑
- 实现自己的服务容器，遵循相同的接口设计
- 扩展服务接口，添加额外功能

### 7.2 服务监控与日志
AsakiContext在Core层没有集成日志系统，但可以通过以下方式添加监控和日志：
- 在服务的生命周期方法中添加日志
- 封装AsakiContext的API，添加日志记录
- 实现事件钩子，在服务注册、获取和销毁时触发事件

## 8. 总结
AsakiContext是Asaki Framework的核心服务容器，提供了线程安全的服务注册、获取和生命周期管理功能。它采用"多读少写"的设计模式，优化了高并发场景的性能，支持模块生命周期管理和依赖注入。

AsakiContext的设计简洁高效，易于使用，适用于各种规模的游戏项目。通过合理使用AsakiContext，可以实现游戏服务的解耦和模块化，提高代码的可维护性和可扩展性。