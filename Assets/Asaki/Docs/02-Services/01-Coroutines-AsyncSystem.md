# Coroutines-AsyncSystem 异步协同系统

## 1. 概述
- **核心职责**：Coroutines-AsyncSystem是Asaki Framework的异步协同系统，提供完整的时间控制、条件等待和任务管理能力，简化Unity中的异步编程。
- **设计哲学**：基于Task的异步模型，结合Unity的帧更新机制，提供统一的异步操作入口，支持取消令牌和丰富的等待方法。
- **适用场景**：
  - 游戏中的延迟执行：如延迟显示UI、延迟播放动画等
  - 条件等待：如等待玩家输入、等待资源加载完成等
  - 任务管理：如并行执行多个异步操作、顺序执行任务序列等
  - 帧级控制：如等待特定帧、物理帧等
  - 重试机制：如网络请求失败后的重试逻辑
- **依赖关系**：依赖AsakiContext模块（IAsakiService接口），使用.NET Task和CancellationToken机制。

## 2. 核心组件
### IAsakiRoutineService
**职责**：异步协同系统的核心接口，提供完整的时间控制、条件等待和任务管理能力。
**生命周期**：由AsakiContext管理，初始化时注册到服务容器，销毁时由AsakiContext清理。
**关键API**：

```csharp
// === 基本等待方法 ===

/// <summary>
/// 等待指定秒数 (受 TimeScale 影响)
/// </summary>
/// <param name="seconds">等待的秒数，必须大于等于0</param>
/// <param name="token">取消令牌，用于取消等待操作</param>
/// <returns>表示异步操作的Task</returns>
/// <remarks>
/// 线程安全：是
/// 性能：低，仅依赖帧更新
/// 注意：受TimeScale影响，适合游戏时间相关的等待
/// </remarks>
Task WaitSeconds(float seconds, CancellationToken token = default(CancellationToken));

/// <summary>
/// 等待指定秒数 (真实时间，不受 TimeScale 影响)
/// </summary>
/// <param name="seconds">等待的秒数，必须大于等于0</param>
/// <param name="token">取消令牌，用于取消等待操作</param>
/// <returns>表示异步操作的Task</returns>
/// <remarks>
/// 线程安全：是
/// 性能：低，仅依赖帧更新
/// 注意：不受TimeScale影响，适合真实时间相关的等待
/// </remarks>
Task WaitSecondsUnscaled(float seconds, CancellationToken token = default(CancellationToken));

// === 帧等待 ===

/// <summary>
/// 等待下一帧 (Update)
/// </summary>
/// <param name="token">取消令牌，用于取消等待操作</param>
/// <returns>表示异步操作的Task</returns>
/// <remarks>
/// 线程安全：是
/// 性能：极低，仅在Update时触发
/// 注意：等待直到下一帧Update调用
/// </remarks>
Task WaitFrame(CancellationToken token = default(CancellationToken));

/// <summary>
/// 等待指定数量的帧
/// </summary>
/// <param name="count">等待的帧数，必须大于等于1</param>
/// <param name="token">取消令牌，用于取消等待操作</param>
/// <returns>表示异步操作的Task</returns>
/// <remarks>
/// 线程安全：是
/// 性能：低，每帧检查一次
/// 注意：等待指定数量的Update调用
/// </remarks>
Task WaitFrames(int count, CancellationToken token = default(CancellationToken));

/// <summary>
/// 等待物理/固定帧 (FixedUpdate)
/// </summary>
/// <param name="token">取消令牌，用于取消等待操作</param>
/// <returns>表示异步操作的Task</returns>
/// <remarks>
/// 线程安全：是
/// 性能：极低，仅在FixedUpdate时触发
/// 注意：等待直到下一帧FixedUpdate调用
/// </remarks>
Task WaitFixedFrame(CancellationToken token = default(CancellationToken));

/// <summary>
/// 等待指定数量的物理帧
/// </summary>
/// <param name="count">等待的物理帧数，必须大于等于1</param>
/// <param name="token">取消令牌，用于取消等待操作</param>
/// <returns>表示异步操作的Task</returns>
/// <remarks>
/// 线程安全：是
/// 性能：低，每物理帧检查一次
/// 注意：等待指定数量的FixedUpdate调用
/// </remarks>
Task WaitFixedFrames(int count, CancellationToken token = default(CancellationToken));

// === 条件等待 ===

/// <summary>
/// 挂起直到条件为 true
/// </summary>
/// <param name="predicate">条件谓词，每帧调用一次</param>
/// <param name="token">取消令牌，用于取消等待操作</param>
/// <returns>表示异步操作的Task</returns>
/// <remarks>
/// 线程安全：是
/// 性能：取决于条件谓词的复杂度和等待时间
/// 注意：条件谓词每帧调用一次，直到返回true
/// </remarks>
Task WaitUntil(Func<bool> predicate, CancellationToken token = default(CancellationToken));

/// <summary>
/// 挂起直到条件为 false
/// </summary>
/// <param name="predicate">条件谓词，每帧调用一次</param>
/// <param name="token">取消令牌，用于取消等待操作</param>
/// <returns>表示异步操作的Task</returns>
/// <remarks>
/// 线程安全：是
/// 性能：取决于条件谓词的复杂度和等待时间
/// 注意：条件谓词每帧调用一次，直到返回false
/// </remarks>
Task WaitWhile(Func<bool> predicate, CancellationToken token = default(CancellationToken));

/// <summary>
/// 等待直到条件为 true，带超时时间
/// </summary>
/// <param name="predicate">条件谓词，每帧调用一次</param>
/// <param name="timeoutSeconds">超时时间，必须大于0</param>
/// <param name="token">取消令牌，用于取消等待操作</param>
/// <returns>如果条件满足返回true，超时返回false</returns>
/// <remarks>
/// 线程安全：是
/// 性能：取决于条件谓词的复杂度和等待时间
/// 注意：带超时时间的条件等待，超时后返回false
/// </remarks>
Task<bool> WaitUntil(Func<bool> predicate, float timeoutSeconds, CancellationToken token = default(CancellationToken));

/// <summary>
/// 等待直到条件为 false，带超时时间
/// </summary>
/// <param name="predicate">条件谓词，每帧调用一次</param>
/// <param name="timeoutSeconds">超时时间，必须大于0</param>
/// <param name="token">取消令牌，用于取消等待操作</param>
/// <returns>如果条件变为false返回true，超时返回false</returns>
/// <remarks>
/// 线程安全：是
/// 性能：取决于条件谓词的复杂度和等待时间
/// 注意：带超时时间的条件等待，超时后返回false
/// </remarks>
Task<bool> WaitWhile(Func<bool> predicate, float timeoutSeconds, CancellationToken token = default(CancellationToken));

// === 任务管理 ===

/// <summary>
/// 异步执行一个任务，自动处理取消和异常
/// </summary>
/// <param name="taskFunc">要执行的异步任务</param>
/// <param name="token">取消令牌，用于取消任务</param>
/// <returns>表示异步操作的Task</returns>
/// <remarks>
/// 线程安全：是
/// 性能：低，仅启动任务的开销
/// 注意：自动处理任务的取消和异常
/// </remarks>
Task RunTask(Func<Task> taskFunc, CancellationToken token = default(CancellationToken));

/// <summary>
/// 异步执行一个带返回值的任务
/// </summary>
/// <typeparam name="T">任务的返回值类型</typeparam>
/// <param name="taskFunc">要执行的异步任务</param>
/// <param name="token">取消令牌，用于取消任务</param>
/// <returns>表示异步操作的Task，包含任务的返回值</returns>
/// <remarks>
/// 线程安全：是
/// 性能：低，仅启动任务的开销
/// 注意：自动处理任务的取消和异常
/// </remarks>
Task<T> RunTask<T>(Func<Task<T>> taskFunc, CancellationToken token = default(CancellationToken));

/// <summary>
/// 延迟执行一个动作
/// </summary>
/// <param name="delaySeconds">延迟时间，必须大于等于0</param>
/// <param name="action">要执行的动作</param>
/// <param name="token">取消令牌，用于取消延迟执行</param>
/// <param name="unscaledTime">是否使用真实时间，不受TimeScale影响</param>
/// <returns>表示异步操作的Task</returns>
/// <remarks>
/// 线程安全：是
/// 性能：低，仅依赖帧更新
/// 注意：延迟执行指定动作，支持缩放时间和真实时间
/// </remarks>
Task DelayedCall(float delaySeconds, Action action, CancellationToken token = default(CancellationToken), bool unscaledTime = false);

/// <summary>
/// 在下一帧执行一个动作
/// </summary>
/// <param name="action">要执行的动作</param>
/// <param name="token">取消令牌，用于取消执行</param>
/// <returns>表示异步操作的Task</returns>
/// <remarks>
/// 线程安全：是
/// 性能：极低，仅在Update时触发
/// 注意：在下一帧Update时执行指定动作
/// </remarks>
Task NextFrameCall(Action action, CancellationToken token = default(CancellationToken));

/// <summary>
/// 当条件满足时执行一个动作
/// </summary>
/// <param name="condition">条件谓词，每帧调用一次</param>
/// <param name="action">要执行的动作</param>
/// <param name="token">取消令牌，用于取消执行</param>
/// <returns>表示异步操作的Task</returns>
/// <remarks>
/// 线程安全：是
/// 性能：取决于条件谓词的复杂度和等待时间
/// 注意：当条件满足时执行指定动作
/// </remarks>
Task When(Func<bool> condition, Action action, CancellationToken token = default(CancellationToken));

// === 批量任务管理 ===

/// <summary>
/// 等待所有任务完成
/// </summary>
/// <param name="tasks">要等待的任务数组</param>
/// <returns>表示异步操作的Task</returns>
/// <remarks>
/// 线程安全：是
/// 性能：取决于任务数量和执行时间
/// 注意：等待所有任务完成，类似于Task.WaitAll
/// </remarks>
Task WaitAll(params Task[] tasks);

/// <summary>
/// 等待任意一个任务完成
/// </summary>
/// <param name="tasks">要等待的任务数组</param>
/// <returns>表示异步操作的Task</returns>
/// <remarks>
/// 线程安全：是
/// 性能：取决于任务数量和执行时间
/// 注意：等待任意一个任务完成，类似于Task.WaitAny
/// </remarks>
Task WaitAny(params Task[] tasks);

/// <summary>
/// 顺序执行多个异步操作
/// </summary>
/// <param name="actions">要顺序执行的异步操作数组</param>
/// <returns>表示异步操作的Task</returns>
/// <remarks>
/// 线程安全：是
/// 性能：取决于操作数量和执行时间
/// 注意：按顺序执行所有异步操作，前一个完成后执行下一个
/// </remarks>
Task Sequence(params Func<Task>[] actions);

/// <summary>
/// 并行执行多个异步操作
/// </summary>
/// <param name="actions">要并行执行的异步操作数组</param>
/// <returns>表示异步操作的Task</returns>
/// <remarks>
/// 线程安全：是
/// 性能：取决于操作数量和执行时间
/// 注意：并行执行所有异步操作，使用Task.WhenAll
/// </remarks>
Task Parallel(params Func<Task>[] actions);

/// <summary>
/// 重试执行异步操作
/// </summary>
/// <param name="action">要重试的异步操作</param>
/// <param name="maxRetries">最大重试次数，必须大于等于0</param>
/// <param name="retryDelay">重试间隔，必须大于等于0</param>
/// <param name="token">取消令牌，用于取消重试</param>
/// <returns>表示异步操作的Task</returns>
/// <remarks>
/// 线程安全：是
/// 性能：取决于操作执行时间和重试次数
/// 注意：自动重试指定操作，直到成功或达到最大重试次数
/// </remarks>
Task Retry(Func<Task> action, int maxRetries = 3, float retryDelay = 1f, CancellationToken token = default(CancellationToken));

// === 高级等待模式 ===

/// <summary>
/// 等待一个自定义的等待源
/// </summary>
/// <param name="waitSource">自定义等待源，实现IAsakiWaitSource接口</param>
/// <param name="token">取消令牌，用于取消等待</param>
/// <returns>表示异步操作的Task</returns>
/// <remarks>
/// 线程安全：是
/// 性能：取决于自定义等待源的实现
/// 注意：等待自定义的等待源完成
/// </remarks>
Task WaitCustom(IAsakiWaitSource waitSource, CancellationToken token = default(CancellationToken));

/// <summary>
/// 创建可配置的等待构建器
/// </summary>
/// <returns>等待构建器实例，用于构建复杂的等待逻辑</returns>
/// <remarks>
/// 线程安全：是
/// 性能：低，仅创建对象的开销
/// 注意：使用流畅API构建复杂的等待逻辑
/// </remarks>
IWaitBuilder CreateWaitBuilder();

// === 状态和取消 ===

/// <summary>
/// 当前运行的任务数量
/// </summary>
/// <value>当前运行的任务数量</value>
/// <remarks>
/// 线程安全：是
/// 性能：极低，仅返回计数器值
/// 注意：只读属性，返回当前运行的任务数量
/// </remarks>
int RunningTaskCount { get; }

/// <summary>
/// 取消所有正在运行的任务
/// </summary>
/// <remarks>
/// 线程安全：是
/// 性能：低，仅遍历任务列表并取消
/// 注意：取消所有正在运行的任务，使用每个任务的取消令牌
/// </remarks>
void CancelAllTasks();

/// <summary>
/// 创建一个链接到服务生命周期的取消令牌
/// </summary>
/// <param name="externalToken">外部取消令牌，可与服务生命周期令牌链接</param>
/// <returns>链接到服务生命周期的取消令牌</returns>
/// <remarks>
/// 线程安全：是
/// 性能：低，仅创建取消令牌的开销
/// 注意：当服务销毁时，该令牌会自动取消
/// </remarks>
CancellationToken CreateLinkedToken(CancellationToken externalToken = default(CancellationToken));
```

