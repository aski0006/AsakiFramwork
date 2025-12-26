using Asaki.Core.Blackboard;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Asaki.Core.Graphs
{
	public class AsakiGraphRuntimeContext : IDisposable
	{
		public IAsakiBlackboard Blackboard;

		public GameObject Owner;

		public void Dispose()
		{
			// M-01: 级联释放，断开所有属性订阅
			Blackboard?.Dispose();
			Blackboard = null;
			Owner = null;
		}
	}
}
