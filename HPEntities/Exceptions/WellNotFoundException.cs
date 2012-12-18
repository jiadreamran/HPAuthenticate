using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HPEntities.Exceptions {
	public class WellNotFoundException: Exception {
		public WellNotFoundException(int wellId)
			: base("No well was found for WellID " + wellId.ToString()) {
		}
	}
}
