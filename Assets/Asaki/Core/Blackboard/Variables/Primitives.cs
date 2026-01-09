using System;
using UnityEngine;

namespace Asaki.Core.Blackboard.Variables
{
    /// <summary>
    /// 表示整数类型的黑板值。
    /// 继承自 <see cref="AsakiValue{T}"/>，其中 <typeparamref name="T"/> 为 <see cref="int"/>，
    /// 用于在黑板系统中存储和处理整数值。
    /// </summary>
    [Serializable]
    public class AsakiInt : AsakiValue<int>
    {
        public AsakiInt() : base(() => new AsakiInt()) { }
        public AsakiInt(int value) : this()
        {
            Value = value;
        }
    }

    /// <summary>
    /// 表示单精度浮点数类型的黑板值。
    /// 继承自 <see cref="AsakiValue{T}"/>，其中 <typeparamref name="T"/> 为 <see cref="float"/>，
    /// 用于在黑板系统中存储和处理浮点数值。
    /// </summary>
    [Serializable]
    public class AsakiFloat : AsakiValue<float>
    {
        public AsakiFloat() : base(() => new AsakiFloat()) { }
        public AsakiFloat(float value) : this()
        {
            Value = value;
        }
    }

    /// <summary>
    /// 表示布尔类型的黑板值。
    /// 继承自 <see cref="AsakiValue{T}"/>，其中 <typeparamref name="T"/> 为 <see cref="bool"/>，
    /// 用于在黑板系统中存储和处理布尔值。
    /// </summary>
    [Serializable]
    public class AsakiBool : AsakiValue<bool>
    {
        public AsakiBool() : base(() => new AsakiBool()) { }
        public AsakiBool(bool value) : this()
        {
            Value = value;
        }
    }

    /// <summary>
    /// 表示字符串类型的黑板值。
    /// 继承自 <see cref="AsakiValue{T}"/>，其中 <typeparamref name="T"/> 为 <see cref="string"/>，
    /// 用于在黑板系统中存储和处理字符串值。
    /// </summary>
    [Serializable]
    public class AsakiString : AsakiValue<string>
    {
        public AsakiString() : base(() => new AsakiString()) { }
        public AsakiString(string value) : this()
        {
            Value = value;
        }
    }

    /// <summary>
    /// 表示三维向量类型的黑板值。
    /// 继承自 <see cref="AsakiValue{T}"/>，其中 <typeparamref name="T"/> 为 <see cref="Vector3"/>，
    /// 用于在黑板系统中存储和处理三维向量值。
    /// </summary>
    [Serializable]
    public class AsakiVector3 : AsakiValue<Vector3>
    {
        public AsakiVector3() : base(() => new AsakiVector3()) { }
        public AsakiVector3(Vector3 value) : this()
        {
            Value = value;
        }
    }

    /// <summary>
    /// 表示二维向量类型的黑板值。
    /// 继承自 <see cref="AsakiValue{T}"/>，其中 <typeparamref name="T"/> 为 <see cref="Vector2"/>，
    /// 用于在黑板系统中存储和处理二维向量值。
    /// </summary>
    [Serializable]
    public class AsakiVector2 : AsakiValue<Vector2>
    {
        public AsakiVector2() : base(() => new AsakiVector2()) { }
        public AsakiVector2(Vector2 value) : this()
        {
            Value = value;
        }
    }

    /// <summary>
    /// 表示二维整数向量类型的黑板值。
    /// 继承自 <see cref="AsakiValue{T}"/>，其中 <typeparamref name="T"/> 为 <see cref="Vector2Int"/>，
    /// 用于在黑板系统中存储和处理二维整数向量值。
    /// </summary>
    [Serializable]
    public class AsakiVector2Int : AsakiValue<Vector2Int>
    {
        public AsakiVector2Int() : base(() => new AsakiVector2Int()) { }
        public AsakiVector2Int(Vector2Int value) : this()
        {
            Value = value;
        }
    }

    /// <summary>
    /// 表示三维整数向量类型的黑板值。
    /// 继承自 <see cref="AsakiValue{T}"/>，其中 <typeparamref name="T"/> 为 <see cref="Vector3Int"/>，
    /// 用于在黑板系统中存储和处理三维整数向量值。
    /// </summary>
    [Serializable]
    public class AsakiVector3Int : AsakiValue<Vector3Int>
    {
        public AsakiVector3Int() : base(() => new AsakiVector3Int()) { }
        public AsakiVector3Int(Vector3Int value) : this()
        {
            Value = value;
        }
    }

    /// <summary>
    /// 表示颜色类型的黑板值。
    /// 继承自 <see cref="AsakiValue{T}"/>，其中 <typeparamref name="T"/> 为 <see cref="Color"/>，
    /// 用于在黑板系统中存储和处理颜色值。
    /// </summary>
    [Serializable]
    public class AsakiColor : AsakiValue<Color>
    {
        public AsakiColor() : base(() => new AsakiColor()) { }
        public AsakiColor(Color value) : this()
        {
            Value = value;
        }
    }

    /// <summary>
    /// 表示游戏对象类型的黑板值。
    /// 继承自 <see cref="AsakiValue{T}"/>，其中 <typeparamref name="T"/> 为 <see cref="GameObject"/>，
    /// 用于在黑板系统中存储和处理游戏对象值。
    /// </summary>
    [Serializable]
    public class AsakiGameObject : AsakiValue<GameObject>
    {
        public AsakiGameObject() : base(() => new AsakiGameObject()) { }
        public AsakiGameObject(GameObject value) : this()
        {
            Value = value;
        }
    }
}