### IAsakiWaitSource
**职责**：自定义等待源接口，用于扩展IAsakiRoutineService的等待能力。
**生命周期**：由实现者管理，用于WaitCustom方法。
**关键API**：

```csharp
/// <summary>
/// 自定义等待源接口
/// </summary>
public interface IAsakiWaitSource
{
    /// <summary>
    /// 等待是否完成
    /// </summary>
    /// <value>如果等待完成返回true，否则返回false</value>
    /// <remarks>线程安全：取决于实现者</remarks>
    bool IsCompleted { get; }

    /// <summary>
    /// 等待进度
    /// </summary>
    /// <value>等待进度，范围[0, 1]</value>
    /// <remarks>线程安全：取决于实现者</remarks>
    float Progress { get; }

    /// <summary>
    /// 更新等待状态
    /// </summary>
    /// <remarks>线程安全：取决于实现者</remarks>
    void Update();
}
```

### IWaitBuilder
**职责**：等待构建器接口，提供流畅API用于构建复杂的等待逻辑。
**生命周期**：由IAsakiRoutineService.CreateWaitBuilder()创建，使用后可销毁。
**关键API**：

```csharp
/// <summary>
/// 等待构建器接口（流畅API）
/// </summary>
public interface IWaitBuilder
{
    /// <summary>
    /// 添加等待秒数
    /// </summary>
    /// <param name="seconds">等待的秒数，必须大于等于0</param>
    /// <param name="unscaled">是否使用真实时间，不受TimeScale影响</param>
    /// <returns>等待构建器实例，用于链式调用</returns>
    /// <remarks>线程安全：是</remarks>
    IWaitBuilder Seconds(float seconds, bool unscaled = false);

    /// <summary>
    /// 添加等待帧数
    /// </summary>
    /// <param name="count">等待的帧数，必须大于等于1</param>
    /// <returns>等待构建器实例，用于链式调用</returns>
    /// <remarks>线程安全：是</remarks>
    IWaitBuilder Frames(int count);

    /// <summary>
    /// 添加等待物理帧数
    /// </summary>
    /// <param name="count">等待的物理帧数，必须大于等于1</param>
    /// <returns>等待构建器实例，用于链式调用</returns>
    /// <remarks>线程安全：是</remarks>
    IWaitBuilder FixedFrames(int count);

    /// <summary>
    /// 添加条件等待，直到条件为true
    /// </summary>
    /// <param name="condition">条件谓词，每帧调用一次</param>
    /// <returns>等待构建器实例，用于链式调用</returns>
    /// <remarks>线程安全：是</remarks>
    IWaitBuilder Until(Func<bool> condition);

    /// <summary>
    /// 添加条件等待，直到条件为false
    /// </summary>
    /// <param name="condition">条件谓词，每帧调用一次</param>
    /// <returns>等待构建器实例，用于链式调用</returns>
    /// <remarks>线程安全：是</remarks>
    IWaitBuilder While(Func<bool> condition);

    /// <summary>
    /// 构建并执行等待逻辑
    /// </summary>
    /// <param name="token">取消令牌，用于取消等待操作</param>
    /// <returns>表示异步操作的Task</returns>
    /// <remarks>线程安全：是</remarks>
    Task Build(CancellationToken token = default(CancellationToken));
}
```

