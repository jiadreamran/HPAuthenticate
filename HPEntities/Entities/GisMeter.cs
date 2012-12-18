using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HPEntities.Entities {
	public class GisMeter {

		public int MeterInstallationId { get; set; }
		public string Manufacturer { get; set; }
		public string Model { get; set; }
		public string Serial { get; set; }
		public string Size { get; set; }
		public string Guid { get; set; }
		public string Gf_Manu { get; set; }
		public string Gf_Model { get; set; }
		public DateTime? inst_date { get; set; }
		public DateTime? read_date { get; set; }
		public string Producer { get; set; }
		public string Owner { get; set; }
		public int Shape { get; set; }
		public int ActualUserId { get; set; }
		public int ActingUserId { get; set; }

		public double? InitialReading { get; set; }

		public string Application { get; set; }
	}
}
