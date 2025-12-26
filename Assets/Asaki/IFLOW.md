# Asaki Framework

## 项目概述

**Asaki Framework** 是一个专为Unity游戏开发设计的高性能、模块化游戏框架。框架采用微内核架构，提供了一套完整的服务容器、事件系统、响应式属性、对象池、图系统和时钟系统等核心组件。

### 核心特性
- **极速微内核服务容器** (AsakiContext): 采用Copy-On-Write + Snapshot Swap架构，读操作无锁，写操作有锁
- **高性能事件总线** (AsakiBroker): 泛型静态桶设计，无GC分配，支持线程安全的事件发布/订阅
- **响应式属性系统** (MVVM): 强类型响应式属性，支持Action委托和IAsakiObserver接口两种观察模式
- **智能对象池** (AsakiSmartPool): 支持异步操作、超时回收和统计管理的智能对象池
- **可视化图系统** (Graphs): 支持节点编辑、数据流和黑板系统的可视化图系统
- **统一时钟系统** (Simulation): 支持优先级排序的Tick和FixedTick系统，统一管理游戏逻辑更新

### 技术架构
- **分层架构**: Core(核心层) → Unity(Unity桥接层) → Editor(编辑器层)
- **模块化设计**: 每个功能模块独立成包，通过.asmdef程序集定义文件管理依赖
- **代码生成**: 支持音频ID、UIID等枚举的自动生成
- **性能优先**: 大量使用AggressiveInlining、对象池、无锁设计等性能优化手段

## 项目结构

```
E:\Projects\UnityGame\Asaki\Assets\Asaki\
├───CodeGen\                    # 代码生成器DLL
│   ├───Asaki.CodeGen.dll
│   └───Asaki.CodeGen.pdb
├───Core\                      # 核心框架层
│   ├───Asaki.Core.asmdef      # 核心程序集定义
│   ├───Attributes\            # 框架属性标记
│   ├───Audio\                 # 音频服务接口
│   ├───Blackboard\            # 黑板数据系统
│   ├───Broker\                # 事件总线系统
│   ├───Configuration\         # 配置服务接口
│   ├───Context\               # 服务容器
│   ├───Coroutines\            # 协程服务接口
│   ├───Graphs\                # 可视化图系统
│   ├───MVVM\                  # 响应式属性系统
│   ├───Pooling\               # 对象池系统
│   ├───Resources\             # 资源服务接口
│   ├───Serialization\         # 序列化服务接口
│   ├───Simulation\            # 时钟系统
│   └───UI\                    # UI服务接口
├───Docs\                      # 框架文档
│   └───01-Core\               # 核心模块文档
├───Editor\                    # 编辑器扩展
│   └───Asaki.Editor.asmdef    # 编辑器程序集定义
├───Generated\                 # 自动生成的代码
│   ├───Asaki.Generated.asmdef # 生成代码程序集定义
│   ├───AudioID.cs             # 音频ID枚举
│   └───UIID.cs                # UIID枚举
└───Unity\                     # Unity桥接层
    └───Asaki.Unity.asmdef     # Unity桥接程序集定义
```

## 核心模块详解

### 1. AsakiContext - 服务容器 (Core/Context/)
**位置**: `Core/Context/AsakiContext.cs`

**核心特性**:
- 极速微内核设计，读操作无锁 (O(1))
- Copy-On-Write写入机制，写操作有锁 (O(n))
- 支持服务注册、获取、替换和懒加载
- 支持模块生命周期管理 (IAsakiModule)
- 支持容器冻结，防止运行时随意注册服务

**关键API**:
```csharp
// 注册服务
AsakiContext.Register<T>(T service);
AsakiContext.Register(Type type, IAsakiService service);

// 获取服务
T AsakiContext.Get<T>();
bool AsakiContext.TryGet<T>(out T service);

// 懒加载获取
T AsakiContext.GetOrRegister<T>(Func<T> factory);

// 替换服务 (热更新)
AsakiContext.Replace<T>(T service);

// 容器控制
AsakiContext.Freeze();  // 冻结容器
AsakiContext.ClearAll(); // 清空所有服务
```

**使用场景**: 服务注册、模块管理、依赖注入

### 2. AsakiBroker - 事件总线 (Core/Broker/)
**位置**: `Core/Broker/AsakiBroker.cs`

**核心特性**:
- 泛型静态桶设计，每种事件类型独立存储
- 无GC分配，事件必须是struct
- 发布操作无锁，订阅/取消订阅有锁
- 支持异常处理委托
- 支持Roslyn代码生成自动订阅

**关键API**:
```csharp
// 发布事件
AsakiBroker.Publish<T>(T e) where T : struct, IAsakiEvent;

// 订阅事件
AsakiBroker.Subscribe<T>(IAsakiHandler<T> handler) where T : struct, IAsakiEvent;

// 取消订阅
AsakiBroker.Unsubscribe<T>(IAsakiHandler<T> handler) where T : struct, IAsakiEvent;

// 异常处理
AsakiBroker.OnException = ex => { /* 处理异常 */ };

// 清理所有订阅
AsakiBroker.Cleanup();
```

**使用场景**: 模块间通信、UI事件、游戏状态变化、异步操作通知

### 3. MVVM - 响应式属性 (Core/MVVM/)
**位置**: `Core/MVVM/AsakiProperty.cs`

**核心特性**:
- 强类型泛型设计，支持两种观察模式
- 完整的相等性比较实现
- 禁止用作字典键（可变对象）
- 支持隐式类型转换
- 零GC设计（使用IAsakiObserver接口）

**关键API**:
```csharp
// 创建属性
var health = new AsakiProperty<int>(100);
var name = new AsakiProperty<string>("Player");

// 订阅变化（Action模式）
health.Subscribe(value => Debug.Log($"Health: {value}"));

// 绑定观察者（接口模式）
health.Bind(healthObserver);

// 设置值（自动触发通知）
health.Value = 80;

// 相等性比较
if (health == 100) { /* ... */ }
if (health == anotherHealth) { /* ... */ }

// 隐式转换
int currentHealth = health;
```

**使用场景**: UI数据绑定、状态管理、事件驱动编程

### 4. AsakiSmartPool - 智能对象池 (Core/Pooling/)
**位置**: `Core/Pooling/AsakiSmartPool.*.cs`

**核心特性**:
- 支持GameObject池化
- 支持IAsakiPoolable接口自定义初始化和清理
- 支持超时回收和后台维护
- 支持异步操作和协程集成
- 支持统计和监控

**关键API**:
```csharp
// 注册预制体
AsakiSmartPool.Register("enemy", enemyPrefab);

// 生成对象
var enemy = AsakiSmartPool.Spawn("enemy", position, rotation, parent);

// 回收对象
AsakiSmartPool.Despawn(enemy, "enemy");

// 清理池
AsakiSmartPool.Cleanup();

// 设置默认容量
AsakiSmartPool.SetDefaultCapacity(32);
```

**使用场景**: 频繁创建销毁的对象、粒子系统、子弹、敌人

### 5. Graphs - 可视化图系统 (Core/Graphs/)
**位置**: `Core/Graphs/AsakiGraphBase.cs`

**核心特性**:
- 支持节点式可视化编辑
- 支持数据流和黑板系统
- 运行时拓扑缓存，查询复杂度O(1)
- 支持多输出端口和单输出端口
- 支持反向连接查询

**关键API**:
```csharp
// 运行时初始化
graph.InitializeRuntime();

// 获取入口节点
var entryNode = graph.GetEntryNode<AsakiNodeBase>();

// 获取下一个节点（单输出）
var nextNode = graph.GetNextNode(currentNode, "Out");

// 获取所有下一个节点（多输出）
var nextNodes = graph.GetNextNodes(currentNode, "Broadcast");

// 获取节点通过GUID
var node = graph.GetNodeByGUID(guid);

// 获取输入连接
var edge = graph.GetInputConnection(targetNode, "InputPort");
```

**使用场景**: 行为树、对话系统、任务系统、技能系统

### 6. Simulation - 时钟系统 (Core/Simulation/)
**位置**: `Core/Simulation/AsakiSimulationManager.cs`

**核心特性**:
- 统一管理Tick和FixedTick
- 支持优先级排序（数值越小越先执行）
- 统一时钟源，解耦Unity时间系统
- 支持IAsakiTickable和IAsakiFixedTickable接口

**关键API**:
```csharp
// 注册Tick对象
simManager.Register(tickable, (int)TickPriority.Normal);
simManager.Register(tickable, customPriority);

// 注销Tick对象
simManager.Unregister(tickable);

// 执行Tick（通常由AsakiMonoDriver调用）
simManager.Tick(deltaTime);
simManager.FixedTick(fixedDeltaTime);
```

**优先级枚举**:
```csharp
public enum TickPriority {
    High = 0,      // 输入、传感器
    Normal = 1000, // 游戏逻辑、状态机
    Low = 2000,    // UI、音频、视图同步
}
```

**使用场景**: 游戏逻辑更新、物理模拟、状态机、AI行为

## 开发约定

### 1. 程序集依赖
- **Asaki.Core**: 无外部依赖，仅依赖.NET基础库
- **Asaki.Unity**: 依赖Unity Engine，提供Unity桥接实现
- **Asaki.Editor**: 依赖Unity Editor，提供编辑器扩展
- **Asaki.Generated**: 依赖Core和Unity，包含自动生成的代码

