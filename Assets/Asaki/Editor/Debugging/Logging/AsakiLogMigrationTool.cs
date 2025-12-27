using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Asaki.Editor.Debugging.Logging
{
    public class AsakiLogMigrationTool : EditorWindow
    {
        private struct SearchResult
        {
            public string FilePath;
            public int LineNumber;
            public string Content;
            public string Type; // Log, Warning, Error
        }

        private List<SearchResult> _results = new List<SearchResult>();
        private ListView _listView;
        private Label _statusLabel;

        [MenuItem("Asaki/Tools/Log Migration Scanner")]
        public static void ShowWindow()
        {
            var wnd = GetWindow<AsakiLogMigrationTool>("Log Migrator");
            wnd.minSize = new Vector2(600, 400);
            wnd.Show();
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            
            // Toolbar
            var toolbar = new VisualElement { style = { flexDirection = FlexDirection.Row, height = 30, backgroundColor = new Color(0.2f, 0.2f, 0.2f), alignItems = Align.Center, paddingLeft = 10 } };
            toolbar.Add(new Button(ScanProject) { text = "Scan Assets Folder", style = { width = 120 } });
            _statusLabel = new Label("Ready") { style = { marginLeft = 10, color = Color.yellow } };
            toolbar.Add(_statusLabel);
            root.Add(toolbar);

            // List
            _listView = new ListView();
            _listView.fixedItemHeight = 40;
            _listView.makeItem = () => 
            {
                var container = new VisualElement { 
                    style = { 
                        flexDirection = FlexDirection.Column, 
                        paddingLeft = 5, 
                        justifyContent = Justify.Center,
                        borderBottomWidth = 1,
                        borderBottomColor = new Color(0.15f, 0.15f, 0.15f) // 增加分割线让列表更清晰
                    } 
                };
    
                // 关键修正 1: 给控件指定 name 属性
                var lblMsg = new Label { 
                    name = "lblMsg", // <--- 命名
                    style = { unityFontStyleAndWeight = FontStyle.Bold } 
                };
    
                var lblPath = new Label { 
                    name = "lblPath", // <--- 命名
                    style = { fontSize = 10, color = Color.gray } 
                };
    
                container.Add(lblMsg);
                container.Add(lblPath);
                return container;
            };
            
            _listView.bindItem = (e, i) => 
            {
                if (i >= _results.Count) return;
                var data = _results[i];
                var container = e as VisualElement;
    
                // 关键修正 2: 通过名字精确查找，消除了歧义，也解决了覆盖问题
                container.Q<Label>("lblMsg").text = $"[{data.Type}] {data.Content.Trim()}";
    
                // 颜色区分增强可读性
                var msgLabel = container.Q<Label>("lblMsg");
                switch(data.Type)
                {
                    case "LogError":   msgLabel.style.color = new Color(1f, 0.4f, 0.4f); break;
                    case "LogWarning": msgLabel.style.color = Color.yellow; break;
                    default:           msgLabel.style.color = Color.white; break;
                }

                container.Q<Label>("lblPath").text = $"{System.IO.Path.GetFileName(data.FilePath)}:{data.LineNumber}";
            };
            
            _listView.itemsSource = _results;
            _listView.selectionChanged += OnSelect;
            
            root.Add(_listView);
        }

        private void ScanProject()
        {
            _results.Clear();
            string[] files = Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories);
            
            // Regex match: Debug.Log, Debug.LogWarning, Debug.LogError
            Regex regex = new Regex(@"Debug\.(Log|LogWarning|LogError|LogFormat)\s*\(", RegexOptions.Compiled);

            int count = 0;
            foreach (var file in files)
            {
                // Skip Asaki Framework itself
                if (file.Replace("\\", "/").Contains("/Asaki/")) continue;

                var lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    var match = regex.Match(lines[i]);
                    if (match.Success)
                    {
                        // Check if it's commented out
                        if (lines[i].Trim().StartsWith("//")) continue;

                        _results.Add(new SearchResult
                        {
                            FilePath = file,
                            LineNumber = i + 1,
                            Content = lines[i].Trim(),
                            Type = match.Groups[1].Value
                        });
                        count++;
                    }
                }
            }
            
            _statusLabel.text = $"Found {count} legacy logs.";
            _listView.RefreshItems();
        }

        private void OnSelect(IEnumerable<object> selection)
        {
            foreach (SearchResult res in selection)
            {
                UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(res.FilePath, res.LineNumber);
            }
        }
    }
}