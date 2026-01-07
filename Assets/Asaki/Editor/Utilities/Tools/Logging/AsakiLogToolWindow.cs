using System.IO;
using UnityEditor;
using UnityEngine;

namespace Asaki.Editor.Utilities.Tools.Logging
{
	public class AsakiLogToolWindow : EditorWindow
	{
		private string _selectedFilePath;
		private AsakiLogOptimizer.OptimizationResult? _lastResult;
		private string _message;
		private MessageType _messageType;

		[MenuItem("Asaki/Tools/Log Optimizer", false, 101)]
		public static void ShowWindow()
		{
			GetWindow<AsakiLogToolWindow>("Log Optimizer").Show();
		}

		private void OnGUI()
		{
			GUILayout.Label("Asaki Log File Slimming Tool", EditorStyles.boldLabel);
			GUILayout.Space(10);

			// === 文件选择区域 ===
			using (new EditorGUILayout.HorizontalScope())
			{
				EditorGUILayout.PrefixLabel("Target Log:");
				if (GUILayout.Button(string.IsNullOrEmpty(_selectedFilePath) ? "Select .asakilog..." : Path.GetFileName(_selectedFilePath), EditorStyles.objectField))
				{
					string path = EditorUtility.OpenFilePanel("Select Log File", Application.persistentDataPath, "asakilog");
					if (!string.IsNullOrEmpty(path))
					{
						_selectedFilePath = path;
						_lastResult = null;
						_message = "";
					}
				}
			}

			if (!string.IsNullOrEmpty(_selectedFilePath))
			{
				GUILayout.Label(_selectedFilePath, EditorStyles.miniLabel);
			}

			GUILayout.Space(15);

			// === 操作区域 ===
			GUI.enabled = !string.IsNullOrEmpty(_selectedFilePath) && File.Exists(_selectedFilePath);
			if (GUILayout.Button("Start Optimization", GUILayout.Height(30)))
			{
				try
				{
					_lastResult = AsakiLogOptimizer.Process(_selectedFilePath);
					_message = "Optimization Complete!";
					_messageType = MessageType.Info;
					EditorUtility.RevealInFinder(_lastResult.Value.OutputPath);
				}
				catch (System.Exception ex)
				{
					_message = $"Error: {ex.Message}";
					_messageType = MessageType.Error;
					Debug.LogException(ex);
				}
			}
			GUI.enabled = true;

			GUILayout.Space(10);

			// === 结果展示区域 ===
			if (_message != null)
			{
				EditorGUILayout.HelpBox(_message, _messageType);
			}

			if (_lastResult.HasValue)
			{
				AsakiLogOptimizer.OptimizationResult r = _lastResult.Value;
				GUILayout.Space(10);
				GUILayout.Label("Result Statistics:", EditorStyles.boldLabel);
				DrawStat("Lines", r.OriginalLines, r.OptimizedLines);
				DrawStat("Size", FormatSize(r.OriginalSize), FormatSize(r.OptimizedSize));

				float compressRatio = 1f - (float)r.OptimizedSize / r.OriginalSize;
				EditorGUILayout.HelpBox($"Compression Ratio: {compressRatio:P2}", MessageType.None);
			}
		}

		private void DrawStat(string label, object original, object optimized)
		{
			using (new EditorGUILayout.HorizontalScope())
			{
				GUILayout.Label(label, GUILayout.Width(50));
				GUILayout.Label($"{original} -> {optimized}", EditorStyles.label);
			}
		}

		private void DrawStat(string label, long original, long optimized)
		{
			DrawStat(label, original.ToString(), optimized.ToString());
		}

		private string FormatSize(long bytes)
		{
			if (bytes < 1024) return bytes + " B";
			if (bytes < 1024 * 1024) return (bytes / 1024f).ToString("F2") + " KB";
			return (bytes / 1024f / 1024f).ToString("F2") + " MB";
		}
	}
}
