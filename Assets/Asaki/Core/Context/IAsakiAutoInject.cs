namespace Asaki.Core.Context
{
	public interface IAsakiAutoInject { }
	
	
	/// <summary>
	/// [Asaki V5] 分布式注入器接口
	/// <para>每个程序集都会生成一个实现此接口的类。</para>
	/// </summary>
	public interface IAsakiInjector
	{
		/// <summary>
		/// 尝试为目标注入依赖。
		/// </summary>
		void Inject(object target);
	}
}
