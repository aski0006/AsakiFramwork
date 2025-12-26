using System;

namespace Asaki.Core
{
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
	public class AsakiGraphContextAttribute : Attribute
	{
		public Type GraphType { get; private set; }
		public string Path { get; private set; }
		public AsakiGraphContextAttribute(Type graphType, string path)
		{
			GraphType = graphType;
			Path = path;
		}
	}

	[AttributeUsage(AttributeTargets.Field)]
	public class AsakiNodeInputAttribute : Attribute
	{
		public string PortName;
		public bool Multiple; // 是否允许多连
		public AsakiNodeInputAttribute(string name = "In", bool multiple = true)
		{
			PortName = name;
			Multiple = multiple;
		}
	}

	[AttributeUsage(AttributeTargets.Field)]
	public class AsakiNodeOutputAttribute : Attribute
	{
		public string PortName;
		public bool Multiple;
		public AsakiNodeOutputAttribute(string name = "Out", bool multiple = false)
		{
			PortName = name;
			Multiple = multiple;
		}
	}
}
