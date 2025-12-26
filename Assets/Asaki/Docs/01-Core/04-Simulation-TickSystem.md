# Simulation-TickSystem 模拟时钟系统

## 1. 概述
- **核心职责**：Simulation-TickSystem是Asaki Framework的核心时钟系统，负责统一管理游戏中的模拟更新，包括普通Tick和固定Tick，提供优先级排序和线程安全的注册/注销机制。
- **设计哲学**：采用统一时钟源设计，将Unity的时间系统与游戏逻辑解耦，支持优先级排序，确保更新顺序的可预测性，同时追求高性能和低开销。
- **适用场景**：
  - 游戏逻辑更新：如玩家移动、AI行为、物理模拟等
  - 系统服务更新：如音频、UI、资源管理等系统的定期更新
  - 状态同步：如网络同步、状态机更新等
  - 动画和特效：如骨骼动画、粒子系统等的更新
  - 调试和监控：如性能监控、日志记录等的定期执行
- **依赖关系**：依赖AsakiContext模块（IAsakiService接口），Unity环境下依赖Unity Engine（AsakiMonoDriver）。

## 2. 核心组件
### AsakiSimulationManager
**职责**：模拟管理器的核心实现，负责管理Tickable和FixedTickable对象，以及执行Tick和FixedTick方法。
**生命周期**：由AsakiBootstrapper创建和管理，初始化后通过AsakiMonoDriver驱动，游戏退出时由AsakiBootstrapper销毁。
**关键API**：

```csharp
/// <summary>
/// 注册普通Tick对象
/// </summary>
/// <param name="tickable">要注册的IAsakiTickable对象，不能为空</param>
/// <param name="priority">优先级，数值越小越先执行，默认值为TickPriority.Normal</param>
/// <remarks>
/// 线程安全：否
/// 性能：O(N)，需要查重，N为当前注册的Tickable数量
/// 注意：同一对象不会被重复注册
/// </remarks>
public void Register(IAsakiTickable tickable, int priority = (int)TickPriority.Normal)

/// <summary>
/// 注销普通Tick对象
/// </summary>
/// <param name="tickable">要注销的IAsakiTickable对象，不能为空</param>
/// <remarks>
/// 线程安全：否
/// 性能：O(N)，需要遍历查找，N为当前注册的Tickable数量
/// 注意：如果对象未注册，不会抛出异常
/// </remarks>
public void Unregister(IAsakiTickable tickable)

/// <summary>
/// 执行普通Tick更新
/// </summary>
/// <param name="deltaTime">时间增量，通常来自Unity的Time.deltaTime</param>
/// <remarks>
/// 线程安全：否
/// 性能：O(N)，N为当前注册的Tickable数量
/// 注意：
/// 1. 如果有新注册的Tickable，会先进行排序（稳定排序，保证同优先级按注册顺序执行）
/// 2. 正序遍历执行Tick方法，假设不会在Tick循环中Unregister自身
/// 3. 防御性编程，防止空引用
/// </remarks>
public void Tick(float deltaTime)

/// <summary>
/// 执行固定Tick更新
/// </summary>
/// <param name="fixedDeltaTime">固定时间增量，通常来自Unity的Time.fixedDeltaTime</param>
/// <remarks>
/// 线程安全：否
/// 性能：O(N)，N为当前注册的FixedTickable数量
/// 注意：
/// 1. 固定Tick用于物理模拟等需要固定时间步长的场景
/// 2. 不支持优先级排序，按注册顺序执行
/// </remarks>
public void FixedTick(float fixedDeltaTime)
```

### IAsakiTickable
**职责**：普通Tick接口，定义了游戏逻辑的普通更新方法。
**生命周期**：由实现者管理，注册到AsakiSimulationManager时开始接收Tick调用，注销时停止接收Tick调用。
**关键API**：

