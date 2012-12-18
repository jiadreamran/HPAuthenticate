using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Net.Mail;
using System.Configuration;
using libMatt.Converters;
using System.Net;

namespace HPAuthenticate.Helpers {
	public static class MailHelper {

		public static bool NotifyAdmins(string subject, string message) {
			var adminEmails = ConfigurationManager.AppSettings["admin_email_addresses"];
			return Send(adminEmails, subject, message);
		}

		public static bool Send(string to, string subject, string body) {
			return Send(to, null, subject, body);
		}

		public static bool Send(string to, string replyTo, string subject, string body) {
			try {
				var host = ConfigurationManager.AppSettings["smtp_host"];
				var port = ConfigurationManager.AppSettings["smtp_port"].ToInteger();
				var username = ConfigurationManager.AppSettings["smtp_username"];
				var password = ConfigurationManager.AppSettings["smtp_password"];
				var client = new SmtpClient(host, port);

				if (!string.IsNullOrWhiteSpace(username)) {
					client.Credentials = new System.Net.NetworkCredential(username, password);
				}

				var fr = "High Plains Water District <" + ConfigurationManager.AppSettings["mail_from_address"] + ">";
				var msg = new MailMessage(fr, to, subject, body);
				if (!string.IsNullOrWhiteSpace(replyTo)) {
					msg.ReplyToList.Clear();
					msg.ReplyToList.Add(replyTo);
				}
				client.SendAsync(msg, null);
				Logger.LogMessage(string.Format("Sent mail from {0}{1} to {2}, subject: {3}", fr, string.IsNullOrWhiteSpace(replyTo) ? "" : " (reply-to " + replyTo + ")", to, subject));
			} catch (Exception ex) {
				// Suppress further error messages, since we were unable to send this one,
				// but log the problem somewhere to disk.
				Logger.LogMailError(ex);
				return false;
			}

			return true;
		}


		internal static void NotifyAdmins(string message) {
			NotifyAdmins("[HPWD web] Admin notification", message);
		}
	}
}