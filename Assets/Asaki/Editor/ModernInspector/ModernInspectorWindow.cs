using Asaki.Editor. Utilities. Extensions;
using System. Collections.Generic;
using System. Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Asaki.Editor.ModernInspector
{
    public class ModernInspectorWindow :  EditorWindow
    {
        // UI 元素引用
        private VisualElement root;
        private VisualElement gameObjectHeader;
        private VisualElement tabContainer;
        private ScrollView detailScrollView;  // 改为 ScrollView
        private VisualElement detailPanel;
        private VisualElement noSelectionMessage;
        
        // 数据
        private GameObject selectedGameObject;
        private Component[] components;
        private HashSet<int> activeTabIndices = new HashSet<int>();
        private List<Toggle> tabToggles = new List<Toggle>();
        private Toggle allTabToggle;
        private bool isAllTabActive = false;  // 跟踪 All 标签状态
        
        // 缓存
        private Dictionary<Component, UnityEditor.Editor> editorCache = new Dictionary<Component, UnityEditor.Editor>();

        [MenuItem("Window/Modern Inspector")]
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
                    detailScrollView. AddToClassList("detail-scroll-view");
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

        private void UpdateInspector()
        {
            selectedGameObject = Selection.activeGameObject;
            
            if (selectedGameObject == null)
            {
                ShowNoSelection();
                return;
            }
            
            // 显示内容区域
            if (noSelectionMessage != null)
                noSelectionMessage.style.display = DisplayStyle.None;
            
            if (gameObjectHeader != null)
                gameObjectHeader.style.display = DisplayStyle.Flex;
            
            if (tabContainer != null)
                tabContainer.style.display = DisplayStyle.Flex;
            
            if (detailScrollView != null)
                detailScrollView.style.display = DisplayStyle.Flex;
            
            // 更新 GameObject 头部信息
            UpdateGameObjectHeader();
            
            // 获取所有组件
            components = selectedGameObject.GetComponents<Component>();
            
            // 重建选项卡
            RebuildTabs();
            
            // 默认显示 Transform/RectTransform
            SelectDefaultTab();
        }

        private void ShowNoSelection()
        {
            if (noSelectionMessage != null)
                noSelectionMessage.style.display = DisplayStyle. Flex;
            
            if (gameObjectHeader != null)
                gameObjectHeader.style.display = DisplayStyle.None;
            
            if (tabContainer != null)
                tabContainer.style.display = DisplayStyle.None;
            
            if (detailScrollView != null)
                detailScrollView.style.display = DisplayStyle.None;
            
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
            nameField.value = selectedGameObject. name;
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
                Undo. RecordObject(selectedGameObject, "Change Static");
                selectedGameObject. isStatic = evt.newValue;
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
                selectedGameObject. layer = evt.newValue;
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
            
            // 设置容器为横向布局
            tabContainer.style.flexDirection = FlexDirection.Row;
            tabContainer.style.flexWrap = Wrap.NoWrap;
            
            // 添加 "All" 标签
            allTabToggle = CreateAllTabToggle();
            tabContainer.Add(allTabToggle);
            
            // 添加组件标签
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
            
            // 图标
            var icon = new Image();
            icon.image = EditorGUIUtility.IconContent("SceneViewTools").image;
            icon.AddToClassList("tab-icon");
            
            // 标签
            var label = new Label("All");
            label.AddToClassList("tab-label");
            
            toggle.Add(icon);
            toggle.Add(label);
            
            // 点击事件
            toggle.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue)
                {
                    // 激活 All 标签
                    SelectAllTab();
                }
                else
                {
                    // 再次点击 All，取消激活，显示默认组件
                    DeselectAllTab();
                }
            });
            
            return toggle;
        }

        private Toggle CreateTabToggle(Component component, int index)
        {
            var toggle = new Toggle();
            toggle.AddToClassList("tab-toggle");
            
            // 组件图标
            var icon = new Image();
            icon.image = EditorGUIUtility. ObjectContent(component, component.GetType()).image;
            icon.AddToClassList("tab-icon");
            
            // 组件名称
            var label = new Label(component.GetType().Name);
            label. AddToClassList("tab-label");
            
            toggle.Add(icon);
            toggle.Add(label);
            
            // 点击事件
            toggle.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue)
                {
                    // 取消 All 选中
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
            // 清除所有单独选中
            activeTabIndices.Clear();
            isAllTabActive = true;
            
            // All 标签选中
            if (allTabToggle != null)
            {
                allTabToggle.SetValueWithoutNotify(true);
                allTabToggle.AddToClassList("tab-toggle-active");
            }
            
            // 取消所有组件标签选中
            foreach (var toggle in tabToggles)
            {
                toggle.SetValueWithoutNotify(false);
                toggle.RemoveFromClassList("tab-toggle-active");
            }
            
            RefreshDetailPanel();
        }

        private void DeselectAllTab()
        {
            // All 标签变为灰色
            isAllTabActive = false;
            
            if (allTabToggle != null)
            {
                allTabToggle.SetValueWithoutNotify(false);
                allTabToggle.RemoveFromClassList("tab-toggle-active");
            }
            
            // 显示默认组件（Transform 或 RectTransform）
            SelectDefaultTab();
        }

        private void SelectDefaultTab()
        {
            // 清除所有选中
            activeTabIndices. Clear();
            isAllTabActive = false;
            
            if (allTabToggle != null)
            {
                allTabToggle.SetValueWithoutNotify(false);
                allTabToggle.RemoveFromClassList("tab-toggle-active");
            }
            
            foreach (var toggle in tabToggles)
            {
                toggle. SetValueWithoutNotify(false);
                toggle.RemoveFromClassList("tab-toggle-active");
            }
            
            // 查找 Transform 或 RectTransform
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
            
            // 选中默认组件
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
            
            // 如果 All 标签激活，显示所有组件
            if (isAllTabActive)
            {
                RenderAllComponents();
                return;
            }
            
            // 否则显示选中的组件
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

        private void RenderComponentDetail(Component component)
        {
            if (detailPanel == null) return;
            
            // 获取或创建 Editor
            if (! editorCache.TryGetValue(component, out UnityEditor. Editor editor) || editor == null)
            {
                editor = UnityEditor. Editor.CreateEditor(component);
                editorCache[component] = editor;
            }
            
            // 创建组件容器
            var componentContainer = new VisualElement();
            componentContainer.AddToClassList("component-container");
            
            // 使用 Foldout 包裹组件
            var foldout = new Foldout();
            foldout.text = component.GetType().Name;
            foldout.value = true; // 默认展开
            foldout.AddToClassList("component-foldout");
            
            // Inspector 内容
            var inspectorContainer = new IMGUIContainer(() =>
            {
                if (editor != null && editor.target != null)
                {
                    EditorGUILayout.BeginVertical();
                    editor.OnInspectorGUI();
                    EditorGUILayout.EndVertical();
                }
            });
            inspectorContainer.AddToClassList("inspector-container");
            
            foldout.Add(inspectorContainer);
            componentContainer.Add(foldout);
            
            detailPanel.Add(componentContainer);
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
            foreach (var editor in editorCache. Values)
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
            
            // 无选择提示
            noSelectionMessage = new VisualElement();
            var noSelectionLabel = new Label("No GameObject selected");
            noSelectionLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            noSelectionLabel.style.fontSize = 14;
            noSelectionLabel.style.color = Color.gray;
            noSelectionMessage.Add(noSelectionLabel);
            noSelectionMessage.style.flexGrow = 1;
            noSelectionMessage.style.justifyContent = Justify.Center;
            
            // GameObject 头部
            gameObjectHeader = new VisualElement();
            gameObjectHeader.style.paddingBottom = 10;
            gameObjectHeader.style.paddingTop = 10;
            gameObjectHeader.style.paddingLeft = 5;
            gameObjectHeader.style.paddingRight = 5;
            
            // 选项卡容器
            tabContainer = new VisualElement();
            tabContainer.style.flexDirection = FlexDirection.Row;
            tabContainer.style. flexWrap = Wrap.NoWrap;
            tabContainer.style.height = 28;
            tabContainer.style.SetPadding(4);
            tabContainer.style.backgroundColor = new Color(0.23f, 0.23f, 0.23f);
            
            // 详细面板（使用 ScrollView）
            detailScrollView = new ScrollView(ScrollViewMode.Vertical);
            detailScrollView.style.flexGrow = 1;
            
            detailPanel = new VisualElement();
            detailPanel.style.paddingBottom = 10;
            detailPanel.style.paddingTop = 10;
            detailPanel.style.paddingLeft = 10;
            detailPanel.style.paddingRight = 10;
            
            detailScrollView.Add(detailPanel);
            
            container.Add(noSelectionMessage);
            container. Add(gameObjectHeader);
            container.Add(tabContainer);
            container.Add(detailScrollView);
            
            root.Add(container);
        }
    }
}