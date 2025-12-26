namespace Asaki.Core.MVVM
{
	public interface IAsakiObserver<T>
	{
		void OnValueChange(T value);
	}
}
