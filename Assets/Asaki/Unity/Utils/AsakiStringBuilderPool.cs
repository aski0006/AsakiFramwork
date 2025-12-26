using System.Collections.Generic;
using System.Text;

namespace Asaki.Unity.Utils
{
	/// <summary>
	/// [工具类] StringBuilder 对象池。
	/// 用于避免字符串拼接时的临时内存分配。
	/// </summary>
	public static class AsakiStringBuilderPool
	{
		// 静态栈，用于缓存对象
		private static readonly Stack<StringBuilder> _pool = new Stack<StringBuilder>(8);
		private const int MAX_CAPACITY = 1024; // 如果 Builder 变得太大，就丢弃，防止内存占用过高

		/// <summary>
		/// 借出一个 StringBuilder
		/// </summary>
		public static StringBuilder Rent()
		{
			if (_pool.Count > 0)
			{
				return _pool.Pop();
			}
			return new StringBuilder(256); // 默认 256 字符容量
		}

		/// <summary>
		/// 归还一个 StringBuilder
		/// </summary>
		public static void Return(StringBuilder sb)
		{
			if (sb == null) return;

			// 如果容量撑得太大，直接丢弃，不入池
			if (sb.Capacity > MAX_CAPACITY)
			{
				return;
			}

			// 清空内容，准备下次使用
			sb.Clear();
			_pool.Push(sb);
		}

		/// <summary>
		/// 快捷方法：借出 -> 转换 -> 归还
		/// </summary>
		public static string GetStringAndRelease(StringBuilder sb)
		{
			string result = sb.ToString();
			Return(sb);
			return result;
		}
	}
}