### 2. 命名规范
- 接口: `IAsakiXXX` (如: IAsakiService, IAsakiModule)
- 属性类: `AsakiXXXAttribute` (如: AsakiBindAttribute)
- 核心类: `AsakiXXX` (如: AsakiContext, AsakiBroker)
- 枚举: `XXXID` (如: AudioID, UIID)

### 3. 性能优化
- 热路径使用`[MethodImpl(MethodImplOptions.AggressiveInlining)]`
- 频繁创建的对象使用对象池
- 避免在Update中分配内存
- 使用struct代替class减少GC
- 使用volatile和lock确保线程安全

### 4. 代码生成
框架支持多种代码生成器：
- **音频ID生成**: 从音频资源生成AudioID枚举
- **UIID生成**: 从UI资源生成UIID枚举
- **事件订阅生成**: 使用Roslyn生成自动订阅/取消订阅代码
- **属性绑定生成**: 使用[AsakiBind]生成属性绑定代码

### 5. 版本定义
框架支持条件编译：
- `ASAKI_USE_UNITASK`: 启用UniTask异步支持
- `ASAKI_USE_ADDRESSABLE`: 启用Addressables资源系统支持

## 构建和运行

### Unity环境
1. 将Asaki文件夹放入Unity项目的Assets目录
2. Unity会自动识别.asmdef文件并编译程序集
3. 在Unity编辑器中使用框架功能

### 依赖管理
框架可选依赖：
- **UniTask**: 用于异步操作 (`com.cysharp.unitask`)
- **Addressables**: 用于资源管理 (`com.unity.addressables`)

在Unity Package Manager中添加这些包后，框架会自动启用相应的编译条件。

## 测试和验证

### 单元测试
框架核心层(Asaki.Core)不依赖Unity，可以编写标准单元测试：
```bash
# 使用dotnet test运行测试
dotnet test Asaki.Core.Tests
```

### 集成测试
Unity相关功能需要在Unity编辑器中测试：
1. 创建测试场景
2. 添加测试脚本
3. 在编辑器中运行测试

### 性能测试
框架内置性能监控：
- AsakiSmartPool支持统计生成/回收次数
- AsakiBroker支持事件发布监控
- AsakiContext支持服务查询性能分析

## 扩展和定制

### 添加新服务
1. 定义服务接口继承IAsakiService
2. 实现服务类
3. 在模块的OnInit中注册服务
4. 通过AsakiContext.Get<T>()获取服务

### 添加新模块
1. 实现IAsakiModule接口
2. 添加[AsakiModule]属性标记
3. 在OnInit中注册服务和依赖
4. 在OnInitAsync中执行异步初始化
5. 在OnDispose中清理资源

### 扩展图系统
1. 继承AsakiNodeBase创建新节点类型
2. 实现节点逻辑
3. 在编辑器中创建自定义节点视图
4. 使用AsakiGraphBase管理节点和连接

### 自定义代码生成
1. 继承框架的代码生成器基类
2. 实现资源扫描和代码生成逻辑
3. 在Unity编辑器中触发生成
4. 生成的代码自动放入Generated文件夹

## 最佳实践

### 1. 服务设计
- 保持服务接口简洁，遵循单一职责原则
- 使用接口抽象，避免直接依赖具体实现
- 服务应该是无状态的或线程安全的

### 2. 模块设计
- 模块应该自包含，有明确的依赖关系
- 在OnInit中只获取配置和注册服务
- 在OnInitAsync中执行耗时操作
- 在OnDispose中正确释放资源

### 3. 事件设计
- 事件应该是struct，避免GC分配
- 事件数据应该精简，只包含必要信息
- 使用强类型事件，避免使用object

### 4. 性能优化
- 频繁创建的对象使用对象池
- 避免在Update中分配内存
- 使用TryGet代替Get避免异常
- 合理使用优先级控制系统执行顺序

### 5. 代码生成
- 将生成的代码放入Generated文件夹
- 不要手动修改生成的代码
- 使用partial类扩展生成的代码
- 在版本控制中忽略生成的代码

## 常见问题

### Q: 服务注册失败，提示已存在？
A: 使用TryGet先检查服务是否已注册，或使用GetOrRegister实现懒加载。

### Q: 事件处理程序未被调用？
A: 检查事件类型是否正确实现IAsakiEvent接口，检查是否正确订阅。

### Q: 对象池对象状态异常？
A: 确保实现IAsakiPoolable接口，在OnSpawn和OnDespawn中正确重置状态。

### Q: 图系统查询性能低？
A: 确保调用InitializeRuntime()构建拓扑缓存，将查询复杂度降到O(1)。

### Q: Tick顺序不符合预期？
A: 检查优先级设置，数值越小越先执行，同优先级按注册顺序执行。

## 相关文档

- [Core模块文档](Docs/01-Core/)
- [服务接口文档](Core/)
- [编辑器扩展文档](Editor/)

## Unity桥接层服务实现

### 7. Bootstrapper - 启动系统 (Unity/Bootstrapper/)

#### 7.1 架构设计
**核心文件**: `Unity/Bootstrapper/AsakiBootstrapper.cs`

启动系统采用**DAG（有向无环图）拓扑排序**解决模块依赖，确保初始化顺序正确：

```csharp
// 启动流程（AsakiBootstrapper.cs）
1. Awake() -> 注册配置 + 初始化核心驱动
2. Start() -> 模块发现 → 拓扑排序 → 两阶段初始化 → 框架冻结
3. 发送FrameworkReadyEvent事件
```

**关键特性**：
- **两阶段初始化**：同步注册（OnInit）+ 异步初始化（OnInitAsync）
- **框架冻结机制**：`AsakiContext.Freeze()`防止运行时随意注册服务
- **优先级控制**：模块通过`[AsakiModule(priority, dependencies)]`属性声明依赖

**启动顺序示例**：
```
75   → EventBusModule (事件总线)
100  → RoutineModule (协程服务)
150  → SmartPoolModule (对象池)  
200  → ResKitModule (资源管理)
300  → UIModule (UI系统)
400  → AudioModule (音频系统)
```

#### 7.2 核心实现
```csharp
[DefaultExecutionOrder(-9999)]
public class AsakiBootstrapper : MonoBehaviour
{
    [SerializeField] private AsakiConfig _config;
    [SerializeField] private List<string> _scanAssemblies = new List<string> 
    { 
        "Asaki.Unity", 
        "Game.Logic",
        "Game.View"
    };

    private void Awake()
    {
        // 1. 注册全局配置
        if (_config != null) AsakiContext.Register(_config);
        
        // 2. 初始化核心驱动
        SetupCoreDriver();
    }

    private async void Start()
    {
        // 3. 启动模块加载器
        var discovery = new AsakiReflectionModuleDiscovery(_scanAssemblies);
        await AsakiModuleLoader.Startup(discovery);
        
        // 4. 架构冻结！
        AsakiContext.Freeze();
        
        // 5. 发送就绪事件
        AsakiBroker.Publish(new FrameworkReadyEvent());
    }
}
```

#### 7.3 模块加载器（AsakiModuleLoader）
```csharp
// 拓扑排序算法（Kahn算法）
- 构建依赖图：模块 → 依赖模块
- 检测循环依赖并抛出异常
- 按优先级排序（数值越小越先初始化）
- 两阶段初始化：OnInit() → OnInitAsync()
```

**使用场景**: 框架启动、模块管理、依赖解析

---

### 8. AudioService - 音频服务 (Unity/Services/Audio/)

#### 8.1 架构设计
**核心文件**: `Unity/Services/Audio/AsakiAudioService.cs`

**核心特性**：
- **纯C#实现**：不继承MonoBehaviour，由服务容器托管生命周期
- **对象池管理**：使用`AsakiSmartPool`管理SoundAgent实例
- **跨场景持久化**：创建`[AsakiAudioSystem]`根节点并调用`DontDestroyOnLoad`

```csharp
public class AsakiAudioService : IAsakiAudioService, IAsakiModule
{
    // 构造函数注入配置
    public AsakiAudioService(AsakiAudioConfig config, GameObject agentPrefab, int poolSize)
    
    // 两阶段初始化
    public void OnInit()          // 创建根节点，初始化配置
    public async Task OnInitAsync() // 预热对象池
    
    // 句柄管理
    private Dictionary<AsakiAudioHandle, AsakiSoundAgent> _activeAgents
}
```

#### 8.2 音频播放流程
```csharp
// Play方法实现
1. 通过assetId查找配置路径
2. 从对象池获取SoundAgent
3. 生成唯一句柄AsakiAudioHandle
4. 启动异步播放任务
5. 播放完成后自动回收并移除句柄

// 异步任务链
PlayInternal → agent.PlayAsync → 加载音频 → 播放 → 完成/取消 → 回收
```

**代码示例**：
```csharp
// 播放音频
var handle = audioService.Play((int)AudioID.Bgm_Main);

// 暂停/恢复
audioService.Pause(handle);
audioService.Resume(handle);

// 停止（带淡出）
audioService.Stop(handle, fadeDuration: 0.5f);

// 停止所有
audioService.StopAll(fadeDuration: 1f);
```

