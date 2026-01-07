using Asaki.Core;
using Asaki.Core.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Asaki.Editor.PropertyDrawers
{
	[CustomPropertyDrawer(typeof(AsakiInterfaceAttribute))]
	public class AsakiInterfaceDrawer : PropertyDrawer
	{
		// 缓存反射结果，避免每次重绘都扫描程序集
		private static Dictionary<Type, List<Type>> _typeCache = new Dictionary<Type, List<Type>>();

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			// 如果折叠展开，计算子属性高度；否则只占一行
			return EditorGUI.GetPropertyHeight(property, true);
		}

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			EditorGUI.BeginProperty(position, label, property);

			// 1. 获取 Attribute 传入的接口类型
			AsakiInterfaceAttribute attr = attribute as AsakiInterfaceAttribute;
			Type interfaceType = attr.InterfaceType;

			// 2. 绘制标题栏和下拉菜单按钮
			// 计算按钮位置 (位于属性标签右侧)
			Rect labelRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

			// 获取当前对象的类型名称
			string currentName = "Null (Empty)";
			if (property.managedReferenceValue != null)
			{
				Type currentType = property.managedReferenceValue.GetType();
				currentName = currentType.Name;
			}

			// 绘制标签与 Foldout
			property.isExpanded = EditorGUI.Foldout(new Rect(position.x, position.y, position.width - 20, EditorGUIUtility.singleLineHeight), property.isExpanded, label, true);

			// 绘制类型选择按钮 (放在最右侧)
			Rect buttonRect = new Rect(position.x + EditorGUIUtility.labelWidth + 20, position.y, position.width - EditorGUIUtility.labelWidth - 20, EditorGUIUtility.singleLineHeight);
			if (GUI.Button(buttonRect, currentName, EditorStyles.popup))
			{
				ShowTypePopup(property, interfaceType);
			}

			// 3. 如果展开且不为空，绘制对象内容的默认属性面板
			if (property.isExpanded)
			{
				if (property.managedReferenceValue != null)
				{
					EditorGUI.indentLevel++;
					// 使用 Iterate 绘制所有子字段
					SerializedProperty iterator = property.Copy();
					SerializedProperty endProperty = iterator.GetEndProperty();

					// 下移一行开始绘制内容
					Rect contentRect = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight + 2, position.width, position.height);

					// 利用 Unity 原生绘制器绘制内部属性
					iterator.NextVisible(true);
					while (SerializedProperty.EqualContents(iterator, endProperty) == false)
					{
						float h = EditorGUI.GetPropertyHeight(iterator);
						contentRect.height = h;
						EditorGUI.PropertyField(contentRect, iterator, true);
						contentRect.y += h + 2;
						iterator.NextVisible(false);
					}
					EditorGUI.indentLevel--;
				}
				else
				{
					// 如果是空，提示用户选择类型
					Rect helpRect = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight, position.width, EditorGUIUtility.singleLineHeight);
					EditorGUI.HelpBox(helpRect, "Select a type implementation from the dropdown.", MessageType.Info);
				}
			}

			EditorGUI.EndProperty();
		}

		private void ShowTypePopup(SerializedProperty property, Type interfaceType)
		{
			GenericMenu menu = new GenericMenu();

			// 添加 "Null" 选项
			menu.AddItem(new GUIContent("None"), property.managedReferenceValue == null, () =>
			{
				property.managedReferenceValue = null;
				property.serializedObject.ApplyModifiedProperties();
			});

			// 获取所有实现该接口的非抽象类
			var types = GetImplementations(interfaceType);

			foreach (Type type in types)
			{
				menu.AddItem(new GUIContent(type.Name),
					property.managedReferenceValue != null && property.managedReferenceValue.GetType() == type,
					() =>
					{
						// 实例化并赋值给 managedReferenceValue
						// [注意] 这里必须使用 Activator.CreateInstance 且类必须有无参构造
						object newInstance = Activator.CreateInstance(type);
						property.managedReferenceValue = newInstance;
						property.serializedObject.ApplyModifiedProperties();
					});
			}

			menu.ShowAsContext();
		}

		// 获取接口实现类 (带缓存)
		private List<Type> GetImplementations(Type interfaceType)
		{
			if (_typeCache.TryGetValue(interfaceType, out var cached))
			{
				return cached;
			}

			// 扫描所有程序集 (Unity TypeCache 在 Editor 下更快，但为了兼容性这里用标准反射)
			var types = AppDomain.CurrentDomain.GetAssemblies()
			                     .SelectMany(a => a.GetTypes())
			                     .Where(t => interfaceType.IsAssignableFrom(t) && t.IsClass && !t.IsAbstract)
			                     .ToList();

			_typeCache[interfaceType] = types;
			return types;
		}
	}
}
