﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using ClownFish.HttpTest;
using Fiddler;

namespace ClownFish.FiddlerPulgin
{
	public class FiddlerExtention : IAutoTamper
	{
		#region 变量定义

		private TabPage _tabPageDbAction;
		private TabPage _tabPageNotReasonable;

		private DbActionListControl _dbActionCtrl;
		private NotReasonableControl _notReasonableCtrl;
		
		private MenuItem _menuItemCopySessionRequest;
		private MenuItem _menuItemCopySessionAsTestCase;
		private MethodInfo _lvSessions_KeyDown;

		private string _headerFlag;
		private string _fileVersion;

		#endregion

		#region IFiddlerExtension 成员

		public void OnLoad()
		{
			// 添加右窗口的展示选项卡
			SetupTabPage();

			// 设置上下文菜单
			SetMenu();

			// 替换原有的快捷键处理程序
			ReplaceCopyEvent();

			FileVersionInfo verInfo = FileVersionInfo.GetVersionInfo(typeof(FiddlerExtention).Assembly.Location);
			_fileVersion = verInfo.FileVersion;
		}
		
		public void OnBeforeUnload()
		{

		}

		#endregion


		#region 初始化Fiddler插件UI

		private void SetupTabPage()
		{
			// 【数据库访问】 选项卡
			AddDbActionTabPage();

			// 【不规范设计】 选项卡
			AddNotReasonableTabPage();


			// 绑定Fiddler主窗体事件
			FiddlerApplication.UI.tabsViews.SelectedIndexChanged += tabsViews_SelectedIndexChanged;
			FiddlerApplication.UI.lvSessions.SelectedIndexChanged += lvSessions_SelectedIndexChanged;

			// 刷新请求标记
			OnEnableProfilerStatusChanged(null, null);
		}

		private void AddDbActionTabPage()
		{
			FiddlerApplication.UI.tabsViews.ImageList.Images.Add(Properties.Resources.SQL);

			// 创建Fiddler选项卡
			_tabPageDbAction = new TabPage("数据库访问");
			_tabPageDbAction.ImageIndex = FiddlerApplication.UI.tabsViews.ImageList.Images.Count - 1;
			_dbActionCtrl = new DbActionListControl();

			_tabPageDbAction.Controls.Add(_dbActionCtrl);
			_dbActionCtrl.Dock = DockStyle.Fill;

			// 订阅启用禁用事件
			_dbActionCtrl.OnEnableProfilerChanged += OnEnableProfilerStatusChanged;

			FiddlerApplication.UI.tabsViews.TabPages.Add(_tabPageDbAction);
		}
		
		private void AddNotReasonableTabPage()
		{
			FiddlerApplication.UI.tabsViews.ImageList.Images.Add(Properties.Resources.Error);

			// 创建Fiddler选项卡
			_tabPageNotReasonable = new TabPage("不规范设计");
			_tabPageNotReasonable.ImageIndex = FiddlerApplication.UI.tabsViews.ImageList.Images.Count - 1;
			_notReasonableCtrl = new NotReasonableControl();

			_tabPageNotReasonable.Controls.Add(_notReasonableCtrl);
			_notReasonableCtrl.Dock = DockStyle.Fill;

			// 订阅启用禁用事件
			_notReasonableCtrl.OnEnableProfilerChanged += OnEnableProfilerStatusChanged;

			FiddlerApplication.UI.tabsViews.TabPages.Add(_tabPageNotReasonable);
		}


		private void SetMenu()
		{
			// 扩展菜单项
			_menuItemCopySessionRequest = new MenuItem("保存所有选择会话的Request到剪切板......");
			_menuItemCopySessionRequest.Shortcut = Shortcut.CtrlC;
			_menuItemCopySessionRequest.ShowShortcut = true;
			FiddlerApplication.UI.miSessionCopy.MenuItems.Add(_menuItemCopySessionRequest);
			_menuItemCopySessionRequest.Click += menuItemCopySessionRequest_Click;


			_menuItemCopySessionAsTestCase = new MenuItem("从所有选择会话创建测试用例，并将内容复制到剪切板......");
			FiddlerApplication.UI.miSessionCopy.MenuItems.Add(_menuItemCopySessionAsTestCase);
			_menuItemCopySessionAsTestCase.Click += menuItemCopySessionAsTestCase_Click;
		}


