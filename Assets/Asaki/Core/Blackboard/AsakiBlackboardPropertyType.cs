using Asaki.Core.Blackboard.Variables;
using System;
using UnityEngine;

namespace Asaki.Core.Blackboard
{
	/// <summary>
	/// 表示黑板系统中的变量定义。
	/// 该类用于存储变量的相关信息，包括名称、值数据以及是否暴露的标志，
	/// 同时提供了获取变量类型名称的便捷方式。
	/// </summary>
	[Serializable]
	public class AsakiVariableDef
	{
		/// <summary>
		/// 获取或设置变量的名称。
		/// 此名称用于在黑板系统中标识变量。
		/// </summary>
		public string Name;

		/// <summary>
		/// 获取或设置变量的值数据。
		/// 类型为 <see cref="AsakiValueBase"/>，可存储各种具体类型的值，
		/// 通过 <see cref="SerializeReference"/> 特性支持序列化和反序列化不同类型的派生类。
		/// </summary>
		[SerializeReference]
		public AsakiValueBase ValueData;

		/// <summary>
		/// 获取或设置一个值，指示该变量是否暴露。
		/// 用于控制变量在某些场景下的可见性或可访问性。
		/// </summary>
		public bool IsExposed { get; set; }

		/// <summary>
		/// 获取变量值数据的类型名称。
		/// 如果 <see cref="ValueData"/> 不为 null，则返回其 <see cref="AsakiValueBase.TypeName"/>；
		/// 否则返回 "Null"。
		/// </summary>
		// 辅助属性：快速获取类型名
		public string TypeName => ValueData != null? ValueData.TypeName : "Null";
	}
}
