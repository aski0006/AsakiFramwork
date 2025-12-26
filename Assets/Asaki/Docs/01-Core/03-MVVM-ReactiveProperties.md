# MVVM 响应式属性系统

## 1. 概述
- **核心职责**：MVVM响应式属性系统是Asaki Framework的核心组件之一，提供了强类型、高性能的响应式属性实现，支持属性变化通知和自动绑定，实现了Model和View的解耦。
- **设计哲学**：采用泛型设计，支持两种观察模式（Action委托和IAsakiObserver接口），追求零GC和高性能，同时保持易用性和灵活性。
- **适用场景**：
  - UI绑定：将数据模型与UI控件自动绑定，实现数据驱动UI
  - 状态管理：管理游戏状态变化，如玩家生命值、金币数量等
  - 事件驱动编程：实现基于属性变化的事件驱动架构
  - 数据同步：在不同模块间同步数据变化
  - 自动测试：便于编写基于属性变化的自动化测试
- **依赖关系**：仅依赖.NET基础库（System、System.Collections.Generic、System.ComponentModel），无外部依赖。

## 2. 核心组件
### AsakiProperty<T>
**职责**：泛型响应式属性类，用于封装可观察的值，支持属性变化通知和自动绑定。
**生命周期**：由创建者管理，可在任意时间创建和销毁，销毁时会自动取消所有订阅。
**关键API**：

