using Asaki.Core.Context;
using Asaki.Core.Context.Resolvers;
using System;
using UnityEditor;
using UnityEngine;

namespace Asaki.Editor.Context
{
	[CustomEditor(typeof(AsakiSceneContext))]
	public class AsakiSceneContextEditor : UnityEditor.Editor
	{
		private SerializedProperty _preconfiguredServicesProp;
		private bool _foldoutRuntime = true;

		private void OnEnable()
		{
			_preconfiguredServicesProp = serializedObject.FindProperty("_preconfiguredServices");
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			AsakiSceneContext context = (AsakiSceneContext)target;

			// =================================================
			// Header
			// =================================================
			DrawHeader();

			// =================================================
			// Configuration Area (Edit Mode)
			// =================================================
			EditorGUILayout.Space(5);
			EditorGUILayout.LabelField("Pre-Configured Services (Pure C#)", EditorStyles.boldLabel);

			// 使用 Unity 原生的 List 绘制，它会自动调用元素上的 [AsakiInterfaceDrawer]
			EditorGUILayout.PropertyField(_preconfiguredServicesProp, true);

			if (_preconfiguredServicesProp.arraySize == 0)
			{
				EditorGUILayout.HelpBox("No local pure C# services configured. \nMonoBehaviours in the scene should use [AsakiInject] to get dependencies.", MessageType.Info);
			}

			serializedObject.ApplyModifiedProperties();

			// =================================================
			// Runtime Debugger (Play Mode Only)
			// =================================================
			if (Application.isPlaying)
			{
				DrawRuntimeDebugger(context);
			}
		}

		private new void DrawHeader()
		{
			EditorGUILayout.BeginVertical(EditorStyles.helpBox);
			EditorGUILayout.LabelField("Asaki Scene Context", EditorStyles.boldLabel);
			EditorGUILayout.LabelField("Scope: Current Scene Only", EditorStyles.miniLabel);
			EditorGUILayout.EndVertical();
		}

		private void DrawRuntimeDebugger(AsakiSceneContext context)
		{
			EditorGUILayout.Space(10);
			EditorGUILayout.LabelField("Runtime Debugger", EditorStyles.boldLabel);

			var services = context.GetRuntimeServices();

			// 绘制统计信息
			EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
			EditorGUILayout.LabelField($"Active Services: {services.Count}");
			// 可以在这里加一个 Repaint 按钮或者自动 Repaint 逻辑
			if (GUILayout.Button("Refresh", GUILayout.Width(60)))
			{
				Repaint();
			}
			EditorGUILayout.EndHorizontal();

			_foldoutRuntime = EditorGUILayout.Foldout(_foldoutRuntime, "Service List", true);
			if (_foldoutRuntime)
			{
				EditorGUI.indentLevel++;
				if (services.Count > 0)
				{
					foreach (var kvp in services)
					{
						DrawServiceEntry(kvp.Key, kvp.Value);
					}
				}
				else
				{
					EditorGUILayout.LabelField("Empty Context");
				}
				EditorGUI.indentLevel--;
			}

			// 强制不断刷新以保持数据最新 (可选，如果性能极其敏感可去掉)
			if (Event.current.type == EventType.Layout)
			{
				Repaint();
			}
		}

		private void DrawServiceEntry(Type type, IAsakiService service)
		{
			EditorGUILayout.BeginVertical(EditorStyles.helpBox);

			// 第一行：类型名 + 实例状态
			EditorGUILayout.BeginHorizontal();
			GUILayout.Label(type.Name, EditorStyles.boldLabel);
			string status = service != null ? "Active" : "Null";
			GUILayout.Label($"[{status}]", EditorStyles.miniLabel, GUILayout.Width(50));
			EditorGUILayout.EndHorizontal();

			// 第二行：具体实现类型
			if (service != null)
			{
				Type implType = service.GetType();
				if (implType != type)
				{
					EditorGUILayout.LabelField($"Impl: {implType.Name}", EditorStyles.miniLabel);
				}

				// 这里可以扩展：如果 service 实现了 ToString，显示一部分内容
				// EditorGUILayout.LabelField(service.ToString(), EditorStyles.wordWrappedMiniLabel);
			}

			EditorGUILayout.EndVertical();
		}
	}
}
