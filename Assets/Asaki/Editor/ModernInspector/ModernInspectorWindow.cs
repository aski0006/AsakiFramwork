using Asaki.Editor.Utilities.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Asaki.Editor.ModernInspector
{
    public class ModernInspectorWindow : EditorWindow
    {
        // UI 元素引用
        private VisualElement root;
        private VisualElement gameObjectHeader;
        private VisualElement tabContainer;
        private ScrollView detailScrollView;
        private VisualElement detailPanel;
        private VisualElement noSelectionMessage;
        private Button addComponentButton;

        // 数据
        private GameObject selectedGameObject;
        private Component[] components;
        private HashSet<int> activeTabIndices = new HashSet<int>();
        private List<Toggle> tabToggles = new List<Toggle>();
        private Toggle allTabToggle;
        private bool isAllTabActive = false;

        // 缓存
        private Dictionary<Component, UnityEditor.Editor> editorCache = new Dictionary<Component, UnityEditor.Editor>();
        
        // 拖拽相关
        private Component draggingComponent;
        private VisualElement dragIndicator;

        [MenuItem("Asaki/Tools/Modern Inspector")]
        public static void ShowWindow()
        {
            ModernInspectorWindow wnd = GetWindow<ModernInspectorWindow>();
            wnd.titleContent = new GUIContent("Modern Inspector");
            wnd.minSize = new Vector2(300, 400);
        }

        public void CreateGUI()
        {
            root = rootVisualElement;

            // 加载 UXML
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Assets/Asaki/Editor/ModernInspector/ModernInspectorWindow.uxml");

            if (visualTree != null)
            {
                visualTree.CloneTree(root);
            }
            else
            {
                CreateUIFallback();
            }

            // 加载 USS
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Assets/Asaki/Editor/ModernInspector/ModernInspectorWindow.uss");

            if (styleSheet != null)
            {
                root.styleSheets.Add(styleSheet);
            }

            // 获取 UI 元素引用
            gameObjectHeader = root.Q<VisualElement>("gameobject-header");
            tabContainer = root.Q<VisualElement>("tab-container");
            detailScrollView = root.Q<ScrollView>("detail-scroll-view");
            noSelectionMessage = root.Q<VisualElement>("no-selection");
            addComponentButton = root.Q<Button>("add-component-button");

            // 如果 UXML 中没有 ScrollView，创建一个
            if (detailScrollView == null)
            {
                var oldDetailPanel = root.Q<VisualElement>("detail-panel");
                if (oldDetailPanel != null)
                {
                    var parent = oldDetailPanel.parent;
                    parent.Remove(oldDetailPanel);

                    detailScrollView = new ScrollView(ScrollViewMode.Vertical);
                    detailScrollView.name = "detail-scroll-view";
                    detailScrollView.AddToClassList("detail-scroll-view");
                    parent.Add(detailScrollView);
                }
            }

            // 创建详细面板容器
            if (detailScrollView != null)
            {
                detailPanel = new VisualElement();
                detailPanel.name = "detail-panel";
                detailPanel.AddToClassList("detail-panel");
                detailScrollView.Add(detailPanel);
            }

            // 创建拖拽指示器
            CreateDragIndicator();

            // 设置添加组件按钮
            SetupAddComponentButton();

            // 初始化
            UpdateInspector();
        }

        private void OnEnable()
        {
            Selection.selectionChanged += OnSelectionChanged;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            ClearEditorCache();
        }

        private void OnSelectionChanged()
        {
            UpdateInspector();
        }

        private void OnHierarchyChanged()
        {
            if (selectedGameObject == null && Selection.activeGameObject != null)
            {
                UpdateInspector();
            }
        }

        // =========================================================
        // 拖拽指示器
        // =========================================================

        private void CreateDragIndicator()
        {
            dragIndicator = new VisualElement();
            dragIndicator.name = "drag-indicator";
            dragIndicator.AddToClassList("drag-indicator");
            dragIndicator.style.display = DisplayStyle.None;
            dragIndicator.style.height = 2;
            dragIndicator.style.backgroundColor = new Color(0.3f, 0.6f, 1f); // 蓝色指示线
        }

        // =========================================================
        // 添加组件按钮
        // =========================================================

        private void SetupAddComponentButton()
        {
            if (addComponentButton == null) return;

            addComponentButton.text = "Add Component";
            addComponentButton.clicked += ShowAddComponentMenu;

            // 设置图标
            var icon = new Image();
            icon.image = EditorGUIUtility.IconContent("Toolbar Plus").image;
            icon.AddToClassList("add-component-icon");
            addComponentButton.Insert(0, icon);
        }

        private void ShowAddComponentMenu()
        {
            if (selectedGameObject == null) return;

            var addComponentWindow = ScriptableObject.CreateInstance<AddComponentWindow>();
            addComponentWindow.Show(selectedGameObject, (component) =>
            {
                if (component != null)
                {
                    Undo.RegisterCreatedObjectUndo(component, "Add Component");
                    UpdateInspector();
                }
            });
        }

        private void UpdateInspector()
        {
            selectedGameObject = Selection.activeGameObject;

            if (selectedGameObject == null)
            {
                ShowNoSelection();
                return;
            }

            // 显示/隐藏 UI 元素
            if (noSelectionMessage != null)
                noSelectionMessage.style.display = DisplayStyle.None;

            if (gameObjectHeader != null)
                gameObjectHeader.style.display = DisplayStyle.Flex;

            if (tabContainer != null)
                tabContainer.style.display = DisplayStyle.Flex;

            if (detailScrollView != null)
                detailScrollView.style.display = DisplayStyle.Flex;

            if (addComponentButton != null)
                addComponentButton.style.display = DisplayStyle.Flex;

            // 更新内容
            UpdateGameObjectHeader();
            components = selectedGameObject.GetComponents<Component>();
            RebuildTabs();
            SelectDefaultTab();
        }

        private void ShowNoSelection()
        {
            if (noSelectionMessage != null)
                noSelectionMessage.style.display = DisplayStyle.Flex;

            if (gameObjectHeader != null)
                gameObjectHeader.style.display = DisplayStyle.None;

            if (tabContainer != null)
                tabContainer.style.display = DisplayStyle.None;

            if (detailScrollView != null)
                detailScrollView.style.display = DisplayStyle.None;

            if (addComponentButton != null)
                addComponentButton.style.display = DisplayStyle.None;

            if (detailPanel != null)
                detailPanel.Clear();
        }

        private void UpdateGameObjectHeader()
        {
            if (gameObjectHeader == null) return;

            gameObjectHeader.Clear();

            // GameObject 名称和图标
            var headerContainer = new VisualElement();
            headerContainer.AddToClassList("header-container");

            var icon = new Image();
            icon.image = EditorGUIUtility.ObjectContent(selectedGameObject, typeof(GameObject)).image;
            icon.AddToClassList("gameobject-icon");

            var nameField = new TextField();
            nameField.value = selectedGameObject.name;
            nameField.AddToClassList("gameobject-name");
            nameField.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(selectedGameObject, "Rename GameObject");
                selectedGameObject.name = evt.newValue;
                EditorUtility.SetDirty(selectedGameObject);
            });

            headerContainer.Add(icon);
            headerContainer.Add(nameField);

            // Static、Tag、Layer 信息
            var infoContainer = new VisualElement();
            infoContainer.AddToClassList("info-container");

            var staticToggle = new Toggle("Static");
            staticToggle.value = selectedGameObject.isStatic;
            staticToggle.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(selectedGameObject, "Change Static");
                selectedGameObject.isStatic = evt.newValue;
            });

            var tagField = new TagField("Tag");
            tagField.value = selectedGameObject.tag;
            tagField.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(selectedGameObject, "Change Tag");
                selectedGameObject.tag = evt.newValue;
            });

            var layerField = new LayerField("Layer");
            layerField.value = selectedGameObject.layer;
            layerField.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(selectedGameObject, "Change Layer");
                selectedGameObject.layer = evt.newValue;
            });

            infoContainer.Add(staticToggle);
            infoContainer.Add(tagField);
            infoContainer.Add(layerField);

            gameObjectHeader.Add(headerContainer);
            gameObjectHeader.Add(infoContainer);
        }

        private void RebuildTabs()
        {
            if (tabContainer == null) return;

            tabContainer.Clear();
            tabToggles.Clear();
            activeTabIndices.Clear();
            isAllTabActive = false;

            tabContainer.style.flexDirection = FlexDirection.Row;
            tabContainer.style.flexWrap = Wrap.NoWrap;

            allTabToggle = CreateAllTabToggle();
            tabContainer.Add(allTabToggle);

            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] == null) continue;

                int index = i;
                Toggle tabToggle = CreateTabToggle(components[i], index);
                tabToggles.Add(tabToggle);
                tabContainer.Add(tabToggle);
            }
        }

        private Toggle CreateAllTabToggle()
        {
            var toggle = new Toggle();
            toggle.AddToClassList("tab-toggle");
            toggle.AddToClassList("tab-toggle-all");

            var icon = new Image();
            icon.image = EditorGUIUtility.IconContent("SceneViewTools").image;
            icon.AddToClassList("tab-icon");

            var label = new Label("All");
            label.AddToClassList("tab-label");

            toggle.Add(icon);
            toggle.Add(label);

            toggle.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue)
                {
                    SelectAllTab();
                }
                else
                {
                    DeselectAllTab();
                }
            });

            return toggle;
        }

        private Toggle CreateTabToggle(Component component, int index)
        {
            var toggle = new Toggle();
            toggle.AddToClassList("tab-toggle");

            var icon = new Image();
            icon.image = EditorGUIUtility.ObjectContent(component, component.GetType()).image;
            icon.AddToClassList("tab-icon");

            var label = new Label(component.GetType().Name);
            label.AddToClassList("tab-label");

            toggle.Add(icon);
            toggle.Add(label);

            toggle.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue)
                {
                    if (allTabToggle != null)
                    {
                        allTabToggle.SetValueWithoutNotify(false);
                        allTabToggle.RemoveFromClassList("tab-toggle-active");
                        isAllTabActive = false;
                    }

                    activeTabIndices.Add(index);
                    toggle.AddToClassList("tab-toggle-active");
                }
                else
                {
                    activeTabIndices.Remove(index);
                    toggle.RemoveFromClassList("tab-toggle-active");
                }

                RefreshDetailPanel();
            });

            return toggle;
        }

        private void SelectAllTab()
        {
            activeTabIndices.Clear();
            isAllTabActive = true;

            if (allTabToggle != null)
            {
                allTabToggle.SetValueWithoutNotify(true);
                allTabToggle.AddToClassList("tab-toggle-active");
            }

            foreach (var toggle in tabToggles)
            {
                toggle.SetValueWithoutNotify(false);
                toggle.RemoveFromClassList("tab-toggle-active");
            }

            RefreshDetailPanel();
        }

        private void DeselectAllTab()
        {
            isAllTabActive = false;

            if (allTabToggle != null)
            {
                allTabToggle.SetValueWithoutNotify(false);
                allTabToggle.RemoveFromClassList("tab-toggle-active");
            }

            SelectDefaultTab();
        }

        private void SelectDefaultTab()
        {
            activeTabIndices.Clear();
            isAllTabActive = false;

            if (allTabToggle != null)
            {
                allTabToggle.SetValueWithoutNotify(false);
                allTabToggle.RemoveFromClassList("tab-toggle-active");
            }

            foreach (var toggle in tabToggles)
            {
                toggle.SetValueWithoutNotify(false);
                toggle.RemoveFromClassList("tab-toggle-active");
            }

            int defaultIndex = -1;
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] == null) continue;

                var type = components[i].GetType();
                if (type == typeof(RectTransform))
                {
                    defaultIndex = i;
                    break;
                }
                else if (type == typeof(Transform))
                {
                    defaultIndex = i;
                }
            }

            if (defaultIndex >= 0 && defaultIndex < tabToggles.Count)
            {
                activeTabIndices.Add(defaultIndex);
                tabToggles[defaultIndex].SetValueWithoutNotify(true);
                tabToggles[defaultIndex].AddToClassList("tab-toggle-active");
            }

            RefreshDetailPanel();
        }

        private void RefreshDetailPanel()
        {
            if (detailPanel == null) return;

            detailPanel.Clear();

            if (isAllTabActive)
            {
                RenderAllComponents();
                return;
            }

            if (activeTabIndices.Count == 0)
            {
                ClearDetailPanel();
                return;
            }

            var sortedIndices = activeTabIndices.OrderBy(i => i).ToList();
            foreach (var index in sortedIndices)
            {
                if (index >= 0 && index < components.Length && components[index] != null)
                {
                    RenderComponentDetail(components[index]);
                }
            }
        }

        private void RenderAllComponents()
        {
            if (detailPanel == null || components == null) return;

            foreach (var component in components)
            {
                if (component == null) continue;
                RenderComponentDetail(component);
            }
        }

        // =========================================================
        // 渲染组件详情（增强版）
        // =========================================================

        private void RenderComponentDetail(Component component)
        {
            if (detailPanel == null || component == null) return;

            if (!editorCache.TryGetValue(component, out UnityEditor.Editor editor) || editor == null)
            {
                editor = UnityEditor.Editor.CreateEditor(component);
                editorCache[component] = editor;
            }

            var componentContainer = new VisualElement();
            componentContainer.AddToClassList("component-container");
            componentContainer.userData = component; // 存储组件引用用于拖拽

            var header = CreateComponentHeader(component, editor, componentContainer);
            componentContainer.Add(header);

            var inspectorContainer = new IMGUIContainer(() =>
            {
                if (editor != null && editor.target != null)
                {
                    EditorGUILayout.BeginVertical();
                    editor.OnInspectorGUI();
                    EditorGUILayout.EndVertical();
                }
            });
            inspectorContainer.name = "inspector-container";
            inspectorContainer.AddToClassList("inspector-container");

            componentContainer.Add(inspectorContainer);

            // ✅ 注册拖拽事件
            RegisterDragAndDropEvents(header, componentContainer, component);

            detailPanel.Add(componentContainer);
        }

        /// <summary>
        /// 创建组件 Header（带右键菜单）
        /// </summary>
        private VisualElement CreateComponentHeader(Component component, UnityEditor.Editor editor, VisualElement componentContainer)
        {
            var header = new VisualElement();
            header.name = "component-header";
            header.AddToClassList("component-header");

            var headerContent = new VisualElement();
            headerContent.AddToClassList("component-header-content");
            headerContent.style.flexDirection = FlexDirection.Row;
            headerContent.style.alignItems = Align.Center;

            // ✅ 启用/禁用指示器（绿色/红色圆点）
            if (SupportsEnableToggle(component))
            {
                var enableIndicator = new VisualElement();
                enableIndicator.AddToClassList("component-enable-indicator");
                
                bool isEnabled = IsComponentEnabled(component);
                enableIndicator.AddToClassList(isEnabled ? "indicator-enabled" : "indicator-disabled");

                // 点击切换启用/禁用
                enableIndicator.RegisterCallback<ClickEvent>(evt =>
                {
                    bool currentEnabled = IsComponentEnabled(component);
                    SetComponentEnabled(component, !currentEnabled);
                    
                    // 更新指示器颜色
                    enableIndicator.RemoveFromClassList("indicator-enabled");
                    enableIndicator.RemoveFromClassList("indicator-disabled");
                    enableIndicator.AddToClassList(!currentEnabled ? "indicator-enabled" : "indicator-disabled");
                    
                    editor.Repaint();
                });

                headerContent.Add(enableIndicator);
            }

            var icon = new Image();
            icon.image = EditorGUIUtility.ObjectContent(component, component.GetType()).image;
            icon.AddToClassList("component-icon");
            headerContent.Add(icon);

            var label = new Label(ObjectNames.NicifyVariableName(component.GetType().Name));
            label.AddToClassList("component-label");
            label.style.flexGrow = 1;
            headerContent.Add(label);

            var foldoutToggle = new Toggle();
            foldoutToggle.AddToClassList("component-foldout-toggle");
            foldoutToggle.value = true;

            var foldoutIcon = new Image();
            foldoutIcon.image = EditorGUIUtility.IconContent("IN foldout").image;
            foldoutIcon.AddToClassList("foldout-icon");
            foldoutToggle.Add(foldoutIcon);

            headerContent.Add(foldoutToggle);

            header.Add(headerContent);

            // ✅ 修复：在 header 添加后注册右键菜单
            header.RegisterCallback<ContextualMenuPopulateEvent>(evt =>
            {
                ShowComponentContextMenu(evt, component, editor);
            });

            // ✅ 确保右键菜单能触发
            header.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button == 1) // 右键
                {
                    evt.StopPropagation();
                }
            });

            // 折叠功能
            foldoutToggle.RegisterValueChangedCallback(evt =>
            {
                var inspectorContainer = componentContainer.Q<IMGUIContainer>("inspector-container");
                if (inspectorContainer != null)
                {
                    inspectorContainer.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
                }

                foldoutIcon.style.rotate = new Rotate(new Angle(evt.newValue ? 0 : -90));
            });

            return header;
        }

        // =========================================================
        // 拖拽排序
        // =========================================================

        private void RegisterDragAndDropEvents(VisualElement header, VisualElement componentContainer, Component component)
        {
            // Transform 不能拖拽
            if (component is Transform)
                return;

            header.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button == 0) // 左键
                {
                    draggingComponent = component;
                    header.AddToClassList("dragging");
                }
            });

            header.RegisterCallback<MouseMoveEvent>(evt =>
            {
                if (draggingComponent == component && evt.pressedButtons == 1)
                {
                    // 显示拖拽效果
                    header.style.opacity = 0.5f;
                }
            });

            header.RegisterCallback<MouseUpEvent>(evt =>
            {
                if (draggingComponent == component)
                {
                    header.style.opacity = 1f;
                    header.RemoveFromClassList("dragging");
                    draggingComponent = null;
                    
                    // 隐藏拖拽指示器
                    if (dragIndicator.parent != null)
                    {
                        dragIndicator.parent.Remove(dragIndicator);
                    }
                    dragIndicator.style.display = DisplayStyle.None;
                }
            });

            // 拖拽目标区域
            componentContainer.RegisterCallback<MouseEnterEvent>(evt =>
            {
                if (draggingComponent != null && draggingComponent != component)
                {
                    // 显示插入指示器
                    ShowDragIndicator(componentContainer, true);
                }
            });

            componentContainer.RegisterCallback<MouseLeaveEvent>(evt =>
            {
                HideDragIndicator(componentContainer);
            });

            componentContainer.RegisterCallback<MouseUpEvent>(evt =>
            {
                if (draggingComponent != null && draggingComponent != component)
                {
                    // 执行组件移动
                    MoveComponent(draggingComponent, component);
                    
                    draggingComponent = null;
                    HideDragIndicator(componentContainer);
                    
                    // 刷新显示
                    UpdateInspector();
                }
            });
        }

        private void ShowDragIndicator(VisualElement target, bool above)
        {
            if (dragIndicator.parent != null)
            {
                dragIndicator.parent.Remove(dragIndicator);
            }

            dragIndicator.style.display = DisplayStyle.Flex;

            if (above)
            {
                target.parent.Insert(target.parent.IndexOf(target), dragIndicator);
            }
            else
            {
                target.parent.Insert(target.parent.IndexOf(target) + 1, dragIndicator);
            }
        }

        private void HideDragIndicator(VisualElement target)
        {
            if (dragIndicator.parent != null)
            {
                dragIndicator.parent.Remove(dragIndicator);
            }
            dragIndicator.style.display = DisplayStyle.None;
        }

        private void MoveComponent(Component source, Component target)
        {
            if (source == null || target == null || source == target)
                return;

            var allComponents = selectedGameObject.GetComponents<Component>();
            int sourceIndex = Array.IndexOf(allComponents, source);
            int targetIndex = Array.IndexOf(allComponents, target);

            if (sourceIndex < 0 || targetIndex < 0)
                return;

            Undo.RecordObject(selectedGameObject, "Reorder Components");

            // 移动组件
            if (sourceIndex < targetIndex)
            {
                // 向下移动
                for (int i = sourceIndex; i < targetIndex; i++)
                {
                    ComponentUtilityHelper.MoveComponentDown(source);
                }
            }
            else
            {
                // 向上移动
                for (int i = sourceIndex; i > targetIndex; i--)
                {
                    ComponentUtilityHelper.MoveComponentUp(source);
                }
            }

            EditorUtility.SetDirty(selectedGameObject);
        }

        /// <summary>
        /// 显示组件右键菜单
        /// </summary>
        private void ShowComponentContextMenu(ContextualMenuPopulateEvent evt, Component component, UnityEditor.Editor editor)
        {
            evt.menu.AppendAction("Reset", action =>
            {
                Undo.RecordObject(component, "Reset Component");

                var method = component.GetType().GetMethod("Reset",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (method != null)
                {
                    method.Invoke(component, null);
                }
                else
                {
                    ResetComponentToDefault(component);
                }

                editor.Repaint();
                EditorUtility.SetDirty(component);
            });

            evt.menu.AppendSeparator("");

            evt.menu.AppendAction("Remove Component", action =>
            {
                if (EditorUtility.DisplayDialog("Remove Component",
                    $"Are you sure you want to remove {component.GetType().Name}?",
                    "Remove", "Cancel"))
                {
                    Undo.DestroyObjectImmediate(component);
                    UpdateInspector();
                }
            }, action =>
            {
                if (component is Transform)
                    return DropdownMenuAction.Status.Disabled;
                return DropdownMenuAction.Status.Normal;
            });

            evt.menu.AppendSeparator("");

            evt.menu.AppendAction("Move Up", action =>
            {
                if (ComponentUtilityHelper.MoveComponentUp(component))
                {
                    UpdateInspector();
                }
            }, action =>
            {
                return ComponentUtilityHelper.CanMoveComponentUp(component)
                    ? DropdownMenuAction.Status.Normal
                    : DropdownMenuAction.Status.Disabled;
            });

            evt.menu.AppendAction("Move Down", action =>
            {
                if (ComponentUtilityHelper.MoveComponentDown(component))
                {
                    UpdateInspector();
                }
            }, action =>
            {
                return ComponentUtilityHelper.CanMoveComponentDown(component)
                    ? DropdownMenuAction.Status.Normal
                    : DropdownMenuAction.Status.Disabled;
            });

            evt.menu.AppendSeparator("");

            evt.menu.AppendAction("Copy Component", action =>
            {
                ComponentUtilityHelper.CopyComponent(component);
            });

            evt.menu.AppendAction("Paste Component Values", action =>
            {
                Undo.RecordObject(component, "Paste Component Values");
                if (ComponentUtilityHelper.PasteComponentValues(component))
                {
                    editor.Repaint();
                    EditorUtility.SetDirty(component);
                }
            }, action =>
            {
                return ComponentUtilityHelper.CanPasteComponentValues(component)
                    ? DropdownMenuAction.Status.Normal
                    : DropdownMenuAction.Status.Disabled;
            });

            evt.menu.AppendSeparator("");

            if (component is MonoBehaviour)
            {
                evt.menu.AppendAction("Edit Script", action =>
                {
                    var script = MonoScript.FromMonoBehaviour((MonoBehaviour)component);
                    if (script != null)
                    {
                        AssetDatabase.OpenAsset(script);
                    }
                });
            }
        }

        private void ResetComponentToDefault(Component component)
        {
            var serializedObject = new SerializedObject(component);
            var iterator = serializedObject.GetIterator();

            while (iterator.NextVisible(true))
            {
                if (iterator.propertyPath == "m_Script")
                    continue;

                switch (iterator.propertyType)
                {
                    case SerializedPropertyType.Integer:
                    case SerializedPropertyType.LayerMask:
                    case SerializedPropertyType.Character:
                    case SerializedPropertyType.ArraySize:
                    case SerializedPropertyType.FixedBufferSize:
                    case SerializedPropertyType.RenderingLayerMask:
                        iterator.intValue = 0;
                        break;
                    case SerializedPropertyType.Float:
                        iterator.floatValue = 0f;
                        break;
                    case SerializedPropertyType.Boolean:
                        iterator.boolValue = false;
                        break;
                    case SerializedPropertyType.String:
                        iterator.stringValue = "";
                        break;
                    case SerializedPropertyType.Color:
                        iterator.colorValue = Color.white;
                        break;
                    case SerializedPropertyType.Vector2:
                        iterator.vector2Value = Vector2.zero;
                        break;
                    case SerializedPropertyType.Vector3:
                        iterator.vector3Value = Vector3.zero;
                        break;
                    case SerializedPropertyType.Vector4:
                        iterator.vector4Value = Vector4.zero;
                        break;
                    case SerializedPropertyType.ObjectReference:
                    case SerializedPropertyType.ExposedReference:
                    case SerializedPropertyType.ManagedReference:
                        iterator.objectReferenceValue = null;
                        break;
                    case SerializedPropertyType.Enum:
                        iterator.enumValueIndex = 0;
                        break;
                    case SerializedPropertyType.Rect:
                        iterator.rectValue = Rect.zero;
                        break;
                    case SerializedPropertyType.AnimationCurve:
                        iterator.animationCurveValue = new AnimationCurve();
                        break;
                    case SerializedPropertyType.Bounds:
                        iterator.boundsValue = new Bounds();
                        break;
                    case SerializedPropertyType.Gradient:
                        iterator.gradientValue = new Gradient();
                        break;
                    case SerializedPropertyType.Quaternion:
                        iterator.quaternionValue = Quaternion.identity;
                        break;
                    case SerializedPropertyType.Vector2Int:
                        iterator.vector2IntValue = Vector2Int.zero;
                        break;
                    case SerializedPropertyType.Vector3Int:
                        iterator.vector3IntValue = Vector3Int.zero;
                        break;
                    case SerializedPropertyType.RectInt:
                        iterator.rectIntValue = new RectInt();
                        break;
                    case SerializedPropertyType.BoundsInt:
                        iterator.boundsIntValue = new BoundsInt();
                        break;
                    case SerializedPropertyType.Hash128:
                        iterator.vector4Value = Vector4.zero;
                        break;
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        // =========================================================
        // 组件启用/禁用支持
        // =========================================================

        private bool SupportsEnableToggle(Component component)
        {
            var type = component.GetType();
            var property = type.GetProperty("enabled", BindingFlags.Public | BindingFlags.Instance);
            return property != null && property.CanRead && property.CanWrite;
        }

        private bool IsComponentEnabled(Component component)
        {
            var type = component.GetType();
            var property = type.GetProperty("enabled", BindingFlags.Public | BindingFlags.Instance);
            if (property != null)
            {
                return (bool)property.GetValue(component);
            }
            return true;
        }

        private void SetComponentEnabled(Component component, bool enabled)
        {
            var type = component.GetType();
            var property = type.GetProperty("enabled", BindingFlags.Public | BindingFlags.Instance);
            if (property != null)
            {
                Undo.RecordObject(component, enabled ? "Enable Component" : "Disable Component");
                property.SetValue(component, enabled);
                EditorUtility.SetDirty(component);
            }
        }

        private void ClearDetailPanel()
        {
            if (detailPanel == null) return;
            detailPanel.Clear();

            var emptyLabel = new Label("Select a component tab to view details");
            emptyLabel.AddToClassList("empty-message");
            detailPanel.Add(emptyLabel);
        }

        private void ClearEditorCache()
        {
            foreach (var editor in editorCache.Values)
            {
                if (editor != null)
                {
                    DestroyImmediate(editor);
                }
            }
            editorCache.Clear();
        }

        private void CreateUIFallback()
        {
            var container = new VisualElement();
            container.style.flexGrow = 1;

            noSelectionMessage = new VisualElement();
            var noSelectionLabel = new Label("No GameObject selected");
            noSelectionLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            noSelectionLabel.style.fontSize = 14;
            noSelectionLabel.style.color = Color.gray;
            noSelectionMessage.Add(noSelectionLabel);
            noSelectionMessage.style.flexGrow = 1;
            noSelectionMessage.style.justifyContent = Justify.Center;

            gameObjectHeader = new VisualElement();
            gameObjectHeader.style.paddingBottom = 10;
            gameObjectHeader.style.paddingTop = 10;
            gameObjectHeader.style.paddingLeft = 5;
            gameObjectHeader.style.paddingRight = 5;

            tabContainer = new VisualElement();
            tabContainer.style.flexDirection = FlexDirection.Row;
            tabContainer.style.flexWrap = Wrap.NoWrap;
            tabContainer.style.height = 28;
            tabContainer.style.SetPadding(4);
            tabContainer.style.backgroundColor = new Color(0.23f, 0.23f, 0.23f);

            addComponentButton = new Button();
            addComponentButton.text = "Add Component";
            addComponentButton.style.height = 24;
            addComponentButton.style.marginTop = 5;

            detailScrollView = new ScrollView(ScrollViewMode.Vertical);
            detailScrollView.style.flexGrow = 1;

            detailPanel = new VisualElement();
            detailPanel.style.paddingBottom = 10;
            detailPanel.style.paddingTop = 10;
            detailPanel.style.paddingLeft = 10;
            detailPanel.style.paddingRight = 10;

            detailScrollView.Add(detailPanel);

            container.Add(noSelectionMessage);
            container.Add(gameObjectHeader);
            container.Add(tabContainer);
            container.Add(detailScrollView);
            container.Add(addComponentButton);

            root.Add(container);
        }
    }

    // =========================================================
    // 添加组件窗口（保持不变）
    // =========================================================

    public class AddComponentWindow : EditorWindow
    {
        private GameObject targetGameObject;
        private System.Action<Component> onComponentAdded;
        private string searchString = "";
        private Vector2 scrollPosition;

        private List<System.Type> componentTypes;
        private List<System.Type> filteredTypes;

        public void Show(GameObject gameObject, System.Action<Component> callback)
        {
            targetGameObject = gameObject;
            onComponentAdded = callback;

            componentTypes = System.AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => typeof(Component).IsAssignableFrom(type)
                              && !type.IsAbstract
                              && type != typeof(Component)
                              && type != typeof(Transform)
                              && type != typeof(RectTransform))
                .OrderBy(type => type.Name)
                .ToList();

            filteredTypes = new List<System.Type>(componentTypes);

            var window = GetWindow<AddComponentWindow>(true, "Add Component", true);
            window.minSize = new Vector2(300, 400);
            window.maxSize = new Vector2(300, 600);
            window.ShowUtility();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUI.SetNextControlName("SearchField");
            var newSearchString = EditorGUILayout.TextField(searchString, EditorStyles.toolbarSearchField);

            if (newSearchString != searchString)
            {
                searchString = newSearchString;
                FilterComponentTypes();
            }

            if (GUILayout.Button("", EditorStyles.toolbarButton, GUILayout.Width(20)))
            {
                searchString = "";
                FilterComponentTypes();
                GUI.FocusControl("SearchField");
            }
            EditorGUILayout.EndHorizontal();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            foreach (var type in filteredTypes)
            {
                if (GUILayout.Button(ObjectNames.NicifyVariableName(type.Name), EditorStyles.toolbarButton))
                {
                    AddComponent(type);
                    Close();
                }
            }

            EditorGUILayout.EndScrollView();

            if (Event.current.type == EventType.Layout)
            {
                EditorGUI.FocusTextInControl("SearchField");
            }
        }

        private void FilterComponentTypes()
        {
            if (string.IsNullOrEmpty(searchString))
            {
                filteredTypes = new List<System.Type>(componentTypes);
            }
            else
            {
                filteredTypes = componentTypes
                    .Where(type => type.Name.ToLower().Contains(searchString.ToLower()))
                    .ToList();
            }
        }

        private void AddComponent(Type componentType)
        {
            if (targetGameObject != null)
            {
                var component = targetGameObject.AddComponent(componentType);
                onComponentAdded?.Invoke(component);
            }
        }
    }
}