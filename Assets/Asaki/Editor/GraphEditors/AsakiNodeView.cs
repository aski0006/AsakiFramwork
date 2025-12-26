using Asaki.Core;
using Asaki.Core.Graphs;
using Asaki.Editor.GraphEditors;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Asaki.Editor.GraphEditors
{
	public class AsakiNodeView : Node
	{
		public AsakiNodeBase node;
		public List<Port> Inputs = new List<Port>();
		public List<Port> Outputs = new List<Port>();

		// 需要持有 Graphs 的 SO 才能绘制子属性
		private SerializedObject _graphSerializedObject;

		public AsakiNodeView(AsakiNodeBase data, SerializedObject graphSO)
		{
			node = data;
			_graphSerializedObject = graphSO;
			viewDataKey = data.GUID;

			// 设置位置
			style.left = data.Position.x;
			style.top = data.Position.y;

			title = data.Title;

			// 1. 使用缓存生成端口 (O(1) 访问)
			GeneratePortsFromCache();

			// 2. 绘制节点内容 (PropertyField)
			CreateExtension();

			base.expanded = true;
			RefreshExpandedState();
		}

		private void GeneratePortsFromCache()
		{
			// ★ 使用缓存，不再直接反射
			var ports = AsakiGraphTypeCache.GetPorts(node.GetType());

			foreach (PortInfo info in ports)
			{
				Direction direction = info.IsInput ? Direction.Input : Direction.Output;
				Port.Capacity capacity = info.AllowMultiple ? Port.Capacity.Multi : Port.Capacity.Single;

				// 为了通用性，端口类型暂时用 info.DataType 或 typeof(float)
				// GraphView 的类型检查比较弱，主要靠 PortName 匹配
				Port port = InstantiatePort(Orientation.Horizontal, direction, capacity, typeof(float));

				port.portName = info.PortName;
				port.userData = info.PortName;

				if (info.IsInput)
				{
					inputContainer.Add(port);
					Inputs.Add(port);
				}
				else
				{
					outputContainer.Add(port);
					Outputs.Add(port);
				}
			}
		}

		private void CreateExtension()
		{
			if (_graphSerializedObject == null) return;

			_graphSerializedObject.Update();

			SerializedProperty nodesProp = _graphSerializedObject.FindProperty("Nodes");
			if (nodesProp == null || !nodesProp.isArray) return;

			SerializedProperty myProp = null;
			for (int i = 0; i < nodesProp.arraySize; i++)
			{
				SerializedProperty element = nodesProp.GetArrayElementAtIndex(i);
				if (element.managedReferenceValue == node)
				{
					myProp = element;
					break;
				}
			}

			if (myProp != null)
			{
				SerializedProperty iterator = myProp.Copy();
				SerializedProperty endProperty = iterator.GetEndProperty();

				// ★ [New] 获取节点类型，用于检测字段是否有端口属性
				Type nodeType = node.GetType();

				if (iterator.NextVisible(true))
				{
					do
					{
						if (SerializedProperty.EqualContents(iterator, endProperty))
							break;

						// 1. 排除基础字段
						if (iterator.name == "GUID" || iterator.name == "Position" || iterator.name == "ExecutionOrder")
							continue;

						// 2. ★ [修复] 排除端口字段 (Port Fields)
						// 如果绘制这些空结构体或用于连接的字段，会导致 SerializedProperty 偏移量计算错误
						FieldInfo fieldInfo = nodeType.GetField(iterator.name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
						if (fieldInfo != null)
						{
							// 检查是否有 [AsakiNodeInput] 或 [AsakiNodeOutput] 特性
							if (Attribute.IsDefined(fieldInfo, typeof(AsakiNodeInputAttribute)) ||
							    Attribute.IsDefined(fieldInfo, typeof(AsakiNodeOutputAttribute)))
							{
								continue; // 跳过绘制端口字段
							}
						}

						PropertyField field = new PropertyField(iterator.Copy());
						field.Bind(_graphSerializedObject);
						extensionContainer.Add(field);

					} while (iterator.NextVisible(false));
				}
			}
		}

		// 当移动结束时，我们需要同步位置数据
		public void SyncPosition()
		{
			node.Position = GetPosition().position;
		}

		// ================================================================
		// ★ Debug Visuals
		// ================================================================

		private const float HIGHLIGHT_DURATION = 0.5f; // 高亮持续 0.5秒
		private float _highlightTime = 0f;
		private bool _isActive = false;

		public void Highlight()
		{
			_isActive = true;
			_highlightTime = (float)EditorApplication.timeSinceStartup;

			// 立即变色 (例如：明亮的青色边框)
			style.borderLeftColor = new StyleColor(Color.cyan);
			style.borderRightColor = new StyleColor(Color.cyan);
			style.borderTopColor = new StyleColor(Color.cyan);
			style.borderBottomColor = new StyleColor(Color.cyan);
			style.borderLeftWidth = 2f;
			style.borderRightWidth = 2f;
			style.borderTopWidth = 2f;
			style.borderBottomWidth = 2f;

			// 确保每帧刷新以执行 Fade Out
			EditorApplication.update -= UpdateHighlight;
			EditorApplication.update += UpdateHighlight;
		}

		private void UpdateHighlight()
		{
			if (!_isActive) return;

			float elapsed = (float)EditorApplication.timeSinceStartup - _highlightTime;

			if (elapsed > HIGHLIGHT_DURATION)
			{
				// 时间到，恢复原状
				ResetStyle();
				_isActive = false;
				EditorApplication.update -= UpdateHighlight;
			}
		}

		private void ResetStyle()
		{
			// 恢复默认边框 (通常是黑色或灰色)
			style.borderLeftColor = new StyleColor(Color.clear);
			style.borderRightColor = new StyleColor(Color.clear);
			style.borderTopColor = new StyleColor(Color.clear);
			style.borderBottomColor = new StyleColor(Color.clear);
			style.borderLeftWidth = 0f; // 或者恢复到 1f
		}
	}
}
