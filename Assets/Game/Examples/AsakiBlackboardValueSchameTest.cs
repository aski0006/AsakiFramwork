using Asaki.Core;
using Asaki.Core.Attributes;
using Asaki.Core.Blackboard.Variables;
using System;

namespace Game.Examples
{

	[Serializable]
	[AsakiBlackboardValueSchema]
	public struct ProductData
	{
		public int Id;
		public int Quality;
	}
	
	[Serializable]
	public class AsakiProduct : AsakiValue<ProductData> { }
}
