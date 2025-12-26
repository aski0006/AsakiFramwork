using Asaki.Core.Graphs;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Asaki.Editor.GraphEditors
{
	public class AsakiGraphView : GraphView
	{
		private AsakiGraphBase _graph;
		public AsakiGraphBase GraphAsset => _graph;
		private SerializedObject _serializedGraph; // 缓存 Graphs 的 SO
		public AsakiBlackboardProvider BlackboardProvider;
		public AsakiGraphView(AsakiGraphBase graph)
		{
			_graph = graph;
			_serializedGraph = new SerializedObject(graph); // 初始化 SO

			// 1. 基础交互配置
			SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
			this.AddManipulator(new ContentDragger());
			this.AddManipulator(new SelectionDragger());
			this.AddManipulator(new RectangleSelector());


			GridBackground grid = new GridBackground();
			Insert(0, grid);
			grid.StretchToParentSize();

			StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Asaki/Editor/Style/AsakiGraphView.uss");
			if (styleSheet != null)
			{
				styleSheets.Add(styleSheet);
			}
			else
			{
				Debug.LogWarning("[AsakiGraph] Could not find 'AsakiGraphView.uss' in Resources folder.");
			}
			// 3. 生成节点
			PopulateView();

			graphViewChanged = OnGraphViewChanged;
			Undo.undoRedoPerformed += OnUndoRedo;
			RegisterCallback<DetachFromPanelEvent>(OnDetach);
			nodeCreationRequest = OnNodeCreationRequest;


			BlackboardProvider = new AsakiBlackboardProvider(this, graph, _serializedGraph);
			RegisterCallback<DragEnterEvent>(OnDragEnter);
			RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
			RegisterCallback<DragPerformEvent>(OnDragPerform);
			RegisterCallback<DragLeaveEvent>(OnDragLeave);
		}

		private void OnDragEnter(DragEnterEvent evt)
		{
			// 检查数据源是否合法
			object data = DragAndDrop.GetGenericData("AsakiVariable");
			if (data != null)
			{
				// 设置鼠标样式为 "Copy" (绿加号)
				DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
				evt.StopImmediatePropagation();
			}
		}

		// 2. 拖拽更新 (每帧调用)
		private void OnDragUpdated(DragUpdatedEvent evt)
		{
			object data = DragAndDrop.GetGenericData("AsakiVariable");
			if (data != null)
			{
				DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
				evt.StopImmediatePropagation();
			}
		}

		// 3. 离开区域 (重置)
		private void OnDragLeave(DragLeaveEvent evt)
		{
			// DragAndDrop.visualMode = DragAndDropVisualMode.None; // 不需要手动重置，Unity 会处理
		}

		// 4. 执行放置 (松开鼠标)
		private void OnDragPerform(DragPerformEvent evt)
		{
			AsakiVariableDef data = DragAndDrop.GetGenericData("AsakiVariable") as AsakiVariableDef;
			if (data != null)
			{
				// 计算放置位置 (鼠标屏幕坐标 -> Graph 本地坐标)
				Vector2 localPos = contentViewContainer.WorldToLocal(evt.mousePosition);

				// 弹出菜单供用户选择 Get 或 Set
				ShowVariableMenu(data, localPos);

				DragAndDrop.AcceptDrag();
				evt.StopImmediatePropagation();
			}
		}

		private void ShowVariableMenu(AsakiVariableDef variable, Vector2 position)
		{
			GenericMenu menu = new GenericMenu();

			// Option 1: Get Variable
			menu.AddItem(new GUIContent($"Get {variable.Name}"), false, () =>
			{
				CreateVariableNode<AsakiGetVariableNode>(variable, position);
			});

			// Option 2: Set Variable
			menu.AddItem(new GUIContent($"Set {variable.Name}"), false, () =>
			{
				CreateVariableNode<AsakiSetVariableNode>(variable, position);
			});

			menu.ShowAsContext();
		}

		private void CreateVariableNode<T>(AsakiVariableDef variable, Vector2 position) where T : AsakiNodeBase, new()
		{
			// 使用 IO 工具创建纯数据节点
			T nodeData = AsakiGraphIOUtils.AddNode<T>(_graph, position);

			// ★ 注入变量信息
			// 这里利用反射或模式匹配，因为 T 是泛型
			if (nodeData is AsakiGetVariableNode getNode)
			{
				getNode.VariableName = variable.Name;
				getNode.VariableType = variable.Type;
			}
			else if (nodeData is AsakiSetVariableNode setNode)
			{
				setNode.VariableName = variable.Name;
				setNode.VariableType = variable.Type;
			}

			// 立即在视图中生成
			CreateNodeView(nodeData);
		}
		private void OnNodeCreationRequest(NodeCreationContext context)
		{
			AsakiNodeSearchWindow searchWindow = ScriptableObject.CreateInstance<AsakiNodeSearchWindow>();
			EditorWindow window = EditorWindow.focusedWindow;
			searchWindow.Initialize(this, _graph, window);
			SearchWindow.Open(new SearchWindowContext(context.screenMousePosition), searchWindow);
		}

		private void OnDetach(DetachFromPanelEvent evt)
		{
			Undo.undoRedoPerformed -= OnUndoRedo;
		}

		private void OnUndoRedo()
		{
			schedule.Execute(PopulateView);
		}

		private void PopulateView()
		{
			graphViewChanged -= OnGraphViewChanged;
			DeleteElements(graphElements.ToList());

			// 必须更新 SO，否则 Undo 后获取的 Property 可能是旧的
			_serializedGraph.Update();

			foreach (AsakiNodeBase nodeData in _graph.Nodes)
			{
				CreateNodeView(nodeData);
			}

			foreach (AsakiEdgeData edgeData in _graph.Edges)
			{
				AsakiNodeView baseNode = GetNodeByGUID(edgeData.BaseNodeGUID);
				AsakiNodeView targetNode = GetNodeByGUID(edgeData.TargetNodeGUID);

				if (baseNode != null && targetNode != null)
				{
					Port outputPort = baseNode.Outputs.FirstOrDefault(p => (string)p.userData == edgeData.BasePortName);
					Port inputPort = targetNode.Inputs.FirstOrDefault(p => (string)p.userData == edgeData.TargetPortName);

					if (outputPort != null && inputPort != null)
					{
						Edge edge = outputPort.ConnectTo(inputPort);
						AddElement(edge);
					}
				}
			}

			graphViewChanged += OnGraphViewChanged;
		}

		public void CreateNodeView(AsakiNodeBase node)
		{
			// ★ 传入 SerializedObject，以便 NodeView 能够绘制 Inspector
			AsakiNodeView nodeView = new AsakiNodeView(node, _serializedGraph);
			AddElement(nodeView);
			nodeView.SetPosition(new Rect(node.Position, Vector2.zero));
		}

		public AsakiNodeView GetNodeByGUID(string guid)
		{
			return nodes.ToList().Cast<AsakiNodeView>().FirstOrDefault(n => n.node.GUID == guid);
		}

		private GraphViewChange OnGraphViewChanged(GraphViewChange graphViewChange)
		{
			if (graphViewChange.movedElements != null)
			{
				// ★ 关键修改：Undo 记录的是 Graphs 本身
				Undo.RecordObject(_graph, "Move Nodes");

				foreach (GraphElement element in graphViewChange.movedElements)
				{
					if (element is AsakiNodeView nodeView)
					{
						// 只需要更新内存中的数据
						nodeView.SyncPosition();
					}
				}
				// 标记 Dirty 确保保存
				EditorUtility.SetDirty(_graph);
			}

			if (graphViewChange.edgesToCreate != null)
			{
				foreach (Edge edge in graphViewChange.edgesToCreate)
				{
					AsakiNodeView outputNode = edge.output.node as AsakiNodeView;
					AsakiNodeView inputNode = edge.input.node as AsakiNodeView;
					if (outputNode == null || inputNode == null) continue;

					AsakiEdgeData newEdge = new AsakiEdgeData
					{
						BaseNodeGUID = outputNode.node.GUID,
						BasePortName = (string)edge.output.userData,
						TargetNodeGUID = inputNode.node.GUID,
						TargetPortName = (string)edge.input.userData,
					};
					AsakiGraphIOUtils.AddEdge(_graph, newEdge);
				}
			}

			if (graphViewChange.elementsToRemove != null)
			{
				Undo.IncrementCurrentGroup();
				Undo.SetCurrentGroupName("Delete Elements");
				int undoGroup = Undo.GetCurrentGroup();

				foreach (GraphElement element in graphViewChange.elementsToRemove)
				{
					if (element is AsakiNodeView nodeView)
					{
						// 这里逻辑不变，Utility 内部已经处理了 Undo.RecordObject(_graph)
						AsakiGraphIOUtils.DeleteNode(_graph, nodeView.node);
					}
					else if (element is Edge edge)
					{
						AsakiNodeView outputNode = edge.output.node as AsakiNodeView;
						AsakiNodeView inputNode = edge.input.node as AsakiNodeView;
						if (outputNode != null && inputNode != null)
						{
							AsakiEdgeData edgeToRemove = new AsakiEdgeData
							{
								BaseNodeGUID = outputNode.node.GUID,
								BasePortName = (string)edge.output.userData,
								TargetNodeGUID = inputNode.node.GUID,
								TargetPortName = (string)edge.input.userData,
							};
							AsakiGraphIOUtils.RemoveEdge(_graph, edgeToRemove);
						}
					}
				}
				Undo.CollapseUndoOperations(undoGroup);
			}

			return graphViewChange;
		}

		// ... (GetCompatiblePorts 保持不变) ...
		public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
		{
			var compatiblePorts = new List<Port>();
			ports.ForEach(port =>
			{
				if (startPort != port && startPort.node != port.node && startPort.direction != port.direction)
				{
					compatiblePorts.Add(port);
				}
			});
			return compatiblePorts;
		}
	}
}
