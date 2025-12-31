using System;
using System.Collections.Generic;
using Asaki.Core.Graphs;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEditor.VersionControl;
using UnityEngine;
using UnityEngine.UIElements;

namespace Asaki.Editor.GraphEditors
{
	/// <summary>
	/// 负责管理 GraphView 中的 Blackboard 面板 (UI Toolkit)
	/// </summary>
	public class AsakiBlackboardProvider
	{
		private const string AssetPath = "Assets/Resources/Asaki/Configuration/GlobalBlackboard.asset";
		public Blackboard Blackboard { get; private set; }

		private readonly AsakiGraphView _graphView;
		private readonly AsakiGraphBase _graphAsset;
		private readonly SerializedObject _serializedGraph;
		private AsakiGlobalBlackboardAsset _globalAsset;
		public AsakiBlackboardProvider(AsakiGraphView graphView, AsakiGraphBase graphAsset, SerializedObject serializedGraph)
		{
			_graphView = graphView;
			_graphAsset = graphAsset;
			_serializedGraph = serializedGraph;
			LoadGlobalBlackboard();
			InitializeBlackboard();
			RefreshBlackboard();
		}


		private void LoadGlobalBlackboard()
		{
			string globalAssetPath = AssetPath;
			_globalAsset = AssetDatabase.LoadAssetAtPath<AsakiGlobalBlackboardAsset>(globalAssetPath);

			if (!_globalAsset)
			{
				Debug.LogWarning("[AsakiBlackboardProvider] Global Blackboard not found. Please create it via menu.");
			}
		}

		private void InitializeBlackboard()
		{
			// 1. 创建 Blackboard UI 控件
			Blackboard = new Blackboard(_graphView)
			{
				title = "Blackboard",
				subTitle = "Variables",
				// 2. 配置添加按钮 (Add Item)
				addItemRequested = _ =>
				{
					GenericMenu menu = new GenericMenu();
					foreach (AsakiBlackboardPropertyType type in Enum.GetValues(typeof(AsakiBlackboardPropertyType)))
					{
						menu.AddItem(new GUIContent(type.ToString()), false, () => AddVariable(type));
					}
					menu.ShowAsContext();
				},
				// 3. 配置重命名与移动
				editTextRequested = (bb, element, newName) =>
				{
					BlackboardField field = (BlackboardField)element;
					AsakiVariableDef variable = (AsakiVariableDef)field.userData;

					if (string.IsNullOrEmpty(newName) || newName == variable.Name) return;
					if (_globalAsset?.GlobalVariables.Contains(variable) == true)
					{
						EditorUtility.DisplayDialog("Error", "Cannot edit global variables here. Use Global Blackboard Editor.", "OK");
						return;
					}
					// 简单的重名检查
					if (_graphAsset.Variables.Exists(v => v.Name == newName))
					{
						EditorUtility.DisplayDialog("Error", "Variable name already exists!", "OK");
						return;
					}

					Undo.RecordObject(_graphAsset, "Rename Variable");
					variable.Name = newName;
					field.text = newName;
					EditorUtility.SetDirty(_graphAsset);
				},
			};

			// 4. 设置位置 (默认左上角，稍微偏移)
			Blackboard.SetPosition(new Rect(10, 30, 200, 300));

			// 5. 添加到 GraphView
			_graphView.Add(Blackboard);

			// 6. 开启 Scrollable
			Blackboard.scrollable = true;

			Blackboard.RegisterCallback<KeyDownEvent>(evt =>
			{
				if (evt.keyCode == KeyCode.Delete || evt.keyCode == KeyCode.Backspace)
				{
					DeleteSelectedVariables();
					evt.StopImmediatePropagation();
				}
			});

			Blackboard.addItemRequested = _ =>
			{
				GenericMenu menu = new GenericMenu();
				foreach (AsakiBlackboardPropertyType type in Enum.GetValues(typeof(AsakiBlackboardPropertyType)))
				{
					menu.AddItem(new GUIContent(type.ToString()), false, () => AddVariable(type));
				}
				menu.ShowAsContext();
			};

			Blackboard.RegisterCallback<ContextualMenuPopulateEvent>(evt =>
			{
				if (Blackboard.selection.Count > 0)
				{
					evt.menu.AppendAction("Promote to Global", OnPromoteToGlobal, DropdownMenuAction.Status.Normal);
				}
			});
		}

