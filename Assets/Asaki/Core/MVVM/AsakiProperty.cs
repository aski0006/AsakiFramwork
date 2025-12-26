using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Asaki.Core.MVVM
{
	[Serializable]
	public class AsakiProperty<T> : IEquatable<AsakiProperty<T>>
	{
		[EditorBrowsable(EditorBrowsableState.Never)]
		public T _value;

		[NonSerialized]
		private Action<T> _onValueChangedAction;

		[NonSerialized]
		private readonly List<IAsakiObserver<T>> _observers = new List<IAsakiObserver<T>>();

		public AsakiProperty()
		{
			_value = default(T);
		}
		public AsakiProperty(T initialValue = default(T))
		{
			_value = initialValue;
		}

		public T Value
		{
			get => _value;
			set
			{
				if (EqualityComparer<T>.Default.Equals(_value, value)) return;
				_value = value;
				Notify();
			}
		}

		public void Subscribe(Action<T> action)
		{
			_onValueChangedAction += action;
			action?.Invoke(_value);
		}

		public void Unsubscribe(Action<T> action)
		{
			_onValueChangedAction -= action;
		}

		public void Bind(IAsakiObserver<T> observer)
		{
			if (_observers.Contains(observer)) return;
			_observers.Add(observer);
			observer.OnValueChange(_value);
		}

		public void Unbind(IAsakiObserver<T> observer)
		{
			_observers.Remove(observer);
		}

		private void Notify()
		{
			_onValueChangedAction?.Invoke(_value);
			for (int i = _observers.Count - 1; i >= 0; i--)
			{
				_observers[i].OnValueChange(_value);
			}
		}

		// ========================================================================
		// 相等性实现
		// ========================================================================

		public override int GetHashCode()
		{
			// 抛出异常，明确禁止将其用作字典键
			throw new NotSupportedException($"{nameof(AsakiProperty<T>)} should not be used as a dictionary key due to its mutable nature.");
		}

		/// <summary>
		/// 实现 IEquatable<T> 接口以获得更好的类型安全性和性能
		/// </summary>
		public bool Equals(AsakiProperty<T> other)
		{
			if (ReferenceEquals(null, other)) return false;
			if (ReferenceEquals(this, other)) return true;
			return EqualityComparer<T>.Default.Equals(_value, other._value);
		}

		/// <summary>
		/// 重写 Equals 方法，保持与运算符重载一致
		/// </summary>
		public override bool Equals(object obj)
		{
			return obj switch
			       {
				       // 相同类型，使用类型安全的 Equals 方法
				       AsakiProperty<T> other => Equals(other),
				       // T 类型，直接比较值
				       T val => EqualityComparer<T>.Default.Equals(_value, val),
				       // 其他类型不相等
				       _ => false,
			       };
		}

		// ========================================================================
		// 运算符重载
		// ========================================================================

		/// <summary>
		/// AsakiProperty<T> 与 AsakiProperty<T> 的相等比较
		/// </summary>
		public static bool operator ==(AsakiProperty<T> left, AsakiProperty<T> right)
		{
			// 处理两个都为 null 的情况
			if (ReferenceEquals(left, right)) return true;

			// 处理其中一个为 null 的情况
			if (ReferenceEquals(left, null) || ReferenceEquals(right, null)) return false;

			return EqualityComparer<T>.Default.Equals(left._value, right._value);
		}

		public static bool operator !=(AsakiProperty<T> left, AsakiProperty<T> right)
		{
			return !(left == right);
		}

		/// <summary>
		/// T 与 AsakiProperty<T> 的相等比较（T 在左）
		/// </summary>
		public static bool operator ==(T left, AsakiProperty<T> right)
		{
			// 如果 right 为 null，只有 left 为 null 时才相等（对于引用类型）
			if (ReferenceEquals(right, null))
				return EqualityComparer<T>.Default.Equals(left, default(T));

			return EqualityComparer<T>.Default.Equals(left, right._value);
		}

		public static bool operator !=(T left, AsakiProperty<T> right)
		{
			return !(left == right);
		}

		/// <summary>
		/// AsakiProperty<T> 与 T 的相等比较（AsakiProperty<T> 在左）
		/// </summary>
		public static bool operator ==(AsakiProperty<T> left, T right)
		{
			// 如果 left 为 null，只有 right 为 null 时才相等（对于引用类型）
			if (ReferenceEquals(left, null))
				return EqualityComparer<T>.Default.Equals(default(T), right);

			return EqualityComparer<T>.Default.Equals(left._value, right);
		}

		public static bool operator !=(AsakiProperty<T> left, T right)
		{
			return !(left == right);
		}

		// ========================================================================
		// 其他方法
		// ========================================================================

		public override string ToString()
		{
			return _value?.ToString() ?? "null";
		}

		public static implicit operator T(AsakiProperty<T> property)
		{
			if (property == null) return default(T);
			return property._value;
		}
	}
}
