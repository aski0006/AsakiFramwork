没有历史版本

## 1. 概述
- **核心职责**：提供异步的基于Slot的游戏存档/读档系统，支持二进制和JSON序列化，确保主线程安全和零GC优化。
- **设计哲学**：分离序列化与IO操作，主线程进行内存快照，后台线程处理文件IO；支持对象复用减少GC；提供调试友好的JSON格式和高效的二进制格式。
- **适用场景**：
  - 开放世界游戏的多存档系统（支持不同角色或进度槽位）
  - 回合制游戏的快速存档/读档功能
  - 在线游戏的本地缓存存档（防止网络中断丢失进度）
  - 游戏编辑器中的场景状态保存
  - 角色培养类游戏的多角色存档管理
- **依赖关系**：AsakiBroker（事件发布）、AsakiContext（依赖注入）、可选UniTask（异步线程切换）、Unity Engine（基础数据类型支持）。

## 2. 核心组件
### IAsakiSaveService
**职责**：提供基于槽位的存档管理核心API，包括保存、加载、删除和查询存档。
**生命周期**：通过AsakiSaveModule初始化，注册到AsakiContext中，在应用生命周期内持续可用。
**关键API**：
```csharp
// 异步保存指定槽位的存档数据
// 参数：
// - slotId：槽位ID，必须为非负整数
// - meta：存档元数据，必须实现IAsakiSlotMeta接口
// - data：存档数据，必须实现IAsakiSavable接口
// 返回值：异步任务
// 线程安全：是，内部自动处理主线程和后台线程切换
// 性能注意事项：数据序列化在主线程，文件IO在后台线程
Task SaveSlotAsync<TMeta, TData>(int slotId, TMeta meta, TData data)
    where TMeta : IAsakiSlotMeta where TData : IAsakiSavable;

// 异步加载指定槽位的存档数据
// 参数：
// - slotId：槽位ID，必须为非负整数
// 返回值：包含元数据和数据的元组
// 线程安全：是，内部自动处理后台线程和主线程切换
// 性能注意事项：文件IO在后台线程，反序列化在主线程
// 异常：当槽位不存在时抛出FileNotFoundException
Task<(TMeta Meta, TData Data)> LoadSlotAsync<TMeta, TData>(int slotId)
    where TMeta : IAsakiSlotMeta, new() where TData : IAsakiSavable, new();

// 获取所有已使用的槽位ID列表
// 返回值：已使用槽位ID的列表
// 线程安全：是，仅读取文件系统
// 性能注意事项：遍历保存目录，性能取决于已存在的存档数量
List<int> GetUsedSlots();

// 删除指定槽位的存档
// 参数：
// - slotId：槽位ID，必须为非负整数
// 返回值：删除成功返回true，否则返回false
// 线程安全：是，仅执行文件系统操作
bool DeleteSlot(int slotId);

// 检查指定槽位是否存在
// 参数：
// - slotId：槽位ID，必须为非负整数
// 返回值：存在返回true，否则返回false
// 线程安全：是，仅检查文件是否存在
bool SlotExists(int slotId);
```

### IAsakiSavable
**职责**：定义可序列化对象的契约，任何需要被保存的游戏对象都必须实现此接口。
**生命周期**：与实现类的生命周期一致，在序列化/反序列化时被调用。
**关键API**：
```csharp
// 将对象数据序列化到写入器
// 参数：
// - writer：用于写入数据的IAsakiWriter实例
// 线程安全：否，必须在主线程调用
// 性能注意事项：避免在Serialize中执行复杂计算
void Serialize(IAsakiWriter writer);

// 从读取器反序列化数据到对象
// 参数：
// - reader：用于读取数据的IAsakiReader实例
// 线程安全：否，必须在主线程调用
// 性能注意事项：避免在Deserialize中执行复杂计算
void Deserialize(IAsakiReader reader);
```

