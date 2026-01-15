using System.Collections.Generic;

namespace Asaki.Core.Blackboard
{
	public static class BlackboardExtensions
	{
		public static void BatchSet(this IAsakiBlackboard blackboard, params (string key, object value)[] updates)
		{
			using (blackboard.BeginBatch())
			{
				foreach (var (key, value) in updates)
				{
					var hashKey = new AsakiBlackboardKey(key);
					SetValueDynamic(blackboard, hashKey, value);
				}
			}
		}

		public static void BatchSet(this IAsakiBlackboard blackboard, Dictionary<string, object> updates)
		{
			using (blackboard.BeginBatch())
			{
				foreach (var kvp in updates)
				{
					var hashKey = new AsakiBlackboardKey(kvp.Key);
					SetValueDynamic(blackboard, hashKey, kvp.Value);
				}
			}
		}

		private static void SetValueDynamic(IAsakiBlackboard blackboard, AsakiBlackboardKey key, object value)
		{
			switch (value)
			{
				case int v:    blackboard.SetValue(key, v); break;
				case float v:  blackboard.SetValue(key, v); break;
				case bool v:   blackboard.SetValue(key, v); break;
				case string v: blackboard.SetValue(key, v); break;
				default:
					var method = typeof(IAsakiBlackboard).GetMethod("SetValue");
					var generic = method?.MakeGenericMethod(value.GetType());
					generic?.Invoke(blackboard, new object[] { key, value });
					break;
			}
		}
	}
}
