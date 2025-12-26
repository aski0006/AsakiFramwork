# Pooling-SmartPool 模块

## 1. 概述

### 1.1 核心职责

SmartPool 模块是 Asaki Framework 中的对象池服务，提供高效的对象复用机制，旨在减少频繁创建和销毁游戏对象带来的性能开销。该模块实现了自动维护、异步预热和智能裁剪等高级功能，同时保持简洁易用的 API 设计。

### 1.2 设计哲学

- **高效性能**：采用栈结构实现 O(1) 复杂度的 Spawn/Despawn 操作，减少内存分配和垃圾回收
- **智能管理**：自动维护池大小，根据使用情况动态调整，闲置对象自动回收
- **异步优化**：支持分帧预热，避免一次性大量创建对象导致的卡顿
- **可扩展性**：通过 `IAsakiPoolable` 接口支持自定义对象生命周期管理
- **零配置使用**：简化的 API 设计，无需复杂配置即可快速集成

### 1.3 适用场景

- **频繁创建销毁的对象**：如子弹、粒子效果、敌人、UI 元素等
- **性能敏感场景**：移动设备或高帧率游戏开发
- **大规模对象管理**：需要同时管理大量相似对象的场景
- **减少 GC 压力**：对内存分配和垃圾回收敏感的应用

### 1.4 依赖关系

- **AsyncSystem**：用于实现异步预热和维护循环
- **Unity Engine**：依赖 Transform、GameObject 等核心组件

## 2. 核心组件

### 2.1 AsakiSmartPool

主对象池管理类，提供静态 API 用于对象的注册、生成和回收。

#### 2.1.1 核心属性

| 属性名 | 类型 | 说明 | 默认值 |
|-------|------|------|--------|
| _pool | Dictionary<string, Stack<PoolItem>> | 存储所有对象池的字典，以键值对形式管理不同类型的对象池 | 空字典 |
| _prefabs | Dictionary<string, GameObject> | 存储已注册的预制体字典，用于动态创建新对象 | 空字典 |
| _root | Transform | 所有回收对象的根节点，用于统一管理 | null |
| _defaultStackCapacity | int | 新对象池的默认容量 | 16 |
| _isQuitting | bool | 标记应用是否正在退出，用于避免退出时的对象处理 | false |

#### 2.1.2 主要方法

| 方法名 | 签名 | 说明 |
|-------|------|------|
| Register | `void Register(string key, GameObject prefab)` | 注册预制体到对象池系统 |
| Spawn | `GameObject Spawn(string key, Vector3 position, Quaternion rotation, Transform parent = null)` | 从对象池获取或创建新对象 |
| Despawn | `void Despawn(GameObject go, string key)` | 将对象回收回对象池 |
| SpawnBatch | `List<GameObject> SpawnBatch(string key, int count, Vector3 position)` | 批量生成对象 |
| DespawnBatch | `void DespawnBatch(IEnumerable<GameObject> objects, string key)` | 批量回收对象 |
| PrewarmAsync | `async Task PrewarmAsync(string key, int count, int itemsPerFrame = 5)` | 异步预热对象池，分帧创建对象 |
| SetDefaultCapacity | `void SetDefaultCapacity(int capacity)` | 设置新对象池的默认容量 |
| Cleanup | `void Cleanup()` | 清理所有对象池资源 |

### 2.2 PoolItem

池项结构体，用于管理单个池对象的状态和生命周期。

#### 2.2.1 核心属性

| 属性名 | 类型 | 说明 |
|-------|------|------|
| GameObject | GameObject | 池中的实际游戏对象 |
| Transform | Transform | 缓存的 Transform 组件，避免重复 GetComponent 调用 |
| asakiPoolable | IAsakiPoolable | 实现了 IAsakiPoolable 接口的组件引用 |
| LastActiveTime | float | 最后一次激活时间，用于判断对象是否超时 |

### 2.3 IAsakiPoolable

对象池可扩展接口，允许自定义对象的生成和回收逻辑。

#### 2.3.1 核心方法

| 方法名 | 签名 | 说明 |
|-------|------|------|
| OnSpawn | `void OnSpawn()` | 对象从池获取时调用，用于初始化对象状态 |
| OnDespawn | `void OnDespawn()` | 对象回收时调用，用于重置对象状态 |

## 3. 设计模式与实现细节

### 3.1 对象池核心实现

SmartPool 采用字典+栈的双层结构实现高效对象管理：

- **字典层**：使用 `Dictionary<string, Stack<PoolItem>>` 存储不同类型的对象池，通过键名快速查找
- **栈层**：每个对象池内部使用 `Stack<PoolItem>` 实现 O(1) 复杂度的 Spawn/Despawn 操作
- **对象包装**：使用 `PoolItem` 结构体包装实际游戏对象，缓存常用组件并记录状态

```csharp
// 核心池结构
private static readonly Dictionary<string, Stack<PoolItem>> _pool = new Dictionary<string, Stack<PoolItem>>();
private static readonly Dictionary<string, GameObject> _prefabs = new Dictionary<string, GameObject>();
```