		private void OnPromoteToGlobal(DropdownMenuAction action)
		{
			var selection = Blackboard.selection;
			if (selection == null || selection.Count == 0) return;

			// 加载全局资产
			string globalAssetPath = AssetPath;
			var globalAsset = AssetDatabase.LoadAssetAtPath<AsakiGlobalBlackboardAsset>(globalAssetPath);
			if (!globalAsset)
			{
				EditorUtility.DisplayDialog("Error", "Global Blackboard asset not found. Please open Global Blackboard Editor first.", "OK");
				return;
			}

			Undo.RecordObject(_graphAsset, "Promote Variables to Global");
			Undo.RecordObject(globalAsset, "Promote Variables to Global");

			bool promotedAny = false;

			foreach (ISelectable element in selection)
			{
				if (element is BlackboardField { userData: AsakiVariableDef localVar })
				{
					// 1. 创建或获取全局变量
					var globalVar = globalAsset.GetOrCreateVariable(localVar.Name, localVar.Type);

					// 2. 复制当前值到全局
					CopyVariableValue(localVar, globalVar);

					// 3. 从局部黑板移除（可选：保留引用）
					_graphAsset.Variables.Remove(localVar);
					promotedAny = true;
				}
			}

			if (promotedAny)
			{
				EditorUtility.SetDirty(_graphAsset);
				EditorUtility.SetDirty(globalAsset);
				RefreshBlackboard();
				AssetDatabase.SaveAssets();
				EditorUtility.DisplayDialog("Success", "Variables promoted to Global Blackboard!", "OK");
			}
		}

		private void CopyVariableValue(AsakiVariableDef source, AsakiVariableDef dest)
		{
			dest.Type = source.Type;
			switch (source.Type)
			{
				case AsakiBlackboardPropertyType.Int:        dest.IntVal = source.IntVal; break;
				case AsakiBlackboardPropertyType.Float:      dest.FloatVal = source.FloatVal; break;
				case AsakiBlackboardPropertyType.Bool:       dest.BoolVal = source.BoolVal; break;
				case AsakiBlackboardPropertyType.String:     dest.StringVal = source.StringVal; break;
				case AsakiBlackboardPropertyType.Vector3:    dest.Vector3Val = source.Vector3Val; break;
				case AsakiBlackboardPropertyType.Vector2:    dest.Vector2Val = source.Vector2Val; break;
				case AsakiBlackboardPropertyType.Vector3Int: dest.Vector3IntVal = source.Vector3IntVal; break;
				case AsakiBlackboardPropertyType.Vector2Int: dest.Vector2IntVal = source.Vector2IntVal; break;
				case AsakiBlackboardPropertyType.Color:      dest.ColorVal = source.ColorVal; break;
			}
		}


		private void DeleteSelectedVariables()
		{
			// 1. 获取当前选中的黑板元素
			var selection = Blackboard.selection;
			if (selection == null || selection.Count == 0) return;

			// 2. 记录 Undo (支持撤销)
			Undo.RecordObject(_graphAsset, "Delete Variables");

			bool changed = false;

			// 3. 倒序遍历删除（防止索引错位，虽然这里是 foreach，但在 List Remove 时需小心）
			// 这里我们先收集要删除的变量，再统一移除
			var varsToRemove = new List<AsakiVariableDef>();

			foreach (ISelectable element in selection)
			{
				// 只有选中的是 BlackboardField (具体变量行) 时才删除
				if (element is BlackboardField field && field.userData is AsakiVariableDef def)
				{
					varsToRemove.Add(def);
				}
			}

			foreach (AsakiVariableDef def in varsToRemove)
			{
				_graphAsset.Variables.Remove(def);
				changed = true;
			}

			// 4. 如果有数据变动，刷新 UI 并标记 Dirty
			if (changed)
			{
				// 刷新黑板 UI
				RefreshBlackboard();

				// 标记 Asset 已修改
				EditorUtility.SetDirty(_graphAsset);
			}
		}

