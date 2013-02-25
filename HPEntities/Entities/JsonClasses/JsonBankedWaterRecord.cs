using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HPEntities.Entities.JsonClasses {
	public class JsonBankedWaterRecord {

        public JsonBankedWaterRecord(int objectId, int acres, double bankedInches, int year)
        {
			this.caObjectId = objectId;
			this.acres = acres;
			this.bankedInches = bankedInches;
			this.year = year;
		}

		public int caObjectId { get; set; }
		public int acres { get; set; }
		public double bankedInches { get; set; } //mjia: round banked water to the 10ths. 
		public int year { get; set; }

	}
}
