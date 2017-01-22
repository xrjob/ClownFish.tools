using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Script.Serialization;

namespace ClownFish.CommonLibrary
{
	internal class DemoCodeSimpleHttpClient
	{
		public void Demo1()
		{
			// 1、（必需）实例化SimpleHttpClient对象
			string url = "http://www.ab.com/aaa.aspx";
			using( SimpleHttpClient httpClient = new SimpleHttpClient(url) ) {

				// 2、（可选步骤）指定提交数据，用于POST场景

				// 以下4种方法都可以设计提交数据（一次只能使用一种！）
				byte[] bb = File.ReadAllBytes(@"c:\\aa.txt");
				httpClient.SetRequestData(bb);

				// or
				httpClient.SetRequestData("aaaaaaaaaaaaaa");

				// or
				var data = new { a = 1, b = 2, c = "xxx" };
				httpClient.SetRequestData(data);

				// or
				var list = new List<string>();
				// 省略赋值过程
				httpClient.SetRequestJsonData(list);

				// 3、（可选步骤）可以设置其它请求头
				httpClient.Request.Headers.Add("aa", "bb");

				// 4、（必需）发送请求，获取服务端响应
				HttpWebResponse response = httpClient.GetResponse();

				// 5、（可选步骤）获取响应头
				string xx = response.Headers["cc"];

				// 6、（必需）获取调用结果
				string result = httpClient.GetResult<string>();
			}
		}
	}


	/// <summary>
	/// 一个简单的HTTP客户端
	/// 说明：ClownFish.Web.Client.HttpClient 实现了更完整的HTTP客户端功能。
	/// </summary>
	public sealed class SimpleHttpClient : IDisposable
	{
		static SimpleHttpClient()
		{
			ServicePointManager.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback(CheckValidationResult);
		}

		/// <summary>
		/// 访问https请求时，验证证书回调操作，默认总是接受
		/// </summary>
		/// <param name="sender">事件发送对象</param>
		/// <param name="certificate">X509格式证书文件</param>
		/// <param name="chain">X509证书链</param>
		/// <param name="errors"></param>
		/// <returns></returns>
		private static bool CheckValidationResult(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors errors)
		{
			return true;
		}

		/// <summary>
		/// 默认的UserAgent请求头
		/// </summary>
		public static string DefaultUserAgent = "Fish/C# Tool/SimpleHttpClient/1.0";

		private HttpWebRequest _request;
		private HttpWebResponse _response;

		/// <summary>
		/// 需要提交的数据
		/// </summary>
		private byte[] _postData;

		/// <summary>
		/// HttpWebRequest的实例
		/// </summary>
		public HttpWebRequest Request { get { return _request; } }


		/// <summary>
		/// 构造函数
		/// </summary>
		/// <param name="url"></param>
		public SimpleHttpClient(string url)
		{
			if( string.IsNullOrEmpty(url) )
				throw new ArgumentNullException("url");

			HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
			request.UserAgent = DefaultUserAgent;
			request.Method = "GET";

			// 默认数据的提交格式：表单，UTF-8
			request.ContentType = "application/x-www-form-urlencoded; charset=utf-8";
			_request = request;
		}


		/// <summary>
		/// 写入要提交的数据到请求体中
		/// </summary>
		/// <param name="postData"></param>
		public void SetRequestData(byte[] postData)
		{
			if( postData == null )
				throw new ArgumentNullException("postData");

			if( _postData != null )
				throw new InvalidOperationException("不允许重复调用SetRequestData方法。");

			_postData = postData;
		}

		/// <summary>
		/// 写入要提交的数据到请求体中（同步版本）
		/// </summary>
		/// <param name="postData"></param>
		public void SetRequestData(string postData)
		{
			if( string.IsNullOrEmpty(postData) )
				return;

			if( _postData != null )
				throw new InvalidOperationException("不允许重复调用SetRequestData方法。");

			// 默认就用UTF-8编码发送数据
			_postData = Encoding.UTF8.GetBytes(postData);
		}