#### 8.3 性能优化
- **零GC设计**：使用struct类型的AudioHandle
- **取消令牌**：支持CancellationToken，可中途取消播放
- **异常处理**：FireAndForget模式捕获异常，防止未观察异常
- **引用计数**：依赖资源服务自动管理音频资源生命周期

**使用场景**: BGM管理、音效播放、语音对话、环境音效

---

### 9. UIManager - UI服务 (Unity/Services/UI/)

#### 9.1 架构设计
**核心文件**: `Unity/Services/UI/AsakiUIManager.cs`

**核心特性**：
- **双模式支持**：池化（UsePool=true）和非池化UI
- **线程安全**：使用`ConcurrentQueue`处理关闭请求
- **Tick驱动**：实现`IAsakiTickable`接口，由SimulationManager统一驱动

```csharp
public class AsakiUIManager : IAsakiUIService, IAsakiTickable
{
    // 窗口栈管理
    private Stack<IAsakiWindow> _normalStack;
    
    // 池化资源驻留
    private Dictionary<string, IDisposable> _poolKeepers;
    
    // 线程安全队列
    private readonly ConcurrentQueue<IAsakiWindow> _pendingDestroyQueue;
    
    // Tick驱动
    public void Tick(float deltaTime)  // 处理关闭队列
}
```

#### 9.2 UI打开流程
```csharp
// 分支A：池化UI
1. 检查_poolKeepers是否已加载
2. 首次加载：异步加载资源 → 注册到对象池 → 持有句柄
3. 从池中Spawn实例
4. 标记IsPooled=true，PoolKey=资源路径
5. 不持有ResHandle（由UIManager担保生命周期）

// 分支B：非池化UI
1. 异步加载资源
2. Instantiate实例
3. 持有ResHandle，关闭时释放
```

**代码示例**：
```csharp
// 打开UI
var mainMenu = await uiManager.OpenAsync<MainMenuWindow>((int)UIID.MainMenu);

// 关闭UI（支持泛型）
uiManager.Close<MainMenuWindow>();

// Back操作
uiManager.Back();

// 直接关闭实例
uiManager.Close(mainMenu);
```

#### 9.3 UI关闭流程
```csharp
// 线程安全设计
public void Close(IAsakiWindow window)
{
    _pendingDestroyQueue.Enqueue(window);  // 不立即执行，入队
}

// Tick中处理
public void Tick(float deltaTime)
{
    while(_pendingDestroyQueue.TryDequeue(out var window))
    {
        ProcessCloseRequest(window);
    }
}

// 支持从栈中间移除
private void RemoveWindowFromStackMiddle(IAsakiWindow target)
{
    // 使用临时栈重建，O(N)复杂度
}
```

#### 9.4 性能优化
- **资源驻留**：池化UI资源永久驻留内存，避免重复加载
- **延迟销毁**：关闭请求入队，统一在Tick处理，避免递归调用
- **栈管理**：支持Back操作和任意位置关闭
- **跨场景**：UIRoot使用DontDestroyOnLoad

**使用场景**: 界面管理、弹窗系统、HUD、菜单系统

---

### 10. ResService - 资源服务 (Unity/Services/Resources/)

#### 10.1 架构设计
**核心文件**: `Unity/Services/Resources/AsakiResService.cs`

**核心特性**：
- **依赖加载**：支持资源依赖链自动加载
- **引用计数**：精细控制资源生命周期
- **进度回调**：支持加载进度报告
- **批量加载**：优化批量资源加载性能

```csharp
private class ResRecord
{
    public string Location;
    public Object Asset;
    public int RefCount;                    // 引用计数
    public HashSet<string> DependencyLocations;  // 依赖列表
    public TaskCompletionSource<Object> LoadingTcs;
    public Action<float> ProgressCallbacks; // 进度回调
}
```

#### 10.2 加载流程
```csharp
// 加载算法
1. GetOrCreateRecord：检查缓存，不存在则创建并启动加载任务
2. 增加引用计数（乐观锁）
3. 注册进度回调
4. 等待LoadingTcs.Task完成
5. 返回ResHandle（释放时自动调用Release）

// 异步加载任务（SafeStartLoadTask）
1. 加载所有依赖（递归调用GetOrCreateRecord）
2. 等待依赖完成（带超时检测，防止循环依赖）
3. 加载自身资源
4. 设置LoadingTcs结果
5. 报告进度
```

**代码示例**：
```csharp
// 加载单个资源
using (var handle = await resService.LoadAsync<GameObject>("Prefabs/Enemy"))
{
    var enemy = handle.Asset;
    // 使用资源
} // 自动释放

// 带进度加载
var handle = await resService.LoadAsync<GameObject>(
    "Prefabs/Player", 
    progress => Debug.Log($"Loading: {progress:P}"), 
    token);

// 批量加载
var handles = await resService.LoadBatchAsync<GameObject>(
    new[] { "Prefab/A", "Prefab/B", "Prefab/C" },
    progress => Debug.Log($"Batch progress: {progress:P}"),
    token);
```

#### 10.3 释放流程
```csharp
// 引用计数释放
private void ReleaseInternal(string rootLocation)
{
    var pendingRelease = new Stack<string>();
    pendingRelease.Push(rootLocation);
    
    while(pendingRelease.Count > 0)
    {
        var current = pendingRelease.Pop();
        if(_cache.TryGetValue(current, out var record))
        {
            record.RefCount--;
            if(record.RefCount <= 0)
            {
                // 卸载资源
                _strategy.UnloadAssetInternal(current, record.Asset);
                _cache.Remove(current);
                
                // 将依赖加入待释放栈
                foreach(var dep in record.DependencyLocations)
                    pendingRelease.Push(dep);
            }
        }
    }
}
```

#### 10.4 批量加载优化
```csharp
// 进度聚合算法
float[] progresses = new float[locations.Count];
Action<float> GetProgressHandler(int index)
{
    return (p) => {
        progresses[index] = p;
        onProgress(progresses.Average());  // 报告平均进度
    };
}
```

#### 10.5 工厂模式
**位置**: `Unity/Services/Resources/AsakiResKitFactory.cs`

支持三种加载策略：
- **Resources模式**：原生Resources.Load
- **Addressables模式**：Unity Addressables（需定义`ASAKI_USE_ADDRESSABLE`）
- **Custom模式**：支持自定义策略

```csharp
public static void RegisterCustom(
    Func<IAsakiResStrategy> strategyBuilder,
    Func<IAsakiResDependencyLookup> lookupBuilder
)
```

**使用场景**: 资源加载、依赖管理、批量加载、热更新

---

### 11. Configuration - 配置系统 (Unity/Configuration/)

#### 11.1 设计模式
**核心文件**: `Unity/Configuration/AsakiConfig.cs`

```csharp
[CreateAssetMenu(fileName = "AsakiConfig", menuName = "Asaki/AsakiConfig")]
public class AsakiConfig : ScriptableObject, IAsakiService
{
    // 仿真设置
    [SerializeField] private int tickRate = 60;
    
    // 性能设置
    [SerializeField] private int defaultPoolSize = 128;
    
    // 资源策略
    [SerializeField] private AsakiResKitMode asakiResKitMode;
    
    // 音频配置
    [SerializeField] private AsakiAudioConfig asakiAudioConfig;
    [SerializeField] private GameObject _soundAgentPrefab;
    
    // UI配置
    [SerializeField] private AsakiUIConfig _uiConfig;
    [SerializeField] private Vector2 _referenceResolution;
    [SerializeField] private float _matchWidthOrHeight;
}
```

**使用场景**: 全局配置、服务参数、性能调优

---

### 12. MonoDriver - 核心驱动 (Unity/Bridge/)

#### 12.1 架构设计
**核心文件**: `Unity/Bridge/AsakiMonoDriver.cs`

**设计原则**：
- **唯一时间源**：整个框架唯一允许读取`Time.deltaTime`的地方
- **解耦设计**：将Unity生命周期转换为框架的Tick系统
- **轻量级**：仅做转发，不包含业务逻辑

```csharp
public class AsakiMonoDriver : MonoBehaviour
{
    private void Update()
    {
        _simManager.Tick(Time.deltaTime);      // 传递deltaTime
    }
    
    private void FixedUpdate()
    {
        _simManager.FixedTick(Time.fixedDeltaTime);
    }
}
```

**使用场景**: Unity生命周期桥接、统一时间源

---

### 13. ModuleSystem - 模块系统编辑器 (Editor/ModuleSystem/)

#### 13.1 功能特点
**核心文件**: `Editor/ModuleSystem/AsakiModuleDashboard.cs`

**主要功能**：
- **可视化DAG图**: 使用Kahn算法进行拓扑排序，展示模块初始化顺序
- **依赖关系分析**: 双向展示模块依赖（Dependencies）和被依赖（Dependents）关系
- **优先级编辑**: 支持通过UI直接修改模块优先级，使用Regex自动更新源代码
- **交互式高亮**: 选中模块时，上游依赖显示橙色，下游依赖显示绿色
- **代码跳转**: 一键打开模块脚本文件

**使用方法**：
```csharp
// 菜单路径: Asaki/Module Dashboard
// 快捷键: 无

// 功能:
// 1. 查看所有模块的初始化顺序（拓扑排序结果）
// 2. 点击模块查看依赖关系图
// 3. 拖拽滑块修改优先级并自动保存到源码
// 4. 检测循环依赖错误并红色高亮显示
// 5. 双击模块打开脚本文件
```

