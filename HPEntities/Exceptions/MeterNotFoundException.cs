using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HPEntities.Exceptions {
	public class MeterNotFoundException: Exception {

		public MeterNotFoundException(int meterInstallationId)
			: base("Meter installation ID " + meterInstallationId.ToString() + " was not found in the database.") { }
	}
}