		private void menuItemCopySessionAsTestCase_Click(object sender, EventArgs e)
		{
			Fiddler.Session[] sessions = FiddlerApplication.UI.GetSelectedSessions();
			if( sessions == null || sessions.Length == 0 )
				return;

			List<RequestTest> list = new List<HttpTest.RequestTest>(sessions.Length);

			// 获取所有的请求内容
			foreach( Fiddler.Session session in sessions ) {
				string lineHeaders = session.oRequest.headers.ToString(true, false, true);
				lineHeaders = FilterRequestHeader(lineHeaders);
				string postData = session.GetRequestBodyAsString();


				RequestTest test = new RequestTest();
				list.Add(test);

				if( IsWithoutPostData(session.RequestMethod))
					test.Request = "\r\n" + lineHeaders;
				else
					test.Request = "\r\n" + lineHeaders + "\r\n" + postData + "\r\n";

				test.Category = string.Empty;
				test.Title = string.Empty;
				test.Response = new ResponseAssert();


				if( session.bHasResponse ) {
					test.Response.StatusCode = session.ResponseHeaders.HTTPResponseCode;
					test.Response.Headers = new List<ResponseHeaderAssert>();
					test.Response.Body = new List<ResponseBodyAssert>();

					// 默认用 Content-Type 做为示例
					var header = session.ResponseHeaders.FirstOrDefault(x => x.Name == "Content-Type");
					if( header != null ) {
						test.Response.Headers.Add(new ResponseHeaderAssert {
							Name = header.Name,
							AssertMode = "==",
							Value = header.Value });
					}
					else {
						// 增加一个空模板
						test.Response.Headers.Add(new ResponseHeaderAssert {
							Name = "header_Name",
							AssertMode = "",
							Value = "this_is_placeholder"  // 加下划线方便双击选择
						});
					}

					// 增加一个空模板
					test.Response.Body.Add(new ResponseBodyAssert {
						Name = "Text",
						AssertMode = "",
						Value = "this_is_placeholder"  // 加下划线方便双击选择
					});
				}
			}
			

			string xml = ClownFish.Base.Xml.XmlHelper.XmlSerialize(list, Encoding.UTF8);
			Clipboard.SetText(xml);
		}


		private bool IsWithoutPostData(string method)
		{
			// 参考 Fiddler 的判断规则
			return (method == "GET"
				|| method == "HEAD"
				|| method == "TRACE"
				|| method == "DELETE"
				|| method == "CONNECT"
				|| method == "MKCOL"
				|| method == "COPY"
				|| method == "MOVE"
				|| method == "UNLOCK"
				|| method == "OPTIONS"
				);
		}

		private string FilterRequestHeader(string text)
		{
			StringBuilder sb = new StringBuilder();

			// 去掉一些无用头
			using( StringReader reader = new StringReader(text) ) {
				string line = null;
				while( (line = reader.ReadLine()) != null ) {
					if( line.StartsWith("X-Fiddler-Profiler:", StringComparison.OrdinalIgnoreCase)
						|| line.StartsWith("Host:", StringComparison.OrdinalIgnoreCase)
						|| line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)
						)
						continue;

					sb.AppendLine(line);
				}
			}

			return sb.ToString();
		}


		private void menuItemCopySessionRequest_Click(object sender, EventArgs e)
		{
			Fiddler.Session[] sessions = FiddlerApplication.UI.GetSelectedSessions();
			if( sessions == null || sessions.Length == 0 )
				return;

			StringBuilder sb = new StringBuilder();

			// 获取所有的请求内容
			foreach( Fiddler.Session session in sessions ) {
				string lineHeaders = session.oRequest.headers.ToString(true, false, true);
				lineHeaders = FilterRequestHeader(lineHeaders);
				string postData = session.GetRequestBodyAsString();

				sb.AppendLine(lineHeaders);
				sb.AppendLine(postData).AppendLine("\r\n");
			}

			Clipboard.SetText(sb.ToString());
		}


		/// <summary>
		/// 替换原有的快捷键处理程序: CTRL - C
		/// </summary>
		private void ReplaceCopyEvent()
		{
			_lvSessions_KeyDown = FiddlerApplication.UI.GetType().GetMethod("lvSessions_KeyDown", BindingFlags.Instance | BindingFlags.NonPublic);
			if( _lvSessions_KeyDown == null )
				return;

			// 移除原先的事件处理器
			KeyEventHandler handler = (KeyEventHandler)Delegate.CreateDelegate(typeof(KeyEventHandler), FiddlerApplication.UI, _lvSessions_KeyDown);
			MethodInfo removeEvent = typeof(Control).GetMethod("remove_KeyDown", BindingFlags.Instance | BindingFlags.Public);
			removeEvent.Invoke(FiddlerApplication.UI.lvSessions, new object[] { handler });


			// 重新挂接事件处理器
			MethodInfo eventMethod = this.GetType().GetMethod("lvSessionsKeyDown", BindingFlags.Instance | BindingFlags.NonPublic);
			KeyEventHandler handler2 = (KeyEventHandler)Delegate.CreateDelegate(typeof(KeyEventHandler), this, eventMethod);

			MethodInfo addEvent = typeof(Control).GetMethod("add_KeyDown", BindingFlags.Instance | BindingFlags.Public);
			addEvent.Invoke(FiddlerApplication.UI.lvSessions, new object[] { handler2 });
		}

