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
        public double bankedWaterFromPreviousYear { get; set; } //mjia: round banked water to the 10ths. 

        public double desiredBankInches { get; set; } //mjia: round banked water to the 10ths. 

	}
}
