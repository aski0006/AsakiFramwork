using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using UnityEngine; // For JsonUtility

namespace Asaki.Core.Logging
{
	public class AsakiLogAggregator
	{
		// === 内部结构体定义 ===
		private struct LogPacket
		{
			public AsakiLogLevel Level;
			public string Message;
			public string Payload;
			public string File;
			public int Line;
			public Exception Exception;
			public long Timestamp;
		}

		private struct LogSignature : IEquatable<LogSignature>
		{
			private readonly int _hash;
			public LogSignature(string file, int line, string msg)
			{
				unchecked
				{
					_hash = (file?.GetHashCode() ?? 0) * 397 ^ line ^ (msg?.GetHashCode() ?? 0);
				}
			}
			public override int GetHashCode()
			{
				return _hash;
			}
			public bool Equals(LogSignature other)
			{
				return _hash == other._hash;
			}
		}

		// === 1. 输入端 (生产者) ===
		private readonly ConcurrentQueue<LogPacket> _inputQueue = new ConcurrentQueue<LogPacket>();
		private const int MAX_QUEUE_DEPTH = 5000; // [优化] 队列深度限制

		// === 2. 聚合端 (主线程状态) ===
		private readonly Dictionary<LogSignature, AsakiLogModel> _signatureMap = new Dictionary<LogSignature, AsakiLogModel>();
		private readonly List<AsakiLogModel> _displayList = new List<AsakiLogModel>();
		private int _idCounter = 1;

		// [优化] 私有锁，仅供 Snapshot 使用
		private readonly object _snapshotLock = new object();

		// === 3. IO 端 (双缓冲) ===
		// 这种模式下，MainThread 只写 current，Writer 只读 back，互不干扰
		private List<LogWriteCommand> _ioBufferCurrent = new List<LogWriteCommand>(256);
		private List<LogWriteCommand> _ioBufferBack = new List<LogWriteCommand>(256);
		private readonly object _ioSwapLock = new object(); // 仅在交换指针的一瞬间加锁

		// === API ===

		public void Log(AsakiLogLevel level, string message, string payload, string file, int line, Exception ex)
		{
			// [优化] 简单的背压保护
			if (_inputQueue.Count >= MAX_QUEUE_DEPTH)
			{
				// 队列爆了，丢弃 Trace/Info，保留 Error，或者直接丢弃并报错
				if (level < AsakiLogLevel.Error) return;
			}

			_inputQueue.Enqueue(new LogPacket
			{
				Level = level,
				Message = message,
				Payload = payload,
				File = file,
				Line = line,
				Exception = ex,
				Timestamp = DateTime.UtcNow.Ticks,
			});
		}

		// 外部驱动调用 (LateUpdate)
		public void Sync(int batchLimit = 1000)
		{
			int count = 0;
			// 批量从并发队列取出
			while (count < batchLimit && _inputQueue.TryDequeue(out LogPacket packet))
			{
				count++;
				ProcessSingleLog(ref packet);
			}
		}

		// [优化] 外部获取快照的安全入口
		public List<AsakiLogModel> GetSnapshot()
		{
			lock (_snapshotLock)
			{
				return new List<AsakiLogModel>(_displayList);
			}
		}

		// [优化] 供 Writer 线程调用：交换缓冲区
		// Writer 拿走填满的 Current，给 Aggregator 一个空的 Back
		public List<LogWriteCommand> SwapIOBuffer()
		{
			lock (_ioSwapLock)
			{
				if (_ioBufferCurrent.Count == 0) return null;

				var filledBuffer = _ioBufferCurrent;
				_ioBufferCurrent = _ioBufferBack; // 换上空盘子
				_ioBufferBack = filledBuffer;     // 拿走满盘子

				// 确保换上来的 buffer 是干净的 (理论上 Writer 会清理，但防御性清空)
				_ioBufferCurrent.Clear();

				return filledBuffer;
			}
		}

		// === 内部逻辑 ===

		private void ProcessSingleLog(ref LogPacket p)
		{
			LogSignature sig = new LogSignature(p.File, p.Line, p.Message);

			// 锁定显示列表 (为了 Snapshot 安全)
			lock (_snapshotLock)
			{
				if (_signatureMap.TryGetValue(sig, out AsakiLogModel model))
				{
					// === Inc ===
					model.Count++;
					model.LastTimestamp = p.Timestamp;

					// 池化创建 Command
					LogWriteCommand cmd = LogCommandPool.Get();
					cmd.Type = LogWriteCommand.CmdType.Inc;
					cmd.Id = model.ID;
					cmd.IncAmount = 1;

					// 写入 Current Buffer (此时只有主线程在访问 Current，无需锁)
					_ioBufferCurrent.Add(cmd);
				}
				else
				{
					// === Def ===
					// 解析堆栈
					var stack = CaptureSmartStackTrace(p.Exception);
					string stackJson = stack != null && stack.Count > 0
						? JsonUtility.ToJson(new StackWrapper { F = stack })
						: "{}";

					model = new AsakiLogModel
					{
						ID = _idCounter++,
						LastTimestamp = p.Timestamp,
						Level = p.Level,
						Message = p.Message,
						PayloadJson = p.Payload,
						CallerPath = p.File,
						CallerLine = p.Line,
						Count = 1,
						StackFrames = stack,
					};

					_signatureMap[sig] = model;
					_displayList.Add(model);

					// 池化创建 Command
					LogWriteCommand cmd = LogCommandPool.Get();
					cmd.Type = LogWriteCommand.CmdType.Def;
					cmd.Id = model.ID;
					cmd.LevelInt = (int)model.Level;
					cmd.Timestamp = model.LastTimestamp;
					cmd.Message = model.Message;
					cmd.Payload = model.PayloadJson;
					cmd.Path = model.CallerPath;
					cmd.Line = model.CallerLine;
					cmd.StackJson = stackJson; // [优化] 预序列化

					_ioBufferCurrent.Add(cmd);
				}
			}
		}

		private List<StackFrameModel> CaptureSmartStackTrace(Exception ex)
		{
			var list = new List<StackFrameModel>();

			// 如果有异常，从异常取；否则抓取当前调用栈
			// 3 表示跳过: CaptureSmartStackTrace -> Log -> ALog.Trace -> UserCode
			System.Diagnostics.StackTrace trace = ex != null ? new System.Diagnostics.StackTrace(ex, true) : new System.Diagnostics.StackTrace(3, true);

			var frames = trace.GetFrames();
			if (frames == null) return list;

			foreach (StackFrame frame in frames)
			{
				MethodBase method = frame.GetMethod();
				if (method == null) continue;

				string fileName = frame.GetFileName();
				fileName = fileName?.Replace('\\', '/') ?? string.Empty;

				bool isUserCode = fileName.Contains("/Assets/") &&
				                  !fileName.Contains("/Asaki/") &&
				                  !fileName.Contains("Library/PackageCache");

				list.Add(new StackFrameModel
				{
					DeclaringType = method.DeclaringType?.Name ?? "Global",
					MethodName = method.Name,
					FilePath = fileName,
					LineNumber = frame.GetFileLineNumber(),
					IsUserCode = isUserCode,
				});
			}
			return list;
		}

		public void Clear()
		{
			lock (_snapshotLock)
			{
				_signatureMap.Clear();
				_displayList.Clear();
				_idCounter = 1;
			}
			// IO Buffer 会在 Writer 下次 Swap 时被清空
		}

		[Serializable]
		private struct StackWrapper
		{
			public List<StackFrameModel> F;
		}
	}
}