```csharp
/// <summary>
/// 构造函数，创建一个默认值的响应式属性
/// </summary>
/// <remarks>线程安全：否</remarks>
public AsakiProperty()

/// <summary>
/// 构造函数，创建一个带有初始值的响应式属性
/// </summary>
/// <param name="initialValue">初始值</param>
/// <remarks>线程安全：否</remarks>
public AsakiProperty(T initialValue = default(T))

/// <summary>
/// 属性值，设置时会触发变化通知
/// </summary>
/// <value>属性的当前值</value>
/// <remarks>
/// 线程安全：否
/// 注意：设置相同值时不会触发变化通知
/// </remarks>
public T Value

/// <summary>
/// 订阅属性变化事件
/// </summary>
/// <param name="action">属性变化时调用的委托</param>
/// <remarks>
/// 线程安全：否
/// 注意：订阅后会立即调用一次委托，传入当前值
/// </remarks>
public void Subscribe(Action<T> action)

/// <summary>
/// 取消订阅属性变化事件
/// </summary>
/// <param name="action">要取消订阅的委托</param>
/// <remarks>线程安全：否</remarks>
public void Unsubscribe(Action<T> action)

/// <summary>
/// 绑定观察者
/// </summary>
/// <param name="observer">实现了IAsakiObserver<T>接口的观察者</param>
/// <remarks>
/// 线程安全：否
/// 注意：绑定后会立即调用观察者的OnValueChange方法，传入当前值
/// </remarks>
public void Bind(IAsakiObserver<T> observer)

/// <summary>
/// 取消绑定观察者
/// </summary>
/// <param name="observer">要取消绑定的观察者</param>
/// <remarks>线程安全：否</remarks>
public void Unbind(IAsakiObserver<T> observer)

/// <summary>
/// 重写GetHashCode方法，禁止将AsakiProperty用作字典键
/// </summary>
/// <returns>不返回，总是抛出异常</returns>
/// <exception cref="NotSupportedException">总是抛出，明确禁止将其用作字典键</exception>
/// <remarks>线程安全：是</remarks>
public override int GetHashCode()

/// <summary>
/// 实现IEquatable接口，比较两个AsakiProperty<T>的值
/// </summary>
/// <param name="other">另一个AsakiProperty<T>实例</param>
/// <returns>如果值相等则返回true，否则返回false</returns>
/// <remarks>线程安全：是</remarks>
public bool Equals(AsakiProperty<T> other)

/// <summary>
/// 重写Equals方法，支持与AsakiProperty<T>或T类型比较
/// </summary>
/// <param name="obj">要比较的对象</param>
/// <returns>如果相等则返回true，否则返回false</returns>
/// <remarks>线程安全：是</remarks>
public override bool Equals(object obj)

/// <summary>
/// 重写ToString方法，返回值的字符串表示
/// </summary>
/// <returns>值的字符串表示</returns>
/// <remarks>线程安全：是</remarks>
public override string ToString()

/// <summary>
/// 隐式转换为T类型
/// </summary>
/// <param name="property">AsakiProperty<T>实例</param>
/// <returns>属性的值</returns>
/// <remarks>线程安全：是</remarks>
public static implicit operator T(AsakiProperty<T> property)

/// <summary>
/// AsakiProperty<T>与AsakiProperty<T>的相等比较
/// </summary>
/// <param name="left">左操作数</param>
/// <param name="right">右操作数</param>
/// <returns>如果值相等则返回true，否则返回false</returns>
/// <remarks>线程安全：是</remarks>
public static bool operator ==(AsakiProperty<T> left, AsakiProperty<T> right)

/// <summary>
/// AsakiProperty<T>与AsakiProperty<T>的不相等比较
/// </summary>
/// <param name="left">左操作数</param>
/// <param name="right">右操作数</param>
/// <returns>如果值不相等则返回true，否则返回false</returns>
/// <remarks>线程安全：是</remarks>
public static bool operator !=(AsakiProperty<T> left, AsakiProperty<T> right)

/// <summary>
/// T与AsakiProperty<T>的相等比较（T在左）
/// </summary>
/// <param name="left">左操作数，T类型</param>
/// <param name="right">右操作数，AsakiProperty<T>类型</param>
/// <returns>如果值相等则返回true，否则返回false</returns>
/// <remarks>线程安全：是</remarks>
public static bool operator ==(T left, AsakiProperty<T> right)

/// <summary>
/// T与AsakiProperty<T>的不相等比较（T在左）
/// </summary>
/// <param name="left">左操作数，T类型</param>
/// <param name="right">右操作数，AsakiProperty<T>类型</param>
/// <returns>如果值不相等则返回true，否则返回false</returns>
/// <remarks>线程安全：是</remarks>
public static bool operator !=(T left, AsakiProperty<T> right)

/// <summary>
/// AsakiProperty<T>与T的相等比较（AsakiProperty<T>在左）
/// </summary>
/// <param name="left">左操作数，AsakiProperty<T>类型</param>
/// <param name="right">右操作数，T类型</param>
/// <returns>如果值相等则返回true，否则返回false</returns>
/// <remarks>线程安全：是</remarks>
public static bool operator ==(AsakiProperty<T> left, T right)

/// <summary>
/// AsakiProperty<T>与T的不相等比较（AsakiProperty<T>在左）
/// </summary>
/// <param name="left">左操作数，AsakiProperty<T>类型</param>
/// <param name="right">右操作数，T类型</param>
/// <returns>如果值不相等则返回true，否则返回false</returns>
/// <remarks>线程安全：是</remarks>
public static bool operator !=(AsakiProperty<T> left, T right)
```

### IAsakiObserver<T>
**职责**：观察者接口，用于接收AsakiProperty<T>的变化通知。
**生命周期**：由实现者管理，绑定到AsakiProperty<T>时注册，取消绑定时移除。
**关键API**：

```csharp
/// <summary>
/// 当观察的属性值变化时调用
/// </summary>
/// <param name="value">属性的新值</param>
/// <remarks>线程安全：取决于实现者</remarks>
void OnValueChange(T value);
```

## 3. 设计模式与实现细节
### 3.1 两种观察模式
AsakiProperty<T>支持两种观察模式：

1. **Action委托模式**：
   - 优点：使用简单，适合快速开发
   - 缺点：可能产生GC分配，性能相对较低
   - 适用场景：开发阶段、低频更新场景

2. **IAsakiObserver接口模式**：
   - 优点：零GC，高性能
   - 缺点：实现略复杂，需要创建实现IAsakiObserver接口的类或结构体
   - 适用场景：性能敏感场景、高频更新场景

