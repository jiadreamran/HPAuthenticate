using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HPEntities.Dalcs {
	public class LogDalc: AuthDalcBase {

		public void SaveLogMessage(string message) {
			ExecuteNonQuery(@"
insert into Logs (
	Message,
	CreateDatetime
) values (
	@msg,
	getdate()
);", new Param("@msg", message));
		}

		public void SaveError(User user, Exception ex) {
			SaveLogMessage(string.Format(
				"[EXCEPTION] User: {0}; Message: {1}; Stack trace: {2}",
				user != null ? user.Email : "(null)",
				ex.Message,
				ex.StackTrace
			));
			
		}

	}
}