**界面截图说明**：
```
┌─────────────────────────────────────────┐
│ Asaki Module Dashboard                  │
├─────────────────────────────────────────┤
│ [模块列表]                              │
│ □ EventBusModule    Priority: 75      │
│ □ RoutineModule     Priority: 100     │
│ ■ SmartPoolModule   Priority: 150     │ ← 选中
│ □ ResKitModule      Priority: 200     │
│ □ UIModule          Priority: 300     │
│ □ AudioModule       Priority: 400     │
├─────────────────────────────────────────┤
│ [依赖关系]                              │
│ 上游依赖 (橙色):                        │
│   └─ EventBusModule                   │
│ 下游依赖 (绿色):                        │
│   ├─ ResKitModule                     │
│   └─ UIModule                         │
└─────────────────────────────────────────┘
```

#### 13.2 关键技术实现
```csharp
// 模块扫描
var moduleTypes = TypeCache.GetTypesDerivedFrom<IAsakiModule>();

// 拓扑排序（Kahn算法）
var sorted = new List<Type>();
var queue = new Queue<Type>(nodes.Where(n => n.InDegree == 0));

while(queue.Count > 0)
{
    var node = queue.Dequeue();
    sorted.Add(node.Value);
    
    foreach(var child in node.Children)
    {
        child.InDegree--;
        if(child.InDegree == 0) queue.Enqueue(child);
    }
}

// 优先级更新（Regex）
var regex = new Regex(@"\[AsakiModule\([^\)]*Priority\s*=\s*\d+");
var newCode = regex.Replace(code, $"[AsakiModule(Priority = {newPriority}");
```

**使用场景**: 模块依赖管理、启动顺序调试、架构优化

---

### 14. GraphEditors - 图编辑器 (Editor/GraphEditors/)

#### 14.1 主窗口和视图
**核心文件**: 
- `Editor/GraphEditors/AsakiGraphWindow.cs`
- `Editor/GraphEditors/AsakiGraphView.cs`
- `Editor/GraphEditors/AsakiNodeView.cs`

**功能特点**：
- **自定义控制器注册**: 支持为不同图类型注册专用控制器
- **运行时调试**: 集成`AsakiGraphDebugger`实现运行时节点高亮
- **变量拖拽**: 支持从Blackboard拖拽变量到图中创建Get/Set节点
- **节点搜索**: 集成`AsakiNodeSearchWindow`支持快速创建节点
- **端口缓存**: 使用`AsakiGraphTypeCache`实现O(1)端口查询
- **Undo/Redo**: 完整的撤销重做支持

**使用方法**：
```csharp
// 自定义图编辑器注册
AsakiGraphWindow.Register<MyGraph>(graph => new MyGraphController(graph));

// 节点创建方式：
// 1. 右键空白处打开搜索窗口
// 2. 输入节点名称模糊搜索
// 3. 拖拽Blackboard变量到图中
// 4. 连接端口创建边
```

#### 14.2 Blackboard系统
**核心文件**: `Editor/GraphEditors/AsakiBlackboardProvider.cs`

**功能特点**：
- **变量管理**: 支持Int、Float、Bool、String、Vector3、Vector2、Color等多种类型
- **拖拽支持**: 变量可拖拽到图中创建Get/Set节点
- **默认值编辑**: 内联编辑器修改变量默认值
- **删除支持**: 支持删除变量（带确认对话框）
- **重命名**: 双击变量名进行重命名

**变量类型**：
```csharp
public enum AsakiBlackboardPropertyType
{
    Int, Float, Bool, String, 
    Vector3, Vector2, Vector3Int, Vector2Int, Color
}
```

**使用示例**：
```csharp
// 在图中使用Blackboard变量
// 1. 在Blackboard面板点击"+"添加变量
// 2. 设置变量名、类型、默认值
// 3. 拖拽变量到图中创建Get节点
// 4. 拖拽变量到图中创建Set节点
```

#### 14.3 类型缓存系统
**核心文件**: `Editor/GraphEditors/AsakiGraphTypeCache.cs`

**功能特点**：
- **端口缓存**: 缓存节点类型的所有端口定义，避免重复反射
- **性能优化**: 使用静态字典实现O(1)查询
- **自动刷新**: 使用`[InitializeOnLoadMethod]`在脚本重载时清空缓存

```csharp
// 端口定义缓存
private static readonly Dictionary<Type, NodePortDefinition> _portCache;

// 查询端口
public static NodePortDefinition GetPortDefinition(Type nodeType)
{
    if (_portCache.TryGetValue(nodeType, out var def))
        return def;
    
    def = BuildPortDefinition(nodeType);
    _portCache[nodeType] = def;
    return def;
}
```

**使用场景**: 行为树编辑、对话系统、技能编辑器、任务流程

---

### 15. PropertyDrawers - 属性绘制器 (Editor/PropertyDrawers/)

#### 15.1 AsakiProperty绘制器
**核心文件**: `Editor/PropertyDrawers/AsakiPropertyDrawer.cs`

**功能特点**：
- **泛型支持**: 自定义`AsakiProperty<T>`的Inspector显示
- **序列化兼容**: 通过查找`_value`字段实现与Unity序列化系统兼容
- **复杂类型支持**: 支持Vector3、Struct等复杂类型的嵌套绘制

**使用示例**：
```csharp
// 在MonoBehaviour中使用AsakiProperty
public class MyComponent : MonoBehaviour
{
    [SerializeField] 
    private AsakiProperty<int> _health = new AsakiProperty<int>(100);
    
    [SerializeField] 
    private AsakiProperty<Vector3> _position = new AsakiProperty<Vector3>(Vector3.zero);
    
    // Inspector中会自动显示为可编辑字段
    // 支持嵌套类型的展开和折叠
}
```

**Inspector显示效果**：
```
MyComponent
├─ Health: 100
└─ Position: (0.0, 0.0, 0.0)
```

**使用场景**: 组件数据绑定、状态可视化、编辑器调试

---

### 16. Debugging - 调试工具 (Editor/Debugging/)

#### 16.1 事件调试器
**核心文件**: `Editor/Debugging/AsakiEventDebuggerWindow.cs`

**功能特点**：
- **事件列表**: 显示所有实现`IAsakiEvent`的struct类型
- **实时统计**: Play模式下显示发布次数、订阅者数量
- **手动触发**: 支持在编辑器中手动触发事件进行测试
- **订阅者查看**: 显示当前所有订阅该事件的处理器
- **字段编辑器**: 支持编辑事件字段后手动发布

**使用方法**：
```csharp
// 菜单路径: Asaki/Debugger/Event Inspector
// 快捷键: F8

// 功能:
// 1. 搜索和筛选事件类型
// 2. 查看事件元数据（命名空间、接口、字段）
// 3. Play模式下查看实时统计数据
// 4. 手动编辑并发布事件进行测试
// 5. 查看订阅者列表
```

**界面布局**：
```
┌─────────────────────────────────────────┐
│ Event Inspector                    [X] │
├─────────────────────────────────────────┤
│ [搜索框]                                │
│ □ FrameworkReadyEvent    Pub: 1  Sub: 5 │
│ □ PlayerDamagedEvent       Pub: 0  Sub: 2 │
│ ■ GameStateChangedEvent    Pub: 3  Sub: 1 │ ← 选中
├─────────────────────────────────────────┤
│ [字段编辑]                              │
│ NewState: ▼ Playing                     │
│ OldState: ▼ Loading                     │
│ [Publish Event]                         │
└─────────────────────────────────────────┘
```

#### 16.2 智能对象池调试器
**核心文件**: `Editor/Debugging/AsakiSmartPoolDebuggerWindow.cs`

**功能特点**：
- **池列表**: 显示所有注册的对象池
- **实时统计**: Inactive数量、Spawn次数、Hit次数、峰值容量
- **命中率计算**: 自动计算并显示颜色编码的命中率（>80%绿色，<30%红色）
- **手动操作**: 支持预热（Prewarm）、清理（Trim）操作
- **自动刷新**: Play模式下自动刷新数据

**统计指标**：
```csharp
// 命中率计算公式
HitRate = SpawnCount > 0 ? (float)HitCount / SpawnCount * 100 : 0;

// 颜色编码
if (hitRate >= 80) color = Color.green;   // 优秀
else if (hitRate >= 50) color = Color.yellow; // 一般
else color = Color.red;                   // 较差，需要调整容量
```

**使用场景**: 性能监控、对象池调优、内存泄漏检测

---

### 17. 代码生成和自动化工具 (Editor/Utilities/Tools/)

#### 17.1 音频ID生成器
**核心文件**: `Editor/Utilities/Tools/AsakiAudioGenerator.cs`

**功能特点**：
- **自动扫描**: 扫描项目中所有AudioClip资源
- **命名冲突检测**: 检测并报告命名冲突
- **代码生成**: 生成`AudioID.cs`枚举和扩展方法
- **配置同步**: 同步数据到`AsakiAudioConfig.asset`
- **黑名单过滤**: 支持配置忽略路径（如Editor、Plugins）

**生成代码示例**：
```csharp
// Generated/AudioID.cs
public enum AudioID
{
    None = 0,
    Bgm_Main = 123456789,
    Sfx_Click = 987654321,
    Voice_Dialog_01 = 111111111,
}

public static class AudioExtensions
{
    public static AsakiAudioHandle Play(this IAsakiAudioService service, 
        AudioID id, AsakiAudioParams p = default, CancellationToken token = default)
    {
        return service.Play((int)id, p, token);
    }
}
```

