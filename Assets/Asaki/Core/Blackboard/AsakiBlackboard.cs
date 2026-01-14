using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using Asaki.Core.MVVM;

namespace Asaki.Core.Blackboard
{
    /// <summary>
    /// 实现 <see cref="IAsakiBlackboard"/> 接口的具体黑板类。
    /// 用于在应用程序中管理和存储各种类型的数据，通过分桶的方式提高存储和检索效率，并确保数据类型的安全性。
    /// </summary>
    public sealed class AsakiBlackboard : IAsakiBlackboard
    {
        /// <summary>
        /// 获取或设置此黑板的父级黑板。
        /// 用于构建黑板的层级结构，实现数据的继承和共享。
        /// </summary>
        public IAsakiBlackboard Parent { get; private set; }

        // [Core Constraint 2] 独立类型注册表
        // 任何 Key 在首次写入时必须“登记户口”。后续访问必须查验类型，防止静默失败。
        /// <summary>
        /// 用于存储键与对应数据类型的字典。
        /// 在首次写入数据时，会将键和对应的数据类型登记在此字典中，
        /// 后续访问时会根据此字典查验类型，以确保类型安全。
        /// </summary>
        private readonly Dictionary<int, Type> _typeRegistry = new Dictionary<int, Type>();

        // --- 物理分桶 (Lazy Initialization) ---
        // 只有当真正存储数据时才分配内存
        private Dictionary<int, AsakiProperty<int>> _intBucket;
        /// <summary>
        /// 获取用于存储整数类型数据的字典。
        /// 采用延迟初始化，只有在首次存储整数数据时才会分配内存。
        /// </summary>
        private Dictionary<int, AsakiProperty<int>> IntBucket => _intBucket ??= new Dictionary<int, AsakiProperty<int>>();

        private Dictionary<int, AsakiProperty<float>> _floatBucket;
        /// <summary>
        /// 获取用于存储单精度浮点数类型数据的字典。
        /// 采用延迟初始化，只有在首次存储浮点数数据时才会分配内存。
        /// </summary>
        private Dictionary<int, AsakiProperty<float>> FloatBucket => _floatBucket ??= new Dictionary<int, AsakiProperty<float>>();

        private Dictionary<int, AsakiProperty<bool>> _boolBucket;
        /// <summary>
        /// 获取用于存储布尔类型数据的字典。
        /// 采用延迟初始化，只有在首次存储布尔数据时才会分配内存。
        /// </summary>
        private Dictionary<int, AsakiProperty<bool>> BoolBucket => _boolBucket ??= new Dictionary<int, AsakiProperty<bool>>();

        private Dictionary<int, AsakiProperty<string>> _stringBucket;
        /// <summary>
        /// 获取用于存储字符串类型数据的字典。
        /// 采用延迟初始化，只有在首次存储字符串数据时才会分配内存。
        /// </summary>
        private Dictionary<int, AsakiProperty<string>> StringBucket => _stringBucket ??= new Dictionary<int, AsakiProperty<string>>();

        private Dictionary<int, AsakiProperty<Vector3>> _vector3Bucket;
        /// <summary>
        /// 获取用于存储三维向量类型数据的字典。
        /// 采用延迟初始化，只有在首次存储三维向量数据时才会分配内存。
        /// </summary>
        private Dictionary<int, AsakiProperty<Vector3>> Vector3Bucket => _vector3Bucket ??= new Dictionary<int, AsakiProperty<Vector3>>();

        private Dictionary<int, object> _genericBucket;
        /// <summary>
        /// 获取用于存储其他类型数据的通用字典。
        /// 采用延迟初始化，只有在首次存储其他类型数据时才会分配内存。
        /// </summary>
        private Dictionary<int, object> GenericBucket => _genericBucket ??= new Dictionary<int, object>();

        // --- 构造函数 (环路检测) ---
        /// <summary>
        /// 使用指定的父级黑板初始化 <see cref="AsakiBlackboard"/> 实例。
        /// 构造函数会检测是否存在循环依赖，如果存在则抛出异常。同时，会限制黑板层级深度，防止深度过大。
        /// </summary>
        /// <param name="parent">此黑板的父级黑板，默认为 null。</param>
        public AsakiBlackboard(IAsakiBlackboard parent = null)
        {
            if (parent != null)
            {
                IAsakiBlackboard p = parent;
                int depth = 0;
                while (p != null)
                {
                    if (p == this) throw new InvalidOperationException("[AsakiBlackboard] Circular dependency detected!");
                    p = p.Parent;
                    if (++depth > 32) throw new InvalidOperationException("[AsakiBlackboard] Scope depth exceeds limit (32).");
                }
            }
            Parent = parent;
        }

        // ==========================================================
        // 核心安全逻辑 (Type Safety)
        // ==========================================================

