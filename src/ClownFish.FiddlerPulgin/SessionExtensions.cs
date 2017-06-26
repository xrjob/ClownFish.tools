using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Fiddler;

namespace ClownFish.FiddlerPulgin
{
	internal static class SessionExtensions
	{
		internal static T GetRequestHeader<T>(this Session oSession, string headerName)
		{
			try {
				string text = oSession.oRequest.headers[headerName];

				return (T)Convert.ChangeType(text, typeof(T));
			}
			catch {
				return default(T);
			}
		}


		internal static T GetResponseHeader<T>(this Session oSession, string headerName)
		{
			try {
				string text = oSession.oResponse.headers[headerName];

				return (T)Convert.ChangeType(text, typeof(T));
			}
			catch {
				return default(T);
			}
		}


        public static List<DbActionInfo> TryGetDbActionList(this Session oSession)
        {
            List<DbActionInfo> list = null;

            int index = 1;
            for( ;;) {
                string headerName = "X-SQL-Action-" + (index++).ToString();
                string value = oSession.GetResponseHeader<string>(headerName);
                if( string.IsNullOrEmpty(value) )
                    break;

                DbActionInfo info = DbActionInfo.Deserialize(value);

                if( list == null )
                    list = new List<DbActionInfo>();
                list.Add(info);
            }

            return list;
        }


        public static bool AnalyzeSqlActions(this Session oSession, out int bigSqlCount, out TimeSpan sumTimeSpan)
        {
            sumTimeSpan = TimeSpan.FromMilliseconds(0d);
            bigSqlCount = 0;

            List<DbActionInfo> list = oSession.TryGetDbActionList();
            if( list == null )
                return false;
                        

            foreach( DbActionInfo info in list ) {
                if( info.SqlText == DbActionInfo.OpenConnectionFlag ) 
                    continue;

                sumTimeSpan += info.Time;
                if( info.Time.TotalMilliseconds > 1000 )
                    bigSqlCount++;
            }

            return true;
        }
    }
}
