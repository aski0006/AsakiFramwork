using Asaki.Core.Blackboard;
using Asaki.Core.Context;
using System;
using System.Reflection;
using UnityEngine;

namespace Asaki.Core.Graphs
{
    /// <summary>
    /// 图执行器的抽象基类，提供图实例化、黑板作用域链构建、节点执行和变量管理的完整运行时支持。
    /// </summary>
    /// <typeparam name="TGraph">图资源类型，必须继承自 <see cref="AsakiGraphBase"/>。</typeparam>
    /// <remarks>
    /// <para>核心职责：</para>
    /// <list type="bullet">
    ///     <item>图运行时初始化（<see cref="InitializeRuntime"/>）和缓存预构建</item>
    ///     <item>黑板作用域链搭建：Local → Global 的层次化变量查找与隔离</item>
    ///     <item>节点执行分发与生命周期管理（<see cref="ExecuteNode"/>）</item>
    ///     <item>输入值自动解析与上游节点回溯求值（<see cref="GetInputValue{T}"/>）</item>
    ///     <item>类型安全的变量读写（<see cref="GetVariable{T}"/>/<see cref="SetVariable{T}"/>）</item>
    ///     <item>通过 <see cref="OnNodeExecuted"/> 事件提供执行流调试支持</item>
    /// </list>
    /// 
    /// <para>执行模型：</para>
    /// <list type="number">
    ///     <item><b>初始化阶段：</b>构建拓扑缓存、创建运行时上下文、初始化黑板变量</item>
    ///     <item><b>执行阶段：</b>从入口节点开始深度优先遍历，通过 <see cref="ExecuteNode"/> 分发到具体处理器</item>
    ///     <item><b>清理阶段：</b>在 <see cref="OnDestroy"/> 中释放黑板资源，断开属性订阅防止内存泄漏</item>
    /// </list>
    /// 
    /// <para>作用域链设计：</para>
    /// Local Blackboard（图级）→ Global Blackboard（应用级）的链式查找机制，遵循"就近原则"：
    /// <list type="bullet">
    ///     <item>变量读取：先在Local查找，不存在则向上追溯Global</item>
    ///     <item>变量写入：仅在Local作用域修改，<strong>绝不</strong>污染Global</item>
    ///     <item>变量遮蔽：Local可覆盖同名的Global变量值，但Global本身保持不变</item>
    /// </list>
    /// 
    /// <para>类型分发策略：</para>
    /// <see cref="WriteToBlackboard"/> 实现两级分发：
    /// <list type="bullet">
    ///     <item><b>Fast Path（原生类型）：</b>对 int/float/bool/string/Vector 等常见类型使用 switch 直接调用泛型方法，避免反射开销</item>
    ///     <item><b>Universal Path（泛型反射）：</b>对自定义类型通过 <see cref="WriteGenericToBlackboard"/> 动态构造 <c>SetValue&lt;T&gt;</c> 方法</item>
    /// </list>
    /// 
    /// <para>子类扩展点：</para>
    /// <list type="table">
    ///     <item>
    ///         <term><see cref="OnGraphInitialized"/></term>
    ///         <description>图初始化完成后调用，用于绑定UI、注册服务</description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="OnExecuteCustomNode"/></term>
    ///         <description>处理特定业务节点（如 PlaySound, MoveTo, Spawn），必须重写</description>
    ///     </item>
    ///     <item>
    ///         <term><see cref="ResolveNodeValue{T}"/></term>
    ///         <description>节点作为值源时的求值逻辑，可扩展支持更多值节点类型</description>
    ///     </item>
    /// </list>
    /// </remarks>
    /// <example>
    /// 实现对话系统执行器：
    /// <code>
    /// public class DialogueRunner : AsakiGraphRunner&lt;DialogueGraph&gt;
    /// {
    ///     public UnityEvent&lt;string&gt; OnShowText;
    ///     
    ///     protected override void OnGraphInitialized()
    ///     {
    ///         // 1. 绑定UI系统
    ///         var uiManager = AsakiContext.Get&lt;IUIManager&gt;();
    ///         OnShowText.AddListener(uiManager.ShowDialogue);
    ///         
    ///         // 2. 设置全局黑板引用
    ///         var globalBB = Resources.Load&lt;GlobalDialogueState&gt;("GlobalDialogueState");
    ///         if (AsakiContext.TryGet&lt;IAsakiBlackboard&gt;(out var globalBlackboard))
    ///         {
    ///             // 将全局状态注入到上下文
    ///             _context.Blackboard.SetValue("SpeakerName", globalBB.CurrentSpeaker);
    ///         }
    ///     }
    ///     
    ///     protected override void OnExecuteCustomNode(AsakiNodeBase node)
    ///     {
    ///         switch (node)
    ///         {
    ///             case ShowTextNode showText:
    ///                 OnShowText?.Invoke(showText.Text);
    ///                 var next = GraphAsset.GetNextNode(node);
    ///                 if (next != null) ExecuteNode(next);
    ///                 break;
    ///                 
    ///             case BranchNode branch:
    ///                 bool condition = GetVariable&lt;bool&gt;(branch.ConditionKey);
    ///                 var port = condition ? "True" : "False";
    ///                 var branchNext = GraphAsset.GetNextNode(node, port);
    ///                 if (branchNext != null) ExecuteNode(branchNext);
    ///                 break;
    ///         }
    ///     }
    ///     
    ///     protected override T ResolveNodeValue&lt;T&gt;(AsakiNodeBase node, string outputPortName)
    ///     {
    ///         if (node is VariableNode varNode)
    ///         {
    ///             // 从黑板读取变量值作为节点输出
    ///             return GetVariable&lt;T&gt;(varNode.VariableName);
    ///         }
    ///         return base.ResolveNodeValue&lt;T&gt;(node, outputPortName);
    ///     }
    /// }
    /// </code>
    /// </example>
    public abstract class AsakiGraphRunner<TGraph> : MonoBehaviour
        where TGraph : AsakiGraphBase
    {
        /// <summary>
        /// 关联的图资源资产，在Inspector中赋值。
        /// </summary>
        /// <remarks>
        /// 此字段必须在编辑器中或使用代码在 <see cref="Start"/> 前赋值。
        /// 若为 <c>null</c>，Runner将不执行任何操作并输出警告。
        /// </remarks>
        [Header("Graphs Data")]
        public TGraph GraphAsset;

        /// <summary>
        /// 图运行时上下文，封装黑板和Owner引用。
        /// </summary>
        /// <remarks>
        /// <para>生命周期：</para>
        /// <list type="bullet">
        ///     <item>在 <see cref="Start"/> 中创建并初始化</item>
        ///     <item>在图执行期间被所有节点共享</item>
        ///     <item>在 <see cref="OnDestroy"/> 中通过 <see cref="IDisposable.Dispose"/> 释放资源</item>
        /// </list>
        /// <para>访问权限：protected，允许子类直接使用，但不应在类外部修改。</para>
        /// </remarks>
        protected AsakiGraphRuntimeContext _context;

        /// <summary>
        /// Unity 初始化回调，在组件启用时执行。
        /// </summary>
        /// <remarks>
        /// <para>标准初始化流程：</para>
        /// <list type="number">
        ///     <item>验证 <see cref="GraphAsset"/> 不为空</item>
        ///     <item>调用 <see cref="AsakiGraphBase.InitializeRuntime"/> 构建拓扑缓存</item>
        ///     <item>调用 <see cref="InitializeContext"/> 创建运行时上下文</item>
        ///     <item>调用 <see cref="InitializeBlackboardValues"/> 初始化黑板变量</item>
        ///     <item>触发 <see cref="OnGraphInitialized"/> 子类钩子</item>
        /// </list>
        /// <para>错误处理：若 <see cref="GraphAsset"/> 为 <c>null</c>，记录警告并静默返回，避免运行时异常。</para>
        /// </remarks>
        protected virtual void Start()
        {
            if (GraphAsset == null)
            {
                Debug.LogWarning($"[AsakiGraphRunner] GraphAsset is null on {name}");
                return;
            }

            // 1. 初始化图结构缓存 (O(1) Lookup)
            GraphAsset.InitializeRuntime();

            // 2. 构建运行时上下文
            InitializeContext();

            // 3. 将 Asset 中配置的变量初值填入 Runtime 黑板
            InitializeBlackboardValues();

            OnGraphInitialized();
        }

        /// <summary>
        /// 构建运行时上下文并搭建黑板作用域链。
        /// </summary>
        /// <remarks>
        /// <para><strong>作用域链构建流程：</strong></para>
        /// <list type="number">
        ///     <item>创建新的 <see cref="AsakiGraphRuntimeContext"/> 实例</item>
        ///     <item>设置 <see cref="AsakiGraphRuntimeContext.Owner"/> 为当前 <c>GameObject</c></item>
        ///     <item>尝试从 <see cref="AsakiContext"/> 获取全局黑板实例（若已注册）</item>
        ///     <item>创建Local黑板，并将Global黑板作为父作用域传入构造函数</item>
        /// </list>
        /// 
        /// <para>Thread Safety：</para>
        /// <see cref="AsakiContext.TryGet{T}"/> 是线程安全的，可直接在 <see cref="Start"/> 中调用。
        /// 
        /// <para>设计依据：</para>
        /// 实现"Local → Global"的变量查找链，遵循 v2 设计规范：对Local黑板的修改绝不污染Global。
        /// </remarks>
        private void InitializeContext()
        {
            _context = new AsakiGraphRuntimeContext();
            _context.Owner = gameObject;

            // --- 作用域链构建 (Scope Chain Integration) ---

            // A. 尝试获取全局黑板 (Global Scope)
            IAsakiBlackboard globalScope = null;
            if (AsakiContext.TryGet<IAsakiBlackboard>(out IAsakiBlackboard globalBB))
            {
                globalScope = globalBB;
            }

            // B. 创建本地黑板 (Local Scope)
            _context.Blackboard = new AsakiBlackboard(globalScope);
        }

        /// <summary>
        /// 将图资源和全局黑板中的变量初始值写入运行时黑板。
        /// </summary>
        /// <remarks>
        /// <para>加载顺序（优先级从高到低）：</para>
        /// <list type="number">
        ///     <item>图资源的局部变量（<see cref="AsakiGraphBase.Variables"/>）</item>
        ///     <item>全局黑板变量（<see cref="AsakiGlobalBlackboardAsset"/>），仅当局部无同名变量时加载</item>
        /// </list>
        /// <para>实现机制：遍历变量列表，调用 <see cref="WriteVariableToRuntime"/> 将 <see cref="AsakiVariableDef.ValueData"/> 应用到黑板。</para>
        /// <para>全局黑板路径：硬编码为 "GlobalBlackboard"，可通过 Resources 加载。</para>
        /// </remarks>
        private void InitializeBlackboardValues()
        {
            if (GraphAsset == null || _context.Blackboard == null) return;

            string globalAssetPath = "GlobalBlackboard";
            AsakiGlobalBlackboardAsset globalAsset = UnityEngine.Resources.Load<AsakiGlobalBlackboardAsset>(globalAssetPath);
            if (globalAsset != null)
            {
                // 先加载全局变量（作为父作用域）
                foreach (AsakiVariableDef globalVar in globalAsset.GlobalVariables)
                {
                    // 如果局部没有同名变量，则继承全局值
                    if (!GraphAsset.Variables.Exists(v => v.Name == globalVar.Name))
                    {
                        WriteVariableToRuntime(globalVar.Name, globalVar);
                    }
                }
            }

            // 加载局部变量（可覆盖全局同名变量）
            foreach (AsakiVariableDef variable in GraphAsset.Variables)
            {
                WriteVariableToRuntime(variable.Name, variable);
            }
        }

        /// <summary>
        /// 将变量定义的值写入运行时黑板。
        /// </summary>
        /// <param name="name">变量名称。</param>
        /// <param name="variable">变量定义实例，包含类型信息和值数据。</param>
        /// <remarks>
        /// <para>内部逻辑：</para>
        /// 调用 <see cref="AsakiValueBase.ApplyTo(IAsakiBlackboard, string)"/> 将多态值数据应用到黑板。
        /// 此方法自动处理类型转换和存储。
        /// <para>警告日志：若 <paramref name="variable"/>.ValueData 为 <c>null</c>，记录警告但不抛出异常。</para>
        /// </remarks>
        private void WriteVariableToRuntime(string name, AsakiVariableDef variable)
        {
            if (variable.ValueData != null)
            {
                variable.ValueData.ApplyTo(_context.Blackboard, name);
            }
            else
            {
                Debug.LogWarning($"[AsakiRunner] Variable '{name}' has no data!");
            }
        }

        /// <summary>
        /// 图初始化完成后的钩子方法，供子类扩展。
        /// </summary>
        /// <remarks>
        /// <para>典型用途：</para>
        /// <list type="bullet">
        ///     <item>绑定UI系统事件</item>
        ///     <item>注册特定服务到 <see cref="AsakiContext"/></item>
        ///     <item>注入全局状态或配置</item>
        ///     <item>初始化调试或分析工具</item>
        /// </list>
        /// <para>调用时机：在所有内部初始化（缓存、上下文、黑板）完成后，但在任何节点执行前。</para>
        /// <para>默认实现为空，子类可根据需要重写。</para>
        /// </remarks>
        protected virtual void OnGraphInitialized() { }

        /// <summary>
        /// Unity 销毁回调，必须调用以释放黑板资源。
        /// </summary>
        /// <remarks>
        /// <para>清理职责：</para>
        /// <list type="bullet">
        ///     <item>调用 <see cref="AsakiGraphRuntimeContext.Dispose"/>，级联释放黑板及其所有 <see cref="AsakiProperty{T}"/></item>
        ///     <item>断开所有属性订阅，防止已销毁对象被通知导致内存泄漏</item>
        ///     <item>清理 <see cref="OnNodeExecuted"/> 事件订阅</item>
        ///     <item>将 <see cref="_context"/> 置 <c>null</c> 辅助GC</item>
        /// </list>
        /// <para>重要性：未正确释放可能导致Property订阅者持有已销毁对象引用，引发Editor模式下的内存泄漏警告。</para>
        /// </remarks>
        protected virtual void OnDestroy()
        {
            // 必须清理，否则 Blackboard 中的 AsakiProperty 可能会残留 UI 订阅，导致内存泄漏
            if (_context != null)
            {
                _context.Dispose();
                _context = null;
            }
            OnNodeExecuted = null;
        }

        // --- 辅助 API (Helpers) ---

        /// <summary>
        /// 从运行时黑板获取变量值（支持字符串键自动转哈希）。
        /// </summary>
        /// <typeparam name="T">变量类型。</typeparam>
        /// <param name="key">变量名称字符串。</param>
        /// <returns>变量值，若未找到返回 <typeparamref name="T"/> 的默认值。</returns>
        /// <remarks>
        /// <para>隐式转换：通过 <see cref="AsakiBlackboardKey.implicit operator AsakiBlackboardKey(string)"/> 自动将字符串转换为FNV-1a哈希键。</para>
        /// <para>作用域解析：自动处理Local → Global的作用域链查找。</para>
        /// <para>性能：哈希键计算有轻微开销，但在高频调用中可忽略。</para>
        /// </remarks>
        public T GetVariable<T>(string key)
        {
            if (_context?.Blackboard == null) return default(T);
            return _context.Blackboard.GetValue<T>(key);
        }

        /// <summary>
        /// 向运行时黑板设置变量值（Shadowing：仅修改本地作用域）。
        /// </summary>
        /// <typeparam name="T">变量类型。</typeparam>
        /// <param name="key">变量名称字符串。</param>
        /// <param name="value">要设置的值。</param>
        /// <remarks>
        /// <para>Shadowing语义：</para>
        /// 写入操作<strong>仅影响Local黑板</strong>，不会修改Global黑板中的同名变量。
        /// 这实现了安全的变量遮蔽，防止意外污染全局状态。
        /// <para>隐式转换：同 <see cref="GetVariable{T}"/>，自动转换字符串键为哈希键。</para>
        /// </remarks>
        public void SetVariable<T>(string key, T value)
        {
            if (_context?.Blackboard == null) return;
            _context.Blackboard.SetValue<T>(key, value);
        }

        /// <summary>
        /// 获取节点的输入端口值。如果有连线，则回溯上游节点并计算返回值；否则返回默认值。
        /// </summary>
        /// <typeparam name="T">期望的值类型。</typeparam>
        /// <param name="currentNode">当前节点实例。</param>
        /// <param name="inputPortName">输入端口名称。</param>
        /// <param name="fallback">默认值（当端口未连接时）。</param>
        /// <returns>从上游节点解析的值或 <paramref name="fallback"/>。</returns>
        /// <remarks>
        /// <para>值解析流程：</para>
        /// <list type="number">
        ///     <item>查询 <see cref="AsakiGraphBase.GetInputConnection"/> 获取连接到此端口的边</item>
        ///     <item>若无连接，返回 <paramref name="fallback"/></item>
        ///     <item>通过 <see cref="AsakiGraphBase.GetNodeByGUID"/> 获取上游源节点</item>
        ///     <item>调用 <see cref="ResolveNodeValue{T}"/> 计算源节点的输出值</item>
        /// </list>
        /// <para>懒加载求值：仅在需要时计算上游节点，支持复杂的依赖链。</para>
        /// </remarks>
        protected T GetInputValue<T>(AsakiNodeBase currentNode, string inputPortName, T fallback = default(T))
        {
            // 1. 查找连接到该端口的线
            AsakiEdgeData edge = GraphAsset.GetInputConnection(currentNode, inputPortName);
            if (edge == null) return fallback;

            // 2. 找到上游节点
            AsakiNodeBase sourceNode = GraphAsset.GetNodeByGUID(edge.BaseNodeGUID);
            if (sourceNode == null) return fallback;

            // 3. 计算上游节点的值 (Resolve)
            return ResolveNodeValue<T>(sourceNode, edge.BasePortName);
        }

        /// <summary>
        /// 解析节点的值（当节点被当作数据源连接时调用）。
        /// </summary>
        /// <typeparam name="T">期望的值类型。</typeparam>
        /// <param name="node">源节点实例。</param>
        /// <param name="outputPortName">输出端口名称。</param>
        /// <returns>节点输出的值，若无法解析返回 <typeparamref name="T"/> 的默认值。</returns>
        /// <remarks>
        /// <para>默认实现：</para>
        /// <list type="bullet">
        ///     <item>识别 <see cref="AsakiGetVariableNode"/> 类型，从黑板读取变量值</item>
        ///     <item>其他节点类型记录警告并返回默认值</item>
        /// </list>
        /// <para>子类应重写此方法以支持更多值节点类型（如常量节点、表达式节点）。</para>
        /// <para>类型转换：返回值会隐式转换为 <typeparamref name="T"/>，若类型不匹配可能抛出异常。</para>
        /// </remarks>
        protected virtual T ResolveNodeValue<T>(AsakiNodeBase node, string outputPortName)
        {
            if (node is AsakiGetVariableNode getVarNode)
            {
                if (_context.Blackboard == null) return default(T);

                T value = _context.Blackboard.GetValue<T>(getVarNode.VariableName);

                return value;
            }

            Debug.LogWarning($"[AsakiRunner] Cannot resolve value from node type: {node.GetType().Name}");
            return default(T);
        }

        // ================================================================
        // ★ 核心驱动逻辑 2: 执行流 (Execution Flow)
        // ================================================================

        /// <summary>
        /// 节点执行事件（Editor Only），在每次执行节点时触发。
        /// </summary>
        /// <remarks>
        /// <para>订阅场景：</para>
        /// <list type="bullet">
        ///     <item>图编辑器高亮当前执行节点</item>
        ///     <item>调试窗口显示节点执行顺序</item>
        ///     <item>性能分析工具记录节点耗时</item>
        /// </list>
        /// <para>条件编译：仅在 <c>UNITY_EDITOR</c> 下触发，避免Release构建的性能开销。</para>
        /// <para>线程注意：在Unity主线程触发，可直接操作UI。</para>
        /// </remarks>
        public event Action<AsakiNodeBase> OnNodeExecuted;

        /// <summary>
        /// 执行单个节点逻辑的核心分发方法。
        /// </summary>
        /// <param name="node">要执行的节点实例。</param>
        /// <remarks>
        /// <para>分发策略：</para>
        /// <list type="bullet">
        ///     <item>若 <paramref name="node"/> 为 <c>null</c>，立即返回</item>
        ///     <item>在Editor模式下触发 <see cref="OnNodeExecuted"/> 事件</item>
        ///     <item><see cref="AsakiSetVariableNode"/> 特殊处理：读取输入值并写入黑板，然后执行后续节点</item>
        ///     <item>其他节点类型委托给 <see cref="OnExecuteCustomNode"/></item>
        /// </list>
        /// <para>递归执行：对于顺序节点（如SetVariable），该方法会自动调用 <see cref="GetNextNode"/> 并继续执行，形成深度优先遍历。</para>
        /// </remarks>
        protected virtual void ExecuteNode(AsakiNodeBase node)
        {
            if (node == null) return;

#if UNITY_EDITOR
            // 触发调试事件
            OnNodeExecuted?.Invoke(node);
#endif

            // --- Case A: Set Variable Node (写入黑板) ---
            if (node is AsakiSetVariableNode setVarNode)
            {
                if (_context.Blackboard == null) return;

                // 1. 获取要写入的值
                object valueToWrite = GetInputValue<object>(setVarNode, "Value");

                // 2. 写入黑板 (需要处理类型分发)
                WriteToBlackboard(setVarNode.VariableName, valueToWrite);

                // 3. 继续执行后续节点
                AsakiNodeBase nextNode = GraphAsset.GetNextNode(node, "Out");
                if (nextNode != null) ExecuteNode(nextNode);
                return;
            }

            // --- Case B: 其他执行节点 ---
            OnExecuteCustomNode(node);
        }

        /// <summary>
        /// 通过类型分发将对象值写入黑板，优先使用Fast Path优化性能。
        /// </summary>
        /// <param name="key">变量名称。</param>
        /// <param name="value">要写入的对象值。</param>
        /// <remarks>
        /// <para>Fast Path（常见原生类型）：</para>
        /// <list type="bullet">
        ///     <item>int/float/bool/string</item>
        ///     <item>Vector2/Vector3/Vector4</item>
        ///     <item>Vector2Int/Vector3Int</item>
        ///     <item>Color</item>
        /// </list>
        /// <para>Universal Path（自定义类型）：</para>
        /// 使用 <see cref="WriteGenericToBlackboard"/> 通过反射调用泛型方法。
        /// 
        /// <para>错误处理：捕获异常并输出错误日志，避免单个变量写入失败影响整体执行。</para>
        /// <para>GC注意：在Error路径中 <c>value.GetType()</c> 会产生轻微GC，但在错误场景下可忽略。</para>
        /// </remarks>
        private void WriteToBlackboard(string key, object value)
        {
            if (value == null)
            {
                Debug.LogWarning($"[AsakiRunner] Trying to set null value for key '{key}'. Ignore.");
                return;
            }

            try
            {
                // 1. 优先匹配 AsakiBlackboard 内部优化的“原生类型” (Fast Path)
                switch (value)
                {
                    case int i:
                        _context.Blackboard.SetValue(key, i);
                        break;
                    case float f:
                        _context.Blackboard.SetValue(key, f);
                        break;
                    case bool b:
                        _context.Blackboard.SetValue(key, b);
                        break;
                    case string s:
                        _context.Blackboard.SetValue(key, s);
                        break;
                    case Vector3 v3:
                        _context.Blackboard.SetValue(key, v3);
                        break;
                    case Vector2 v2:
                        _context.Blackboard.SetValue(key, v2);
                        break;
                    case Vector3Int v3i:
                        _context.Blackboard.SetValue(key, v3i);
                        break;
                    case Vector2Int v2i:
                        _context.Blackboard.SetValue(key, v2i);
                        break;
                    case Color c:
                        _context.Blackboard.SetValue(key, c);
                        break;
                    default:
                        // 2. 兜底逻辑：处理所有用户自定义类型 (Universal Path)
                        WriteGenericToBlackboard(key, value);
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[AsakiRunner] SetVariable Failed: Key={key}, Type={value.GetType().Name}, Value={value}. Error: {e.Message}");
            }
        }

        /// <summary>
        /// 通过反射调用泛型方法将自定义类型值写入黑板。
        /// </summary>
        /// <param name="key">变量名称。</param>
        /// <param name="value">自定义类型的值。</param>
        /// <remarks>
        /// <para>反射流程：</para>
        /// <list type="number">
        ///     <item>通过 <see cref="Type.GetMethod"/> 获取 <c>SetValue&lt;T&gt;</c> 的开放方法信息</item>
        ///     <item>使用 <see cref="MethodInfo.MakeGenericMethod"/> 构造具体泛型方法</item>
        ///     <item>通过 <see cref="MethodInfo.Invoke"/> 调用，传递 <paramref name="value"/> 作为参数</item>
        /// </list>
        /// <para>性能注意：此方法有显著反射开销，仅对Fast Path未覆盖的类型调用。</para>
        /// <para>异常：若方法未找到，抛出 <see cref="MissingMethodException"/>。</para>
        /// </remarks>
        private void WriteGenericToBlackboard(string key, object value)
        {
            MethodInfo methodInfo = _context.Blackboard.GetType().GetMethod("SetValue");
            if (methodInfo != null)
            {
                MethodInfo genericMethod = methodInfo.MakeGenericMethod(value.GetType());
                genericMethod.Invoke(_context.Blackboard, new object[] { key, value });
            }
            else
            {
                throw new MissingMethodException("[AsakiRunner] Could not find SetValue<T> on Blackboard instance.");
            }
        }

        /// <summary>
        /// 子类必须重写此方法以处理特定业务节点（如 PlaySound, MoveTo, Spawn）。
        /// </summary>
        /// <param name="node">要执行的节点实例。</param>
        /// <remarks>
        /// <para>实现要求：</para>
        /// <list type="number">
        ///     <item>使用模式匹配或类型检查识别自定义节点类型</item>
        ///     <item>执行节点特定逻辑（如播放音频、移动GameObject、实例化预制体）</item>
        ///     <item>通过 <see cref="GraphAsset.GetNextNode"/> 获取下一个节点并递归调用 <see cref="ExecuteNode"/></item>
        ///     <item>处理节点返回值（如Success/Failure）以决定执行分支</item>
        /// </list>
        /// <para>默认实现为空，子类不实现时节点无操作。</para>
        /// 
        /// <para>执行控制：</para>
        /// 子类决定执行流，可支持顺序、分支、并行、循环等多种模式。
        /// </remarks>
        protected virtual void OnExecuteCustomNode(AsakiNodeBase node) { }
    }
}