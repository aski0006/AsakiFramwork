using System;
using UnityEngine;

namespace Asaki.Core.Graphs
{
    /// <summary>
    /// 图节点基类，定义所有节点共有的基础数据和可视化属性。
    /// </summary>
    /// <remarks>
    /// <para>设计目的：提供统一的节点数据模型，支持：</para>
    /// <list type="bullet">
    ///     <item>节点位置持久化（<see cref="Position"/>）</item>
    ///     <item>唯一标识符（<see cref="GUID"/>）用于序列化和引用</item>
    ///     <item>执行顺序控制（<see cref="ExecutionOrder"/>）</item>
    ///     <item>可定制的节点标题显示（<see cref="Title"/>）</item>
    ///     <item>节点创建时的初始化钩子（<see cref="OnCreated"/>）</item>
    /// </list>
    /// <para>序列化策略：所有字段标记为 <c>[HideInInspector]</c>，避免在Inspector中显示，但保留序列化能力。</para>
    /// <para>继承要求：所有具体节点类型必须继承此类并实现执行逻辑（通常在 <c>Execute</c> 方法中）。</para>
    /// </remarks>
    /// <example>
    /// 创建自定义节点类型：
    /// <code>
    /// [Serializable]
    /// public class LogNode : AsakiNodeBase
    /// {
    ///     public string Message = "Hello World";
    ///     
    ///     public override string Title => $"Log: {Message}"; // 动态标题
    ///     
    ///     public override void OnCreated()
    ///     {
    ///         // 节点创建时自动生成GUID（若为空）
    ///         if (string.IsNullOrEmpty(GUID))
    ///         {
    ///             GUID = System.Guid.NewGuid().ToString();
    ///         }
    ///     }
    ///     
    ///     protected override NodeStatus OnExecute(AsakiGraphRunner runner)
    ///     {
    ///         Debug.Log(Message);
    ///         return NodeStatus.Success;
    ///     }
    /// }
    /// </code>
    /// </example>
    [Serializable]
    public abstract class AsakiNodeBase
    {
        /// <summary>
        /// 节点在图编辑器中的二维坐标位置，用于持久化布局信息。
        /// </summary>
        /// <remarks>
        /// 坐标系原点为编辑器视图的左上角，单位像素。
        /// Unity GraphView 会自动更新此值以响应用户的拖拽操作。
        /// </remarks>
        [HideInInspector]
        public Vector2 Position;

        /// <summary>
        /// 节点全局唯一标识符（GUID），用于跨序列化周期稳定引用节点。
        /// </summary>
        /// <remarks>
        /// <para>生成策略：</para>
        /// 应在节点首次创建时（通常在 <see cref="OnCreated"/> 或编辑器创建逻辑中）通过 <c>Guid.NewGuid().ToString()</c> 生成。
        /// <para>稳定性要求：</para>
        /// 一旦生成，不应再修改，即使节点被复制或序列化/反序列化。
        /// 在复制节点时，必须生成新的GUID以避免冲突。
        /// <para>用途：</para>
        /// <list type="bullet">
        ///     <item>边数据（<see cref="AsakiEdgeData"/>）通过GUID引用源和目标节点</item>
        ///     <item>节点引用稳定性（跨编辑器会话）</item>
        ///     <item>节点链接的撤销/重做系统</item>
        /// </list>
        /// </remarks>
        [HideInInspector]
        public string GUID;

        /// <summary>
        /// 节点执行顺序，数值越小越早执行（仅对需要排序的节点有效，如事件触发器）。
        /// </summary>
        /// <remarks>
        /// 默认值为0，表示未指定顺序。
        /// 对于并行节点或事件节点，执行器可能按此值排序以确定优先级。
        /// 对于顺序执行的节点（如行为树），此值通常被忽略。
        /// </remarks>
        [HideInInspector]
        public int ExecutionOrder;

