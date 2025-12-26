# AsakiBroker 事件系统

## 1. 概述
- **核心职责**：AsakiBroker是一个全局强类型消息总线，负责运行时的高性能事件分发，实现组件和模块间的解耦通信。
- **设计哲学**：采用泛型静态桶设计，追求极致性能，确保无GC分配，实现线程安全的事件发布/订阅机制。
- **适用场景**：
  - 游戏事件系统：处理玩家输入、游戏状态变化、UI交互等
  - 模块间通信：实现音频、UI、资源等模块间的解耦通信
  - 异步操作通知：如资源加载完成、网络请求返回、任务完成等
  - 状态管理：管理游戏全局状态变化，如玩家等级提升、任务完成等
  - 调试和监控：在编辑器中监控事件发布，便于调试和性能分析
- **依赖关系**：仅依赖.NET基础库（System、System.Collections.Generic、System.Runtime.CompilerServices），无外部依赖。

## 2. 核心组件
### AsakiBroker
**职责**：全局强类型消息总线的核心实现，提供事件的发布、订阅和取消订阅功能，以及生命周期管理。
**生命周期**：静态类，随进程启动而存在，调用Cleanup方法时清理所有事件订阅。
**关键API**：

```csharp
/// <summary>
/// 异常处理委托，当事件处理程序抛出异常时调用
/// </summary>
/// <remarks>线程安全：是</remarks>
public static Action<Exception> OnException

/// <summary>
/// 订阅事件
/// </summary>
/// <typeparam name="T">事件类型，必须是结构体并实现IAsakiEvent接口</typeparam>
/// <param name="handler">事件处理程序，不能为空</param>
/// <remarks>
/// 线程安全：是（使用锁）
/// 开销：低（涉及数组复制）
/// 注意：同一处理程序不会被重复订阅
/// </remarks>
/// <performance>O(n) 写入操作，需要获取锁，n为当前订阅者数量</performance>
public static void Subscribe<T>(IAsakiHandler<T> handler) where T : struct, IAsakiEvent

/// <summary>
/// 取消订阅事件
/// </summary>
/// <typeparam name="T">事件类型，必须是结构体并实现IAsakiEvent接口</typeparam>
/// <param name="handler">事件处理程序，不能为空</param>
/// <remarks>
/// 线程安全：是（使用锁）
/// </remarks>
/// <performance>O(n) 写入操作，需要获取锁，n为当前订阅者数量</performance>
public static void Unsubscribe<T>(IAsakiHandler<T> handler) where T : struct, IAsakiEvent

/// <summary>
/// 发布事件
/// </summary>
/// <typeparam name="T">事件类型，必须是结构体并实现IAsakiEvent接口</typeparam>
/// <param name="e">事件实例，包含事件数据</param>
/// <remarks>
/// 线程安全：是（无锁并发读取）
/// 开销：极低（直接数组迭代，无GC）
/// 注意：如果事件处理程序抛出异常，会调用OnException委托，但不会中断后续事件处理
/// </remarks>
/// <performance>O(n) 读取操作，无锁，n为当前订阅者数量</performance>
[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static void Publish<T>(T e) where T : struct, IAsakiEvent

/// <summary>
/// 清理所有事件订阅
/// </summary>
/// <remarks>
/// 线程安全：是（使用锁）
/// 时机：应在AsakiBootstrapper.OnDestroy或重启游戏时调用
/// 注意：不会清除_cleanupActions列表，因为静态类的静态构造函数只执行一次
/// </remarks>
/// <performance>O(n) 操作，需要获取锁，n为已注册的清理操作数量</performance>
public static void Cleanup()
```

### IAsakiEvent
**职责**：事件标记接口，所有事件类型必须实现此接口以确保强类型检查。
**生命周期**：无特定生命周期方法，仅作为事件类型的标记。
**关键API**：
```csharp
/// <summary>
/// 事件标记接口，所有事件类型必须实现此接口
/// </summary>
public interface IAsakiEvent { }
```

