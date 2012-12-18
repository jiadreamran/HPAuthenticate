using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HPEntities.Entities.JsonClasses {
	public class JsonReportingYear {

		public JsonReportingYear() {
			this.calculatedVolumes = new Dictionary<int, object[]>();
			this.contiguousAcres = new List<JsonContiguousAcres>();
		}

		/// <summary>
		/// Keyed by meterInstallationId; value is an array where index 0 == the volume
		/// and index 1 == a bool indicating whether the meter rolled over
		/// </summary>
		public Dictionary<int, object[]> calculatedVolumes { get; set; }

		public List<JsonContiguousAcres> contiguousAcres { get; set; }

	}
}
