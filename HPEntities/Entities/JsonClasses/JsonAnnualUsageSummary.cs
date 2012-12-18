using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HPEntities.Entities.JsonClasses {
	public class JsonAnnualUsageSummary {

		/// <summary>
		/// Contiguous area, in acres
		/// </summary>
		public double contiguousArea { get; set; }
		public double annualVolume { get; set; }
		public int allowableApplicationRate { get; set; }
		public int bankedWaterFromPreviousYear { get; set; }

		public int desiredBankInches { get; set; }

	}
}
