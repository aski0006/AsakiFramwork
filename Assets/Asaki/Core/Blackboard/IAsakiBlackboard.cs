using Asaki.Core.Context;
using System;
using Asaki.Core.MVVM;

namespace Asaki.Core.Blackboard
{
	public interface IAsakiBlackboard : IAsakiService, IDisposable
	{
		// 作用域链
		IAsakiBlackboard Parent { get; }

		// 基础存取 (支持 Shadowing)
		void SetValue<T>(AsakiBlackboardKey key, T value);
		T GetValue<T>(AsakiBlackboardKey key, T defaultValue = default(T));

		// 响应式绑定 (支持 Copy-On-Access)
		AsakiProperty<T> GetProperty<T>(AsakiBlackboardKey key);

		// 元数据查询
		bool Contains(AsakiBlackboardKey key);

		// [New] 获取键的注册类型，用于安全检查或调试
		Type GetKeyType(AsakiBlackboardKey key);
	}
}