		private void AddVariable(AsakiBlackboardPropertyType type)
		{
			Undo.RecordObject(_graphAsset, "Add Variable");

			// 生成唯一名字
			string name = "New" + type;
			int i = 1;
			while (_graphAsset.Variables.Exists(v => v.Name == name))
			{
				name = $"New{type}_{i++}";
			}

			AsakiVariableDef newVar = new AsakiVariableDef { Name = name, Type = type };
			_graphAsset.Variables.Add(newVar);

			EditorUtility.SetDirty(_graphAsset);

			// 刷新 UI
			RefreshBlackboard();
		}

		public void RefreshBlackboard()
		{
			Blackboard.Clear();

			// 1. ★ 添加全局变量节（只读，灰色显示）
			if (_globalAsset != null && _globalAsset.GlobalVariables.Count > 0)
			{
				var globalSection = new BlackboardSection { title = "Global Variables (Read-Only)",
					style =
					{
						backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f)),
					},
				};
				Blackboard.Add(globalSection);

				foreach (var globalVar in _globalAsset.GlobalVariables)
				{
					var field = CreateGlobalVariableField(globalVar);
					globalSection.Add(field);
				}
			}

			// 2. 添加局部变量节（可编辑）
			var localSection = new BlackboardSection { title = "Local Variables" };
			Blackboard.Add(localSection);

