using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace HPEntities.Exceptions {
	/// <summary>
	/// Represents a validation problem with data that was submitted.
	/// Specific validation errors should be provided in the ValidationErrors property.
	/// </summary>
	public class ValidationException: Exception {

		public ValidationException(params string[] errors): base("Validation errors occurred with the provided data.") {
			this.ValidationErrors = errors;
		}

		public ValidationException(IEnumerable<string> errors) {
			this.ValidationErrors = (errors ?? new string[] {}).ToArray();
		}

		public string[] ValidationErrors { get; private set; }

	}
}