## 3. 设计模式与实现细节
### 3.1 基于Task的异步模型
- 采用.NET Task异步编程模型，简化异步操作的编写和管理
- 支持CancellationToken，提供统一的取消机制
- 结合Unity的帧更新机制，实现帧级别的精确控制

### 3.2 分层设计
- 核心接口IAsakiRoutineService提供完整的异步操作能力
- 扩展接口IAsakiWaitSource支持自定义等待源
- 流畅API IWaitBuilder支持复杂等待逻辑的构建

### 3.3 多维度等待支持
- 时间等待：支持受TimeScale影响和不受TimeScale影响的时间等待
- 帧等待：支持Update帧和FixedUpdate帧的等待
- 条件等待：支持带超时和不带超时的条件等待
- 自定义等待：支持通过IAsakiWaitSource扩展等待能力

### 3.4 任务管理能力
- 支持单个任务的执行和管理
- 支持批量任务的并行和顺序执行
- 支持任务的重试机制
- 提供任务数量的监控和全局取消能力

## 4. 使用示例
### 4.1 基本等待示例
```csharp
// 获取服务实例
var routineService = AsakiContext.Get<IAsakiRoutineService>();

// 示例1：等待2秒（受TimeScale影响）
async Task WaitExample() {
    Debug.Log("Start waiting...");
    await routineService.WaitSeconds(2f);
    Debug.Log("Wait finished!");
}

// 示例2：等待3帧
async Task FrameWaitExample() {
    Debug.Log("Start waiting frames...");
    await routineService.WaitFrames(3);
    Debug.Log("Frame wait finished!");
}

// 示例3：等待物理帧
async Task FixedFrameWaitExample() {
    Debug.Log("Start waiting fixed frames...");
    await routineService.WaitFixedFrame();
    Debug.Log("Fixed frame wait finished!");
}
```