        /// <summary>
        /// 验证或注册指定键的类型。
        /// 如果键已在类型注册表中，验证其类型是否与请求的类型匹配；如果键不存在，则为写入操作准备注册新类型。
        /// </summary>
        /// <typeparam name="T">请求的数据类型。</typeparam>
        /// <param name="key">要验证或注册类型的黑板键。</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ValidateOrRegisterType<T>(AsakiBlackboardKey key)
        {
            int hash = key.Hash;
            Type reqType = typeof(T);

            if (_typeRegistry.TryGetValue(hash, out Type regType))
            {
                // 已存在：必须匹配
                if (regType != reqType)
                {
                    throw new InvalidCastException(
                        $"[AsakiBlackboard] Type Mismatch for key '{key}'! Registered: {regType.Name}, Requested: {reqType.Name}");
                }
            }
            else
            {
                // 未存在：注册新户口 (如果是只读操作且父级也没有，这一步其实不会触发，在 GetLocalPropertyOrCreate 中触发)
                // 这里我们稍微放宽，让具体的 CreateLocal 去注册，避免 Read 操作污染 Registry
            }
        }

        /// <summary>
        /// 获取指定键的数据类型。
        /// 首先在本地类型注册表中查找，如果未找到，则在父级黑板中查找。
        /// </summary>
        /// <param name="key">要获取数据类型的黑板键。</param>
        /// <returns>键对应的数据类型，如果未找到则返回 null。</returns>
        public Type GetKeyType(AsakiBlackboardKey key)
        {
            if (_typeRegistry.TryGetValue(key.Hash, out Type t)) return t;
            return Parent?.GetKeyType(key);
        }

        // ==========================================================
        // 读写 API
        // ==========================================================

        /// <summary>
        /// 在黑板中设置指定键的值。
        /// 确保写入本地的值类型匹配（或注册新类型），并将值存储到相应的分桶中。
        /// </summary>
        /// <typeparam name="T">要设置的值的类型。</typeparam>
        /// <param name="key">要设置值的黑板键。</param>
        /// <param name="value">要设置的值。</param>
        public void SetValue<T>(AsakiBlackboardKey key, T value)
        {
            // 写入本地：必须确保类型匹配（或注册新类型）
            GetLocalPropertyOrCreate<T>(key).Value = value;
        }

        /// <summary>
        /// 从黑板中获取指定键的值。
        /// 首先在本地查找，如果未找到，则在父级黑板中查找。如果都未找到，则返回默认值。
        /// </summary>
        /// <typeparam name="T">要获取的值的类型。</typeparam>
        /// <param name="key">要获取值的黑板键。</param>
        /// <param name="defaultValue">如果未找到值时返回的默认值，默认为 default(T)。</param>
        /// <returns>键对应的值，如果未找到则返回默认值。</returns>
        public T GetValue<T>(AsakiBlackboardKey key, T defaultValue = default(T))
        {
            // 1. 查本地
            if (TryGetLocalProperty(key, out AsakiProperty<T> localProp))
            {
                return localProp.Value;
            }

            // 2. 查父级
            if (Parent != null)
            {
                // 注意：父级也必须符合 T 类型，由父级的 Validate 逻辑保证
                return Parent.GetValue(key, defaultValue);
            }

            return defaultValue;
        }

        /// <summary>
        /// 获取指定键的属性对象。
        /// 采用 Copy-On-Access (Strict Shadowing) 策略，如果本地已有该属性，则直接返回；
        /// 如果本地没有，则尝试从父级获取初始值并创建本地副本，切断与父级的引用关联。
        /// </summary>
        /// <typeparam name="T">属性值的类型。</typeparam>
        /// <param name="key">要获取属性的黑板键。</param>
        /// <returns>指定键的属性对象。</returns>
        public AsakiProperty<T> GetProperty<T>(AsakiBlackboardKey key)
        {
            // 策略：Copy-On-Access (Strict Shadowing)

            // 1. 如果本地已有，直接返回
            if (TryGetLocalProperty(key, out AsakiProperty<T> localProp))
            {
                return localProp;
            }

            // 2. 如果本地没有，准备创建本地副本
            // 先尝试从父级获取当前值作为初始值
            T initialValue = default(T);
            if (Parent != null && Parent.Contains(key))
            {
                initialValue = Parent.GetValue<T>(key);
            }

            // 3. 创建本地属性 (此时会注册类型)，切断与父级的引用关联
            var newProp = CreateLocalProperty<T>(key);
            newProp.Value = initialValue;

            return newProp;
        }

        /// <summary>
        /// 检查黑板中是否包含指定的键。
        /// 首先在本地检查，如果未找到，则在父级黑板中检查。
        /// </summary>
        /// <param name="key">要检查的黑板键。</param>
        /// <returns>如果黑板中包含该键，则返回 true；否则返回 false。</returns>
        public bool Contains(AsakiBlackboardKey key)
        {
            if (_typeRegistry.ContainsKey(key.Hash)) return true;
            return Parent != null && Parent.Contains(key);
        }

        // ==========================================================
        // 内部存储逻辑
        // ==========================================================

