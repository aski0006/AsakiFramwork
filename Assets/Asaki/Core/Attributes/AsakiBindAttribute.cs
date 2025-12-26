using System;

namespace Asaki.Core
{
	[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
	public sealed class AsakiBindAttribute : Attribute
	{
		// 以后可以在这里添加参数，比如 [AsakiBind(GenerateUI = true)]
	}
}
