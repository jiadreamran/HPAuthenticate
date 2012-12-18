using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using HPEntities.Dalcs;

namespace HPAuthenticate.Helpers {
	public static class Logger {



		/// <summary>
		/// Returns the exception message and messages from any InnerExceptions (recursively traversed).
		/// </summary>
		/// <param name="ex"></param>
		/// <returns></returns>
		private static string GetExceptionMessage(Exception ex) {
			if (ex != null) {
				return ex.Message + "\n\nStack Trace:\n" + ex.StackTrace + (ex.InnerException != null ? "\n\nInner Exception: " + GetExceptionMessage(ex.InnerException) : "");
			}
			return "Application error occurred; no exception data available. (Logger.GetExceptionMessage: ex is null)";
		}

		/// <summary>
		/// Logs the specified error message to the database.
		/// </summary>
		/// <param name="error"></param>
		public static void LogError(string error) {
			MailHelper.NotifyAdmins("HPWD Meter Registration: Error", error);
		}

		/// <summary>
		/// Logs the specified exception to the database.
		/// </summary>
		/// <param name="ex">(Exception) The exception to log.</param>
		public static void LogError(Exception ex) {
			LogError(GetExceptionMessage(ex));
		}
		
		/// <summary>
		/// Saves the specified message to the database.
		/// </summary>
		/// <param name="message">(string) The message to save.</param>
		//public static void LogMessage(string message) {
		//	new LogDalc().LogMessage(message);
		//}

		/// <summary>
		/// This is a special-purpose function for logging errors that occur
		/// while trying to send mail. It intentionally does _not_ send mail
		/// (because that's the operation that just failed). For general-purpose
		/// error logging, use LogError instead of this function.
		/// </summary>
		/// <param name="ex"></param>
		internal static void LogMailError(Exception ex) {
			new LogDalc().SaveLogMessage(GetExceptionMessage(ex) + ". Stack Trace: " + ex.StackTrace);
		}

		public static void LogMessage(string msg) {
			new LogDalc().SaveLogMessage(msg);
		}


		#region Public formatting helpers

		/// <summary>
		/// Returns the exception contents (including message, stack trace,
		/// and inner exception) as HTML.
		/// </summary>
		/// <param name="ex"></param>
		/// <returns></returns>
		public static string FormatExceptionHtml(Exception ex) {
			// Temp - standin until I decide on better formatting, if ever
			return GetExceptionMessage(ex).Replace("\n", "<br />");
		}


		#endregion

	}
}