        /// <summary>
        /// 获取本地属性，如果不存在则创建。
        /// </summary>
        /// <typeparam name="T">属性值的类型。</typeparam>
        /// <param name="key">要获取或创建属性的黑板键。</param>
        /// <returns>指定键的属性对象。</returns>
        private AsakiProperty<T> GetLocalPropertyOrCreate<T>(AsakiBlackboardKey key)
        {
            if (TryGetLocalProperty(key, out AsakiProperty<T> prop))
            {
                return prop;
            }
            return CreateLocalProperty<T>(key);
        }

        /// <summary>
        /// 创建本地属性并注册其类型。
        /// 将创建的属性存储到相应的分桶中，并在类型注册表中登记键和属性类型。
        /// </summary>
        /// <typeparam name="T">属性值的类型。</typeparam>
        /// <param name="key">要创建属性的黑板键。</param>
        /// <returns>新创建的属性对象。</returns>
        private AsakiProperty<T> CreateLocalProperty<T>(AsakiBlackboardKey key)
        {
            ValidateOrRegisterType<T>(key);
            _typeRegistry[key.Hash] = typeof(T);

            var prop = new AsakiProperty<T>();
            int hash = key.Hash;
            Type t = typeof(T);

            // JIT 会优化掉不走的分支，不会因为 if-else 多而变慢
            if (t == typeof(int))
            {
                // [Safe Cast]
                // 1. (object)prop: 引用类型转 object，零 GC。
                // 2. (AsakiProperty<int>): 显式转换，类型不匹配会抛异常（安全网）。
                IntBucket[hash] = (AsakiProperty<int>)(object)prop;
            }
            else if (t == typeof(float)) FloatBucket[hash] = (AsakiProperty<float>)(object)prop;
            else if (t == typeof(bool)) BoolBucket[hash] = (AsakiProperty<bool>)(object)prop;
            else if (t == typeof(string)) StringBucket[hash] = (AsakiProperty<string>)(object)prop;
            else if (t == typeof(Vector3)) Vector3Bucket[hash] = (AsakiProperty<Vector3>)(object)prop;
            else GenericBucket[hash] = prop;

            return prop;
        }

        /// <summary>
        /// 尝试获取本地属性。
        /// 检查类型注册表中键对应的类型是否与请求的类型匹配，然后从相应的分桶中获取属性。
        /// </summary>
        /// <typeparam name="T">属性值的类型。</typeparam>
        /// <param name="key">要获取属性的黑板键。</param>
        /// <param name="property">获取到的属性对象，如果未找到则为 null。</param>
        /// <returns>如果成功获取到属性，则返回 true；否则返回 false。</returns>
        private bool TryGetLocalProperty<T>(AsakiBlackboardKey key, out AsakiProperty<T> property)
        {
            property = null;
            int hash = key.Hash;

            // 安全检查
            if (_typeRegistry.TryGetValue(hash, out Type regType))
            {
                if (regType != typeof(T))
                {
                    // 这里不需要 Unsafe 也不需要 Assert，直接抛异常，因为这是逻辑错误
                    throw new InvalidCastException($"[AsakiBlackboard] Type Mismatch! Key: {key}, Registered: {regType}, Requested: {typeof(T)}");
                }
            }
            else
            {
                return false;
            }

            Type t = typeof(T);

            // 使用标准转换替代 UnsafeCast
            if (t == typeof(int))
            {
                if (_intBucket != null && _intBucket.TryGetValue(hash, out var p))
                {
                    // 先转 object 再转 T。因为外面包了 if (t == typeof(int))，这里 T 就是 int
                    // 编译器能推断出 T 是 AsakiProperty<int> 的等价类型
                    property = (AsakiProperty<T>)(object)p;
                    return true;
                }
            }
            else if (t == typeof(float))
            {
                if (_floatBucket != null && _floatBucket.TryGetValue(hash, out var p))
                {
                    property = (AsakiProperty<T>)(object)p;
                    return true;
                }
            }
            else if (t == typeof(bool))
            {
                if (_boolBucket != null && _boolBucket.TryGetValue(hash, out var p))
                {
                    property = (AsakiProperty<T>)(object)p;
                    return true;
                }
            }
            else if (t == typeof(string))
            {
                if (_stringBucket != null && _stringBucket.TryGetValue(hash, out var p))
                {
                    property = (AsakiProperty<T>)(object)p;
                    return true;
                }
            }
            else if (t == typeof(Vector3))
            {
                if (_vector3Bucket != null && _vector3Bucket.TryGetValue(hash, out var p))
                {
                    property = (AsakiProperty<T>)(object)p;
                    return true;
                }
            }
            else
            {
                if (_genericBucket != null && _genericBucket.TryGetValue(hash, out object p))
                {
                    property = (AsakiProperty<T>)p; // 泛型桶本身存的就是 object，直接强转
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 清理黑板的本地数据，断开引用。
        /// 清除类型注册表和所有分桶中的数据，并将父级黑板设置为 null。
        /// </summary>
        public void Dispose()
        {
            _typeRegistry.Clear();
            _intBucket?.Clear();
            _floatBucket?.Clear();
            _boolBucket?.Clear();
            _stringBucket?.Clear();
            _vector3Bucket?.Clear();
            _genericBucket?.Clear();
            Parent = null;
        }
    }
}