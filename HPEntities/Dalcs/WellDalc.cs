using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HPEntities.Entities;
using System.Data;
using libMatt.Converters;

namespace HPEntities.Dalcs {
	public class WellDalc: AuthDalcBase {


		public IEnumerable<Well> GetWells(params int[] wellIds) {
			if (wellIds.Length == 0) {
				return new Well[] { };
			}
			string paramNames = string.Join(", ", wellIds.Select((id, ndx) => "@p" + ndx.ToString()).ToArray());
			Param[] parameters = wellIds.Select((id, ndx) => new Param("@p" + ndx.ToString(), id)).ToArray();
			return ExecuteDataTable(@"
select 
	w.WellID,
	w.PermitNumber,
	wl.Latitude,
	wl.Longitude,
	vmi.MeterInstallationID, -- Note selecting the view ID, this will be null if meter was deleted	
	rer.ErrorResponse,
	w.CountyID,
	c.County
from Wells w
left join WellLocations wl
	on w.WellID = wl.WellID
left join MeterInstallationWells mlw
	on mlw.WellID = w.WellID
left join vMeterInstallations vmi
	on vmi.MeterInstallationId = mlw.MeterInstallationId
left join ReportingErrorResponses rer
	on rer.WellId = w.WellId
left join Counties c
	on c.CountyID = w.CountyID
where
	w.WellID in (" + paramNames + ");", parameters).AsEnumerable().GroupBy(row => new {
					   WellId = row["WellID"].ToInteger(),
					   PermitNumber = row["PermitNumber"].ToInteger(),
					   Latitude = row["Latitude"].ToDouble(),
					   Longitude = row["Longitude"].ToDouble(),
					   ErrorResponse = row["ErrorResponse"].GetString(),
					   County = row["County"].GetString(),
					   CountyID = row["CountyID"].ToInteger()
				   }).Select(x => new Well() {
					   WellId = x.Key.WellId,
					   PermitNumber = x.Key.PermitNumber,
					   Latitude = x.Key.Latitude,
					   Longitude = x.Key.Longitude,
					   ErrorResponse = x.Key.ErrorResponse,
					   County = x.Key.County,
					   CountyId = x.Key.CountyID,
					   MeterInstallationIds = x.Where(row => row["MeterInstallationID"] != DBNull.Value).Select(row => row["MeterInstallationID"].ToInteger()).ToArray()
				   });
		}


		public IEnumerable<Well> GetWellsByMeterInstallationIds(params int[] meterIds) {
			if (meterIds.Length == 0) {
				return new List<Well>();
			}
			string[] paramNames;
			var miidParams = ParameterizeInClause(out paramNames, meterIds);

			var wellIds = ExecuteDataTable(@"
select
	WellID
from MeterInstallationWells
where
	MeterInstallationID in (" + string.Join(", ", paramNames) + ");", miidParams).AsEnumerable().Select(row => row["WellID"].ToInteger());
			return GetWells(wellIds.ToArray());
		}

	}
}