		/// <summary>
		/// 替换Fiddler的快捷键处理方式，此方法采用反射调用
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		void lvSessionsKeyDown(object sender, KeyEventArgs e)
		{
			if( e.Control ) {
				if( e.KeyCode == Keys.C ) {
					menuItemCopySessionRequest_Click(null, null);
					return;
				}
				else if( e.KeyCode == Keys.X ) {
					FiddlerApplication.UI.actRemoveAllSessions();
					_dbActionCtrl.ClearUI();
					return;
				}
			}

			// 调用原先的事件处理器
			_lvSessions_KeyDown.Invoke(FiddlerApplication.UI, new object[] { sender, e });

		}


		#endregion


		#region 事件订阅

		void OnEnableProfilerStatusChanged(object sender, EventArgs e)
		{
			_headerFlag =
						(_dbActionCtrl.EnableDbProfiler ? "db," : string.Empty)
						+
						(_notReasonableCtrl.EnableAnalyzeRequest ? "ar," : string.Empty);
		}

		private void tabsViews_SelectedIndexChanged(object sender, EventArgs e)
		{
			// 每次激活选项卡就强制刷新一次
			SessionsSelectedIndexChanged();
		}

		private void lvSessions_SelectedIndexChanged(object sender, EventArgs e)
		{
			SessionsSelectedIndexChanged();
		}

		private void SessionsSelectedIndexChanged()
		{
			if( FiddlerApplication.UI.tabsViews.SelectedTab == _tabPageDbAction ) {
				Session oSession = FiddlerApplication.UI.GetFirstSelectedSession();
				if( oSession == null || oSession.bHasResponse == false )
					return;

				_dbActionCtrl.RefreshUI(oSession);
			}

		}

		
		

		#endregion




		#region IAutoTamper 成员

		public void AutoTamperRequestAfter(Session oSession)
		{

		}

		public void AutoTamperRequestBefore(Session oSession)
		{
			if( string.IsNullOrEmpty(_headerFlag) == false ) { 
			// 发送一个特殊头，供服务端识别
				oSession.oRequest["X-Fiddler-Profiler"] = _headerFlag;
				//oSession.oRequest["X-Fiddler.FiddlerPulgin-Ver"] = _fileVersion;
			}
		}

		private static readonly Regex s_urlTimeVersionRegex = new Regex(@"[?&]_t=\d+", RegexOptions.Compiled);

		public void AutoTamperResponseAfter(Session oSession)
		{
			if( oSession.bHasResponse == false )	// 确保服务端已响应请求
				return;

			string profilerFlag = oSession.GetResponseHeader<string>("X-Fiddler-AnalyzeRequest");
			if( profilerFlag != "OK" )				// 确保是已知的服务端回应
				return;

			//----------------------------------------------------------------------

			if( oSession.responseCode == 404 ) {
				this._notReasonableCtrl.AddSession("404-错误", oSession, false);
			}			
			else if( oSession.responseCode == 500 ) {
				this._notReasonableCtrl.AddSession("500-程序异常", oSession, false);
			}


			if( oSession.responseBodyBytes != null && oSession.responseBodyBytes.Length > 512 * 1024 )
				this._notReasonableCtrl.AddSession("服务端输出内容大于512K", oSession, false);


			int connectionCount = oSession.GetResponseHeader<int>("X-SQL-ConnectionCount");
			if( connectionCount > 3 )
				this._notReasonableCtrl.AddSession("数据库连接次数超过3次*", oSession, true);


			TimeSpan times = oSession.Timers.ClientDoneResponse - oSession.Timers.ClientBeginRequest;
			if( times.TotalMilliseconds > 2000 )
				this._notReasonableCtrl.AddSession("网络请求时间超过2秒", oSession, false);


			string contentType = oSession.GetResponseHeader<string>("Content-Type");
			if( contentType.StartsWith("application/x-javascript", StringComparison.OrdinalIgnoreCase)
				|| contentType.StartsWith("text/css", StringComparison.OrdinalIgnoreCase) ) {

				string expires = oSession.GetResponseHeader<string>("Expires");
				string cacheControl = oSession.GetResponseHeader<string>("Cache-Control");

				if( string.IsNullOrEmpty(expires) || cacheControl.StartsWith("public, max-age=") == false )
					this._notReasonableCtrl.AddSession("资源文件没有设置缓存响应头*", oSession, true);

				if( s_urlTimeVersionRegex.IsMatch(oSession.PathAndQuery) == false )
					this._notReasonableCtrl.AddSession("资源文件没有指定版本号*", oSession, true);
			}


			string requestWith = oSession.GetResponseHeader<string>("X-Requested-With");
			if( string.IsNullOrEmpty(requestWith) == false )
				// 分析重复请求
				this._notReasonableCtrl.AnalyzeRepeatRequest(oSession);
		}

		public void AutoTamperResponseBefore(Session oSession)
		{

		}

		public void OnBeforeReturningError(Session oSession)
		{
			
		}

		#endregion





	}
}