### 3.2 相等性比较
AsakiProperty<T>实现了完整的相等性比较：
- 实现了IEquatable<AsakiProperty<T>>接口
- 重写了Equals(object obj)方法，支持与T类型比较
- 重载了==和!=运算符，支持多种比较方式
- 禁止将其用作字典键（GetHashCode方法抛出异常），因为它是可变的

### 3.3 自动绑定代码生成
通过[AsakiBind]属性标记类，可以自动生成绑定代码：
- 生成PropertyId枚举，用于标识属性
- 生成GetProperty方法，用于通过PropertyId获取属性
- 生成便捷的BindXXX方法，用于绑定属性变化
- 支持两种绑定模式：Action委托和IAsakiObserver接口

### 3.4 零GC设计
- IAsakiObserver接口模式支持零GC实现
- UI观察者实现（如AsakiTMPTextIntObserver）使用对象池和StringBuilder池避免GC分配
- 事件通知使用直接调用，避免中间对象分配

## 4. 使用示例
### 4.1 定义响应式属性
```csharp
// 定义响应式属性
public class PlayerModel {
    public AsakiProperty<int> Health = new AsakiProperty<int>(100);
    public AsakiProperty<float> Experience = new AsakiProperty<float>();
    public AsakiProperty<string> Name = new AsakiProperty<string>("Player");
}
```

### 4.2 使用Action委托观察属性变化
```csharp
PlayerModel player = new PlayerModel();

// 订阅Health属性变化
player.Health.Subscribe(health => {
    Debug.Log($"Health changed to: {health}");
});

// 设置属性值，会触发变化通知
player.Health.Value = 80;
```

### 4.3 使用IAsakiObserver观察属性变化
```csharp
// 实现IAsakiObserver接口
public class HealthObserver : IAsakiObserver<int> {
    public void OnValueChange(int value) {
        Debug.Log($"Health changed to: {value}");
    }
}

// 创建观察者实例
HealthObserver healthObserver = new HealthObserver();

// 绑定到Health属性
player.Health.Bind(healthObserver);

// 设置属性值，会触发变化通知
player.Health.Value = 60;
```

### 4.4 使用自动生成的绑定代码
```csharp
// 使用[AsakiBind]属性标记类
[AsakiBind]
public partial class PlayerModel {
    public AsakiProperty<int> Health = new AsakiProperty<int>(100);
}

// 自动生成的代码会包含BindHealth方法
PlayerModel player = new PlayerModel();
player.BindHealth(health => {
    Debug.Log($"Health changed to: {health}");
});
```

### 4.5 与UI控件绑定
```csharp
// 在Unity中使用UI观察者
public class PlayerUI : MonoBehaviour {
    [SerializeField] private TMP_Text _healthText;
    private AsakiTMPTextIntObserver _healthObserver;
    
    private void Awake() {
        // 创建观察者实例
        _healthObserver = new AsakiTMPTextIntObserver(_healthText, "Health: ");
    }
    
    private void Start() {
        // 获取PlayerModel实例
        PlayerModel player = GameManager.Instance.Player;
        // 绑定观察者
        player.Health.Bind(_healthObserver);
    }
}
```

### 4.6 使用相等性比较
```csharp
PlayerModel player = new PlayerModel();

// 使用==运算符比较
if (player.Health == 100) {
    Debug.Log("Player has full health");
}

// 使用Equals方法比较
if (player.Health.Equals(100)) {
    Debug.Log("Player has full health");
}

// 与另一个AsakiProperty比较
PlayerModel anotherPlayer = new PlayerModel();
anotherPlayer.Health.Value = 100;
if (player.Health == anotherPlayer.Health) {
    Debug.Log("Both players have the same health");
}
```

## 5. 最佳实践
### 5.1 选择合适的观察模式
- 开发阶段：使用Action委托模式，简单易用
- 性能敏感场景：使用IAsakiObserver接口模式，零GC高性能
- 高频更新属性：使用IAsakiObserver接口模式
- 低频更新属性：可选择Action委托模式

