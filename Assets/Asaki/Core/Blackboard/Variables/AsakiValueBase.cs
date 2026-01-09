using Asaki.Core.Logging;
using System;

namespace Asaki.Core.Blackboard.Variables
{
	/// <summary>
	/// 作为所有黑板值类型的抽象基类。
	/// 定义了在黑板系统中处理值的通用接口，包括获取类型名称、应用值到黑板以及克隆自身的抽象方法。
	/// </summary>
	[Serializable]
	public abstract class AsakiValueBase
	{
		/// <summary>
		/// 用于创建此类型实例的工厂委托
		/// </summary>
		protected Func<AsakiValueBase> Factory { get; }

		/// <summary>
		/// 获取值的类型名称。
		/// 此抽象属性应返回表示值类型的字符串名称。
		/// </summary>
		/// <returns>值的类型名称。</returns>
		public abstract string TypeName { get; }

		/// <summary>
		/// 将值应用到指定的黑板实例。
		/// </summary>
		/// <param name="blackboard">要应用值的 <see cref="IAsakiBlackboard"/> 实例。</param>
		/// <param name="key">用于在黑板中存储值的键。</param>
		public abstract void ApplyTo(IAsakiBlackboard blackboard, string key);

		/// <summary>
		/// 创建自身的克隆。
		/// </summary>
		/// <returns>当前值对象的克隆。</returns>
		public abstract AsakiValueBase Clone();

		/// <summary>
		/// 构造函数，用于初始化 <see cref="AsakiValueBase"/> 的实例。
		/// </summary>
		/// <param name="factory">工厂委托，用于创建当前类型的实例。</param>
		protected AsakiValueBase(Func<AsakiValueBase> factory = null)
		{
			Factory = factory;
		}
	}

	/// <summary>
	/// 泛型版本的黑板值类，继承自 <see cref="AsakiValueBase"/>。
	/// 提供了存储特定类型值的功能，并实现了基类定义的抽象方法。
	/// </summary>
	/// <typeparam name="T">值的类型。</typeparam>
	[Serializable]
	public abstract class AsakiValue<T> : AsakiValueBase
	{
		/// <summary>
		/// 构造函数，用于初始化 <see cref="AsakiValue{T}"/> 的实例。
		/// </summary>
		/// <param name="factory"></param>
		protected AsakiValue(Func<AsakiValue<T>> factory = null) : base(
			factory != null ? new Func<AsakiValueBase>(() => factory()) : null) { }

		/// <summary>
		/// 存储的核心数据。
		/// 此属性保存了具体的泛型类型值。
		/// </summary>
		public T Value;

		/// <summary>
		/// 获取值的类型名称。
		/// 此属性返回泛型类型 <typeparamref name="T"/> 的名称。
		/// </summary>
		/// <returns>泛型类型 <typeparamref name="T"/> 的名称。</returns>
		public override string TypeName => typeof(T).Name;

		/// <summary>
		/// 将值应用到指定的黑板实例。
		/// 根据泛型类型 <typeparamref name="T"/>，调用 <see cref="IAsakiBlackboard"/> 的相应设置方法，
		/// 将值存储到黑板中指定的键下。
		/// </summary>
		/// <param name="blackboard">要应用值的 <see cref="IAsakiBlackboard"/> 实例。</param>
		/// <param name="key">用于在黑板中存储值的键。</param>
		public override void ApplyTo(IAsakiBlackboard blackboard, string key)
		{
			blackboard?.SetValue(key, Value);
		}

		/// <summary>
		/// 创建自身的克隆。
		/// 使用 <see cref="Activator.CreateInstance(Type)"/> 创建一个新的当前类型实例，
		/// 并将当前的 <see cref="Value"/> 复制到新实例中。
		/// </summary>
		/// <returns>当前值对象的克隆。</returns>
		public override AsakiValueBase Clone()
		{
			AsakiValue<T> instance;
			if (Factory != null)
			{
				instance = Factory() as AsakiValue<T>;
			}
			else
			{
				instance = Activator.CreateInstance(GetType()) as AsakiValue<T>;
			}
			if (instance != null)
			{
				instance.Value = Value;
				return instance;
			}
            
			ALog.Warn("Failed to clone AsakiValue");
			return null;
		}
	}
}