### 4.2 条件等待示例
```csharp
// 示例4：等待条件为true
async Task ConditionWaitExample() {
    bool isReady = false;
    
    // 模拟异步操作
    _ = Task.Run(async () => {
        await Task.Delay(1000);
        isReady = true;
    });
    
    Debug.Log("Waiting for ready...");
    await routineService.WaitUntil(() => isReady);
    Debug.Log("Ready!");
}

// 示例5：带超时的条件等待
async Task TimeoutWaitExample() {
    bool isReady = false;
    
    Debug.Log("Waiting for ready with timeout...");
    bool success = await routineService.WaitUntil(() => isReady, 2f);
    if (success) {
        Debug.Log("Ready in time!");
    } else {
        Debug.Log("Timeout!");
    }
}
```

### 4.3 任务管理示例
```csharp
// 示例6：并行执行多个任务
async Task ParallelTasksExample() {
    Debug.Log("Start parallel tasks...");
    
    await routineService.Parallel(
        async () => {
            await routineService.WaitSeconds(1f);
            Debug.Log("Task 1 finished");
        },
        async () => {
            await routineService.WaitSeconds(2f);
            Debug.Log("Task 2 finished");
        },
        async () => {
            await routineService.WaitSeconds(3f);
            Debug.Log("Task 3 finished");
        }
    );
    
    Debug.Log("All parallel tasks finished");
}

// 示例7：顺序执行任务
async Task SequenceTasksExample() {
    Debug.Log("Start sequence tasks...");
    
    await routineService.Sequence(
        async () => {
            await routineService.WaitSeconds(1f);
            Debug.Log("Task 1 finished");
        },
        async () => {
            await routineService.WaitSeconds(1f);
            Debug.Log("Task 2 finished");
        },
        async () => {
            await routineService.WaitSeconds(1f);
            Debug.Log("Task 3 finished");
        }
    );
    
    Debug.Log("All sequence tasks finished");
}
```

