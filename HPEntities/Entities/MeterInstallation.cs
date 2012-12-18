using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HPEntities.Entities.Enums;

namespace HPEntities.Entities {
	public class MeterInstallation {

		public int Id { get; set; }
		public int CountyId { get; set; }
		public string County { get; set; }
		public int RolloverValue { get; set; }
		public MeterType MeterType { get; set; }
		public double Multiplier { get; set; }
		/// <summary>
		/// This corresponds to the UnitsOfMeasure table.
		/// If null, assume gallons.
		/// </summary>
		public int? UnitId { get; set; }
	}
}