**使用流程**：
```
1. 菜单: Asaki/Tools/Generate Audio IDs
2. 扫描项目中的所有AudioClip
3. 生成AudioID.cs枚举
4. 同步到AsakiAudioConfig.asset
5. 在代码中使用强类型API
```

#### 17.2 UI生成器
**核心文件**: `Editor/UI/AsakiUIGeneratorWindow.cs`

**功能特点**：
- **拖拽支持**: 支持拖拽Prefab或文件夹批量添加
- **层级配置**: 支持配置UILayer（Background、Normal、Popup、Overlay）
- **路径自定义**: 支持自定义加载路径（Addressable Key或Resources路径）
- **配置同步**: 双向同步数据到`AsakiUIConfig.asset`
- **冲突检测**: 自动检测枚举名冲突

**使用流程**：
```
1. 菜单: Asaki/UI/UI Generator Window
2. 拖拽UI Prefab到窗口
3. 配置Layer和LoadPath
4. 点击"Sync Configuration & Generate Code"
5. 生成UIID.cs枚举
```

#### 17.3 文件树生成器
**核心文件**: `Editor/Utilities/Tools/AsakiFileTreeGenerator.cs`

**功能特点**：
- **目录扫描**: 递归扫描指定目录结构
- **过滤配置**: 支持扩展名白名单、忽略.meta文件
- **深度限制**: 可配置最大扫描深度
- **统计显示**: 显示目录数、文件数、总大小
- **树形输出**: 生成类似`tree`命令的文本格式

**输出示例**：
```
============================================================
Directory Tree: E:/Projects/UnityGame/Asaki/Assets/Asaki
Generated Time: 2025-12-23 14:30:00
============================================================

Asaki/
├───Core/
│   ├───Context/
│   │   ├───AsakiContext.cs (4.5 KB)
│   │   └───IAsakiModule.cs (1.2 KB)
│   └───Broker/
└───Editor/
    └───GraphEditors/

Total: 12 directories, 156 files, 2.3 MB
```

**使用场景**: 项目文档生成、代码审查、架构分析

---

### 18. Configuration - 配置编辑器 (Editor/Configuration/)

#### 18.1 CSV表格编辑器
**核心文件**: `Editor/Configuration/AsakiConfigDashboardWindow.cs`

**功能特点**：
- **CSV表格编辑**: 可视化编辑配置CSV文件
- **分页显示**: 支持大数据量分页
- **行操作**: 支持添加、删除、批量选择行
- **列操作**: 支持批量编辑列数据
- **数据验证**: 支持Vector3、Vector2等特殊类型解析
- **ID管理**: 支持重新索引和排序ID

**支持的类型**：
- 基础类型: int, float, string, bool
- Unity类型: Vector3, Vector2
- 自动处理CSV转义和引号

**使用示例**：
```csharp
// 编辑音频配置
ID, Path, Volume, Loop
1001, Audio/BGM/Main, 0.8, true
1002, Audio/SFX/Click, 1.0, false

// 批量修改
// 1. 选择多行
// 2. 右键 -> Edit Column
// 3. 输入新值
// 4. 应用到所有选中行
```

**使用场景**: 游戏配置、数值调整、本地化数据

---

### 19. 扩展工具 (Editor/Utilities/Extensions/)

#### 19.1 分隔条扩展
**核心文件**: `Editor/Utilities/Extensions/GUILayoutExtensions.cs`

**功能特点**：
- **拖拽调整**: 支持拖拽调整面板宽度
- **范围限制**: 支持min/max宽度限制
- **光标反馈**: 拖拽时显示Resize光标
- **双向支持**: 支持左侧或右侧拖拽

**使用示例**：
```csharp
// 在EditorGUILayout.BeginHorizontal()中使用
GUILayoutExtensions.Splitter(
    ref leftPanelWidth, 
    150f,  // minWidth
    position.width - 200f  // maxWidth
);
```

**使用场景**: 自定义编辑器、面板布局、工具窗口

---

## 架构设计亮点和最佳实践

### 20. 性能优化要点

#### 20.1 零GC设计
- **Struct优先**: 事件、句柄、参数等使用struct代替class
  ```csharp
  // 推荐 ✅
  public struct FrameworkReadyEvent : IAsakiEvent { }
  public struct AsakiAudioHandle : IEquatable<AsakiAudioHandle>
  
  // 避免 ❌
  public class FrameworkReadyEvent : IAsakiEvent { } // 会产生GC
  ```

- **避免装箱**: 使用泛型约束避免接口调用装箱
  ```csharp
  // 推荐 ✅
  public void Publish<T>(T e) where T : struct, IAsakiEvent;
  
  // 避免 ❌
  public void Publish(IAsakiEvent e); // 会装箱
  ```

- **对象池**: 频繁创建的对象使用池化
  ```csharp
  // 音频代理池化
  AsakiSmartPool.Register(AGENT_POOL_KEY, agentPrefab);
  var agent = AsakiSmartPool.Spawn(AGENT_POOL_KEY, ...);
  ```

#### 20.2 无锁设计
- **读操作无锁**: Context使用Copy-On-Write架构
  ```csharp
  // 读操作无锁（O(1)）
  T AsakiContext.Get<T>(); // 直接数组索引
  
  // 写操作有锁（O(n)）
  void AsakiContext.Register<T>(T service); // 复制整个数组
  ```

- **静态泛型桶**: Broker每种事件类型独立存储
  ```csharp
  // 每种事件类型一个静态字段，无竞争
  static AsakiEventBus<T> AsakiBroker.GetOrCreateBus<T>();
  ```

#### 20.3 异步优先
- **异步加载**: 所有IO操作异步化
  ```csharp
  // 推荐 ✅
  await LoadAsync<T>(location, token);
  
  // 避免 ❌
  Resources.Load<T>(path); // 阻塞主线程
  ```

- **取消支持**: 所有异步操作支持CancellationToken
  ```csharp
  var cts = new CancellationTokenSource();
  var handle = await LoadAsync<T>(location, cts.Token);
  cts.Cancel(); // 可取消
  ```

- **进度报告**: 长时间操作提供进度回调
  ```csharp
  await LoadAsync<T>(location, progress => 
  {
      loadingBar.value = progress;
  }, token);
  ```

#### 20.4 批量操作优化
- **进度聚合**: 批量加载时聚合多个任务的进度
  ```csharp
  float[] progresses = new float[count];
  Action<float> GetProgressHandler(int index)
  {
      return (p) => 
      {
          progresses[index] = p;
          onProgress(progresses.Average());
      };
  }
  ```

- **依赖共享**: 批量加载自动共享依赖资源
  ```csharp
  // A和B都依赖C，C只会加载一次，引用计数为2
  await LoadBatchAsync(new[] { "A", "B" }, ...);
  ```

---

### 21. 服务实现模式

#### 21.1 构造函数注入
```csharp
// 推荐 ✅ 构造函数注入
public class AsakiAudioService : IAsakiAudioService, IAsakiModule
{
    private readonly AsakiAudioConfig _config;
    private readonly GameObject _agentPrefab;
    
    public AsakiAudioService(AsakiAudioConfig config, GameObject agentPrefab)
    {
        _config = config;
        _agentPrefab = agentPrefab;
    }
}

// 避免 ❌ 服务定位器反模式
public class AsakiAudioService : IAsakiModule
{
    public void OnInit()
    {
        // 难以测试，依赖隐藏
        _config = AsakiContext.Get<AsakiAudioConfig>();
    }
}
```

#### 21.2 两阶段初始化
```csharp
public class MyService : IAsakiService, IAsakiModule
{
    private IAnotherService _another;
    
    // 阶段1: 同步注册，只获取配置
    public void OnInit()
    {
        // 注册服务
        AsakiContext.Register<IMyService>(this);
        
        // 获取配置
        _config = AsakiContext.Get<AsakiConfig>();
    }
    
    // 阶段2: 异步初始化，执行耗时操作
    public async Task OnInitAsync()
    {
        // 获取依赖（确保已初始化）
        _another = AsakiContext.Get<IAnotherService>();
        
        // 执行耗时操作
        await InitializeAsync();
    }
    
    // 阶段3: 清理资源
    public void OnDispose()
    {
        _another?.Cleanup();
    }
}
```

#### 21.3 资源生命周期管理
```csharp
// RAII模式：使用using自动释放
using (var handle = await resService.LoadAsync<T>(location))
{
    // 使用资源
} // 自动调用Dispose -> Release

// 手动管理（不推荐）
var handle = await resService.LoadAsync<T>(location);
try
{
    // 使用资源
}
finally
{
    handle.Dispose(); // 容易忘记
}
```

---

### 22. 异步操作规范

#### 22.1 CancellationToken使用
```csharp
// 推荐 ✅ 支持取消
public async Task<T> LoadAsync<T>(string location, CancellationToken token)
{
    token.ThrowIfCancellationRequested();
    
    // 传递取消令牌
    var asset = await LoadAssetAsync(location, token);
    
    // 检查取消
    if (token.IsCancellationRequested)
    {
        Unload(asset);
        token.ThrowIfCancellationRequested();
    }
    
    return asset;
}

// 链接多个令牌
var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
    _serviceCts.Token, 
    userToken
);
```

