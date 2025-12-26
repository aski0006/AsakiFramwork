using System;

namespace Asaki.Core.Audio
{
	public readonly struct AsakiAudioHandle : IEquatable<AsakiAudioHandle>
	{
		public readonly int Id;        // 句柄唯一 ID
		public readonly int Timestamp; // 创建时间戳 (用于校验有效性，防止回收后误操作)

		public static readonly AsakiAudioHandle Invalid = new AsakiAudioHandle(0, 0);

		public AsakiAudioHandle(int id, int timestamp)
		{
			Id = id;
			Timestamp = timestamp;
		}

		public bool IsValid => Id != 0;

		// 重写 Equals 和 HashCode 以支持作为 Dictionary Key
		public override bool Equals(object obj)
		{
			if (obj is AsakiAudioHandle other)
			{
				return Equals(other);
			}
			return false;
		}

		public bool Equals(AsakiAudioHandle other)
		{
			return Id == other.Id && Timestamp == other.Timestamp;
		}

		public override int GetHashCode()
		{

			return HashCode.Combine(Id, Timestamp);
		}

		// 可选：重载 == 和 != 运算符以提供更直观的相等比较
		public static bool operator ==(AsakiAudioHandle left, AsakiAudioHandle right)
		{
			return left.Equals(right);
		}

		public static bool operator !=(AsakiAudioHandle left, AsakiAudioHandle right)
		{
			return !left.Equals(right);
		}
	}
}
