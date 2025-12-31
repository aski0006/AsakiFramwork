using System;

namespace Asaki.Core.Graphs
{
	[Serializable]
	[AsakiGraphContext(typeof(AsakiGraphBase), "Variable/Get")] // 适配所有图
	public class AsakiGetVariableNode : AsakiNodeBase
	{
		public override string Title =>  $"Get {VariableName}";
		// 存储变量名 (Key)
		public string VariableName;

		// 存储类型 (用于运行时优化和 Editor 端口着色)
		public AsakiBlackboardPropertyType VariableType;
		public bool IsGlobalVariable = false;
		// 输出端口 (名字固定为 Value，但在 Editor 下我们会动态修改它的显示类型)
		[AsakiNodeOutput("Value")]
		public object Value;
	}

	[Serializable]
	[AsakiGraphContext(typeof(AsakiGraphBase), "Variable/Set")]
	public class AsakiSetVariableNode : AsakiNodeBase
	{
		public override string Title => $"Set {VariableName}";

		public string VariableName;
		public AsakiBlackboardPropertyType VariableType;

		// 执行流输入
		[AsakiNodeInput("In")]
		public AsakiFlowPort InputFlow;

		// 值输入
		[AsakiNodeInput("Value")]
		public object NewValue;

		// 执行流输出
		[AsakiNodeOutput("Out")]
		public AsakiFlowPort OutputFlow;
	}

	[Serializable] public struct AsakiFlowPort { }
}
