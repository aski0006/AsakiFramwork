using Asaki.Core;
using Asaki.Core.Attributes;
using Asaki.Core.Graphs;
using Asaki.Editor.GraphEditors;
using Asaki.Editor.GraphEditors.Impl;
using UnityEngine;

namespace Game.GraphTool
{
	[AsakiGraphContext(typeof(ExampleGraph), "ExampleGraph")]
	public class ExampleNode : AsakiNodeBase
	{
		[AsakiNodeInput] public float a;
	}
	
	[CreateAssetMenu(menuName = "Asaki/ExampleGraph")]
	public class ExampleGraph : AsakiGraphBase{}

	[AsakiCustomGraphEditor(typeof(ExampleGraph))]
	public class ExampleGraphController : GenericAsakiGraphController
	{
		public ExampleGraphController(AsakiGraphBase graph) : base(graph) { }
	}
}
