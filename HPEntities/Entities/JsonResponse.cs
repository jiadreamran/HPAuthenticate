using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HPEntities.Entities {
	public class JsonResponse {

		public JsonResponse(bool success, params string[] errors): this(success, null, errors) { }

		public JsonResponse(bool success, object data, params string[] errors) {
			this.Status = new JsonResponseStatus(success, errors);
			this.Data = data;
		}

		public JsonResponseStatus Status { get; private set; }

		public object Data { get; set; }
	}
}
