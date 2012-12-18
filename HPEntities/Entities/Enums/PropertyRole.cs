using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace HPEntities.Entities.Enums {
	/// <summary>
	/// Defines a user's relationship to property.
	/// </summary>
	public enum PropertyRole {
		[Description("Owner")]
		owner = 0,
		[Description("Operator")]
		authorized_producer = 1,
		[Description("Water Meter Installer")]
		installer = 2
	}
}
