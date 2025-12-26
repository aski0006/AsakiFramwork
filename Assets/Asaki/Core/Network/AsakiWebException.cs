namespace Asaki.Core.Network
{
	public class AsakiWebException : System.Exception
	{
		public long ResponseCode { get; }
		public string Url { get; }
		public AsakiWebException(string message, long code, string url) : base(message)
		{
			ResponseCode = code;
			Url = url;
		}
	}
}