#### 22.2 异常处理
```csharp
// 推荐 ✅ 统一异常处理
try
{
    await operation;
}
catch (OperationCanceledException)
{
    // 取消操作，正常流程
    Debug.Log("Operation cancelled");
}
catch (Exception ex)
{
    // 错误处理
    Debug.LogError($"Load failed: {ex}");
    throw; // 重新抛出
}

// FireAndForget模式（不等待）
task.FireAndForget(ex => 
{
    if (ex is not OperationCanceledException)
        Debug.LogError($"Unhandled error: {ex}");
});
```

#### 22.3 进度报告
```csharp
// 定义进度回调
try
{
    var handle = await LoadAsync<T>(location, progress => 
    {
        // 更新UI
        loadingBar.value = progress * 100;
        progressText.text = $"{progress:P}";
        
        // 检查取消
        if (userCancelled) cts.Cancel();
    }, token);
}
catch (OperationCanceledException)
{
    // 用户取消
}
```

---

### 23. 资源生命周期管理

#### 23.1 ResHandle RAII模式
```csharp
// 1. 加载资源
var handle = await resService.LoadAsync<GameObject>("Prefabs/Enemy");

// 2. 使用资源
var enemy = handle.Asset;
Instantiate(enemy);

// 3. 自动释放（离开作用域）
// handle.Dispose() -> ResService.Release(location)
```

#### 23.2 依赖追踪
```csharp
// 资源依赖链
// A -> depends on -> B, C
// B -> depends on -> D
// 
// 加载A时，自动加载B、C、D
// 引用计数：A=1, B=1, C=1, D=1
// 
// 释放A时，自动释放B、C、D（如果不再被使用）
// 引用计数：A=0, B=0, C=0, D=0
```

#### 23.3 对象池管理
```csharp
// 1. 注册预制体
AsakiSmartPool.Register("enemy", enemyPrefab);

// 2. 从池中获取
var enemy = AsakiSmartPool.Spawn("enemy", position, rotation, parent);

// 3. 使用对象
enemy.GetComponent<EnemyAI>().Initialize(data);

// 4. 回收到池
AsakiSmartPool.Despawn(enemy, "enemy");
// 或
enemy.GetComponent<IAsakiPoolable>().OnDespawn();
```

#### 23.4 跨场景资源管理
```csharp
// 跨场景持久化
var root = new GameObject("[ServiceRoot]");
DontDestroyOnLoad(root); // 跨场景保持

// 池化资源驻留
_poolKeepers[key] = resHandle; // 保持引用，防止被卸载

// 场景切换时清理
private void OnDestroy()
{
    foreach(var keeper in _poolKeepers.Values)
        keeper.Dispose();
    _poolKeepers.Clear();
}
```

---

### 24. 线程安全设计

#### 24.1 ConcurrentQueue使用
```csharp
// 线程安全队列（跨线程请求）
private readonly ConcurrentQueue<IAsakiWindow> _pendingDestroyQueue;

// 生产者（任意线程）
public void Close(IAsakiWindow window)
{
    _pendingDestroyQueue.Enqueue(window);
}

// 消费者（主线程Tick）
public void Tick(float deltaTime)
{
    while(_pendingDestroyQueue.TryDequeue(out var window))
    {
        ProcessCloseRequest(window); // 主线程执行
    }
}
```

#### 24.2 锁粒度控制
```csharp
// 细粒度锁（每个资源独立锁）
private readonly Dictionary<string, object> _locks = new();

// 获取资源锁
private object GetLock(string location)
{
    lock(_locks)
    {
        if(!_locks.TryGetValue(location, out var lockObj))
        {
            lockObj = new object();
            _locks[location] = lockObj;
        }
        return lockObj;
    }
}

// 使用资源锁
lock(GetLock(location))
{
    // 操作资源
}
```

#### 24.3 原子操作
```csharp
// 引用计数使用原子操作
Interlocked.Increment(ref record.RefCount);
Interlocked.Decrement(ref record.RefCount);

// 自增ID
private int _handleCounter = 0;
var newId = Interlocked.Increment(ref _handleCounter);
```

---

### 25. 跨场景持久化

#### 25.1 服务根节点
```csharp
// 创建跨场景根节点
var root = new GameObject("[AsakiAudioSystem]");
DontDestroyOnLoad(root); // 保证跨场景存在

// 挂载所有服务组件
var audioService = root.AddComponent<AudioService>();
var uiRoot = root.AddComponent<UIRoot>();
```

#### 25.2 模块生命周期
```csharp
// 场景切换时保持
public class AsakiBootstrapper : MonoBehaviour
{
    private void Awake()
    {
        if (_instance != null) 
        {
            Destroy(gameObject);
            return;
        }
        
        _instance = this;
        DontDestroyOnLoad(gameObject); // 跨场景单例
    }
}

// 场景切换时清理
private void OnDestroy()
{
    if (_instance == this)
    {
        AsakiContext.ClearAll(); // 触发所有Module.OnDispose()
        _instance = null;
    }
}
```

---

### 26. 架构设计决策

#### 26.1 为什么使用struct事件？
**问题**: 事件发布频繁，如何避免GC？

**决策**: 使用struct代替class
- ✅ 栈分配，零GC
- ✅ 值类型，不可变
- ✅ 泛型约束，类型安全
- ❌ 不能继承（用接口弥补）

#### 26.2 为什么使用Copy-On-Write？
**问题**: 服务注册不频繁，但读取非常频繁

**决策**: Copy-On-Write架构
- ✅ 读操作无锁（O(1)）
- ✅ 写操作有锁（O(n)，但频率低）
- ✅ 快照隔离，读操作不受写操作影响
- ❌ 内存占用略高（多个快照）

#### 26.3 为什么使用两阶段初始化？
**问题**: 模块有依赖关系，如何确保初始化顺序？

**决策**: OnInit() + OnInitAsync()
- ✅ OnInit()只注册服务，不获取依赖
- ✅ OnInitAsync()执行耗时操作，可等待依赖
- ✅ 拓扑排序确保依赖顺序
- ✅ 异步初始化不阻塞主线程

#### 26.4 为什么使用ResHandle？
**问题**: 如何自动管理资源生命周期？

**决策**: RAII模式的ResHandle
- ✅ using自动释放，避免忘记
- ✅ 引用计数精确控制
- ✅ 依赖自动追踪
- ✅ 异常安全（finally保证释放）

#### 26.5 为什么使用Tick驱动UI关闭？
**问题**: UI关闭可能递归调用，导致栈溢出

**决策**: ConcurrentQueue + Tick处理
- ✅ 线程安全，支持任意线程调用
- ✅ 延迟执行，避免递归
- ✅ 统一在Tick处理，简化逻辑
- ✅ 批量处理，性能更好

---

### 27. 关键文件清单

#### 27.1 Bootstrapper层（启动系统）
```
Unity/Bootstrapper/
├── AsakiBootstrapper.cs                      # 框架入口，启动流程
├── AsakiModuleLoader.cs                      # 模块加载和拓扑排序
├── AsakiReflectionModuleDiscovery.cs         # 模块发现
└── Modules/
    ├── AsakiAudioModule.cs                   # 音频模块
    ├── AsakiUIModule.cs                      # UI模块
    ├── AsakiResKitModule.cs                  # 资源模块
    ├── AsakiSmartPoolModule.cs               # 对象池模块
    └── AsakiEventBusModule.cs                # 事件总线模块
```

#### 27.2 核心服务
```
Unity/Services/
├── Audio/
│   └── AsakiAudioService.cs                  # 音频服务实现
├── UI/
│   └── AsakiUIManager.cs                     # UI管理器
└── Resources/
    ├── AsakiResService.cs                    # 资源服务
    └── AsakiResKitFactory.cs                 # 资源工厂
```

#### 27.3 桥接层
```
Unity/Bridge/
└── AsakiMonoDriver.cs                        # Unity生命周期桥接
```

#### 27.4 配置层
```
Unity/Configuration/
└── AsakiConfig.cs                            # 全局配置
```

#### 27.5 编辑器扩展 - 模块系统
```
Editor/ModuleSystem/
└── AsakiModuleDashboard.cs                   # 模块仪表板
```

#### 27.6 编辑器扩展 - 图系统
```
Editor/GraphEditors/
├── AsakiGraphWindow.cs                       # 图编辑器主窗口
├── AsakiGraphView.cs                         # 图视图
├── AsakiNodeView.cs                          # 节点视图
├── AsakiBlackboardProvider.cs                # Blackboard面板
├── AsakiGraphTypeCache.cs                    # 类型缓存
├── AsakiNodeSearchWindow.cs                  # 节点搜索窗口
└── AsakiGraphDebugger.cs                     # 运行时调试器
```

#### 27.7 编辑器扩展 - 调试工具
```
Editor/Debugging/
├── AsakiEventDebuggerWindow.cs               # 事件调试器
└── AsakiSmartPoolDebuggerWindow.cs           # 对象池调试器
```

#### 27.8 编辑器扩展 - 代码生成
```
Editor/Utilities/Tools/
├── AsakiAudioGenerator.cs                    # 音频ID生成器
├── AsakiFileTreeGenerator.cs                 # 文件树生成器
└── Editor/UI/
    └── AsakiUIGeneratorWindow.cs             # UI生成器
```

