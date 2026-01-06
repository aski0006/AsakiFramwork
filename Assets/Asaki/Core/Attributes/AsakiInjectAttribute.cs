using System;

namespace Asaki.Core.Attributes
{
	[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
	public sealed class AsakiInjectAttribute : Attribute { }

}
