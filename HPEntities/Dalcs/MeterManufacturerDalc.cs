using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using libMatt.Converters;

namespace HPEntities.Dalcs {
	public class MeterManufacturerDalc: AuthDalcBase {

		public bool IsMeterAssociatedWithManufacturer(string meterModel, string manufacturer) {
			return ExecuteScalar(@"
select 1
from MeterModels mm
where
	mm.Manufacturer like @manu
	and mm.MeterModel like @model;", new Param("@manu", manufacturer),
								   new Param("@model", meterModel)).ToBoolean();
		}


	}
}