		/// <summary>
		/// 写入要提交的数据到请求体中
		/// </summary>
		/// <param name="postData"></param>
		public void SetRequestData(object postData)
		{
			if( postData == null )
				throw new ArgumentNullException("postData");

			if( _postData != null )
				throw new InvalidOperationException("不允许重复调用SetRequestData方法。");

			StringBuilder sb = new StringBuilder();
			PropertyInfo[] properties = postData.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);

			foreach( PropertyInfo p in properties ) {
				object value = p.GetValue(postData);
				string str = value == null ? string.Empty : value.ToString();

				if( sb.Length > 0 )
					sb.Append("&");

				sb.AppendFormat("{0}={1}",
					HttpUtility.UrlEncode(p.Name),
					HttpUtility.UrlEncode(str));
			}

			this.SetRequestData(sb.ToString());
		}


		/// <summary>
		/// 写入要提交的数据到请求体中
		/// </summary>
		/// <param name="postData"></param>
		public void SetRequestJsonData(object postData)
		{
			if( postData == null )
				throw new ArgumentNullException("postData");

			if( _postData != null )
				throw new InvalidOperationException("不允许重复调用SetRequestData方法。");

			string json = ToJsonString(postData);

			this.SetRequestData(json);
			_request.ContentType = "application/json; charset=utf-8";
		}


		/// <summary>
		/// 将一个对象序列化成JSON字符串。 
		/// </summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		private string ToJsonString(object obj)
		{
			//目前直接调用了System.Web.Extensions中的JavaScriptSerializer，
			//如果以后要换成其它的JSON序列化，可以重新实现这个方法。

			JavaScriptSerializer jss = new JavaScriptSerializer();
			return jss.Serialize(obj);
		}


		/// <summary>
		/// 获取服务端的响应（同步版本），
		/// 注意：这个方法并不读取响应流，仅仅只是获取响应，请在调用该方法后再调用ReadResponse方法
		/// </summary>
		/// <returns></returns>
		public HttpWebResponse GetResponse()
		{
			if( _response != null )
				throw new InvalidOperationException("不允许重复调用GetResponse()方法");


			if( _postData != null && _request.Method == "GET" ) {
				_request.Method = "POST";

				using( BinaryWriter bw = new BinaryWriter(_request.GetRequestStream()) ) {
					bw.Write(_postData);
				}
			}

			try {
				_response = (HttpWebResponse)_request.GetResponse();
			}
			catch( WebException wex ) {
				throw CreateHttpException(wex);
			}
			return _response;
		}

		/// <summary>
		/// 获取服务端的响应（异步版本），
		/// 注意：这个方法并不读取响应流，仅仅只是获取响应，请在调用该方法后再调用ReadResponse方法
		/// </summary>
		/// <returns></returns>
		public async Task<HttpWebResponse> GetResponseAsync()
		{
			if( _response != null )
				throw new InvalidOperationException("不允许重复调用GetResponse()方法");

			if( _postData != null ) {
				_request.Method = "POST";

				using( BinaryWriter bw = new BinaryWriter(await _request.GetRequestStreamAsync()) ) {
					bw.Write(_postData);
				}
			}

			try {
				_response = (HttpWebResponse)await _request.GetResponseAsync();
			}
			catch( WebException wex ) {
				throw CreateHttpException(wex);
			}
			return _response;
		}


		/// <summary>
		/// 读取响应流内容，并转换成指定的数据类型（仅支持 string, byte[]）
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		public T GetResult<T>()
		{
			if( _response == null )
				throw new InvalidOperationException("请先调用GetResponse()方法");

			using( Stream stream = _response.GetResponseStream() ) {

				Stream responseStream = stream;
				try {
					if( _response.Headers["Content-Encoding"] == "gzip" )
						responseStream = new GZipStream(stream, CompressionMode.Decompress);


					if( typeof(T) == typeof(string) )
						return (T)(object)GetText(responseStream);

					else if( typeof(T) == typeof(byte[]) )
						return (T)(object)GetBytes(responseStream);

					else
						throw new NotSupportedException("不支持的参数类型：" + typeof(T).FullName);
				}
				finally {
					if( responseStream != null && responseStream is GZipStream )
						responseStream.Dispose();
				}
			}
		}

