using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClownFish.FiddlerPulgin
{
	public static class SomeExtenstions
	{
		public static string SubstringN(this string text, int keepLength)
		{
			if( string.IsNullOrEmpty(text) )
				return text;

			if( text.Length <= keepLength )
				return text;

			return text.Substring(0, keepLength) + "..." + text.Length.ToString();
		}

		/// <summary>
		/// 修复从CData读取的文本中丢失的换行符
		/// 在XmlCommand的SQL文本中 \r\n 读取后会变成 \n ，这是XML规范问题
		/// </summary>
		/// <param name="text"></param>
		/// <returns></returns>
		private static string RepairLFchar(string text)
		{
			if( string.IsNullOrEmpty(text) )
				return text;

			StringBuilder sb = new StringBuilder();
			string line = null;

			using(StringReader reader = new StringReader(text) ) {
				while((line = reader.ReadLine()) != null ) {
					sb.AppendLine(line);
				}
			}
			return sb.ToString();
		}

		internal static string ToShowText(this DbActionInfo info)
		{
			if( info.SqlText == DbActionInfo.OpenConnectionFlag ) 
				return string.Empty;


			string commandText = RepairLFchar(info.SqlText);
			StringBuilder sb = new StringBuilder(2048);
			sb.AppendLine(commandText);
			sb.AppendLine();


			if( info.ErrorMsg != null ) {
				sb.AppendLine()
					.AppendLine("---------------------------------------------------------------------------------")
					.AppendLine("Error:")
					.AppendLine(info.ErrorMsg)
					.AppendLine();
			}

			if( info.Parameters != null && info.Parameters.Count > 0 ) {
				sb.AppendLine()
					.AppendLine("---------------------------------------------------------------------------------")
					.AppendLine("Parameters:");

				foreach( CommandParameter p in info.Parameters )
					sb.AppendFormat("  {0} = ({1}) {2}\r\n", p.Name, p.DbType, p.Value);

				sb.AppendLine("\r\n");
			}

			return sb.ToString();
		}


        internal static string ToSqlText(this DbActionInfo info)
        {
            if( info.SqlText == DbActionInfo.OpenConnectionFlag )
                return string.Empty;


            string commandText = RepairLFchar(info.SqlText);
            StringBuilder sb = new StringBuilder(2048);

            if( info.Parameters != null && info.Parameters.Count > 0 ) {
                foreach( CommandParameter p in info.Parameters ) 
                    sb.AppendLine(p.ToDeclareString());
            }


            sb.AppendLine(commandText);
            sb.AppendLine("\r\n");

            return sb.ToString();
        }


        private static string ToDeclareString(this CommandParameter p)
        {
            string paraName = p.Name.StartsWith("@") ? p.Name : ("@" + p.Name);
            StringBuilder sb = new StringBuilder(512);
            switch( p.DbType ) {
                case "AnsiString":
                case "AnsiStringFixedLength":
                case "StringFixedLength":
                case "String":
                case "Xml":
                    // 这里不知道字符中的长度，统一定义成 nvarchar(max)
                    sb.AppendLine($"declare {paraName} as nvarchar(max);");
                    sb.AppendLine($"set {paraName} = N'{p.Value}';");
                    break;

                case "Boolean":
                    sb.AppendLine($"declare {paraName} as bit;");
                    string boolValue = p.Value == "NULL" ? "NULL" : (p.Value == "True" ? "1" : "0");
                    sb.AppendLine($"set {paraName} = {boolValue};");
                    break;

                case "Date":
                case "DateTime":
                    sb.AppendLine($"declare {paraName} as datetime;");
                    sb.AppendLine($"set {paraName} = '{p.Value}';");
                    break;


                case "Guid":
                    sb.AppendLine($"declare {paraName} as uniqueidentifier;");
                    sb.AppendLine($"set {paraName} = '{p.Value}';");
                    break;

                case "Int16":
                case "UInt16":
                    sb.AppendLine($"declare {paraName} as smallint;");
                    sb.AppendLine($"set {paraName} = {p.Value};");
                    break;

                case "Int32":
                case "UInt32":
                    sb.AppendLine($"declare {paraName} as int;");
                    sb.AppendLine($"set {paraName} = {p.Value};");
                    break;

                case "Int64":
                case "UInt64":
                    sb.AppendLine($"declare {paraName} as bigint;");
                    sb.AppendLine($"set {paraName} = {p.Value};");
                    break;

                // TODO: 下面几个数字的映射要测试
                case "Currency":
                    sb.AppendLine($"declare {paraName} as money;");
                    sb.AppendLine($"set {paraName} = {p.Value};");
                    break;

                case "Decimal":
                    sb.AppendLine($"declare {paraName} as decimal;");
                    sb.AppendLine($"set {paraName} = {p.Value};");
                    break;

                case "Double":
                case "Single":
                    sb.AppendLine($"declare {paraName} as float;");
                    sb.AppendLine($"set {paraName} = {p.Value};");
                    break;

                case "VarNumeric":
                    sb.AppendLine($"declare {paraName} as real;");
                    sb.AppendLine($"set {paraName} = {p.Value};");
                    break;


                // 不支持的类型，不知道给什么样的类型才好，程序中应该不会用到这些偏门的类型
                case "Object":
                case "SByte":
                case "Binary":
                case "Byte":
                case "DateTime2":
                case "DateTimeOffset":
                case "Time":
                default:
                    sb.AppendLine($"declare {paraName} as xxxxxxxxxxxx;");
                    sb.AppendLine($"set {paraName} = {p.Value};");
                    break;

            }
            return sb.ToString();
        }


    }
}