### IAsakiHandler<T>
**职责**：事件处理程序接口，定义了事件处理方法。
**生命周期**：由实现者管理，订阅时注册到事件总线，取消订阅时从事件总线移除。
**关键API**：

```csharp
/// <summary>
/// 事件处理方法，当事件发布时调用
/// </summary>
/// <typeparam name="T">事件类型，必须是结构体并实现IAsakiEvent接口</typeparam>
/// <param name="e">事件实例，包含事件数据</param>
/// <remarks>
/// 线程安全：取决于实现者
/// 注意：如果抛出异常，会被捕获并调用AsakiBroker.OnException委托，但不会中断后续事件处理
/// </remarks>
void OnEvent(T e);
```

### EventBucket<T>（内部组件）
**职责**：泛型静态桶，为每种事件类型T维护独立的订阅者列表和缓存。
**生命周期**：静态类，在第一次使用时通过静态构造函数初始化，调用Cleanup方法时清理。
**设计特点**：
- 对于每一种事件类型T，JIT会生成一个独立的静态类，拥有独立的内存地址
- 使用List<IAsakiHandler<T>>存储订阅者，使用IAsakiHandler<T>[]作为发布时的缓存
- 订阅/取消订阅时使用锁保护，发布时无锁访问缓存数组
- 订阅/取消订阅后会重建缓存数组

## 3. 设计模式与实现细节
### 3.1 高性能设计
AsakiBroker采用了多种设计手段确保高性能：
- **泛型静态桶**：每种事件类型拥有独立的静态类，避免类型转换开销
- **无GC设计**：事件必须是结构体，发布时无堆分配
- **无锁发布**：发布操作使用volatile数组，无锁并发读取
- **AggressiveInlining**：Publish方法使用[MethodImpl(MethodImplOptions.AggressiveInlining)]标记，减少调用开销
- **懒加载初始化**：订阅者列表在第一次订阅时才初始化

### 3.2 线程安全实现
- **订阅/取消订阅**：使用锁保护，确保线程安全
- **发布**：使用volatile数组，无锁并发读取，确保线程安全
- **清理操作**：使用锁保护，确保所有清理操作有序执行

### 3.3 异常处理
- 事件处理程序抛出的异常会被捕获，不会中断后续事件处理
- 捕获的异常会通过OnException委托通知外部，便于调试和监控
- 这种设计确保了一个处理程序的错误不会影响整个事件系统

### 3.4 编辑器支持
- 在Unity编辑器环境下，AsakiBroker会触发OnPublishEvent事件，用于事件调试
- 编辑器调试窗口可以订阅此事件，监控所有事件发布

## 4. 使用示例
### 4.1 定义事件
```csharp
// 定义事件结构体，必须实现IAsakiEvent接口
public struct PlayerJumpEvent : IAsakiEvent {
    public float JumpForce;
    public Vector3 Position;
}

public struct GameOverEvent : IAsakiEvent {
    public int Score;
    public bool IsVictory;
}
```

### 4.2 实现事件处理程序
```csharp
// 实现IAsakiHandler<T>接口
public class PlayerController : MonoBehaviour, IAsakiHandler<PlayerJumpEvent> {
    public void OnEvent(PlayerJumpEvent e) {
        // 处理玩家跳跃事件
        Debug.Log($"Player jumped with force {e.JumpForce} at position {e.Position}");
    }
}

public class UIManager : MonoBehaviour, IAsakiHandler<GameOverEvent> {
    public void OnEvent(GameOverEvent e) {
        // 处理游戏结束事件
        Debug.Log($"Game over! Score: {e.Score}, Victory: {e.IsVictory}");
        // 显示游戏结束UI
    }
}
```