		/// <summary>
		/// 获取服务端响应的文本内容
		/// </summary>
		/// <returns></returns>
		private string GetText(Stream stream)
		{
			using( StreamReader reader = new StreamReader(stream, GetResponseEncoding()) ) {
				return reader.ReadToEnd();
			}
		}

		private Encoding GetResponseEncoding()
		{
			string encoding = _response.CharacterSet;

			if( encoding == "ISO-8859-1" )
				// 如果在响应头中没有指定编码方式，.NET默认会返回"ISO-8859-1"，
				// 然而这个编码几乎是没人使用的，反而默认都会使用UTF8

				// 最可靠的方法还是读取响应流的内容，但那种方法会比较复杂，具体可参考ClownFish.Web.Client.ResponseReader
				return Encoding.UTF8;
			else
				return Encoding.GetEncoding(encoding);
		}


		/// <summary>
		/// 获取服务端返回的二进制内容
		/// </summary>
		/// <returns></returns>
		private byte[] GetBytes(Stream stream)
		{
			using( MemoryStream ms = new MemoryStream() ) {

				byte[] buffer = new byte[1024];
				int length = 0;

				while( (length = stream.Read(buffer, 0, 1024)) > 0 )
					ms.Write(buffer, 0, length);

				ms.Position = 0;
				return ms.ToArray();
			}
		}


		private HttpInvokeException CreateHttpException(WebException wex)
		{
			if( wex.Response == null )
				return new HttpInvokeException(wex.Message, wex,
										string.Empty, _request.RequestUri.ToString());


			HttpWebResponse response = (HttpWebResponse)wex.Response;

			using( response ) {
				Stream strem = response.GetResponseStream();
				using( StreamReader reader = new StreamReader(strem, Encoding.GetEncoding(response.CharacterSet)) ) {
					string errorHtml = reader.ReadToEnd();
					string title = GetHtmlTitle(errorHtml) ?? wex.Message;

					return new HttpInvokeException(title, wex, errorHtml, _request.RequestUri.ToString());
				}
			}
		}




		/// <summary>
		/// 尝试从一段HTML代码中读取文档标题部分
		/// </summary>
		/// <param name="text">HTML代码</param>
		/// <returns>文档标题</returns>
		private string GetHtmlTitle(string text)
		{
			if( string.IsNullOrEmpty(text) )
				return null;

			int p1 = text.IndexOf("<title>", StringComparison.OrdinalIgnoreCase);
			int p2 = text.IndexOf("</title>", StringComparison.OrdinalIgnoreCase);

			if( p2 > p1 && p1 > 0 ) {
				p1 += "<title>".Length;
				return text.Substring(p1, p2 - p1);
			}

			return null;
		}

		/// <summary>
		/// 实现IDisposable接口
		/// </summary>
		public void Dispose()
		{
			if( _response != null ) {
				_response.Dispose();
				_response = null;
			}
		}
	}


	/// <summary>
	/// 表示一个网络调用异常
	/// </summary>
	public sealed class HttpInvokeException : System.Exception
	{
		/// <summary>
		/// 构造函数
		/// </summary>
		/// <param name="message"></param>
		/// <param name="innerException"></param>
		/// <param name="responseText"></param>
		/// <param name="url"></param>
		internal HttpInvokeException(string message, Exception innerException, string responseText, string url)
			: base(message, innerException)
		{
			this.ResponseText = responseText;
			this.Url = url;
		}

		/// <summary>
		/// 异常发生时，服务端返回的响应内容
		/// </summary>
		public string ResponseText { get; set; }

		/// <summary>
		/// 异常发生时，请求的URL
		/// </summary>
		public string Url { get; set; }
	}

}
