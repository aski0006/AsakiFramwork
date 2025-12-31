# Asaki Framework 指令上下文

## 项目概述

Asaki 是一个功能齐全的 Unity 游戏开发框架，采用模块化架构设计，提供了一整套完整的解决方案，包括服务容器、事件总线、日志系统、资源管理、对象池、音频系统、UI 框架、序列化系统、MVVM 模式支持、网络服务、图形系统等核心功能。

### 核心架构特征

1. **模块化设计**：采用模块化架构，通过 `IAsakiModule` 接口定义模块生命周期
2. **服务容器**：`AsakiContext` 提供高性能的依赖注入容器，采用 Copy-On-Write 架构实现读写分离
3. **事件驱动**：`AsakiBroker` 提供基于事件总线的解耦通信机制
4. **代码生成**：集成 Roslyn 编译器插件，自动生成绑定代码和资源 ID
5. **性能优化**：大量使用内联函数和无锁数据结构优化运行时性能
6. **可扩展性**：高度可扩展的设计模式，支持自定义模块和服务
7. **生命周期管理**：完整的初始化和销毁生命周期管理

### 目录结构说明

```
Asaki/
├── CodeGen/          # Roslyn 编译器插件，用于代码生成
├── Core/             # 核心框架代码
│   ├── Attributes/   # 框架特性定义
│   ├── Audio/        # 音频系统接口
│   ├── Blackboard/   # 黑板系统（用于图形节点数据存储）
│   ├── Broker/       # 事件总线系统
│   ├── Configuration/ # 配置系统接口
│   ├── Context/      # 服务容器 (AsakiContext)
│   ├── Coroutines/   # 协程服务接口
│   ├── Graphs/       # 图形系统（节点、边、变量定义）
│   ├── Logging/      # 日志系统定义
│   ├── MVVM/         # MVVM 模式支持
│   ├── Network/      # 网络服务接口
│   ├── Pooling/      # 对象池系统
│   ├── Resources/    # 资源系统接口
│   ├── Serialization/ # 序列化系统
│   ├── Simulation/   # 模拟系统
│   ├── Time/         # 时间系统
│   └── UI/           # UI 系统接口
├── Editor/           # Unity 编辑器扩展
├── Generated/        # 自动生成的代码
└── Unity/            # Unity 集成层
    ├── Bootstrapper/ # 框架启动器
    ├── Bridge/       # Unity 与框架桥接
    ├── Configuration/ # Unity 平台配置实现
    ├── Extensions/   # Unity 扩展方法
    ├── Modules/      # Unity 平台模块实现
    ├── Services/     # Unity 平台服务实现
    └── Utils/        # Unity 工具类
```

## 核心组件详解

### AsakiContext - 服务容器 (核心组件)

高性能的服务容器，采用 Copy-On-Write 架构：
- **读操作 (Get)**: O(1)，无锁，仅一次引用解引用
- **写操作 (Register)**: O(n)，有锁，触发内存分配（仅在启动时发生）
- **架构策略**：Copy-On-Write (写时复制) + Snapshot Swap (快照交换)
- 支持冻结机制防止运行时注册新服务
- 自动销毁和清理机制
- 支持懒加载注册 `GetOrRegister<T>(Func<T> factory)`
- 包含双重检查锁定，线程安全
- 提供 `TryGetAssignable<T>` 线性查找功能

### AsakiBroker - 事件总线 (核心组件)

提供基于事件的解耦通信：
- 自动修复订阅过早问题（如果事件服务不存在，主动创建并注册）
- 支持事件发布和订阅
- 通过 IAsakiHandler 接口处理事件
- 提供扩展方法如 `AsakiRegister` 和 `AsakiUnregister`

### 日志系统 (V2) - 完整的日志解决方案

现代化的日志系统，具有聚合功能：
- 智能堆栈跟踪，区分用户代码和框架代码
- 聚合计数功能，避免重复日志刷屏
- 可配置日志等级
- 高性能的运行时表现
- 文件写入功能，支持日志轮转
- 通过 `ALog` 静态类提供便捷访问
- 包含 `AsakiLogModel`、`AsakiLogAggregator`、`AsakiLogConfig` 等组件

### AsakiPoolService - 对象池系统

高性能的对象池服务：
- 异步预热功能，分帧实例化避免卡顿
- 依赖资源系统进行资源加载
- 依赖协程系统实现分帧操作
- 支持对象的 Spawn/Despawn 操作
- 自动生命周期管理
- 包含引用计数，防止对象泄漏
- 支持位置和旋转参数设置
- 提供 `IAsakiPoolable` 接口支持对象生命周期回调

### 图形系统 (Asaki Graph System)

可视化节点编辑系统：
- `AsakiGraphBase`：基础图形结构，包含节点、边和变量
- `AsakiNodeBase`：基础节点类
- `AsakiGraphRunner`：图形运行器，执行节点逻辑
- `AsakiGraphAsset`：图形资源资产
- 支持运行时初始化，构建拓扑缓存
- 提供高效的节点查找和连接关系查询
- 支持变量系统（黑板数据）

### UI 系统

现代化的 UI 管理系统：
- `IAsakiUIService`：UI 服务接口，支持异步窗口打开
- `IAsakiWindow`：窗口接口定义
- 支持多层级 UI 管理
- 支持参数传递和窗口关闭
- 提供 `AsakiUILayer` 管理 UI 层级

### 音频系统

完整的音频管理解决方案：
- `IAsakiAudioService`：音频服务接口
- 支持播放、暂停、停止、淡入淡出
- 支持音量、音调、空间混合等参数调整
- 支持全局和分组控制
- 支持 3D 音频位置更新
- 提供 `AsakiAudioHandle` 管理音频实例
- 包含 `AsakiAudioParams` 配置参数