### IAsakiSlotMeta
**职责**：定义存档元数据的契约，包含存档的基本信息。
**生命周期**：在SaveSlotAsync中自动填充并持久化，在LoadSlotAsync中加载。
**关键属性**：
```csharp
// 存档槽位ID
int SlotId { get; set; }

// 最后保存时间（Unix时间戳，秒）
long LastSaveTime { get; set; }

// 存档名称
string SaveName { get; set; }
```

### IAsakiWriter
**职责**：定义序列化写入器的通用接口，支持基础类型、Unity数学类型、复杂对象和集合。
**生命周期**：临时创建，用于单次序列化操作。
**关键API**：
```csharp
// 写入版本号
void WriteVersion(int version);

// 写入基础类型
void WriteInt(string key, int value);
void WriteLong(string key, long value);
void WriteFloat(string key, float value);
void WriteDouble(string key, double value);
void WriteString(string key, string value);
void WriteBool(string key, bool value);
void WriteUInt(string key, uint value);
void WriteULong(string key, ulong value);

// 写入Unity数学类型
void WriteVector2Int(string key, Vector2Int value);
void WriteVector3Int(string key, Vector3Int value);
void WriteVector2(string key, Vector2 value);
void WriteVector3(string key, Vector3 value);
void WriteVector4(string key, Vector4 value);
void WriteQuaternion(string key, Quaternion value);
void WriteBounds(string key, Bounds value);

// 写入复杂对象
void WriteObject<T>(string key, T value) where T : IAsakiSavable;

// 集合控制
void BeginList(string key, int count);
void EndList();
void BeginObject(string key);
void EndObject();
```

### IAsakiReader
**职责**：定义反序列化读取器的通用接口，支持基础类型、Unity数学类型、复杂对象和集合。
**生命周期**：临时创建，用于单次反序列化操作。
**关键API**：
```csharp
// 读取版本号
int ReadVersion();

// 读取基础类型
int ReadInt(string key);
long ReadLong(string key);
float ReadFloat(string key);
double ReadDouble(string key);
string ReadString(string key);
bool ReadBool(string key);
uint ReadUInt(string key);
ulong ReadULong(string key);

// 读取Unity数学类型
Vector2Int ReadVector2Int(string key);
Vector3Int ReadVector3Int(string key);
Vector2 ReadVector2(string key);
Vector3 ReadVector3(string key);
Vector4 ReadVector4(string key);
Quaternion ReadQuaternion(string key);
Bounds ReadBounds(string key);

// 读取复杂对象
T ReadObject<T>(string key, T existingObj = default(T)) where T : IAsakiSavable, new();

// 集合控制
int BeginList(string key);
void EndList();
```

### AsakiBinaryWriter/AsakiBinaryReader
**职责**：高效的二进制序列化实现，特点是体积小、速度快，适合游戏运行时使用。
**生命周期**：临时创建，用于单次序列化/反序列化操作。
**设计特点**：
- 忽略Key参数，仅按顺序写入/读取数据
- 支持对象Null标记
- 支持对象复用，减少GC
- 直接打平Unity数学类型，无额外开销

### AsakiJsonWriter
**职责**：调试友好的JSON序列化实现，用于开发阶段查看存档内容。
**生命周期**：临时创建，用于单次序列化操作。
**设计特点**：
- 格式化输出，便于人工阅读
- 保留Key信息，支持键值对查看
- 仅在Debug模式下使用
- 支持Pretty Print格式

### AsakiSaveModule
**职责**：负责AsakiSaveService的初始化和注册。
**生命周期**：在应用启动时初始化，注册服务到AsakiContext。
**关键API**：
```csharp
// 模块初始化
void OnInit();

// 异步初始化
Task OnInitAsync();

// 模块销毁
void OnDispose();
```

## 3. 事件系统
### AsakiSaveBeginEvent
**触发时机**：当开始保存存档时。
**携带数据**：
```csharp
public class AsakiSaveBeginEvent
{
    public string Filename { get; set; } // 存档文件名，格式为"Slot_{slotId}"
}
```

### AsakiSaveSuccessEvent
**触发时机**：当存档保存成功时。
**携带数据**：
```csharp
public class AsakiSaveSuccessEvent
{
    public string Filename { get; set; } // 存档文件名，格式为"Slot_{slotId}"
}
```

