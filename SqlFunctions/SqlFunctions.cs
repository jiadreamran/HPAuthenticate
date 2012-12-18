using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.SqlServer.Server;
using System.Data.SqlTypes;

	public class T {

		[SqlFunction(DataAccess = DataAccessKind.None, IsDeterministic = true)]
		public static SqlString RegexReplace(SqlChars input, SqlString pattern, SqlString replacement) {
			return new SqlString(Regex.Replace(new String(input.Value), pattern.Value, replacement.Value));
		}

	}

