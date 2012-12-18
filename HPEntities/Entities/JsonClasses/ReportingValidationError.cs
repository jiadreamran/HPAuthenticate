using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HPEntities.Entities.JsonClasses {
	public class ReportingValidationError {

		/// <summary>
		/// Message will optionally have identifiers for replacement, including:
		///		#{wellNumber}
		///		#{meterInstallationId}
		/// </summary>
		public string message { get; set; }
		public ReportingValidationErrorResponse[] responses { get; set; }

	}

	public class ReportingValidationErrorResponse {

		public ReportingValidationErrorResponse() { }

		public ReportingValidationErrorResponse(string user, string hpwd) {
			this.user = user;
			this.hpwd = hpwd;
		}
		public ReportingValidationErrorResponse(string user, string hpwd, string identifier) {
			this.user = user;
			this.hpwd = hpwd;
			this.identifier = identifier;
		}

		public string user { get; set; }
		public string hpwd { get; set; }
		public string identifier { get; set; }
	}

}
