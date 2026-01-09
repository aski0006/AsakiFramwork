using System;

namespace Asaki.Core.Configuration
{
	public struct AsakiConfigLoadInfo
	{
		public string ConfigName;
		public bool IsLoaded;
		public AsakiConfigLoadStrategy Strategy;
		public int Priority;
		public bool Unloadable;
		public long EstimatedSize;
		public int AccessCount;
		public DateTime LastAccessTime;
	}
}
