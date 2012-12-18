using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace HPEntities.Entities.Enums {
	public enum MeterUnits {
		[Description("Unknown")]
		unknown = 0,
		[Description("Gallons")]
		gallons = 1,
		[Description("kWh")]
		kwh = 2,
		[Description("Mcf")]
		mcf = 3,
		[Description("GPM")]
		gpm = 4
	}
}
