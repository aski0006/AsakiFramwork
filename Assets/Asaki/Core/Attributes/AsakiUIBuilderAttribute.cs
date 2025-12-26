using Asaki.Core.UI;
using System;

namespace Asaki.Core
{
	[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
	public class AsakiUIBuilderAttribute : Attribute
	{
		public AsakiUIWidgetType Type { get; }

		/// <summary>
		/// 指定生成的 GameObject 名称。
		/// <para>若为空，自动使用字段名（去除下划线）。</para>
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// 指定父级路径（相对于 Root）。
		/// <para>例如: "Content/Grid"。若为空，则直接挂载在 Root 下。</para>
		/// </summary>
		public string Parent { get; set; }

		public string CustomPrefab { get; }

		public int Order { get; set; } = 0;
		/// <summary>
		/// 声明一个 UI 组件
		/// </summary>
		/// <param name="type">组件类型</param>
		public AsakiUIBuilderAttribute(AsakiUIWidgetType type)
		{
			Type = type;
		}

		/// <summary>
		/// 声明一个自定义组件
		/// </summary>
		public AsakiUIBuilderAttribute(string customPrefabName)
		{
			Type = AsakiUIWidgetType.Custom;
			CustomPrefab = customPrefabName;
		}
	}
}