        /// <summary>
        /// 获取节点在编辑器中显示的标题文本，可重写以提供动态标题。
        /// </summary>
        /// <value>默认返回类型的简短名称（不含命名空间）。</value>
        /// <remarks>
        /// <para>重写建议：</para>
        /// <list type="bullet">
        ///     <item>包含关键参数值以提升可读性（如 <c>"Log: PlayerSpawned"</c>）</item>
        ///     <item>保持标题简洁（建议长度 < 30字符）</item>
        ///     <item>避免频繁变化的值（可能影响编辑器性能）</item>
        /// </list>
        /// <para>调用时机：每次编辑器重绘时调用，实现应考虑性能。</para>
        /// </remarks>
        public virtual string Title => GetType().Name;

        /// <summary>
        /// 节点创建时的生命周期回调，用于初始化默认值和生成GUID。
        /// </summary>
        /// <remarks>
        /// <para>调用时机：</para>
        /// <list type="bullet">
        ///     <item>在编辑器中通过节点创建菜单添加节点时</item>
        ///     <item>通过代码调用 <c>new T() { ... }</c> 初始化后，需手动调用</item>
        ///     <item>反序列化后<strong>不会</strong>自动调用（与构造函数行为一致）</item>
        /// </list>
        /// <para>默认实现为空，子类应重写以完成初始化。</para>
        /// <para>典型实现包括：GUID生成、默认值设置、初始端口创建。</para>
        /// </remarks>
        public virtual void OnCreated() { }
    }

    /// <summary>
    /// 图的边数据结构，表示节点之间的连接关系。
    /// </summary>
    /// <remarks>
    /// <para>设计特点：</para>
    /// <list type="bullet">
    ///     <item>纯数据类，不包含任何运行时逻辑</item>
    ///     <item>使用GUID字符串引用节点，避免直接引用导致的序列化问题</item>
    ///     <item>支持命名端口，允许单个节点拥有多个输入/输出连接</item>
    ///     <item>可序列化，确保图结构持久化</item>
    /// </list>
    /// 
    /// <para>与节点引用的区别：</para>
    /// 直接存储节点引用会导致Unity序列化系统深度克隆整个对象图，引发循环引用和重复数据。
    /// 使用GUID间接引用保持数据扁平化，仅在运行时重建连接。
    /// </remarks>
    /// <example>
    /// 边的创建和序列化：
    /// <code>
    /// // 编辑器中创建连接时生成
    /// var edge = new AsakiEdgeData
    /// {
    ///     BaseNodeGUID = sourceNode.GUID,      // "3f8a2b1c-..."
    ///     BasePortName = "Out",                // 源节点的输出端口名
    ///     TargetNodeGUID = targetNode.GUID,    // "e9c5d7a8-..."
    ///     TargetPortName = "In"                // 目标节点的输入端口名
    /// };
    /// 
    /// graph.Edges.Add(edge); // 序列化时保存到图资源
    /// </code>
    /// </example>
    [Serializable]
    public class AsakiEdgeData
    {
        /// <summary>
        /// 源节点（输出端）的GUID。
        /// </summary>
        /// <remarks>
        /// 对应 <see cref="AsakiNodeBase.GUID"/> 的值，在运行时通过 <see cref="AsakiGraphBase.GetNodeByGUID"/> 解析为实际节点实例。
        /// </remarks>
        public string BaseNodeGUID;

        /// <summary>
        /// 源节点的输出端口名称。
        /// </summary>
        /// <remarks>
        /// 允许节点拥有多个输出端口（如"Success"、"Failure"、"Out"）。
        /// 端口名称在节点类中定义，通常为常量字符串。
        /// </remarks>
        public string BasePortName;

        /// <summary>
        /// 目标节点（输入端）的GUID。
        /// </summary>
        /// <remarks>
        /// 对应 <see cref="AsakiNodeBase.GUID"/> 的值。
        /// </remarks>
        public string TargetNodeGUID;

        /// <summary>
        /// 目标节点的输入端口名称。
        /// </summary>
        /// <remarks>
        /// 允许节点拥有多个输入端口（如"In1"、"In2"、"Condition"）。
        /// 对于单输入节点，通常命名为"In"或"Input"。
        /// </remarks>
        public string TargetPortName;
    }
}