### 3.2 异步预热机制

异步预热功能通过 AsyncSystem 实现，支持分帧创建对象，避免一次性大量创建导致的卡顿：

- 默认每帧创建 5 个对象，可通过 `itemsPerFrame` 参数调整
- 利用 `await _routineService.WaitFrame()` 实现帧间等待
- 预热的对象直接进入对象池，不激活，减少初始化开销

```csharp
// 异步预热核心逻辑
await _routineService.RunTask(async () => {
    var batchCount = 0;
    var tempStore = new List<GameObject>(count);
    
    for (int i = 0; i < count; i++) {
        // 生成但不激活
        var go = Object.Instantiate(prefab, AsakiSmartPool._root);
        go.SetActive(false);
        tempStore.Add(go);
        
        batchCount++;
        if (batchCount >= itemsPerFrame) {
            batchCount = 0;
            // 等待下一帧
            await _routineService.WaitFrame();
        }
    }
    
    // 统一入栈
    foreach (var go in tempStore) {
        AsakiSmartPool._pool[key].Push(new PoolItem(go));
    }
});
```

### 3.3 自动维护与裁剪

SmartPool 实现了自动维护机制，定期清理闲置对象：

- 维护循环每 60 秒执行一次
- 检查对象是否超过 5 分钟未使用，超时则销毁
- 当池大小超过 200 时，自动裁剪到 150 个对象
- 维护过程在异步线程中执行，不阻塞主线程

### 3.4 自定义生命周期管理

通过 `IAsakiPoolable` 接口，用户可以自定义对象的生成和回收逻辑：

- 对象生成时调用 `OnSpawn()`，可用于重置位置、速度、状态等
- 对象回收时调用 `OnDespawn()`，可用于清理资源、停止协程等
- 接口设计简洁，仅包含两个核心方法，易于实现

```csharp
public interface IAsakiPoolable
{
    void OnSpawn();  // 对象从池获取时调用
    void OnDespawn();  // 对象回收时调用
}
```

## 4. 使用示例

### 4.1 基本使用

```csharp
using Asaki.Core.Pooling;
using UnityEngine;

public class SmartPoolExample : MonoBehaviour
{
    [SerializeField] private GameObject bulletPrefab;
    private const string BULLET_KEY = "Bullet";
    
    void Start()
    {
        // 注册预制体
        AsakiSmartPool.Register(BULLET_KEY, bulletPrefab);
        
        // 异步预热 100 个子弹，每帧创建 10 个
        AsakiSmartPool.PrewarmAsync(BULLET_KEY, 100, 10);
    }
    
    void FireBullet(Vector3 position, Quaternion rotation)
    {
        // 从池获取子弹
        GameObject bullet = AsakiSmartPool.Spawn(BULLET_KEY, position, rotation);
        
        // 使用子弹...
        
        // 3 秒后回收
        StartCoroutine(DespawnAfterDelay(bullet, 3f));
    }
    
    private System.Collections.IEnumerator DespawnAfterDelay(GameObject go, float delay)
    {
        yield return new WaitForSeconds(delay);
        // 回收子弹
        AsakiSmartPool.Despawn(go, BULLET_KEY);
    }
}
```

### 4.2 实现自定义池对象

```csharp
using Asaki.Core.Pooling;
using UnityEngine;

public class PoolableBullet : MonoBehaviour, IAsakiPoolable
{
    private Rigidbody _rb;
    
    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }
    
    // 实现 IAsakiPoolable 接口
    public void OnSpawn()
    {
        // 重置刚体状态
        _rb.velocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        
        // 启动粒子效果
        GetComponent<ParticleSystem>().Play();
    }
    
    public void OnDespawn()
    {
        // 停止粒子效果
        GetComponent<ParticleSystem>().Stop();
        
        // 重置位置和旋转
        transform.position = Vector3.zero;
        transform.rotation = Quaternion.identity;
    }
}
```

### 4.3 批量操作

```csharp
using Asaki.Core.Pooling;
using System.Collections.Generic;
using UnityEngine;

public class BatchOperationExample : MonoBehaviour
{
    [SerializeField] private GameObject enemyPrefab;
    private const string ENEMY_KEY = "Enemy";
    
    void SpawnEnemyWave()
    {
        // 批量生成 10 个敌人
        List<GameObject> enemies = AsakiSmartPool.SpawnBatch(ENEMY_KEY, 10, Vector3.zero);
        
        // 分布敌人位置
        for (int i = 0; i < enemies.Count; i++)
        {
            Vector3 position = new Vector3(Random.Range(-10f, 10f), 0, Random.Range(-10f, 10f));
            enemies[i].transform.position = position;
        }
        
        // 5 秒后批量回收
        StartCoroutine(DespawnWaveAfterDelay(enemies, 5f));
    }
    
    private System.Collections.IEnumerator DespawnWaveAfterDelay(List<GameObject> enemies, float delay)
    {
        yield return new WaitForSeconds(delay);
        // 批量回收敌人
        AsakiSmartPool.DespawnBatch(enemies, ENEMY_KEY);
    }
}
```