### AsakiSaveFailedEvent
**触发时机**：当存档保存失败时。
**携带数据**：
```csharp
public class AsakiSaveFailedEvent
{
    public string Filename { get; set; } // 存档文件名，格式为"Slot_{slotId}"
    public string ErrorMessage { get; set; } // 错误信息
}
```

## 4. 存档路径结构
```
{Application.persistentDataPath}/Saves/
├── Slot_0/
│   ├── data.bin      # 二进制存档数据
│   └── meta.json     # JSON格式存档元数据（仅Debug模式）
├── Slot_1/
│   ├── data.bin
│   └── meta.json
└── ...
```

## 5. 使用示例
### 5.1 基础使用流程（手动实现IAsakiSavable）
```csharp
// 1. 定义存档数据类
public class GameSaveData : IAsakiSavable
{
    public int PlayerLevel { get; set; }
    public Vector3 PlayerPosition { get; set; }
    public List<string> Inventory { get; set; }
    
    public void Serialize(IAsakiWriter writer)
    {
        writer.WriteInt("level", PlayerLevel);
        writer.WriteVector3("position", PlayerPosition);
        
        writer.BeginList("inventory", Inventory.Count);
        foreach (var item in Inventory)
        {
            writer.WriteString(null, item);
        }
        writer.EndList();
    }
    
    public void Deserialize(IAsakiReader reader)
    {
        PlayerLevel = reader.ReadInt("level");
        PlayerPosition = reader.ReadVector3("position");
        
        int inventoryCount = reader.BeginList("inventory");
        Inventory = new List<string>(inventoryCount);
        for (int i = 0; i < inventoryCount; i++)
        {
            Inventory.Add(reader.ReadString(null));
        }
        reader.EndList();
    }
}

// 2. 定义存档元数据类
public class GameSaveMeta : IAsakiSlotMeta
{
    public int SlotId { get; set; }
    public long LastSaveTime { get; set; }
    public string SaveName { get; set; }
    
    // 自定义元数据字段
    public string CharacterName { get; set; }
    public int PlayTimeMinutes { get; set; }
    
    public void Serialize(IAsakiWriter writer)
    {
        writer.WriteInt("slotId", SlotId);
        writer.WriteLong("lastSaveTime", LastSaveTime);
        writer.WriteString("saveName", SaveName);
        writer.WriteString("characterName", CharacterName);
        writer.WriteInt("playTime", PlayTimeMinutes);
    }
    
    public void Deserialize(IAsakiReader reader)
    {
        SlotId = reader.ReadInt("slotId");
        LastSaveTime = reader.ReadLong("lastSaveTime");
        SaveName = reader.ReadString("saveName");
        CharacterName = reader.ReadString("characterName");
        PlayTimeMinutes = reader.ReadInt("playTime");
    }
}

// 3. 使用存档服务
public class SaveManager
{
    private IAsakiSaveService _saveService;
    
    public SaveManager()
    {
        _saveService = AsakiContext.Resolve<IAsakiSaveService>();
    }
    
    // 保存游戏
    public async Task SaveGameAsync(int slotId, string saveName, GameSaveData data)
    {
        var meta = new GameSaveMeta
        {
            SaveName = saveName,
            CharacterName = "Player1",
            PlayTimeMinutes = 120
        };
        
        await _saveService.SaveSlotAsync(slotId, meta, data);
    }
    
    // 加载游戏
    public async Task<(GameSaveMeta Meta, GameSaveData Data)> LoadGameAsync(int slotId)
    {
        return await _saveService.LoadSlotAsync<GameSaveMeta, GameSaveData>(slotId);
    }
    
    // 获取已使用的槽位
    public List<int> GetUsedSlots()
    {
        return _saveService.GetUsedSlots();
    }
}
```

### 5.2 自动生成序列化代码（基于Roslyn）
Asaki Framework提供了基于Roslyn的自动代码生成机制，可以通过属性标记自动生成IAsakiSavable的实现，减少重复性工作。

