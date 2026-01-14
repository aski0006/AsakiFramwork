// Asaki/Core/Blackboard/Variables/AsakiTypeBridge.cs

using Asaki.Core.Logging;
using System;
using System.Collections.Generic;

namespace Asaki.Core.Blackboard.Variables
{
	/// <summary>
	/// 提供类型桥接功能，用于在 <see cref="IAsakiBlackboard"/> 中设置不同类型的值。
	/// 它通过预定义的逻辑和可注册的委托来处理不同类型值的设置操作，
	/// 旨在简化在黑板系统中对各种类型数据的存储和检索过程。
	/// </summary>
	public static class AsakiTypeBridge
	{

		/// <summary>
		/// 存储所有设置值的尝试逻辑的列表。
		/// 每个 <see cref="Action"/> 接受 <see cref="IAsakiBlackboard"/>、键和值作为参数，
		/// 用于尝试在黑板中设置对应的值。
		/// </summary>
		private static readonly List<Action<IAsakiBlackboard, string, object>> _setters = new List<Action<IAsakiBlackboard, string, object>>();

		/// <summary>
		/// 存储所有设置值的尝试逻辑的列表。从名称到委托的映射中查找。
		/// 每个 <see cref="Action"/> 接受 <see cref="IAsakiBlackboard"/>、键和值作为参数，
		/// 用于尝试在黑板中设置对应的值。
		/// </summary>
		private static readonly Dictionary<string, Action<IAsakiBlackboard, string, object>> _nameLookup
			= new Dictionary<string, Action<IAsakiBlackboard, string, object>>();
		
		/// <summary>
		/// 在 <see cref="IAsakiBlackboard"/> 中设置值的核心入口方法。
		/// 此方法首先尝试通过硬编码的基本类型路径快速设置值，
		/// 如果失败，则遍历所有注册的设置器来处理该值。
		/// </summary>
		/// <param name="bb">要设置值的 <see cref="IAsakiBlackboard"/> 实例。</param>
		/// <param name="key">要设置值的键。</param>
		/// <param name="value">要设置的值。</param>
		public static void SetValue(IAsakiBlackboard bb, string key, object value)
		{
			// 1. Fast Path (Hardcoded Primitives)
			switch (value)
			{
				case int v:    bb.SetValue(key, v); return;
				case float v:  bb.SetValue(key, v); return;
				case bool v:   bb.SetValue(key, v); return;
				case string v: bb.SetValue(key, v); return;

			}

			Type type = value.GetType();
			if (_typeLookup.TryGetValue(type, out var setter))
			{
				setter(bb, key, value);
				return;
			}
			ALog.Warn($"[Asaki Blackboard] Unknown type: {type.Name}");
		}

		public static bool TrySetValue(IAsakiBlackboard bb, string key, string typeName, object value)
		{
			if (!_nameLookup.TryGetValue(typeName, out var setter)) return false;
			setter(bb, key, value);
			return true;
		}
		
		// 注册表
		/// <summary>
		/// 用于存储类型与对应设置值逻辑的字典。
		/// 键为类型，值为接受 <see cref="IAsakiBlackboard"/>、键和值作为参数的 <see cref="Action"/>，
		/// 用于在黑板中设置对应类型的值。
		/// </summary>
		private static readonly Dictionary<Type, Action<IAsakiBlackboard, string, object>> _typeLookup
			= new Dictionary<Type, Action<IAsakiBlackboard, string, object>>();

		/// <summary>
		/// 注册一个类型的设置逻辑。
		/// 此方法为生成代码提供的 API，用于注册特定类型的设置器。
		/// 每个类型的设置器是一个强类型的闭包委托，它将值转换为指定类型并在 <see cref="IAsakiBlackboard"/> 中设置。
		/// </summary>
		/// <typeparam name="T">要注册的类型。</typeparam>
		public static void Register<T>()
		{
			Type t = typeof(T);
            
			// 构造强类型闭包，这里通过泛型 T 生成了最高效的调用代码
			Action<IAsakiBlackboard, string, object> action = (bb, key, value) =>
			{
				// 这里的 (T)value 是显式强转，速度极快
				bb.SetValue(key, (T)value);
			};

			// 注册 Type 索引
			_typeLookup.TryAdd(t, action);

			
			string name = t.FullName; 
			if (!string.IsNullOrEmpty(name))
			{
				_nameLookup.TryAdd(name, action);
			}
		}
	}
}