### 4.3 订阅和取消订阅事件
```csharp
public class GameManager : MonoBehaviour {
    private PlayerController _playerController;
    private UIManager _uiManager;

    private void Awake() {
        _playerController = FindObjectOfType<PlayerController>();
        _uiManager = FindObjectOfType<UIManager>();
    }

    private void OnEnable() {
        // 订阅事件
        AsakiBroker.Subscribe<PlayerJumpEvent>(_playerController);
        AsakiBroker.Subscribe<GameOverEvent>(_uiManager);
        
        // 使用Rolysn自动生成的注册代码：
        // this.AsakiRegister();
    }

    private void OnDisable() {
        // 取消订阅事件
        AsakiBroker.Unsubscribe<PlayerJumpEvent>(_playerController);
        AsakiBroker.Unsubscribe<GameOverEvent>(_uiManager);
        
        // 使用Rolysn自动生成的注销代码：
        // this.AsakiUnregister();
    }
    
    /* 
    // 补充：由Rolysn直接在编译期生成在代码中
    // <auto-generated/>
    // 生成时间: 12/22/2025 18:00:51
    using System;

    namespace Asaki.Generated
    {
        internal static class AsakiBrokerExtensions
        {
            internal static void AsakiRegister(this object obj)
            {
                // Handler: BrokerExample
                if (obj is global::Game.Test.BrokerExample handler_BrokerExample)
                {
                    global::Asaki.Core.Broker.AsakiBroker.Subscribe<global::Game.Test.AsakiPlayerJumpExampleEvent>(handler_BrokerExample);
                    return;
                }
            }

            internal static void AsakiUnregister(this object obj)
            {
                // Handler: BrokerExample
                if (obj is global::Game.Test.BrokerExample handler_BrokerExample)
                {
                    global::Asaki.Core.Broker.AsakiBroker.Unsubscribe<global::Game.Test.AsakiPlayerJumpExampleEvent>(handler_BrokerExample);
                    return;
                }
            }
        }
    }
 */
}
```

### 4.4 发布事件
```csharp
public class PlayerController : MonoBehaviour, IAsakiHandler<PlayerJumpEvent> {
    public void Jump() {
        // 执行跳跃逻辑
        float jumpForce = 10f;
        Vector3 position = transform.position;
        
        // 发布跳跃事件
        AsakiBroker.Publish(new PlayerJumpEvent {
            JumpForce = jumpForce,
            Position = position
        });
    }

    public void OnEvent(PlayerJumpEvent e) {
        // 处理跳跃事件
    }
}

public class GameManager : MonoBehaviour {
    public void EndGame(int score, bool isVictory) {
        // 发布游戏结束事件
        AsakiBroker.Publish(new GameOverEvent {
            Score = score,
            IsVictory = isVictory
        });
    }
}
```

### 4.5 设置异常处理
```csharp
public class GameManager : MonoBehaviour {
    private void Awake() {
        // 设置异常处理
        AsakiBroker.OnException = ex => {
            Debug.LogError($"Event handler exception: {ex.Message}\n{ex.StackTrace}");
        };
    }
}
```

### 4.6 清理所有事件订阅
```csharp
public class GameManager : MonoBehaviour {
    private void OnDestroy() {
        // 清理所有事件订阅，通常在游戏退出或重启时调用
        AsakiBroker.Cleanup();
    }
}
```

## 5. 最佳实践
### 5.1 事件设计原则
- **使用结构体**：事件必须是结构体，避免GC分配
- **实现IAsakiEvent**：确保强类型检查
- **精简事件数据**：只包含必要的事件数据，减少内存开销
- **命名规范**：使用XXEvent命名，清晰表达事件含义

### 5.2 事件处理程序设计原则
- **线程安全**：如果事件可能在多线程环境下发布，确保处理程序是线程安全的
- **避免阻塞**：事件处理程序应快速执行，避免阻塞事件发布线程
- **异常处理**：内部处理异常，避免抛出到事件总线
- **幂等性**：确保多次调用产生相同结果，避免副作用