```csharp
/// <summary>
/// 普通Tick更新方法
/// </summary>
/// <param name="deltaTime">时间增量，通常来自Unity的Time.deltaTime</param>
/// <remarks>
/// 线程安全：否
/// 注意：
/// 1. 执行频率不固定，取决于帧率
/// 2. 用于非物理相关的游戏逻辑更新
/// </remarks>
void Tick(float deltaTime);
```

### IAsakiFixedTickable
**职责**：固定Tick接口，定义了游戏逻辑的固定更新方法。
**生命周期**：由实现者管理，注册到AsakiSimulationManager时开始接收FixedTick调用，注销时停止接收FixedTick调用。
**关键API**：

```csharp
/// <summary>
/// 固定Tick更新方法
/// </summary>
/// <param name="fixedDeltaTime">固定时间增量，通常来自Unity的Time.fixedDeltaTime</param>
/// <remarks>
/// 线程安全：否
/// 注意：
/// 1. 执行频率固定，默认为0.02秒（50Hz）
/// 2. 用于物理模拟等需要固定时间步长的场景
/// </remarks>
void FixedTick(float fixedDeltaTime);
```

### TickPriority
**职责**：优先级枚举，用于指定Tickable对象的执行顺序。
**生命周期**：静态枚举，无特定生命周期。
**关键API**：

```csharp
/// <summary>
/// Tick优先级枚举，数值越小越先执行
/// </summary>
public enum TickPriority
{
    High = 0,      // Input, Sensors - 高优先级，最先执行
    Normal = 1000, // Game Logic, FSM - 正常优先级，中间执行
    Low = 2000,    // UI, Audio, View Sync - 低优先级，最后执行
}
```

### AsakiMonoDriver
**职责**：Unity桥接类，负责从Unity的Update和FixedUpdate方法中驱动AsakiSimulationManager的Tick和FixedTick方法。
**生命周期**：由AsakiBootstrapper创建和管理，与Unity GameObject绑定，初始化后开始驱动模拟管理器，游戏退出时销毁。
**关键API**：

```csharp
/// <summary>
/// 初始化AsakiMonoDriver
/// </summary>
/// <param name="simManager">AsakiSimulationManager实例，不能为空</param>
/// <remarks>
/// 线程安全：否
/// 注意：必须在使用前初始化
/// </remarks>
public void Initialize(AsakiSimulationManager simManager)
```

## 3. 设计模式与实现细节
### 3.1 统一时钟源设计
- AsakiMonoDriver是整个框架唯一允许读取Unity Time的地方
- 所有游戏逻辑通过AsakiSimulationManager获取时间增量
- 这种设计将Unity的时间系统与游戏逻辑解耦，便于测试和跨平台

### 3.2 优先级排序机制
- 普通Tick支持优先级排序，数值越小越先执行
- 使用稳定排序（Stable Sort），保证同优先级按注册顺序执行
- 只有在有新注册的Tickable时才会重新排序，避免每帧排序的开销

### 3.3 两种Tick类型
1. **普通Tick**：
   - 调用频率不固定，取决于帧率
   - 支持优先级排序
   - 用于非物理相关的游戏逻辑

2. **固定Tick**：
   - 调用频率固定，默认为0.02秒（50Hz）
   - 不支持优先级排序，按注册顺序执行
   - 用于物理模拟等需要固定时间步长的场景

### 3.4 性能优化
- 普通Tick只有在有新注册时才会重新排序
- 注册时进行查重，避免重复注册
- 使用for循环遍历执行Tick，性能优于foreach
- 防御性编程，防止空引用

## 4. 使用示例
### 4.1 实现IAsakiTickable接口
```csharp
public class PlayerMovementSystem : IAsakiTickable {
    public void Tick(float deltaTime) {
        // 处理玩家移动逻辑
        _velocity += _acceleration * deltaTime;
        _position += _velocity * deltaTime;
    }
}
```