### 5.2 属性命名规范
- 使用清晰、描述性的名称
- 避免使用缩写，除非是广为人知的缩写
- 保持一致性，遵循项目的命名规范

### 5.3 生命周期管理
- 及时取消订阅或解绑，避免内存泄漏
- 在MonoBehaviour的OnDisable或OnDestroy方法中取消订阅
- 对于长时间存在的对象，确保在不再需要时取消订阅

### 5.4 避免循环引用
- 注意观察关系中的循环引用，避免内存泄漏
- 考虑使用弱引用或手动管理引用关系

### 5.5 性能优化
- 对于高频更新的属性，使用脏检查避免不必要的更新
- 使用对象池和StringBuilder池避免GC分配
- 避免在属性变化回调中执行耗时操作

## 6. 常见问题与解决方案
### 6.1 内存泄漏
**问题**：AsakiProperty<T>持有对观察者的强引用，导致观察者无法被垃圾回收
**解决方案**：
- 及时取消订阅或解绑
- 在MonoBehaviour的OnDisable或OnDestroy方法中取消订阅
- 考虑使用弱引用实现观察者

### 6.2 GC分配过高
**问题**：使用Action委托模式导致过多的GC分配
**解决方案**：
- 切换到IAsakiObserver接口模式
- 使用对象池避免频繁创建对象
- 减少不必要的属性更新

### 6.3 属性值不更新
**问题**：设置AsakiProperty<T>.Value后，观察者未收到通知
**解决方案**：
- 检查是否正确订阅或绑定了观察者
- 检查设置的值是否与当前值相同（相同值不会触发通知）
- 检查观察者是否被正确初始化

### 6.4 类型不匹配
**问题**：尝试将错误类型的观察者绑定到AsakiProperty<T>
**解决方案**：
- 确保观察者的泛型类型与AsakiProperty<T>的泛型类型匹配
- 使用自动生成的BindXXX方法，它会进行类型检查

## 7. 扩展与定制
### 7.1 实现自定义观察者
```csharp
// 实现IAsakiObserver接口
public class CustomObserver : IAsakiObserver<float> {
    private readonly Action<float> _action;
    
    public CustomObserver(Action<float> action) {
        _action = action;
    }
    
    public void OnValueChange(float value) {
        // 自定义处理逻辑
        _action?.Invoke(value);
    }
}

// 使用自定义观察者
PlayerModel player = new PlayerModel();
CustomObserver observer = new CustomObserver(value => {
    Debug.Log($"Experience changed to: {value}");
});
player.Experience.Bind(observer);
```

### 7.2 扩展AsakiProperty<T>
```csharp
// 扩展AsakiProperty<T>，添加自定义方法
public static class AsakiPropertyExtensions {
    public static void SetIfDifferent<T>(this AsakiProperty<T> property, T value) {
        // 只有当值不同时才设置，避免不必要的通知
        if (!EqualityComparer<T>.Default.Equals(property.Value, value)) {
            property.Value = value;
        }
    }
    
    public static void Reset<T>(this AsakiProperty<T> property) {
        property.Value = default(T);
    }
}

// 使用扩展方法
player.Health.SetIfDifferent(100);
player.Experience.Reset();
```

## 8. 版本历史
### 1.0.0
- 初始版本，提供基本的响应式属性功能
- 支持Action委托和IAsakiObserver接口两种观察模式
- 实现了完整的相等性比较
- 支持自动绑定代码生成
- 包含UI观察者实现

## 9. 总结
Asaki Framework的MVVM响应式属性系统是一个强大、灵活的组件，提供了强类型、高性能的响应式属性实现。它支持两种观察模式，适合不同的使用场景，同时具备零GC设计和自动绑定代码生成功能。

通过合理使用AsakiProperty<T>，可以实现数据驱动的UI设计，简化状态管理，提高代码的可维护性和可测试性。它的设计兼顾了易用性和性能，适合各种规模的游戏项目。

遵循最佳实践，选择合适的观察模式，合理管理生命周期，可以充分发挥AsakiProperty<T>的优势，构建高效、可靠的MVVM架构。