### 5.3 订阅和取消订阅原则
- **成对出现**：在OnEnable/Start中订阅，在OnDisable/OnDestroy中取消订阅
- **避免重复订阅**：虽然AsakiBroker会自动去重，但应避免不必要的订阅调用
- **及时取消订阅**：不再需要时及时取消订阅，避免内存泄漏

### 5.4 性能优化
- **避免在高频事件中执行耗时操作**：如在每一帧发布的事件中执行复杂计算
- **合理使用事件粒度**：不要过度细分事件，也不要将过多不相关的数据放在一个事件中
- **批量处理**：对于高频事件，考虑批量处理以减少事件发布次数

## 6. 常见问题与解决方案
### 6.1 事件处理程序未被调用
**问题**：发布事件后，事件处理程序未被调用
**解决方案**：
- 检查事件类型是否正确实现了IAsakiEvent接口
- 检查事件是否是结构体类型
- 检查是否正确订阅了事件
- 检查订阅者是否还存在（未被销毁）
- 检查是否在正确的时间点发布事件

### 6.2 事件处理程序被多次调用
**问题**：同一个事件处理程序被多次调用
**解决方案**：
- 检查是否多次订阅了同一个处理程序（AsakiBroker会自动去重，通常不会出现此问题）
- 检查是否有多个实例订阅了同一事件类型
- 检查事件是否被多次发布

### 6.3 内存泄漏
**问题**：事件处理程序被销毁后，仍被事件总线引用
**解决方案**：
- 确保在处理程序被销毁前取消订阅
- 在MonoBehaviour的OnDisable或OnDestroy方法中取消订阅
- 使用Cleanup方法清理所有订阅，通常在游戏退出或重启时调用

### 6.4 多线程环境下的问题
**问题**：在多线程环境下使用AsakiBroker时出现问题
**解决方案**：
- 确保事件处理程序是线程安全的
- 避免在事件处理程序中修改共享状态，或使用适当的同步机制
- 注意Unity API只能在主线程调用，如果事件可能在其他线程发布，确保处理程序在主线程执行Unity API

## 7. 扩展与定制
### 7.1 自定义事件调试
AsakiBroker在Unity编辑器环境下提供了OnPublishEvent事件，可以用于自定义事件调试：

```csharp
#if UNITY_EDITOR
// 订阅事件发布通知
AsakiBroker.OnPublishEvent += eventType => {
    Debug.Log($"Event published: {eventType.Name}");
};
#endif
```

### 7.2 自定义异常处理
通过设置AsakiBroker.OnException委托，可以实现自定义异常处理：

```csharp
AsakiBroker.OnException = ex => {
    // 记录日志
    Debug.LogError($"Event handler exception: {ex.Message}\n{ex.StackTrace}");
    // 发送错误报告
    ErrorReporter.SendError(ex);
    // 显示错误提示
    UIManager.Instance.ShowError("An error occurred in event handling");
};
```

## 8. 版本历史
### 1.0.0
- 初始版本，提供基本的事件发布、订阅和取消订阅功能
- 采用泛型静态桶设计，确保高性能和无GC
- 支持线程安全的事件处理
- 包含编辑器调试支持

## 9. 总结
AsakiBroker是Asaki Framework的核心事件系统，提供了高性能、强类型、线程安全的事件发布/订阅机制。它采用了泛型静态桶设计，确保无GC分配和极低的发布开销，同时支持线程安全的订阅/取消订阅操作。

AsakiBroker的设计简洁高效，易于使用，适用于各种规模的游戏项目。通过合理使用AsakiBroker，可以实现组件和模块间的解耦通信，提高代码的可维护性和可扩展性。

AsakiBroker的核心优势在于其高性能设计，特别适合对性能要求较高的游戏开发场景。它的强类型设计也确保了类型安全，减少了运行时错误。

通过遵循最佳实践和设计原则，可以充分发挥AsakiBroker的优势，构建高效、可靠的游戏事件系统。