using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HPEntities.Entities.JsonClasses {
	public class JsonContiguousAcres {

		public JsonContiguousAcres() {
			this.meterReadings = new Dictionary<int, JsonMeterReadingContainer>();
			this.annualUsageSummary = new JsonAnnualUsageSummary();
			this.bankedWaterHistory = new Dictionary<int, JsonBankedWaterRecord>();
			this.meterInstallationErrors = new Dictionary<int, JsonErrorCondition>();
			this.cafos = new JsonCafo[] {};
		}

		public JsonContiguousAcres(ContiguousAcres contigAcres, int year): this() {
			this.number = contigAcres.caID;
			this.description = contigAcres.Description;
			this.wells = contigAcres.Wells.Select(x => new JsonWell(x)).ToArray();

			this.year = year;
		}


		public int year { get; set; }
		public bool isSubmitted { get; set; }
		public int number { get; set; }
		public string description { get; set; }
        public bool userRevisedVolume { get; set; }
		public JsonWell[] wells { get; set; }

		public Dictionary<int, JsonErrorCondition> meterInstallationErrors { get; set; }

		public Dictionary<int, JsonMeterReadingContainer> meterReadings { get; set; }

		public JsonAnnualUsageSummary annualUsageSummary { get; set; }

		/// <summary>
		/// Record of previous banked water values, keyed by operating year.
		/// </summary>
		public Dictionary<int, JsonBankedWaterRecord> bankedWaterHistory { get; set; }

		public JsonCafo[] cafos { get; set; }

		public int ownerClientId { get; set; }
		public bool isCurrentUserOwner { get; set; }
	}
}