### 4.2 实现IAsakiFixedTickable接口
```csharp
public class PhysicsSystem : IAsakiFixedTickable {
    public void FixedTick(float fixedDeltaTime) {
        // 处理物理模拟
        foreach (var rigidbody in _rigidbodies) {
            rigidbody.Update(fixedDeltaTime);
        }
    }
}
```

### 4.3 注册和注销Tick对象
```csharp
public class GameSystemManager : IAsakiModule {
    private PlayerMovementSystem _movementSystem;
    private PhysicsSystem _physicsSystem;
    private AsakiSimulationManager _simManager;
    
    public void OnInit() {
        // 获取模拟管理器
        _simManager = AsakiContext.Get<AsakiSimulationManager>();
        
        // 创建系统实例
        _movementSystem = new PlayerMovementSystem();
        _physicsSystem = new PhysicsSystem();
        
        // 注册Tick对象，指定优先级
        _simManager.Register(_movementSystem, (int)TickPriority.Normal);
        
        // 注册固定Tick对象
        // 注意：FixedTick不支持优先级，AsakiSimulationManager内部没有提供FixedTick的Register方法
        // 这是因为FixedTick通常用于物理模拟，顺序不重要
        // 如果需要支持FixedTick的优先级，可以扩展AsakiSimulationManager
    }
    
    public Task OnInitAsync() {
        return Task.CompletedTask;
    }
    
    public void OnDispose() {
        // 注销Tick对象
        _simManager.Unregister(_movementSystem);
        
        // FixedTick的注销方式与普通Tick相同
        // _simManager.Unregister(_physicsSystem);
    }
}
```

### 4.4 自定义优先级
```csharp
// 定义自定义优先级
public static class CustomTickPriorities {
    public const int VeryHigh = -1000; // 比High优先级更高
    public const int Medium = 500;     // 介于High和Normal之间
    public const int VeryLow = 3000;   // 比Low优先级更低
}

// 使用自定义优先级注册
_simManager.Register(_criticalSystem, CustomTickPriorities.VeryHigh);
_simManager.Register(_backgroundSystem, CustomTickPriorities.VeryLow);
```

## 5. 最佳实践
### 5.1 优先级设计原则
- 按更新依赖关系设置优先级：被依赖的系统应具有更高优先级
- 高优先级用于时间敏感的系统：如输入处理、传感器更新
- 正常优先级用于核心游戏逻辑：如状态机、AI行为
- 低优先级用于非时间敏感的系统：如UI更新、音频处理

### 5.2 注册和注销原则
- 在模块的OnInit方法中注册Tick对象
- 在模块的OnDispose方法中注销Tick对象
- 避免在运行时频繁注册和注销Tick对象
- 确保注册和注销成对出现

### 5.3 性能优化
- 避免在Tick方法中执行耗时操作
- 对于高频更新的系统，考虑合并更新或降低更新频率
- 避免在Tick方法中创建或销毁大量对象
- 对于固定更新的物理系统，考虑使用Unity的物理引擎，而不是自定义实现

### 5.4 测试和调试
- 利用优先级排序，确保系统按预期顺序更新
- 在开发阶段，可以添加日志记录，监控Tick执行顺序和时间
- 考虑添加性能监控，检测耗时的Tick方法

## 6. 常见问题与解决方案
### 6.1 Tick方法未被调用
**问题**：注册了Tick对象，但Tick方法未被调用
**解决方案**：
- 检查是否正确注册了Tick对象
- 检查AsakiMonoDriver是否已初始化
- 检查AsakiSimulationManager是否已正确创建
- 检查是否在OnDispose方法中过早注销了Tick对象

### 6.2 Tick顺序不正确
**问题**：Tick对象的执行顺序与预期不符
**解决方案**：
- 检查优先级设置是否正确，数值越小越先执行
- 确保使用了稳定的优先级值，避免冲突
- 检查是否在运行时动态修改了优先级