#### 27.9 编辑器扩展 - 配置和属性
```
Editor/Configuration/
└── AsakiConfigDashboardWindow.cs             # CSV配置编辑器

Editor/PropertyDrawers/
└── AsakiPropertyDrawer.cs                    # AsakiProperty绘制器

Editor/Utilities/Extensions/
└── GUILayoutExtensions.cs                    # 分隔条扩展
```

#### 27.10 核心层（Core）
```
Core/
├── Context/
│   ├── AsakiContext.cs                       # 服务容器
│   ├── IAsakiModule.cs                       # 模块接口
│   └── IAsakiService.cs                      # 服务接口
├── Broker/
│   ├── AsakiBroker.cs                        # 事件总线
│   ├── AsakiEventBus.cs                      # 事件总线实现
│   └── IAsakiEvent.cs                        # 事件接口
├── MVVM/
│   ├── AsakiProperty.cs                      # 响应式属性
│   └── IAsakiObserver.cs                     # 观察者接口
├── Pooling/
│   ├── AsakiSmartPool.Core.cs                # 对象池核心
│   ├── AsakiSmartPool.Async.cs               # 对象池异步
│   ├── AsakiSmartPool.Management.cs          # 对象池管理
│   └── IAsakiPoolable.cs                     # 池化接口
├── Graphs/
│   ├── AsakiGraphBase.cs                     # 图基类
│   ├── AsakiNodeBase.cs                      # 节点基类
│   ├── AsakiGraphAsset.cs                    # 图资源
│   └── AsakiBlackboardData.cs                # Blackboard数据
└── Simulation/
    ├── AsakiSimulationManager.cs             # 时钟管理器
    └── IAsakiTickable.cs                     # Tick接口
```

#### 27.11 接口定义（服务契约）
```
Core/Audio/
└── IAsakiAudioService.cs                     # 音频服务接口

Core/UI/
├── IAsakiUIService.cs                        # UI服务接口
└── IAsakiWindow.cs                           # 窗口接口

Core/Resources/
├── IAsakiResService.cs                       # 资源服务接口
├── IAsakiResStrategy.cs                      # 资源策略接口
└── IAsakiResDependencyLookup.cs              # 依赖查询接口

Core/Configuration/
└── IAsakiConfigService.cs                    # 配置服务接口

Core/Coroutines/
└── IAsakiRoutineService.cs                   # 协程服务接口
```

#### 27.12 配置资源
```
Unity/Configuration/
├── AsakiConfig.cs                            # 主配置（ScriptableObject）
├── AsakiAudioConfig.cs                       # 音频配置
└── AsakiUIConfig.cs                          # UI配置
```

#### 27.13 生成代码（Generated）
```
Generated/
├── AudioID.cs                                # 音频ID枚举（自动生成）
└── UIID.cs                                   # UIID枚举（自动生成）
```

#### 27.14 属性标记（Attributes）
```
Core/Attributes/
├── AsakiModuleAttribute.cs                   # 模块标记
├── AsakiBindAttribute.cs                     # 属性绑定标记
├── AsakiGraphContextAttribute.cs             # 图上下文标记
└── AsakiUIBuilderAttribute.cs                # UI构建器标记
```

---

### 28. 开发流程建议

#### 28.1 新项目接入流程
```
阶段1: 框架集成
├── 1.1 复制Asaki文件夹到Assets/
├── 1.2 检查.asmdef编译（Unity自动）
├── 1.3 配置可选依赖（UniTask、Addressables）
└── 1.4 创建AsakiConfig.asset（右键Create > Asaki > AsakiConfig）

阶段2: 模块开发
├── 2.1 设计模块依赖关系
├── 2.2 实现IAsakiModule接口
├── 2.3 添加[AsakiModule]属性标记
├── 2.4 在OnInit中注册服务
└── 2.5 在OnInitAsync中执行异步初始化

阶段3: 服务实现
├── 3.1 定义服务接口继承IAsakiService
├── 3.2 实现服务类
├── 3.3 使用构造函数注入
├── 3.4 实现两阶段初始化
└── 3.5 在OnDispose中清理资源

阶段4: 图系统开发（可选）
├── 4.1 继承AsakiNodeBase创建节点类型
├── 4.2 实现节点逻辑
├── 4.3 添加[AsakiGraphContext]标记
└── 4.4 使用AsakiGraphWindow编辑图

阶段5: 资源配置
├── 5.1 准备音频资源
├── 5.2 准备UI Prefab
├── 5.3 使用Audio Generator生成AudioID
├── 5.4 使用UI Generator生成UIID
└── 5.5 配置AsakiAudioConfig和AsakiUIConfig

阶段6: 调试优化
├── 6.1 使用Module Dashboard检查依赖
├── 6.2 使用Event Debugger监控事件
├── 6.3 使用Smart Pool Debugger优化对象池
└── 6.4 使用Profiler分析性能
```

#### 28.2 日常开发流程
```
每日开发循环:
├── 1. 启动Unity，进入Play模式
├── 2. 打开Event Debugger（F8）监控关键事件
├── 3. 打开Smart Pool Debugger监控对象池
├── 4. 进行功能开发
├── 5. 使用Graph Editor编辑行为树/对话（如需要）
├── 6. 使用Module Dashboard调整模块优先级（如需要）
└── 7. 提交代码前运行File Tree Generator更新文档

每周优化循环:
├── 1. 分析Profiler数据，识别性能瓶颈
├── 2. 检查对象池命中率（<30%需要调整）
├── 3. 检查事件发布频率（过高需要优化）
├── 4. 检查资源加载时间（过长需要预加载）
└── 5. 更新IFLOW.md文档

每月架构审查:
├── 1. 审查模块依赖关系（检查循环依赖）
├── 2. 审查服务接口（检查是否臃肿）
├── 3. 审查事件设计（检查是否过多）
├── 4. 审查对象池配置（检查容量是否合理）
└── 5. 生成项目架构图
```

---

### 29. 最佳实践

#### 29.1 模块设计最佳实践
```csharp
// ✅ 推荐：单一职责，依赖清晰
[AsakiModule(150, typeof(EventBusModule))]
public class SmartPoolModule : IAsakiModule
{
    public void OnInit()
    {
        // 只注册核心服务
        AsakiContext.Register<ISmartPool>(new AsakiSmartPool());
    }
    
    public async Task OnInitAsync()
    {
        // 异步预热常用对象
        await PreloadCommonPrefabs();
    }
}

// ❌ 避免：职责不清，依赖混乱
[AsakiModule(150)] // 缺少依赖声明
public class SmartPoolModule : IAsakiModule
{
    public void OnInit()
    {
        // 注册过多服务
        AsakiContext.Register<ISmartPool>(...);
        AsakiContext.Register<IObjectFactory>(...);
        AsakiContext.Register<ILoadBalancer>(...);
    }
}
```

#### 29.2 服务设计最佳实践
```csharp
// ✅ 推荐：接口简洁，依赖注入
public class AsakiAudioService : IAsakiAudioService, IAsakiModule
{
    private readonly AsakiAudioConfig _config;
    
    public AsakiAudioService(AsakiAudioConfig config) // 构造函数注入
    {
        _config = config;
    }
    
    public AsakiAudioHandle Play(int assetId, AsakiAudioParams p = default)
    {
        // 简洁的API
    }
}

// ❌ 避免：服务定位器，隐藏依赖
public class AsakiAudioService : IAsakiModule
{
    public void OnInit()
    {
        // 隐藏依赖，难以测试
        _config = AsakiContext.Get<AsakiAudioConfig>();
    }
    
    public async Task<AsakiAudioHandle> PlayAsync(int assetId) // 不必要的异步
    {
        // 复杂的API
    }
}
```

#### 29.3 事件设计最佳实践
```csharp
// ✅ 推荐：精简的struct事件
public struct PlayerDamagedEvent : IAsakiEvent
{
    public int PlayerId { get; set; }
    public int Damage { get; set; }
    public Vector3 HitPosition { get; set; }
}

// 发布事件
var e = new PlayerDamagedEvent { PlayerId = 1, Damage = 10 };
AsakiBroker.Publish(e);

// ❌ 避免：包含逻辑的事件
public struct PlayerDamagedEvent : IAsakiEvent
{
    public PlayerController Player { get; set; } // 引用类型
    public void ApplyDamage() { /* ... */ }     // 包含逻辑
}
```

#### 29.4 对象池最佳实践
```csharp
// ✅ 推荐：实现IAsakiPoolable，正确重置状态
public class Bullet : MonoBehaviour, IAsakiPoolable
{
    public void OnSpawn()
    {
        // 重置位置、旋转
        transform.position = Vector3.zero;
        transform.rotation = Quaternion.identity;
        
        // 启用组件
        gameObject.SetActive(true);
    }
    
    public void OnDespawn()
    {
        // 停止协程
        StopAllCoroutines();
        
        // 禁用组件
        gameObject.SetActive(false);
        
        // 重置Rigidbody
        var rb = GetComponent<Rigidbody>();
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }
}

// ❌ 避免：不重置状态
public class Bullet : MonoBehaviour, IAsakiPoolable
{
    public void OnSpawn() { }
    public void OnDespawn() { }
    // 状态未重置，导致下次使用时数据错误
}
```

