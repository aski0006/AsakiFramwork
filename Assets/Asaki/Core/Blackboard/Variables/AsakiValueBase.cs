using System;

namespace Asaki.Core.Blackboard.Variables
{
	[Serializable]
	public abstract class AsakiValueBase
	{
		public abstract string TypeName { get; }
		public abstract void ApplyTo(IAsakiBlackboard blackboard, string key);
		public abstract AsakiValueBase Clone();
	}
	
	[Serializable]
	public abstract class AsakiValue<T> : AsakiValueBase
	{
		// 核心数据
		public T Value;

		public override string TypeName => typeof(T).Name;

		public override void ApplyTo(IAsakiBlackboard blackboard, string key)
		{
			// 这里调用的是泛型方法，编译器会生成具体的 Call
			// 如果 T 是 int，就会调用 blackboard.SetValue<int>，走 int 专用桶
			// 如果 T 是自定义结构，就会走 generic 桶
			blackboard.SetValue<T>(key, Value);
		}
        
		public override AsakiValueBase Clone()
		{
			// 创建新实例并复制值
			var instance = Activator.CreateInstance(GetType()) as AsakiValue<T>;
			instance.Value = this.Value;
			return instance;
		}
	}
}
