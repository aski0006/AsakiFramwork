using Asaki.Core.Context;
using System;
using Asaki.Core.MVVM;

namespace Asaki.Core.Blackboard
{
    /// <summary>
    /// 定义黑板系统的核心接口。
    /// 该接口集成自 <see cref="IAsakiService"/> 和 <see cref="IDisposable"/>，提供了黑板数据管理的一系列方法，
    /// 包括作用域链访问、数据存取、响应式绑定以及元数据查询等功能。
    /// </summary>
    public interface IAsakiBlackboard : IAsakiService, IDisposable
    {
        /// <summary>
        /// 获取此黑板的父级黑板，用于构建作用域链。
        /// 通过父级黑板，可实现数据的继承和共享，形成层次化的数据结构。
        /// </summary>
        IAsakiBlackboard Parent { get; }

        /// <summary>
        /// 在黑板中设置指定键的值。
        /// 支持 Shadowing 机制，确保在当前黑板作用域内正确设置值，同时保证类型安全。
        /// </summary>
        /// <typeparam name="T">要设置的值的类型。</typeparam>
        /// <param name="key">用于标识值的 <see cref="AsakiBlackboardKey"/>。</param>
        /// <param name="value">要设置的值。</param>
        void SetValue<T>(AsakiBlackboardKey key, T value);

        /// <summary>
        /// 从黑板中获取指定键的值。
        /// 如果在当前黑板作用域内未找到值，则会在父级黑板中查找，支持默认值返回。
        /// </summary>
        /// <typeparam name="T">要获取的值的类型。</typeparam>
        /// <param name="key">用于标识值的 <see cref="AsakiBlackboardKey"/>。</param>
        /// <param name="defaultValue">当未找到值时返回的默认值，默认为 <see cref="default(T)"/>。</param>
        /// <returns>指定键对应的值，如果未找到则返回默认值。</returns>
        T GetValue<T>(AsakiBlackboardKey key, T defaultValue = default(T));

        /// <summary>
        /// 获取指定键的属性对象，用于响应式绑定。
        /// 支持 Copy - On - Access 机制，确保在访问时获取到最新的值，并在需要时创建本地副本。
        /// </summary>
        /// <typeparam name="T">属性值的类型。</typeparam>
        /// <param name="key">用于标识属性的 <see cref="AsakiBlackboardKey"/>。</param>
        /// <returns>指定键的 <see cref="AsakiProperty{T}"/> 对象，可用于响应式编程。</returns>
        AsakiProperty<T> GetProperty<T>(AsakiBlackboardKey key);

        /// <summary>
        /// 检查黑板中是否包含指定的键。
        /// 通过此方法可判断某个键是否在当前黑板或其作用域链中已存在。
        /// </summary>
        /// <param name="key">要检查的 <see cref="AsakiBlackboardKey"/>。</param>
        /// <returns>如果黑板中包含该键，则返回 true；否则返回 false。</returns>
        bool Contains(AsakiBlackboardKey key);

        /// <summary>
        /// 获取指定键的注册类型，用于安全检查或调试。
        /// 通过返回键的注册类型，可在运行时进行类型相关的安全检查或调试操作。
        /// </summary>
        /// <param name="key">要获取注册类型的 <see cref="AsakiBlackboardKey"/>。</param>
        /// <returns>指定键的注册类型，如果未找到则返回 null。</returns>
        Type GetKeyType(AsakiBlackboardKey key);
    }
}