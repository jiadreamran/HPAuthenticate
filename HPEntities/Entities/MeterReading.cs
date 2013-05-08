using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HPEntities.Entities.Enums;
using HPEntities.Entities.JsonClasses;

namespace HPEntities.Entities {
	public class MeterReading {

		public int MeterInstallationReadingId { get; set; }
		public int MeterInstallationId { get; set; }
		public DateTime DateTime { get; set; }
		public string Date {
			get {
				return this.DateTime.ToString("yyyy-MM-dd");
			}
		}
		public double? Reading { get; set; }
		public int ActingUserId { get; set; }
		public string ActingDisplayName { get; set; }
		public int ActualUserId { get; set; }
		public string ActualDisplayName { get; set; }
		public double? Rate { get; set; }

        public int? IsSubmitted { get; set; }
        public int? ReadingType { get; set; }
        public int? ReportingYear { get; set; }
	}
}