### 6.3 性能问题
**问题**：Tick系统导致帧率下降
**解决方案**：
- 检查是否有耗时的Tick方法
- 考虑降低某些系统的更新频率
- 检查是否有过多的Tick对象注册
- 考虑合并某些系统的更新逻辑

### 6.4 固定Tick与物理不同步
**问题**：自定义物理系统与Unity物理不同步
**解决方案**：
- 确保固定Tick使用与Unity物理相同的时间步长
- 考虑使用Unity的物理引擎，而不是自定义实现
- 确保固定Tick的执行频率与Unity的fixedDeltaTime一致

## 7. 扩展与定制
### 7.1 支持FixedTick优先级
```csharp
// 扩展AsakiSimulationManager，支持FixedTick优先级
public class ExtendedSimulationManager : AsakiSimulationManager {
    private struct FixedTickableWrapper {
        public IAsakiFixedTickable Tickable;
        public int Priority;
    }
    
    private readonly List<FixedTickableWrapper> _fixedTickables = new List<FixedTickableWrapper>();
    private bool _fixedIsDirty = false;
    
    public void RegisterFixed(IAsakiFixedTickable tickable, int priority = (int)TickPriority.Normal) {
        // 查重逻辑
        for (int i = 0; i < _fixedTickables.Count; i++) {
            if (_fixedTickables[i].Tickable == tickable) return;
        }
        
        _fixedTickables.Add(new FixedTickableWrapper { Tickable = tickable, Priority = priority });
        _fixedIsDirty = true;
    }
    
    public void UnregisterFixed(IAsakiFixedTickable tickable) {
        // 注销逻辑
        for (int i = 0; i < _fixedTickables.Count; i++) {
            if (_fixedTickables[i].Tickable == tickable) {
                _fixedTickables.RemoveAt(i);
                return;
            }
        }
    }
    
    public new void FixedTick(float fixedDeltaTime) {
        if (_fixedIsDirty) {
            _fixedTickables.Sort((a, b) => a.Priority.CompareTo(b.Priority));
            _fixedIsDirty = false;
        }
        
        for (int i = 0; i < _fixedTickables.Count; i++) {
            var wrapper = _fixedTickables[i];
            if (wrapper.Tickable != null) {
                wrapper.Tickable.FixedTick(fixedDeltaTime);
            }
        }
    }
}
```

### 7.2 添加Tick事件
```csharp
// 扩展AsakiSimulationManager，添加Tick事件
public class EventSimulationManager : AsakiSimulationManager {
    public event Action<float> OnBeforeTick;
    public event Action<float> OnAfterTick;
    
    public new void Tick(float deltaTime) {
        OnBeforeTick?.Invoke(deltaTime);
        base.Tick(deltaTime);
        OnAfterTick?.Invoke(deltaTime);
    }
}
```

## 8. 版本历史
### 1.0.0
- 初始版本，提供基本的Tick和FixedTick功能
- 支持普通Tick的优先级排序
- 包含AsakiMonoDriver桥接类
- 支持统一时钟源设计

## 9. 总结
Simulation-TickSystem是Asaki Framework的核心时钟系统，提供了统一的模拟更新管理。它采用统一时钟源设计，将Unity的时间系统与游戏逻辑解耦，支持优先级排序，确保更新顺序的可预测性，同时追求高性能和低开销。

AsakiSimulationManager是TickSystem的核心组件，负责管理Tickable和FixedTickable对象，以及执行Tick和FixedTick方法。AsakiMonoDriver是Unity桥接类，负责从Unity的Update和FixedUpdate方法中驱动AsakiSimulationManager。

通过合理使用TickSystem，可以实现游戏逻辑的有序更新，提高代码的可维护性和可测试性。遵循最佳实践，可以确保TickSystem的高性能和可靠性。

TickSystem的设计简洁高效，易于扩展和定制，适用于各种规模的游戏项目。它是Asaki Framework的重要组成部分，为游戏逻辑的执行提供了坚实的基础。