```csharp
using Asaki.Core;
using System.Collections.Generic;

[AsakiSave]
public partial class AsakiSaveExampleModel
{
    [AsakiSaveMember(Order = 1)] public int version;
    [AsakiSaveMember(Order = 2)] public List<string> tests;
}

// 基于Roslyn自动生成的代码（无需手动编写）
/*
using Asaki.Core.Serialization;
using UnityEngine;
using System.Collections.Generic;
using System;

partial class AsakiSaveExampleModel : IAsakiSavable
{
    public void Serialize(IAsakiWriter writer)
    {
        writer.BeginObject("AsakiSaveExampleModel");
        writer.WriteInt("version", this.version);
        writer.BeginList("tests", this.tests != null ? this.tests.Count : 0);
        if (this.tests != null)
        {
            foreach (var item in this.tests)
            {
                writer.WriteString("Item", item);
            }
        }
        writer.EndList();
        writer.EndObject();
    }

    public void Deserialize(IAsakiReader reader)
    {
        this.version = reader.ReadInt("version");
        int count_tests = reader.BeginList("tests");
        if (this.tests == null) this.tests = new System.Collections.Generic.List<string>(count_tests);
        this.tests.Clear();
        for (int i = 0; i < count_tests; i++)
        {
            this.tests.Add(reader.ReadString("Item"));
        }
        reader.EndList();
    }
}
*/
```

#### 属性说明

##### [AsakiSave]
**作用**：标记一个类需要自动生成IAsakiSavable实现
**适用范围**：类
**要求**：类必须是partial类

##### [AsakiSaveMember]
**作用**：标记类成员需要被序列化
**适用范围**：字段和属性
**参数**：
- `Order`：序列化顺序，必须为非负整数，默认按照成员声明顺序
- `Name`：序列化时使用的键名，默认使用成员名

#### 使用步骤

1. 确保类是`partial`类
2. 添加`[AsakiSave]`属性到类上
3. 为需要序列化的成员添加`[AsakiSaveMember]`属性
4. Roslyn编译器会在编译时自动生成IAsakiSavable的实现代码
5. 该类即可直接用于AsakiSaveService的保存和加载操作

#### 支持的类型

- 所有基本类型（int, long, float, double, bool, string等）
- 集合类型（List<T>, Dictionary<K, V>等）
- Unity数学类型（Vector2, Vector3, Quaternion等）
- 自定义类型（需同样标记[AsakiSave]）

#### 优势

- **减少样板代码**：无需手动编写Serialize和Deserialize方法
- **提高开发效率**：修改类结构后自动更新序列化代码
- **减少错误**：避免手动编写序列化代码时的拼写错误和顺序错误
- **保持代码整洁**：序列化逻辑与业务逻辑分离
- **支持增量更新**：仅修改需要序列化的成员即可

### 5.3 使用存档服务
```csharp
public class SaveManager
{
    private IAsakiSaveService _saveService;
    
    public SaveManager()
    {
        _saveService = AsakiContext.Resolve<IAsakiSaveService>();
    }
    
    // 保存游戏（使用手动实现的序列化类）
    public async Task SaveGameAsync(int slotId, string saveName, GameSaveData data)
    {
        var meta = new GameSaveMeta
        {
            SaveName = saveName,
            CharacterName = "Player1",
            PlayTimeMinutes = 120
        };
        
        await _saveService.SaveSlotAsync(slotId, meta, data);
    }
    
    // 保存游戏（使用自动生成的序列化类）
    public async Task SaveAutoGeneratedGameAsync(int slotId, AsakiSaveExampleModel data)
    {
        var meta = new GameSaveMeta
        {
            SaveName = "Auto Generated Save",
            CharacterName = "Player1",
            PlayTimeMinutes = 120
        };
        
        await _saveService.SaveSlotAsync(slotId, meta, data);
    }
    
    // 加载游戏（使用手动实现的序列化类）
    public async Task<(GameSaveMeta Meta, GameSaveData Data)> LoadGameAsync(int slotId)
    {
        return await _saveService.LoadSlotAsync<GameSaveMeta, GameSaveData>(slotId);
    }
    
    // 加载游戏（使用自动生成的序列化类）
    public async Task<(GameSaveMeta Meta, AsakiSaveExampleModel Data)> LoadAutoGeneratedGameAsync(int slotId)
    {
        return await _saveService.LoadSlotAsync<GameSaveMeta, AsakiSaveExampleModel>(slotId);
    }
    
    // 获取已使用的槽位
    public List<int> GetUsedSlots()
    {
        return _saveService.GetUsedSlots();
    }
}
```

