using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HPEntities.Entities.JsonClasses {
	public class JsonBankedWaterRecord {

		public JsonBankedWaterRecord(int objectId, int acres, int bankedInches, int year) {
			this.caObjectId = objectId;
			this.acres = acres;
			this.bankedInches = bankedInches;
			this.year = year;
		}

		public int caObjectId { get; set; }
		public int acres { get; set; }
		public int bankedInches { get; set; }
		public int year { get; set; }

	}
}
