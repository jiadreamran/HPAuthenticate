using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace HPEntities.Entities.Enums {
	/// <summary>
	/// Enumerates the possible meter types.
	/// 
	/// WARNING: Do not screw around with these values or descriptions
	/// unless you know what you're doing. The frontend relies on them.
	/// Hardcoded values in script should be identified by // HARDCODE
	/// comments.
	/// </summary>
	public enum MeterType {
		[Description("Water Meter")]
		standard = 0,
		[Description("Electric")]
		electric = 1,
		[Description("Natural gas")]
		natural_gas = 2,
		[Description("Nozzle package")]
		nozzle_package = 3,
		[Description("Bizarro-meter")]
		bizarro = 4
	}
}