#### 29.5 资源管理最佳实践
```csharp
// ✅ 推荐：使用using自动管理
public async Task SpawnEnemy(string prefabPath)
{
    using (var handle = await _resService.LoadAsync<GameObject>(prefabPath))
    {
        var enemy = Instantiate(handle.Asset);
        // 使用enemy
    } // 自动释放
}

// ❌ 避免：手动管理，容易泄漏
public async Task SpawnEnemy(string prefabPath)
{
    var handle = await _resService.LoadAsync<GameObject>(prefabPath);
    var enemy = Instantiate(handle.Asset);
    // 使用enemy
    // 忘记调用handle.Dispose()，导致内存泄漏
}
```

#### 29.6 UI管理最佳实践
```csharp
// ✅ 推荐：使用泛型API，强类型
public async Task OpenMainMenu()
{
    var menu = await _uiManager.OpenAsync<MainMenuWindow>((int)UIID.MainMenu);
    menu.OnStartGame += HandleStartGame;
}

public void CloseMainMenu()
{
    _uiManager.Close<MainMenuWindow>(); // 类型安全
}

// ❌ 避免：使用object，类型不安全
public async Task OpenMainMenu()
{
    var menu = await _uiManager.OpenAsync((int)UIID.MainMenu); // 返回object
    (menu as MainMenuWindow).OnStartGame += HandleStartGame; // 需要转换
}
```

---

### 30. 扩展开发指南

#### 30.1 添加自定义图类型
```csharp
// 步骤1: 创建图类
[CreateAssetMenu(menuName = "Asaki/QuestGraph")]
public class QuestGraph : AsakiGraphBase
{
    // 自定义图逻辑
}

// 步骤2: 创建节点类
[Serializable]
public class QuestNode : AsakiNodeBase
{
    [SerializeField] private string questId;
    [SerializeField] private int requiredLevel;
    
    public override async Task Execute(AsakiGraphRuntimeContext ctx)
    {
        // 节点逻辑
    }
}

// 步骤3: 注册编辑器
[InitializeOnLoad]
public static class QuestGraphEditorRegistration
{
    static QuestGraphEditorRegistration()
    {
        AsakiGraphWindow.Register<QuestGraph>(graph => 
            new QuestGraphEditor(graph));
    }
}

// 步骤4: 创建自定义编辑器
public class QuestGraphEditor : AsakiGraphController
{
    public QuestGraphEditor(AsakiGraphBase graph) : base(graph) { }
    
    // 自定义编辑器逻辑
}
```

#### 30.2 添加自定义节点类型
```csharp
// 步骤1: 创建节点类
[Serializable]
[AsakiNodeInfo("Math/Add")] // 在搜索中显示为"Math/Add"
public class AddNode : AsakiNodeBase
{
    [InputPort] public float a;
    [InputPort] public float b;
    [OutputPort] public float result;
    
    public override void Execute()
    {
        result = a + b;
    }
}

// 步骤2: 创建节点视图（可选）
public class AddNodeView : AsakiNodeView
{
    public AddNodeView(AsakiNodeBase node) : base(node) { }
    
    // 自定义视图（自定义颜色、图标等）
}

// 步骤3: 注册节点视图（可选）
AsakiGraphView.RegisterNodeView<AddNode>(node => new AddNodeView(node));
```

#### 30.3 添加自定义资源策略
```csharp
// 步骤1: 实现资源策略接口
public class CustomResStrategy : IAsakiResStrategy
{
    public Task InitializeAsync()
    {
        // 初始化
        return Task.CompletedTask;
    }
    
    public async Task<Object> LoadAssetInternalAsync(
        string location, 
        Type type, 
        Action<float> onProgress, 
        CancellationToken token)
    {
        // 自定义加载逻辑
        // 例如：从网络加载、从AssetBundle加载等
    }
    
    public void UnloadAssetInternal(string location, Object asset)
    {
        // 自定义卸载逻辑
    }
}

// 步骤2: 实现依赖查询接口
public class CustomDependencyLookup : IAsakiResDependencyLookup
{
    public IEnumerable<string> GetDependencies(string location)
    {
        // 返回资源的依赖列表
        // 例如：从manifest文件读取
    }
}

// 步骤3: 注册自定义策略
AsakiResKitFactory.RegisterCustom(
    () => new CustomResStrategy(),
    () => new CustomDependencyLookup()
);
```

#### 30.4 添加自定义编辑器窗口
```csharp
// 步骤1: 创建编辑器窗口
public class MyToolWindow : EditorWindow
{
    [MenuItem("Asaki/Tools/My Tool")]
    public static void ShowWindow()
    {
        var window = GetWindow<MyToolWindow>("My Tool");
        window.Show();
    }
    
    private void OnGUI()
    {
        // 窗口UI
        if (GUILayout.Button("Do Something"))
        {
            DoSomething();
        }
    }
    
    private void DoSomething()
    {
        // 工具逻辑
    }
}

// 步骤2: 使用框架工具
private void DoSomething()
{
    // 获取服务
    var resService = AsakiContext.Get<IAsakiResService>();
    
    // 使用异步
    LoadAsync().Forget();
}

private async Task LoadAsync()
{
    var handle = await resService.LoadAsync<GameObject>("Prefabs/MyPrefab");
    // ...
}
```

---

### 31. 常见问题和解决方案

#### Q1: 服务注册失败，提示"Service already registered"
**原因**: 尝试注册已存在的服务

**解决方案**:
```csharp
// 方案1: 使用TryGet检查
if (!AsakiContext.TryGet<IMyService>(out var service))
{
    AsakiContext.Register<IMyService>(new MyService());
}

// 方案2: 使用GetOrRegister懒加载
var service = AsakiContext.GetOrRegister<IMyService>(
    () => new MyService()
);

// 方案3: 使用Replace替换（热更新）
AsakiContext.Replace<IMyService>(new MyService());
```

#### Q2: 事件处理程序未被调用
**原因1**: 事件未正确实现IAsakiEvent接口
```csharp
// 检查事件定义
public struct MyEvent : IAsakiEvent  // ✅ 必须实现IAsakiEvent
{
    public int Data { get; set; }
}
```

**原因2**: 未正确订阅事件
```csharp
// 检查订阅
AsakiBroker.Subscribe<MyEvent>(this);  // ✅ 使用正确类型

// 检查处理程序
public void Handle(MyEvent e)  // ✅ 参数类型匹配
{
    // 处理逻辑
}
```

**原因3**: 在冻结后注册订阅
```csharp
// Context冻结后不能注册新订阅
AsakiContext.Freeze();  // 冻结

// 之后尝试订阅会失败
AsakiBroker.Subscribe<MyEvent>(handler);  // ❌ 失败
```

#### Q3: 对象池对象状态异常
**原因**: 未正确实现IAsakiPoolable接口

**解决方案**: 
```csharp
public class MyObject : MonoBehaviour, IAsakiPoolable
{
    public void OnSpawn()
    {
        // ✅ 重置所有状态
        transform.position = Vector3.zero;
        transform.rotation = Quaternion.identity;
        gameObject.SetActive(true);
        
        // 重置组件状态
        GetComponent<Rigidbody>().velocity = Vector3.zero;
    }
    
    public void OnDespawn()
    {
        // ✅ 清理所有状态
        StopAllCoroutines();
        gameObject.SetActive(false);
        
        // 取消所有订阅
        _eventUnsubscriber?.Dispose();
    }
}
```

#### Q4: 图系统查询性能低
**原因**: 未调用InitializeRuntime()构建拓扑缓存

**解决方案**:
```csharp
// 运行时初始化
graph.InitializeRuntime();

// 之后查询性能O(1)
var nextNode = graph.GetNextNode(currentNode, "Out");  // ✅ O(1)
```

#### Q5: 资源卸载后仍然显示在内存中
**原因**: 存在未释放的ResHandle或依赖

**解决方案**:
```csharp
// 1. 检查所有ResHandle是否释放
using (var handle = await LoadAsync<T>(location))
{
    // 使用资源
} // ✅ 自动释放

// 2. 检查池化资源是否驻留
_poolKeepers[key] = resHandle; // 检查是否忘记释放

// 3. 使用Debugger检查引用计数
// 打开Smart Pool Debugger查看
```

#### Q6: Tick顺序不符合预期
**原因**: 优先级设置不当

**解决方案**:
```csharp
// 检查优先级设置
simManager.Register(inputHandler, (int)TickPriority.High);      // 0
simManager.Register(gameLogic, (int)TickPriority.Normal);       // 1000
simManager.Register(uiSync, (int)TickPriority.Low);             // 2000

// 数值越小越先执行
// 同优先级按注册顺序执行
```

#### Q7: UI关闭时崩溃
**原因**: 递归调用Close或在非主线程调用

**解决方案**:
```csharp
// ✅ 推荐：使用Enqueue延迟处理
public void Close(IAsakiWindow window)
{
    _pendingDestroyQueue.Enqueue(window);  // 线程安全
}

// Tick中统一处理
public void Tick(float deltaTime)
{
    while(_pendingDestroyQueue.TryDequeue(out var window))
    {
        ProcessCloseRequest(window);  // 主线程执行
    }
}
```

---

## 版本信息

- **框架版本**: v5.1 Lock-Free Edition
- **最后更新**: 2025-12-23
- **作者**: Asaki Framework Team
- **许可证**: MIT License
