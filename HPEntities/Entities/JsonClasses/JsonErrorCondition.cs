using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HPEntities.Entities.JsonClasses {
	public class JsonErrorCondition {

		public int[] wellIds { get; set; }
		public string errorCondition { get; set; }
		public string userResponse { get; set; }

	}
}
