using Asaki.Core.Coroutines;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Asaki.Unity.Services.Coroutines
{
	// [修改点 1] 增加 IDisposable 接口，养成良好习惯
	public partial class AsakiCoroutineProvider : IAsakiCoroutineService, IDisposable
	{
		private CancellationTokenSource _serviceCts = new CancellationTokenSource();
		private int _runningTaskCount = 0;

		// [修改点 2] 持有 Runner 的引用
		private AsakiCoroutineRunner _runner;

		// [修改点 3] 构造函数初始化
		public AsakiCoroutineProvider()
		{
			InitializeRunner();
		}

		private void InitializeRunner()
		{
			// 防止重复初始化
			if (_runner != null) return;

			// 必须在主线程执行 (构造函数如果不在主线程会报错，但在 Unity 服务定位器模式中通常是安全的)
			GameObject go = new GameObject("[Asaki.Routine.Kernel]");
			Object.DontDestroyOnLoad(go);

			// 挂载驱动器
			_runner = go.AddComponent<AsakiCoroutineRunner>();
		}

		public int RunningTaskCount => _runningTaskCount;

		public void CancelAllTasks()
		{
			if (_serviceCts != null)
			{
				_serviceCts.Cancel();
				_serviceCts.Dispose();
			}
			_serviceCts = new CancellationTokenSource();
		}

		public CancellationToken CreateLinkedToken(CancellationToken externalToken = default(CancellationToken))
		{
			if (_serviceCts.IsCancellationRequested) return CancellationToken.None;
			if (externalToken == CancellationToken.None) return _serviceCts.Token;
			return CancellationTokenSource.CreateLinkedTokenSource(_serviceCts.Token, externalToken).Token;
		}

		private async Task Track(Func<Task> taskFunc)
		{
			Interlocked.Increment(ref _runningTaskCount);
			try
			{
				await taskFunc();
			}
			catch (OperationCanceledException)
			{
				// 任务被取消是正常操作
			}
			catch (Exception e)
			{
				Debug.LogException(e);
				throw;
			}
			finally
			{
				Interlocked.Decrement(ref _runningTaskCount);
			}
		}

		// [修改点 4] 销毁逻辑
		public void Dispose()
		{
			CancelAllTasks();

			// 销毁宿主 GameObject
			if (_runner != null)
			{
				// 区分运行时和编辑器模式的销毁
				if (Application.isPlaying)
					Object.Destroy(_runner.gameObject);
				else
					Object.DestroyImmediate(_runner.gameObject);

				_runner = null;
			}
		}
	}
}
