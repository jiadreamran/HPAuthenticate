using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HPEntities.Entities.JsonClasses {
	public class JsonMeterReading {

		public double? reading { get; set; }
		public double? rate { get; set; }

		// Indicates whether a meter reading is valid
		// for calculating volume (falls within Dec 15 - Jan 15
		// date window) or not (midyear readings, still 
		// displayed but not taken into account for calculations).
		public bool isValidBeginReading{ get; set; }
		public bool isValidEndReading { get; set; }
        public bool isAnnualTotalReading { get; set; }

        public int meterInstalltionReadingID { get; set; }
	}
}