### 4.4 重试机制示例
```csharp
// 示例8：重试机制
async Task RetryExample() {
    int attempt = 0;
    
    Debug.Log("Start retry example...");
    
    await routineService.Retry(async () => {
        attempt++;
        Debug.Log($"Attempt {attempt}");
        
        if (attempt < 3) {
            throw new Exception("Test exception");
        }
        
        Debug.Log("Operation successful!");
    }, maxRetries: 5, retryDelay: 0.5f);
    
    Debug.Log("Retry example finished");
}
```

### 4.5 流畅API示例
```csharp
// 示例9：使用流畅API构建复杂等待逻辑
async Task BuilderExample() {
    Debug.Log("Start builder example...");
    
    // 构建等待逻辑：等待1秒 -> 等待条件 -> 等待2帧
    var builder = routineService.CreateWaitBuilder()
        .Seconds(1f)
        .Until(() => Input.GetKeyDown(KeyCode.Space))
        .Frames(2);
    
    await builder.Build();
    
    Debug.Log("Builder example finished");
}
```

## 5. 最佳实践
### 5.1 合理使用等待方法
- 根据场景选择合适的等待方法：时间等待适合定时操作，帧等待适合精确控制
- 对于长时间等待，考虑使用取消令牌，以便在需要时取消操作
- 对于条件等待，确保条件谓词简洁高效，避免在谓词中执行耗时操作

