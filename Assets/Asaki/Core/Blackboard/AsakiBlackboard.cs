using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;
using Asaki.Core.MVVM;

namespace Asaki.Core.Blackboard
{
	public sealed class AsakiBlackboard : IAsakiBlackboard
	{
		public IAsakiBlackboard Parent { get; private set; }

		// [Core Constraint 2] 独立类型注册表
		// 任何 Key 在首次写入时必须“登记户口”。后续访问必须查验类型，防止静默失败。
		private readonly Dictionary<int, Type> _typeRegistry = new Dictionary<int, Type>();

		// --- 物理分桶 (Lazy Initialization) ---
		// 只有当真正存储数据时才分配内存
		private Dictionary<int, AsakiProperty<int>> _intBucket;
		private Dictionary<int, AsakiProperty<int>> IntBucket => _intBucket ??= new Dictionary<int, AsakiProperty<int>>();

		private Dictionary<int, AsakiProperty<float>> _floatBucket;
		private Dictionary<int, AsakiProperty<float>> FloatBucket => _floatBucket ??= new Dictionary<int, AsakiProperty<float>>();

		private Dictionary<int, AsakiProperty<bool>> _boolBucket;
		private Dictionary<int, AsakiProperty<bool>> BoolBucket => _boolBucket ??= new Dictionary<int, AsakiProperty<bool>>();

		private Dictionary<int, AsakiProperty<string>> _stringBucket;
		private Dictionary<int, AsakiProperty<string>> StringBucket => _stringBucket ??= new Dictionary<int, AsakiProperty<string>>();

		private Dictionary<int, AsakiProperty<Vector3>> _vector3Bucket;
		private Dictionary<int, AsakiProperty<Vector3>> Vector3Bucket => _vector3Bucket ??= new Dictionary<int, AsakiProperty<Vector3>>();

		private Dictionary<int, object> _genericBucket;
		private Dictionary<int, object> GenericBucket => _genericBucket ??= new Dictionary<int, object>();

		// --- 构造函数 (环路检测) ---
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

		public Type GetKeyType(AsakiBlackboardKey key)
		{
			if (_typeRegistry.TryGetValue(key.Hash, out Type t)) return t;
			return Parent?.GetKeyType(key);
		}

		// ==========================================================
		// 读写 API
		// ==========================================================

		public void SetValue<T>(AsakiBlackboardKey key, T value)
		{
			// 写入本地：必须确保类型匹配（或注册新类型）
			GetLocalPropertyOrCreate<T>(key).Value = value;
		}

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

		public bool Contains(AsakiBlackboardKey key)
		{
			if (_typeRegistry.ContainsKey(key.Hash)) return true;
			return Parent != null && Parent.Contains(key);
		}

		// ==========================================================
		// 内部存储逻辑
		// ==========================================================

		private AsakiProperty<T> GetLocalPropertyOrCreate<T>(AsakiBlackboardKey key)
		{
			if (TryGetLocalProperty(key, out AsakiProperty<T> prop))
			{
				return prop;
			}
			return CreateLocalProperty<T>(key);
		}

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

		public void Dispose()
		{
			// 清理本地数据，断开引用
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
