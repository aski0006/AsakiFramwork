using Asaki.Core.Context;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Asaki.Core.Configuration
{
	public interface IAsakiConfigService : IAsakiModule
	{
		Task LoadAllAsync();
		T Get<T>(int id) where T : class, IAsakiConfig, new();
		IReadOnlyList<T> GetAll<T>() where T : class, IAsakiConfig, new();
		IAsyncEnumerable<T> GetAllStreamAsync<T>() where T : class, IAsakiConfig, new();
		
		Task ReloadAsync<T>() where T : class, IAsakiConfig, new();
		// Link
		T Find<T>(Predicate<T> predicate) where T : class, IAsakiConfig, new();
		IReadOnlyList<T> Where<T>(Func<T, bool> predicate) where T : class, IAsakiConfig, new();
		bool Exists<T>(Predicate<T> predicate) where T : class, IAsakiConfig, new();
		
		// Batch Op
		IReadOnlyList<T> GetBatch<T>(IEnumerable<int> ids) where T : class, IAsakiConfig, new();
		
		// Config Meta
		int GetCount<T>() where T : class, IAsakiConfig, new();
		bool IsLoaded<T>() where T : class, IAsakiConfig, new();
		string GetSourcePath<T>() where T : class, IAsakiConfig, new();
		DateTime GetLastModifiedTime<T>() where T : class, IAsakiConfig, new();
	}
}
