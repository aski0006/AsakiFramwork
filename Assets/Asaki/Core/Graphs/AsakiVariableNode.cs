using Asaki.Core.Attributes;
using System;

namespace Asaki.Core.Graphs
{
	[Serializable]
	[AsakiGraphContext(typeof(AsakiGraphBase), "Variable/Get")]
	public class AsakiGetVariableNode : AsakiNodeBase
	{
		public override string Title => $"Get {VariableName}";

		public string VariableName;

		// [重构] 不再存储枚举，而是存储类型名字符串 (主要用于 Editor 连线检查或颜色显示)
		public string VariableTypeName;

		public bool IsGlobalVariable = false;

		[AsakiNodeOutput("Value")]
		public object Value;
	}

	[Serializable]
	[AsakiGraphContext(typeof(AsakiGraphBase), "Variable/Set")]
	public class AsakiSetVariableNode : AsakiNodeBase
	{
		public override string Title => $"Set {VariableName}";

		public string VariableName;

		// [重构] 同上
		public string VariableTypeName;

		[AsakiNodeInput("In")]
		public AsakiFlowPort InputFlow;

		[AsakiNodeInput("Value")]
		public object NewValue;

		[AsakiNodeOutput("Out")]
		public AsakiFlowPort OutputFlow;
	}

	[Serializable] public struct AsakiFlowPort { }
}