### 网络系统

现代化的网络请求系统：
- `IAsakiWebService`：网络服务接口
- 支持 GET/POST/POST Form 等多种请求方式
- 支持拦截器模式，可扩展请求处理逻辑
- 与 Asaki 序列化系统集成
- 使用 `UnityWebRequest` 实现
- 支持泛型请求和响应处理

### MVVM 系统

响应式数据绑定系统：
- `AsakiProperty<T>`：响应式数据属性
- 支持值变更通知
- 支持订阅和取消订阅
- 支持 `IAsakiObserver<T>` 绑定
- 实现了完整的相等性比较和运算符重载
- 支持隐式类型转换

### 序列化系统

现代化的数据序列化：
- 与 Unity 的序列化系统集成
- 支持 `IAsakiSavable` 接口定义可序列化对象
- 事件驱动的保存流程（`AsakiSaveBeginEvent`、`AsakiSaveSuccessEvent`、`AsakiSaveFailedEvent`）
- 支持异步保存操作

### 配置系统

集中式配置管理：
- `AsakiConfig`：ScriptableObject 配置对象
- 包含性能、模拟设置等全局配置
- 支持模块化配置（UI、音频、网络、日志等）
- 在 Unity Inspector 中可编辑

## 开发约定

### 编码风格

- 使用 PascalCase 命名类、方法和公共字段
- 使用 camelCase 命名私有字段和局部变量
- 命名空间使用 Asaki.Core 或 Asaki.Unity 等分层结构
- 常量和枚举值使用 PascalCase
- 接口以 "I" 开头

### 架构约定

- 服务实现 `IAsakiService` 接口
- 模块实现 `IAsakiModule` 接口
- 事件实现 `IAsakiEvent` 接口
- 配置使用 ScriptableObject
- 使用 `[AsakiBind]` 特性标记需要代码生成的类
- 使用 `[AsakiModule]` 特性标记模块类
- 使用 `[AsakiGraphContext]` 特性标记图形上下文

### 模块生命周期

1. 框架启动时，通过 `AsakiModuleLoader` 按依赖关系图 (DAG) 初始化所有模块
2. 模块实现 `IAsakiModule` 接口定义生命周期方法
3. 框架就绪后发布 `FrameworkReadyEvent` 事件
4. 框架销毁时进行资源清理

## 构建与运行

### 初始化流程

1. `AsakiBootstrapper` 在 `Awake()` 阶段初始化日志系统
2. 设置核心驱动和模拟管理器
3. 在 `Start()` 阶段进行模块发现和初始化
4. 冻结服务容器防止后续注册
5. 发布 `FrameworkReadyEvent` 事件

### 模块发现与加载

- `AsakiStaticModuleDiscovery`：静态模块发现器
- `AsakiModuleLoader`：模块加载器，按依赖关系图加载
- 使用 `[AsakiModule]` 特性自动发现模块
- 支持模块间的依赖关系定义

### 配置

- 使用 `AsakiConfig` ScriptableObject 进行全局配置
- 支持模块化配置（UI、音频、网络、日志等）
- 在 Unity Inspector 中可编辑配置参数

## 代码生成系统

框架集成 Roslyn 编译器插件，自动生成常用代码：
- UI 组件 ID 枚举（如 `UIID.cs`）
- 服务绑定代码
- 配置查找表
- 图形节点相关代码

## 扩展性

框架设计为高度可扩展：
- 通过 `IAsakiService` 接口注册自定义服务
- 通过 `IAsakiModule` 接口创建自定义模块
- 通过事件系统实现松耦合通信
- 通过编辑器扩展提供开发工具
- 支持拦截器模式扩展功能
- 模块化设计便于功能扩展

## 调试与测试

- 框架提供完整的日志系统用于调试
- 支持性能分析和内存管理
- 模块化设计便于单元测试
- 事件系统支持调试日志
- 支持运行时调试工具

## 最佳实践

1. 框架初始化阶段只应注册服务，避免在运行时创建新服务
2. 使用依赖注入而非直接访问静态成员
3. 合理使用事件系统实现模块解耦
4. 遵循模块生命周期进行资源管理
5. 利用代码生成减少样板代码
6. 使用对象池管理频繁创建/销毁的对象
7. 合理使用异步操作避免阻塞主线程
8. 使用 MVVM 模式实现数据绑定

## 设计模式

### 创建型模式
- **工厂模式**：服务创建和管理
- **单例模式**：`AsakiContext` 作为全局服务容器

### 结构型模式
- **外观模式**：`AsakiBroker` 作为事件系统的外观
- **适配器模式**：各种服务适配器

### 行为型模式
- **观察者模式**：事件系统实现
- **策略模式**：资源加载和网络请求策略
- **命令模式**：日志写入命令
- **状态模式**：日志聚合器状态管理

## 性能优化

1. **服务容器优化**：Copy-On-Write 架构，读操作 O(1) 无锁
2. **内存管理**：对象池系统避免频繁 GC
3. **异步操作**：分帧加载和处理，避免主线程阻塞
4. **缓存机制**：节点查找缓存，减少重复计算
5. **零 GC 设计**：许多核心操作设计为零 GC

## 依赖关系

框架内部组件之间具有清晰的依赖关系：
- `AsakiContext` 是基础依赖
- `AsakiBroker` 依赖 `AsakiContext`
- `AsakiPoolService` 依赖 `IAsakiCoroutineService`、`IAsakiResService`、`IAsakiEventService`
- 其他服务组件按需依赖基础服务

## 测试策略

框架设计考虑了可测试性：
- 依赖注入便于 mock
- 模块化设计便于单元测试
- 事件系统便于测试业务逻辑
- 提供完整的生命周期便于测试资源管理