## 6. 性能优化建议
1. **减少序列化数据量**：只保存必要的数据，避免保存临时状态或可计算的数据。
2. **合理使用对象复用**：在ReadObject时传入现有对象，减少GC。
3. **避免在Serialize/Deserialize中执行复杂计算**：这些方法在主线程执行，会阻塞游戏逻辑。
4. **批量操作**：多个存档操作尽量合并，减少IO开销。
5. **使用异步API**：所有存档操作都使用异步方法，避免主线程阻塞。

## 7. 最佳实践
1. **明确的存档结构设计**：在开发初期就规划好存档数据的结构，避免频繁修改。
2. **版本控制**：使用WriteVersion/ReadVersion方法管理存档版本，支持向下兼容。
3. **错误处理**：妥善处理SaveSlotAsync和LoadSlotAsync可能抛出的异常。
4. **测试覆盖**：确保存档/读档功能在各种情况下都能正常工作。
5. **Debug模式使用JSON**：在开发阶段利用JSON格式查看存档内容，便于调试。
6. **事件监听**：通过监听存档事件，实现UI反馈或其他逻辑。
7. **优先使用自动生成序列化代码**：对于大多数场景，推荐使用基于Roslyn的自动生成机制，减少样板代码和人为错误。
8. **合理使用Order参数**：为序列化成员设置明确的Order值，确保序列化顺序的稳定性，避免因成员声明顺序变化导致的存档不兼容。
9. **手动实现复杂序列化逻辑**：对于特殊的序列化需求（如自定义加密、压缩或复杂对象图），可以手动实现IAsakiSavable接口。
10. **保持partial类的简洁性**：在标记为[AsakiSave]的partial类中，仅包含需要序列化的数据成员和业务逻辑，避免将与序列化无关的代码放在同一类中。

## 8. 常见问题与解决方案
### Q: 存档操作导致主线程卡顿
**A**: 确保只在Serialize/Deserialize中执行必要的操作，避免复杂计算或大量数据处理。AsakiSaveService已经将IO操作放在后台线程，主线程只负责内存快照。

### Q: 存档文件过大
**A**: 检查是否保存了不必要的数据，如临时状态、可计算的数据或重复数据。考虑使用压缩算法（如LZ4）进一步减小文件体积。

### Q: 存档版本升级后无法加载旧存档
**A**: 在ReadVersion方法中处理不同版本的存档格式，支持向下兼容。可以通过条件判断或适配器模式处理不同版本的数据结构。

### Q: 存档过程中游戏崩溃导致存档损坏
**A**: 考虑实现备份机制，在保存前先备份旧存档，保存成功后再删除备份。

### Q: 移动端存档路径问题
**A**: AsakiSaveService使用Application.persistentDataPath，这是Unity推荐的跨平台存档路径，适用于所有平台。

## 9. 未来扩展方向
1. 支持加密存档，保护游戏数据安全
2. 支持云同步存档功能
3. 支持增量存档，减少重复数据写入
4. 支持多平台存档格式兼容
5. 提供存档编辑器工具
6. 支持存档压缩，进一步减小文件体积

## 10. 总结
Asaki Framework的Serialization-SaveSystem模块提供了一套高效、灵活、易用的游戏存档解决方案。其核心设计理念是分离关注点，确保主线程安全，同时支持高效的二进制格式和调试友好的JSON格式。通过实现IAsakiSavable接口，任何游戏对象都可以轻松支持存档功能。该模块适用于各种类型的游戏开发场景，从简单的单机游戏到复杂的开放世界游戏，都能提供可靠的存档支持。