## 5. 最佳实践

### 5.1 命名规范

- 使用清晰、唯一的键名标识不同类型的对象池，建议采用 PascalCase 命名法
- 键名应能直观反映对象类型，如 "Bullet", "Enemy", "UIButton" 等

### 5.2 预热策略

- 对于频繁使用的对象，建议在游戏加载时进行预热
- 根据实际使用量调整预热数量，避免过度占用内存
- 调整 `itemsPerFrame` 参数平衡预热速度和帧率影响

### 5.3 池对象设计

- 实现 `IAsakiPoolable` 接口处理对象的自定义初始化和重置
- 避免在 `OnEnable`/`OnDisable` 中处理复杂逻辑，改用 `OnSpawn`/`OnDespawn`
- 确保回收时重置所有必要的状态，避免对象状态污染

### 5.4 内存管理

- 定期监控池大小，避免无限增长
- 对于不再使用的对象池，考虑手动清理
- 在场景切换或游戏结束时调用 `Cleanup()` 方法释放资源

### 5.5 性能优化

- 避免在每一帧频繁调用 `Spawn`/`Despawn`，考虑批量操作
- 合理设置池容量，避免过度预分配
- 对于特别频繁使用的对象，考虑使用独立的池管理

## 6. 扩展与定制

### 6.1 自定义维护规则

SmartPool 模块的维护规则可通过修改 `AsakiSmartPool.Management.cs` 文件中的参数进行定制：

- `_maintenanceInterval`：维护循环执行间隔（秒）
- `_idleTimeout`：对象闲置超时时间（秒）
- `_maxPoolSize`：池最大容量
- `_trimmedPoolSize`：裁剪后的目标容量

### 6.2 扩展池类型

虽然 SmartPool 主要用于 GameObject 管理，但核心设计可扩展到其他类型的对象池：

- 基于现有架构创建泛型对象池
- 实现自定义的池项结构体
- 扩展异步预热和维护机制

### 6.3 集成其他系统

SmartPool 可与其他系统集成，实现更复杂的功能：

- 与 TickSystem 集成，使用固定时间步长进行池维护
- 与 MVVM 模块集成，实现 UI 元素的智能池管理
- 与 AudioSystem 集成，管理音效对象池

## 7. 常见问题与解决方案

### 7.1 对象状态异常

**问题**：从对象池获取的对象状态不正确

**解决方案**：
- 确保实现了 `IAsakiPoolable` 接口并正确处理 `OnSpawn`/`OnDespawn` 方法
- 检查对象回收时是否重置了所有必要状态
- 避免在 `OnEnable`/`OnDisable` 中依赖外部状态

### 7.2 内存占用过高

**问题**：对象池占用过多内存

**解决方案**：
- 调整预热数量，避免过度预分配
- 缩短闲置超时时间，加快闲置对象回收
- 减小池最大容量，触发更频繁的裁剪
- 定期监控池大小，手动清理不再使用的对象池

### 7.3 异步预热不生效

**问题**：调用 `PrewarmAsync` 后没有效果

**解决方案**：
- 确保 AsyncSystem 已正确初始化
- 检查是否在调用 `PrewarmAsync` 前已注册预制体
- 验证预制体是否有效，键名是否正确

### 7.4 对象池清理问题

**问题**：场景切换后对象池未正确清理

**解决方案**：
- 在场景切换前调用 `Cleanup()` 方法
- 确保在 `Application.quitting` 事件中正确处理池资源
- 检查是否存在未回收的对象引用导致内存泄漏

## 8. 版本历史

### 1.0.0
- 初始版本
- 实现核心对象池功能
- 支持异步预热和批量操作
- 添加自动维护和裁剪机制
- 实现 `IAsakiPoolable` 接口

### 1.1.0
- 优化池对象回收逻辑
- 增强异步预热性能
- 添加池统计功能
- 改进错误处理和日志记录

### 1.2.0
- 重构池管理架构
- 支持自定义维护规则
- 增加场景切换时的自动清理
- 优化内存占用

## 9. 总结

SmartPool 模块是一个高效、智能的对象池解决方案，为游戏开发提供了强大的性能优化工具。其核心设计理念是在保持易用性的同时提供高级功能，包括异步预热、自动维护和智能裁剪等。通过合理使用对象池，开发者可以显著减少内存分配和垃圾回收，提高游戏性能，特别是在移动设备和高帧率场景下。

该模块的设计具有良好的扩展性，通过 `IAsakiPoolable` 接口支持自定义对象生命周期管理，同时与 AsyncSystem 等其他模块的集成提供了更强大的功能。无论是简单的子弹池还是复杂的 UI 元素管理，SmartPool 都能提供高效可靠的解决方案。

通过遵循最佳实践和合理配置，开发者可以充分发挥 SmartPool 的优势，创建高性能、可维护的游戏应用。