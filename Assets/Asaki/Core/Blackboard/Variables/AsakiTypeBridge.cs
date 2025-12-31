// Asaki/Core/Blackboard/Variables/AsakiTypeBridge.cs

using System;
using System.Collections.Generic;

namespace Asaki.Core.Blackboard.Variables
{
    public static class AsakiTypeBridge
    {
        // 这是一个链式委托（或列表），每个注册者都会往里加自己的检查逻辑
        // Action<IBlackboard, key, value> 是执行 Set 的动作
        // Func<object, bool> 是检查 "这个 value 是不是我这个类型"
        
        // 简化版：直接用一个 List<Action> 存储所有的 Set 尝试逻辑
        private static readonly List<Action<IAsakiBlackboard, string, object>> _setters = new List<Action<IAsakiBlackboard, string, object>>();

        // 核心入口
        public static void SetValue(IAsakiBlackboard bb, string key, object value)
        {
            // 1. Fast Path (Hardcoded Primitives)
            switch (value)
            {
                case int v: bb.SetValue(key, v); return;
                case float v: bb.SetValue(key, v); return;
              	
            }

            // 2. Dynamic Path (User Types)
            // 遍历所有注册进来的 Setter，看看谁能处理这个 value
            // 注意：这种遍历比 switch 慢，但比反射快。为了优化，可以后续引入 Dictionary<Type, Action>
            
            // 优化版：使用 Type 查找
            var type = value.GetType();
            if (_typeLookup.TryGetValue(type, out var setter))
            {
                setter(bb, key, value);
                return;
            }
            
            UnityEngine.Debug.LogWarning($"[Asaki] Unknown type: {type.Name}");
        }

        // 注册表
        private static readonly Dictionary<Type, Action<IAsakiBlackboard, string, object>> _typeLookup 
            = new Dictionary<Type, Action<IAsakiBlackboard, string, object>>();

        // ★ API供生成代码调用
        public static void Register<T>()
        {
            Type t = typeof(T);
            if (!_typeLookup.ContainsKey(t))
            {
                // 生成一个强类型的闭包委托
                _typeLookup.Add(t, (bb, key, value) => 
                {
                    // 这里虽然有 cast (T)value，但因为是从 Dictionary<Type> 查出来的，肯定是安全的
                    // 而且这是泛型方法内的逻辑，JIT 会生成高效代码
                    bb.SetValue(key, (T)value); 
                });
            }
        }
    }
}