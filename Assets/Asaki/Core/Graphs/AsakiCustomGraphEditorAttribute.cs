using System;

namespace Asaki.Editor.GraphEditors
{
	[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
	public sealed class AsakiCustomGraphEditorAttribute : Attribute
	{
		public Type GraphType { get; }

		public AsakiCustomGraphEditorAttribute(Type graphType)
		{
			GraphType = graphType;
		}
	}
}
