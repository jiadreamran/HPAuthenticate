using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HPEntities.Helpers {
	public static class Extensions {
		/* Moved to libMatt
		/// <summary>
		/// If the string is null or empty, returns DbNull.Value, else returns the string.
		/// </summary>
		/// <param name="str"></param>
		/// <returns></returns>
		public static object OrDbNull(this string str) {
			return string.IsNullOrEmpty(str) ? (object)DBNull.Value : str;
		}
		*/
		public static object OrDbNull<T>(this Nullable<T> n) where T: struct {
			return n.HasValue ? n.Value : (object)DBNull.Value;
		}

	}
}
