using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using libMatt.Formatters;
using HPEntities.Dalcs;

namespace HPEntities.Entities.JsonClasses {
	public class JsonMeterInstallation {

		public JsonMeterInstallation() { }

		public JsonMeterInstallation(MeterInstallation mi) {
			this.id = mi.Id;
			this.countyId = mi.CountyId;
			this.county = mi.County;
			this.rolloverValue = mi.RolloverValue;
			this.meterType = mi.MeterType.Description();
			this.meterMultiplier = mi.Multiplier;
			this.unitId = mi.UnitId;
			this.nonStandardUnits = (mi.MeterType == Enums.MeterType.standard ? string.Empty : new MeterDalc().GetUnits(mi.MeterType).Description());
		}

		public int id { get; set; }
		public int countyId { get; set; }
		public string county { get; set; }
		public int rolloverValue { get; set; }
		public string meterType { get; set; }
		public double meterMultiplier { get; set; }
		public int? unitId { get; set; }
		/// <summary>
		/// kwh or mcf for non-water meters; blank otherwise
		/// </summary>
		public string nonStandardUnits { get; set; }

	}
}
