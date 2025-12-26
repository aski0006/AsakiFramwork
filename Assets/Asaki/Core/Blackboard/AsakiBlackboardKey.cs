using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Asaki.Core.Blackboard
{
	/// <summary>
	/// [Core Constraint 1] 确定性哈希键
	/// 强制使用 FNV-1a 算法替代 string.GetHashCode，确保跨平台/跨进程的哈希一致性。
	/// </summary>
	public readonly struct AsakiBlackboardKey : IEquatable<AsakiBlackboardKey>
	{
		public readonly int Hash;

		#if UNITY_EDITOR
		public readonly string DebugName;
		#endif

		// FNV-1a 32-bit Constants
		private const uint FNV_OFFSET_BASIS = 2166136261;
		private const uint FNV_PRIME = 16777619;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public AsakiBlackboardKey(string keyName)
		{
			if (string.IsNullOrEmpty(keyName))
			{
				Hash = 0;
				#if UNITY_EDITOR
				DebugName = "NULL";
				#endif
				return;
			}

			unchecked
			{
				uint hash = FNV_OFFSET_BASIS;
				// 手动遍历字符字节，避免依赖平台特定的 string 实现
				for (int i = 0; i < keyName.Length; i++)
				{
					hash ^= keyName[i];
					hash *= FNV_PRIME;
				}
				Hash = (int)hash;
			}

			#if UNITY_EDITOR
			DebugName = keyName;
			#endif
		}

		public AsakiBlackboardKey(int hash)
		{
			Hash = hash;
			#if UNITY_EDITOR
			DebugName = hash.ToString();
			#endif
		}

		// 隐式转换，方便使用
		public static implicit operator AsakiBlackboardKey(string name)
		{
			return new AsakiBlackboardKey(name);
		}
		public static implicit operator AsakiBlackboardKey(int hash)
		{
			return new AsakiBlackboardKey(hash);
		}

		public bool Equals(AsakiBlackboardKey other)
		{
			return Hash == other.Hash;
		}
		public override bool Equals(object obj)
		{
			return obj is AsakiBlackboardKey other && Equals(other);
		}
		public override int GetHashCode()
		{
			return Hash;
		}

		public static bool operator ==(AsakiBlackboardKey left, AsakiBlackboardKey right)
		{
			return left.Hash == right.Hash;
		}
		public static bool operator !=(AsakiBlackboardKey left, AsakiBlackboardKey right)
		{
			return left.Hash != right.Hash;
		}

		public override string ToString()
		{
			#if UNITY_EDITOR
			return string.IsNullOrEmpty(DebugName) ? Hash.ToString() : $"{DebugName} ({Hash})";
			#else
            return Hash.ToString();
			#endif
		}
	}
}
