using System.Collections.Concurrent;

namespace Asaki.Core.Logging
{
	/// <summary>
	/// [优化] 改为 Class 以支持引用传递，避免大结构体拷贝
	/// </summary>
	public class LogWriteCommand
	{
		public enum CmdType { Def, Inc }
		public CmdType Type;
		public int Id;

		// Def Data
		public int LevelInt; // 存 int 避免枚举转换开销
		public long Timestamp;
		public string Message;
		public string Payload; // 预处理好的 JSON/String
		public string Path;
		public int Line;
		public string StackJson; // 预处理好的堆栈

		// Inc Data
		public int IncAmount;

		public void Reset()
		{
			Message = null;
			Payload = null;
			Path = null;
			StackJson = null;
		}
	}

	/// <summary>
	/// [优化] 极简线程安全对象池
	/// </summary>
	public static class LogCommandPool
	{
		private static readonly ConcurrentStack<LogWriteCommand> _pool = new ConcurrentStack<LogWriteCommand>();

		public static LogWriteCommand Get()
		{
			return _pool.TryPop(out LogWriteCommand cmd) ? cmd : new LogWriteCommand();
		}

		public static void Return(LogWriteCommand cmd)
		{
			cmd.Reset();
			_pool.Push(cmd);
		}
	}
}
