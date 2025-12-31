using System;
using System.Collections.Generic;
using Asaki.Core.Graphs;
using UnityEditor;
using UnityEngine;

namespace Asaki.Editor.GraphEditors
{
    public class GlobalBlackboardWindow : EditorWindow
    {
        private AsakiGlobalBlackboardAsset _globalAsset;
        private Vector2 _scrollPos;
        private string _newVarName = "";
        private AsakiBlackboardPropertyType _newVarType = AsakiBlackboardPropertyType.Float;
        
        // ★ 延迟删除队列
        private readonly Queue<int> _indicesToDelete = new Queue<int>();

        [MenuItem("Asaki/Global Blackboard Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<GlobalBlackboardWindow>("Global Blackboard");
            window.minSize = new Vector2(400, 300);
            
            string assetPath = "Assets/Resources/Asaki/Configuration/GlobalBlackboard.asset";
            window._globalAsset = AssetDatabase.LoadAssetAtPath<AsakiGlobalBlackboardAsset>(assetPath);
            if (window._globalAsset == null)
            {
                window._globalAsset = CreateInstance<AsakiGlobalBlackboardAsset>();
                System.IO.Directory.CreateDirectory("Assets/Resources/Asaki/Configuration");
                AssetDatabase.CreateAsset(window._globalAsset, assetPath);
                AssetDatabase.SaveAssets();
            }
        }

        private void OnGUI()
        {
            if (!_globalAsset) return;

            EditorGUILayout.LabelField("Global Variables", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            EditorGUILayout.BeginHorizontal();
            _newVarName = EditorGUILayout.TextField("Name", _newVarName);
            _newVarType = (AsakiBlackboardPropertyType)EditorGUILayout.EnumPopup("Type", _newVarType);
            if (GUILayout.Button("Add", GUILayout.Width(50)))
            {
                if (!string.IsNullOrWhiteSpace(_newVarName))
                {
                    Undo.RecordObject(_globalAsset, "Add Global Variable");
                    _globalAsset.GetOrCreateVariable(_newVarName, _newVarType);
                    EditorUtility.SetDirty(_globalAsset);
                    _newVarName = "";
                }
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Variables", EditorStyles.boldLabel);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            
            // ★ 反向遍历，更安全
            for (int i = _globalAsset.GlobalVariables.Count - 1; i >= 0; i--)
            {
                var variable = _globalAsset.GlobalVariables[i];
                EditorGUILayout.BeginVertical(GUI.skin.box);

                EditorGUILayout.BeginHorizontal();
                variable.Name = EditorGUILayout.TextField(variable.Name);
                
                // ★ 标记删除，不立即执行
                if (GUILayout.Button("Delete", GUILayout.Width(60)))
                {
                    _indicesToDelete.Enqueue(i);
                }
                EditorGUILayout.EndHorizontal();

                DrawVariableValueEditor(variable);
                EditorGUILayout.EndVertical();
            }
            
            EditorGUILayout.EndScrollView();

            // ★ 处理延迟删除
            ProcessPendingDeletions();
        }

        private void ProcessPendingDeletions()
        {
            if (_indicesToDelete.Count == 0) return;

            Undo.RecordObject(_globalAsset, "Delete Global Variables");
            
            // 降序删除，避免索引问题
            var sortedIndices = _indicesToDelete.ToArray();
            Array.Sort(sortedIndices, (a, b) => b.CompareTo(a));
            
            foreach (int index in sortedIndices)
            {
                if (index >= 0 && index < _globalAsset.GlobalVariables.Count)
                {
                    _globalAsset.GlobalVariables.RemoveAt(index);
                }
            }
            
            _indicesToDelete.Clear();
            EditorUtility.SetDirty(_globalAsset);
        }

        private void DrawVariableValueEditor(AsakiVariableDef variable)
        {
            switch (variable.Type)
            {
                case AsakiBlackboardPropertyType.Int:
                    variable.IntVal = EditorGUILayout.IntField("Value", variable.IntVal);
                    break;
                case AsakiBlackboardPropertyType.Float:
                    variable.FloatVal = EditorGUILayout.FloatField("Value", variable.FloatVal);
                    break;
                case AsakiBlackboardPropertyType.Bool:
                    variable.BoolVal = EditorGUILayout.Toggle("Value", variable.BoolVal);
                    break;
                case AsakiBlackboardPropertyType.String:
                    variable.StringVal = EditorGUILayout.TextField("Value", variable.StringVal);
                    break;
                case AsakiBlackboardPropertyType.Vector3:
                    variable.Vector3Val = EditorGUILayout.Vector3Field("Value", variable.Vector3Val);
                    break;
                case AsakiBlackboardPropertyType.Vector2:
                    variable.Vector2Val = EditorGUILayout.Vector2Field("Value", variable.Vector2Val);
                    break;
                case AsakiBlackboardPropertyType.Vector3Int:
                    variable.Vector3IntVal = EditorGUILayout.Vector3IntField("Value", variable.Vector3IntVal);
                    break;
                case AsakiBlackboardPropertyType.Vector2Int:
                    variable.Vector2IntVal = EditorGUILayout.Vector2IntField("Value", variable.Vector2IntVal);
                    break;
                case AsakiBlackboardPropertyType.Color:
                    variable.ColorVal = EditorGUILayout.ColorField("Value", variable.ColorVal);
                    break;
            }

            if (GUI.changed)
            {
                EditorUtility.SetDirty(_globalAsset);
            }
        }

        private void OnInspectorUpdate()
        {
            Repaint();
        }
    }
}