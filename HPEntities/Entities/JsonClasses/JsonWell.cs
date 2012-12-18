using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HPEntities.Entities.JsonClasses {
	public class JsonWell {

		public JsonWell() { }

		public JsonWell(Well well) {
			this.id = well.WellId;
			this.permitNumber = well.PermitNumber.GetValueOrDefault();
			this.meterInstallationIds = well.MeterInstallationIds;
			this.errorCondition = "";
			this.errorResponse = well.ErrorResponse;
			this.county = well.County;
			this.countyId = well.CountyId;
		}

		public int id { get; set; }
		public int permitNumber { get; set; }
		public int[] meterInstallationIds { get; set; }
		public string errorCondition { get; set; }
		public string errorResponse { get; set; }
		public string county { get; set; }
		public int countyId { get; set; }

	}
}