			foreach (var variable in _graphAsset.Variables)
			{
				var field = CreateLocalVariableField(variable);
				localSection.Add(field);
			}
		}

		private VisualElement CreateGlobalVariableField(AsakiVariableDef globalVar)
		{
			var field = new BlackboardField
			{
				text = globalVar.Name,
				typeText = $"[Global] {globalVar.Type}", // [G] 标记为全局
				userData = globalVar,
				style =
				{
					opacity = 0.6f,                             // 半透明
					unityFontStyleAndWeight = FontStyle.Italic, // 斜体
					color = new StyleColor(Color.gray),         // 灰色文字
				},
			};

			EnableDrag(field, globalVar, isGlobal: true);

			var row = new BlackboardRow(field, CreateGlobalValueView(globalVar));
			return row;
		}

		private VisualElement CreateLocalVariableField(AsakiVariableDef variable)
		{
			var field = new BlackboardField
			{
				text = variable.Name,
				typeText = variable.Type.ToString(),
				userData = variable,
			};

			EnableDrag(field, variable, isGlobal: false);

			var row = new BlackboardRow(field, CreateValueView(variable));
			return row;
		}

		private VisualElement CreateGlobalValueView(AsakiVariableDef variable)
		{
			var container = new VisualElement();
			container.style.paddingLeft = 10;
			container.style.opacity = 0.5f; // 半透明，表示只读

			// 创建只读字段
			IMGUIContainer imgui = new IMGUIContainer(() =>
			{
				// 显示值，但不允许编辑
				EditorGUILayout.LabelField(GetVariableDisplayString(variable), EditorStyles.label);
			});
			container.Add(imgui);

			return container;
		}


		private string GetVariableDisplayString(AsakiVariableDef variable)
		{
			return variable.Type switch
			       {
				       AsakiBlackboardPropertyType.Int => variable.IntVal.ToString(),
				       AsakiBlackboardPropertyType.Float => variable.FloatVal.ToString("F2"),
				       AsakiBlackboardPropertyType.Bool => variable.BoolVal.ToString(),
				       AsakiBlackboardPropertyType.String => $"\"{variable.StringVal}\"",
				       _ => variable.Type.ToString()
			       };
		}
		private void EnableDrag(VisualElement element, AsakiVariableDef variable, bool isGlobal = false)
		{
			Vector2 _mouseDownPos = Vector2.zero;
			bool _readyToDrag = false;

			element.RegisterCallback<MouseDownEvent>(evt =>
			{
				if (evt.button == 0)
				{
					_mouseDownPos = evt.localMousePosition;
					_readyToDrag = true;
				}
			});

			element.RegisterCallback<MouseMoveEvent>(evt =>
			{
				if (!_readyToDrag || evt.pressedButtons != 1) return;

				// 防止重复拖拽
				if (DragAndDrop.GetGenericData("AsakiVariable") != null)
				{
					_readyToDrag = false;
					return;
				}

				// 距离阈值检测
				if (Vector2.Distance(evt.localMousePosition, _mouseDownPos) > 5f)
				{
					_readyToDrag = false;

					// ★★★ 关键修改：包装数据，标记是否全局
					var dragData = new DragVariableData(variable, isGlobal);
            
					DragAndDrop.PrepareStartDrag();
					DragAndDrop.SetGenericData("AsakiVariable", dragData);  // 存储包装类
					DragAndDrop.StartDrag($"Dragging {(isGlobal ? "[G]" : "")} {variable.Name}");

					evt.StopImmediatePropagation();
				}
			});

			element.RegisterCallback<MouseUpEvent>(evt => _readyToDrag = false);
			element.RegisterCallback<MouseLeaveEvent>(evt => _readyToDrag = false);
		}

		// 创建行内的小编辑器 (修改默认值)
		private VisualElement CreateValueView(AsakiVariableDef variable)
		{
			// 这里我们使用简易的 IMGUIContainer 或者 UI Elements
			// 为了简单，直接根据类型创建对应的 Field
			VisualElement container = new VisualElement();
			container.style.paddingLeft = 10;

			switch (variable.Type)
			{
				case AsakiBlackboardPropertyType.Int:
					IntegerField intField = new IntegerField { value = variable.IntVal };
					intField.RegisterValueChangedCallback(evt =>
					{
						variable.IntVal = evt.newValue;
						EditorUtility.SetDirty(_graphAsset);
					});
					container.Add(intField);
					break;
				case AsakiBlackboardPropertyType.Float:
					FloatField floatField = new FloatField { value = variable.FloatVal };
					floatField.RegisterValueChangedCallback(evt =>
					{
						variable.FloatVal = evt.newValue;
						EditorUtility.SetDirty(_graphAsset);
					});
					container.Add(floatField);
					break;
				case AsakiBlackboardPropertyType.Bool:
					Toggle boolField = new Toggle { value = variable.BoolVal };
					boolField.RegisterValueChangedCallback(evt =>
					{
						variable.BoolVal = evt.newValue;
						EditorUtility.SetDirty(_graphAsset);
					});
					container.Add(boolField);
					break;
				case AsakiBlackboardPropertyType.String:
					TextField strField = new TextField { value = variable.StringVal };
					strField.RegisterValueChangedCallback(evt =>
					{
						variable.StringVal = evt.newValue;
						EditorUtility.SetDirty(_graphAsset);
					});
					container.Add(strField);
					break;
				case AsakiBlackboardPropertyType.Vector3:
					Vector3Field vec3Field = new Vector3Field { value = variable.Vector3Val };
					vec3Field.RegisterValueChangedCallback(evt =>
					{
						variable.Vector3Val = evt.newValue;
						EditorUtility.SetDirty(_graphAsset);
					});
					container.Add(vec3Field);
					break;
				case AsakiBlackboardPropertyType.Vector2:
					Vector2Field vec2Field = new Vector2Field { value = variable.Vector2Val };
					vec2Field.RegisterValueChangedCallback(evt =>
					{
						variable.Vector2Val = evt.newValue;
						EditorUtility.SetDirty(_graphAsset);
					});
					break;
				case AsakiBlackboardPropertyType.Vector3Int:
					Vector3IntField vec3IntField = new Vector3IntField { value = variable.Vector3IntVal };
					vec3IntField.RegisterValueChangedCallback(evt =>
					{
						variable.Vector3IntVal = evt.newValue;
						EditorUtility.SetDirty(_graphAsset);
					});
					break;
				case AsakiBlackboardPropertyType.Vector2Int:
					Vector2IntField vec2IntField = new Vector2IntField { value = variable.Vector2IntVal };
					vec2IntField.RegisterValueChangedCallback(evt =>
					{
						variable.Vector2IntVal = evt.newValue;
						EditorUtility.SetDirty(_graphAsset);
					});
					break;
				case AsakiBlackboardPropertyType.Color:
					ColorField colorField = new ColorField { value = variable.ColorVal };
					colorField.RegisterValueChangedCallback(evt =>
					{
						variable.ColorVal = evt.newValue;
						EditorUtility.SetDirty(_graphAsset);
					});
					break;
			}
			return container;
		}
	}
}
