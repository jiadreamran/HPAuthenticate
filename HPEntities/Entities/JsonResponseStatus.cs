using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HPEntities.Entities {
	public class JsonResponseStatus {

		public JsonResponseStatus(bool success, params string[] errors) {
			this.Success = success;
			this.Errors = new List<string>(errors.Where(x => !string.IsNullOrWhiteSpace(x)));
		}

		public bool Success { get; set; }
		public List<string> Errors { get; set; }

	}
}
