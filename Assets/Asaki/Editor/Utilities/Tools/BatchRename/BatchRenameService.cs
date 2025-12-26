using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Asaki.Editor.Utilities.Tools.BatchRename
{
	/// <summary>
	/// 批量重命名业务服务
	/// 设计原则：纯逻辑层，零GUI依赖，便于单元测试和复用
	/// </summary>
	public class BatchRenameService
	{
		/// <summary>
		/// 命名策略接口：预留扩展点（序号、查找替换等）
		/// </summary>
		public interface INamingStrategy
		{
			/// <summary>
			/// 生成新名称
			/// </summary>
			/// <param name="original">原始名称</param>
			/// <param name="prefix">用户输入的前缀</param>
			/// <param name="suffix">用户输入的后缀</param>
			string GenerateName(string original, string prefix, string suffix);
		}

		/// <summary>
		/// 默认策略：简单前缀+后缀拼接
		/// </summary>
		public class DefaultNamingStrategy : INamingStrategy
		{
			public string GenerateName(string original, string prefix, string suffix)
			{
				// 防御性处理：空字符串不添加分隔符
				return $"{prefix}{original}{suffix}";
			}
		}

		/// <summary>
		/// 序号策略：为每个对象添加递增序号
		/// 示例：Cube → Prefix_Cube_001_Suffix
		/// </summary>
		public class SerialNamingStrategy : INamingStrategy
		{
			private int _startIndex;
			private int _padding;

			public SerialNamingStrategy(int startIndex = 1, int padding = 3)
			{
				_startIndex = startIndex;
				_padding = padding;
			}

			public string GenerateName(string original, string prefix, string suffix)
			{
				string serial = (_startIndex++).ToString().PadLeft(_padding, '0');
				return $"{prefix}{original}_{serial}{suffix}";
			}
		}

		/// <summary>
		/// 生成预览操作列表
		/// 性能：O(n)时间复杂度，单次遍历
		/// 过滤：自动移除null和已销毁对象
		/// </summary>
		/// <param name="targets">选中的GameObject数组</param>
		/// <param name="prefix">前缀</param>
		/// <param name="suffix">后缀</param>
		/// <param name="strategy">命名策略（可选）</param>
		public RenameOperation[] GeneratePreview(
			GameObject[] targets,
			string prefix,
			string suffix,
			INamingStrategy strategy = null)
		{
			strategy ??= new DefaultNamingStrategy();

			// 防御性编程：处理targets为null的情况
			if (targets == null || targets.Length == 0)
				return new RenameOperation[0];

			// 性能优化：预分配数组，避免Linq迭代器GC压力
			var operations = new List<RenameOperation>(targets.Length);
			foreach (GameObject go in targets)
			{
				if (go == null) continue; // 过滤已销毁对象

				string newName = strategy.GenerateName(go.name, prefix, suffix);
				operations.Add(RenameOperation.CreateFrom(go, newName));
			}

			return operations.ToArray();
		}

		/// <summary>
		/// 检测命名冲突
		/// 算法：GroupBy分组，筛选count>1的组，O(n log n)复杂度
		/// 返回：冲突对象的InstanceID → 冲突名称映射表
		/// </summary>
		public Dictionary<int, string> DetectConflicts(RenameOperation[] operations)
		{
			var conflicts = new Dictionary<int, string>();

			if (operations == null || operations.Length == 0)
				return conflicts;

			// 使用GroupBy检测重复名称
			var duplicateGroups = operations
			                      .GroupBy(op => op.NewName)
			                      .Where(g => g.Count() > 1);

			// 将冲突项加入字典
			foreach (var group in duplicateGroups)
			{
				foreach (RenameOperation op in group)
				{
					conflicts[op.InstanceId] = op.NewName;
				}
			}

			return conflicts;
		}

		/// <summary>
		/// 验证操作的有效性
		/// 检查：对象是否存在、名称是否未变更、是否有冲突
		/// </summary>
		public ValidationResult ValidateOperation(RenameOperation op, Dictionary<int, string> conflictMap)
		{
			if (op == null)
				return ValidationResult.Invalid("操作对象为空");

			if (!op.TryGetGameObject(out _))
				return ValidationResult.Invalid("目标对象已销毁或不存在");

			if (op.OriginalName == op.NewName)
				return ValidationResult.Invalid("名称未变更");

			if (conflictMap?.ContainsKey(op.InstanceId) ?? false)
				return ValidationResult.Invalid($"命名冲突: {op.NewName}");

			return ValidationResult.Valid;
		}

		/// <summary>
		/// 验证结果结构体
		/// </summary>
		public struct ValidationResult
		{
			public bool IsValid;
			public string ErrorMessage;

			public static ValidationResult Valid => new ValidationResult { IsValid = true };
			public static ValidationResult Invalid(string message)
			{
				return new ValidationResult
				{
					IsValid = false,
					ErrorMessage = message,
				};
			}
		}
	}
}
