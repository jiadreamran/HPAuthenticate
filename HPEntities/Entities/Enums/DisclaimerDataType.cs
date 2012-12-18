using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace HPEntities.Entities.Enums {
	/// <summary>
	/// This is used by the User create form as part of the legalese disclaimer.
	/// </summary>
	[Flags]
	public enum DisclaimerDataType {
		[Description("none")]
		none = 0,
		[Description("agriculture")]
		agriculture = 1,
		[Description("mineral production")]
		mineral_production = 2,
		[Description("industrial/commercial")]
		industrial = 4
	}
}