### 5.2 任务管理最佳实践
- 使用RunTask方法执行异步任务，自动处理异常和取消
- 对于批量任务，根据需要选择并行或顺序执行
- 对于可能失败的操作，考虑使用Retry机制，提高系统的容错性

### 5.3 取消令牌的使用
- 为重要的异步操作创建和传递CancellationToken
- 对于长时间运行的操作，确保定期检查取消令牌的状态
- 考虑使用CreateLinkedToken方法，将外部令牌与服务生命周期令牌链接

### 5.4 性能注意事项
- 避免在条件谓词中执行复杂计算或IO操作
- 对于频繁调用的等待操作，考虑合并或优化
- 监控RunningTaskCount，避免过多任务同时运行

## 6. 常见问题与解决方案
### 6.1 等待未生效
**问题**：调用等待方法后，程序没有按预期等待
**解决方案**：
- 确保方法是异步的（使用async关键字）
- 确保使用了await关键字等待异步操作
- 检查等待条件是否正确
- 检查取消令牌是否被意外取消

### 6.2 任务未被取消
**问题**：调用CancelAllTasks后，部分任务仍在运行
**解决方案**：
- 确保任务内部定期检查CancellationToken的状态
- 对于自定义等待源，确保实现了对取消令牌的支持
- 检查任务是否正确传递了取消令牌

### 6.3 性能问题
**问题**：使用IAsakiRoutineService后，游戏性能下降
**解决方案**：
- 减少不必要的等待操作
- 优化条件谓词的执行效率
- 减少同时运行的任务数量
- 考虑合并或批量处理相似的异步操作

## 7. 扩展与定制
### 7.1 实现自定义等待源
```csharp
// 示例：实现一个自定义等待源
public class CountdownWaitSource : IAsakiWaitSource {
    private float _duration;
    private float _elapsed;
    private bool _unscaled;
    
    public CountdownWaitSource(float duration, bool unscaled = false) {
        _duration = duration;
        _unscaled = unscaled;
        _elapsed = 0f;
    }
    
    public bool IsCompleted => _elapsed >= _duration;
    
    public float Progress => Mathf.Clamp01(_elapsed / _duration);
    
    public void Update() {
        _elapsed += _unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
    }
}

// 使用自定义等待源
async Task CustomWaitExample() {
    var waitSource = new CountdownWaitSource(2f, true);
    await routineService.WaitCustom(waitSource);
    Debug.Log("Custom wait finished!");
}
```

### 7.2 扩展IWaitBuilder
- 可以通过扩展方法扩展IWaitBuilder的功能
- 例如，添加对特定游戏事件的等待支持

## 8. 版本历史
### 1.0.0
- 初始版本，提供基本的异步等待和任务管理功能
- 支持时间等待、帧等待和条件等待
- 支持基本的任务管理和批量操作

### 2.0.0
- 增加了重试机制
- 增加了自定义等待源支持
- 优化了任务管理性能

### 3.0.0
- 增加了流畅API IWaitBuilder
- 完善了取消令牌支持
- 增加了任务数量监控
- 优化了整体设计和性能

## 9. 总结
IAsakiRoutineService是Asaki Framework提供的强大异步协同系统，为Unity开发者提供了完整的异步操作能力。它结合了.NET Task异步编程模型和Unity的帧更新机制，提供了多维度的等待支持和强大的任务管理能力。

通过IAsakiRoutineService，开发者可以轻松实现各种异步操作，如时间等待、帧等待、条件等待等，同时享受Task异步编程模型的便利和取消令牌的统一管理。流畅API IWaitBuilder支持构建复杂的等待逻辑，而扩展接口IAsakiWaitSource则提供了自定义等待能力。

在实际项目中，IAsakiRoutineService可以用于各种场景，如游戏逻辑的异步执行、资源加载的管理、UI动画的控制等。遵循最佳实践，可以充分发挥其优势，提高代码的可读性、可维护性和性能。

作为Asaki Framework的核心服务之一，IAsakiRoutineService为游戏开发提供了强大的异步编程支持，简化了异步操作的编写和管理，是Unity开发者的有力工具。