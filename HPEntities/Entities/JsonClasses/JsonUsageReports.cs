using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HPEntities.Entities.JsonClasses {
	/// <summary>
	/// Used by the admin interface to provide an overview of users' usage reports.
	/// </summary>
	public class JsonUsageReports {

		public int userId { get; set; }
		public string email { get; set; }
		public string name { get; set; }
		public Dictionary<int, IEnumerable<JsonContiguousAcres>> years { get; set; }


	}
}
