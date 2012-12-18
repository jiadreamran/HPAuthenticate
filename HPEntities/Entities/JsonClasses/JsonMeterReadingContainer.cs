using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HPEntities.Entities.JsonClasses {
	public class JsonMeterReadingContainer {

		public JsonMeterReadingContainer() { }

		public JsonMeterReadingContainer(int meterInstallationId) {
			this.meterInstallationId = meterInstallationId;
			this.acceptCalculation = true;

		}

		public JsonMeterReading[] readings { get; set; }

		public int meterInstallationId { get; set; }
		public int calculatedVolume { get; set; }
		public bool acceptCalculation { get; set; }
		public int userRevisedVolume { get; set; }
		public int? userRevisedVolumeUnitId { get; set; }
		public int totalVolumeAcreInches { get; set; }

		#region Preservation properties
		// These properties are just here to preserve data in the event that
		// the meter gets deleted down the road after a CA is submitted - this
		// allows us to know the unitId and whether it was a nozzle package
		// without having to go through gymnastics to get the data from the
		// soft-deleted record in the database.
		// It's kind of a hack - if I'd known about soft deletes when starting,
		// I wouldn't have done it this way - but it works fine as long as meter
		// data is read from stored json state rather than always refreshed from
		// the database.


		public int? unitId { get; set; }
		public bool isNozzlePackage { get; set; }
        public bool isElectric { get; set; }
        public bool isNaturalGas { get; set; }
        public bool isThirdParty { get; set; }
        public double depthToRedbed { get; set; }
		public int rolloverValue { get; set; }
		public string county { get; set; }
		public int countyId { get; set; }
		public string meterType { get; set; }
		public string nonStandardUnits { get; set; }
		public double meterMultiplier { get; set; }

        public double squeezeValue { get; set; } // A test to see if it gets stored in ReportingSummary Json

		#endregion

	}
}
