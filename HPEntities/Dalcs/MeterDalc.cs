using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using libMatt.Converters;
using System.Data;
using HPEntities.Entities;
using HPEntities.Exceptions;
using HPEntities.Entities.Enums;

namespace HPEntities.Dalcs {
	public class MeterDalc: AuthDalcBase {


		#region Helpers

		protected MeterType GetMeterType(string manu, string model) {
			if (manu.ToLower() != "alternate reporting method") {
				return MeterType.standard;
			}

			switch (model.ToLower()) {
				case "alternate: electric":
					return MeterType.electric;
				case "alternate: natural gas":
					return MeterType.natural_gas;
				case "alternate: nozzle package":
					return MeterType.nozzle_package;
				case "alternate: third party":
					return MeterType.bizarro;
				default:
					return MeterType.standard;
			}
		}

		public MeterUnits GetUnits(MeterType meterType) {
			switch (meterType) {
				case MeterType.electric:
					return MeterUnits.kwh;
				case MeterType.natural_gas:
					return MeterUnits.mcf;
				case MeterType.nozzle_package:
					return MeterUnits.gpm;
				case MeterType.standard:
					return MeterUnits.gallons;
				default:
					return MeterUnits.unknown;
			}
		}

		#endregion


		public IEnumerable<MeterInstallation> GetMeterInstallations(params int[] meterInstallationIds) {
			if (meterInstallationIds.Length == 0) {
				return new MeterInstallation[] { };
			}
			var paramNames = string.Join(", ", meterInstallationIds.Select((x, i) => "@p" + i.ToString()));
			var paramValues = meterInstallationIds.Select((x, i) => new Param("@p" + i.ToString(), x)).ToArray();


			// Note the "distinct" here - that's because it's valid for there to be more than
			// one well associated with a meter, in which case the query would return multiple
			// rows (identical in this case due to the columns we care about). Since this is
			// only pulling relevant meter installation metadata, ignore the duplicates.
			return ExecuteDataTable(@"
select distinct
	mi.MeterInstallationId,
	mi.NumberOfDigits, -- used to calculate rollover
	c.County,
	c.CountyID,
	mm.MeterModel,
	mm.Manufacturer,
	mi.multiplier,
	mi.UnitId
from vMeterInstallations mi
left join MeterInstallationWells miw
	on miw.MeterInstallationID = mi.MeterInstallationId
left join Wells w
	on miw.WellID = w.WellID
left join Counties c
	on w.CountyID = c.CountyID
left join MeterModels mm
	on mm.Manufacturer = mi.Manufacturer
	and mm.MeterModel = mi.MeterModel
where
	mi.MeterInstallationId in (" + paramNames + ");", paramValues).AsEnumerable().Select(row => new MeterInstallation() {
									 Id = row["MeterInstallationId"].ToInteger(),
									 CountyId = row["CountyID"].ToInteger(),
									 County = row["County"].GetString(),
									 // As of 20121026, formula to calculate rollover value is:
									 //		9 * (10^(num_digits - 1)) * multiplier
									// RolloverValue = Math.Max(1, (int)Math.Round((Math.Pow(10, row["NumberOfDigits"].ToInteger()) - 1) * row["multiplier"].ToInteger())),
                                     RolloverValue = Math.Max(1, (int)Math.Round((Math.Pow(10, row["NumberOfDigits"].ToInteger()) - 1))),
									 MeterType = GetMeterType(row["Manufacturer"].GetString(), row["MeterModel"].GetString()),
									 Multiplier = (row["multiplier"] == DBNull.Value ? 1.0d : row["multiplier"].ToDouble()),
									 UnitId = row["UnitId"].TryToInteger()
								 });
		}

		public IEnumerable<MeterReading> GetReportingSummaryMeterReadings(int meterInstallationId, int operatingYear) {
			// First, get all the readings for the meter (this won't be many).
			var readings = GetRecentReadings(meterInstallationId, null, null);

			// Next, filter out any readings that took place before the previous year's submittal date.
			// If no submittal date exists, use last year's Dec 15 as the bounding date.
			var lastSubmittalDate = ExecuteScalar("select max(SubmitDatetime) from ReportedMeterVolumes where MeterInstallationId = @meterId and OperatingYear = @year;",
													new Param("@meterId", meterInstallationId),
													new Param("@year", operatingYear)).ToDateTime();
			// We wnat readings that fall between the Dec 15 before the operating year and Jan 15 following the operating year.
			return readings.Where(r => r.DateTime > (lastSubmittalDate ?? new DateTime(operatingYear - 1, 12, 15)) && (r.DateTime < new DateTime(operatingYear + 1, 1, 15)));
		}

		/// <summary>
		/// Returns the [limit] most recent meter readings for the specified meter.
		/// </summary>
		/// <param name="meterInstallationId"></param>
		/// <param name="limit"></param>
		/// <param name="year">If given, only returns readings within the specified year.</param>
		/// <returns></returns>
		public IEnumerable<MeterReading> GetRecentReadings(int meterInstallationId, int? year, int? limit) {
			// Prepend an "initial reading", if it exists.
			var readings = new List<MeterReading>();

            // There is no contrain on number of readings anymore
            limit = null;

			readings.AddRange((from row in ExecuteDataTable(@"
select " + (limit.HasValue ? "top " + limit.ToString() : "") + @"
	mir.MeterInstallationReadingID,
	ReadingDate,
	Reading,
	ActingUserId,
	c_acting.DisplayName as ActingDisplayName,
	ActualUserId,
	c_actual.DisplayName as ActualDisplayName,
	Remarks,
	GallonsPerMinute,
    ReportingYear,
    ReadingType,
    IsSubmitted,
	mi.Manufacturer,
	mi.MeterModel,
	mi.UnitId
from MeterInstallationReadings mir
inner join vMeterInstallations mi -- Inner join weeds out soft deletes
	on mi.MeterInstallationId = mir.MeterInstallationId
left join Clients c_actual
	on c_actual.ClientID = ActualUserId
left join Clients c_acting
	on c_acting.ClientID = ActingUserId
where
	mir.MeterInstallationID = @mird
	" + (year.HasValue ? "and ReportingYear = " + year.ToString() : "") + @"
order by ReadingDate desc;", new Param("@mird", meterInstallationId),
						   new Param("@firstDate", year.HasValue ? (object)new DateTime(year.Value - 1, 12, 15) : DBNull.Value),
						   new Param("@lastDate", year.HasValue ? (object)new DateTime(year.Value + 1, 1, 15, 11, 59, 59) : DBNull.Value)).AsEnumerable()
					select new MeterReading() {
						MeterInstallationReadingId = row["MeterInstallationReadingId"].ToInteger(),
						MeterInstallationId = meterInstallationId,
						DateTime = row["ReadingDate"].ToDateTime().GetValueOrDefault(),
                        Reading = TryToDouble(row["Reading"]),
						ActingUserId = row["ActingUserId"].ToInteger(),
						ActingDisplayName = row["ActingDisplayName"].GetString(),
						ActualUserId = row["ActualUserId"].ToInteger(),
						ActualDisplayName = row["ActualDisplayName"].GetString(),
                        Rate = TryToDouble(row["GallonsPerMinute"]),
                        IsSubmitted = TryToInt(row["IsSubmitted"]).HasValue ? TryToInt(row["IsSubmitted"]) : 0,
                        ReportingYear = TryToInt(row["ReportingYear"]).HasValue ? TryToInt(row["ReportingYear"]) : 2012,
                        ReadingType = TryToInt(row["ReadingType"]).HasValue ? TryToInt(row["ReadingType"]) : 2,
					}));
			
            // Do not add initial readings in
			// If we have a limit specified and the readings hit that count,
			// then forget about loading the "initial" reading on the meter.
			// Otherwise, add it to the list.
            //if (!limit.HasValue || readings.Count < limit.Value) {
            //    var meter = new GisDalc().GetMeter(meterInstallationId);
            //    Func<DateTime, int, bool> withinTimePeriod = (meterReadingDate, currentYear) => 
            //    {
            //        var beginDate = new DateTime(currentYear - 1, 12, 15);
            //        var endDate = new DateTime(currentYear + 1, 1, 15, 11, 59, 59);

            //        return (meterReadingDate <= endDate) && (meterReadingDate >= beginDate);
            //    };
            //    if (meter.InitialReading.HasValue && meter.read_date.HasValue && (!year.HasValue || (withinTimePeriod(meter.read_date.Value, year.Value)))) {

            //        // This meter has an initial reading.
            //        var udalc = new UserDalc();
            //        var actingUser = udalc.GetUser(meter.ActingUserId);
            //        var actualUser = udalc.GetUser(meter.ActualUserId);

            //        var rateForNozzlePackage = new int?();
            //        if(meter.Model.Contains("Nozzle Package"))
            //            if(meter.Size != null && meter.Size != "")
            //                rateForNozzlePackage = Double.Parse(meter.Size).ToInteger();
            //            else
            //                rateForNozzlePackage = new int?();
            //        else
            //            rateForNozzlePackage = new int?();

            //        readings.Add(new MeterReading() {
            //            ActingDisplayName = actingUser != null ? actingUser.DisplayName : "",
            //            ActingUserId = meter.ActingUserId,
            //            ActualDisplayName = actualUser != null ? actualUser.DisplayName : "",
            //            ActualUserId = meter.ActualUserId,
            //            DateTime = meter.read_date.Value,
            //            MeterInstallationId = meter.MeterInstallationId,
            //            MeterInstallationReadingId = 0, // This doesn't have a reading id because it's the initial reading
            //            Rate = rateForNozzlePackage,
            //            Reading = (double)meter.InitialReading.Value
            //        });
            //    }
            //}

			return readings;
		}

        // By mjia
        private double? TryToDouble(object o) 
        {
            double ret;
            if (o == null)
                return new double?();
            
            bool hasValue = Double.TryParse(o.ToString(), out ret);

            if(hasValue)
                return ret;
            else
                return new double?();
        }

        // By mjia
        private int? TryToInt(object o)
        {
            if (o != null)
            {
                int num;
                if (o is bool)
                {
                    return new int?(((bool)o) ? 1 : 0);
                }
                if (int.TryParse(o.ToString(), out num))
                {
                    return new int?(num);
                }
            }
            return null;
        }

		public IEnumerable<MeterReading> GetReadings(int meterInstallationId, int year) {
			return GetRecentReadings(meterInstallationId, year, null);
		}

		/// <summary>
		/// Returns all UnitsOfMeasure records from the database; key: UnitOfMeasureId, value: UnitOfMeasure
		/// </summary>
		/// <returns></returns>
		public Dictionary<int, string> GetUnitOfMeasurementDefinitions() {
			// OK here we can only return top 3 rows
            return ExecuteDataTable(@"
select TOP 3
	UnitOfMeasureID as id,
	UnitOfMeasure as name
from UnitsOfMeasure;").AsEnumerable().ToDictionary(
					 x => x["id"].ToInteger(),
					 x => x["name"].GetString()
				);
		}


		public bool SaveMeterReading(MeterReading reading, out string errorMessage) {
			errorMessage = "";
			try {
                ExecuteNonQuery(@"
merge MeterInstallationReadings as target
using (
	select 
		@installationId as installationId,
		@date as date,
		@reading as reading,
		@actingUserId as actingUserId,
		@actualUserId as actualUserId,
        @gpm as gpm
) as source on (
	target.MeterInstallationID = source.installationId
	and target.ReadingDate = source.date
	and target.Reading = source.reading
	and target.ActingUserId = source.actingUserId
    and target.ActualUserId = source.actualUserId
)
when not matched then 
	insert (
		MeterInstallationID,
		ReadingDate,
		Reading,
		ActingUserId,
		ActualUserId,
		GallonsPerMinute
	) values (
		source.installationId,
		source.date,
		source.reading,
		source.actingUserId,
		source.actualUserId,
		source.gpm
	)
when matched then
	update set
		MeterInstallationID = source.installationId;
",
                    new Param("@installationId", reading.MeterInstallationId),
                    new Param("@date", reading.Date),
                    new Param("@reading", reading.Reading.HasValue ? (object)reading.Reading.Value : DBNull.Value),
                    new Param("@actingUserId", reading.ActingUserId),
                    new Param("@actualUserId", reading.ActualUserId),
                    new Param("@gpm", TryToDouble(reading.Rate) ?? (object)DBNull.Value));
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }

			return true;
		}

        /// <summary>
        /// Streamline reading function
        /// </summary>
        /// <param name="adds">Add reading objects</param>
        /// <param name="deletes">A string array of meter installation reading IDs</param>
        /// <param name="errorMessage"></param>
        /// <returns></returns>
        public bool ApplyReadingEdits(MeterReading[] adds, string[] deletes, out string errorMessage)
        {
            errorMessage = "";
            try
            {
                if (adds.Length > 0)
                {
                    foreach (MeterReading reading in adds)
                    {
                        ExecuteNonQuery(@"
merge MeterInstallationReadings as target
using (
    select 
        @installationId as installationId, 
        @date as date,
        @reading as reading,
        @actingUserId as actingUserId,
        @actualUserId as actualUserId,
        @gpm as gpm,
        @readingType as readingType,
        @isSubmitted as isSubmitted,
        @reportingYear as reportingYear
) as source on (
    target.MeterInstallationID = source.installationId
    and target.ReadingDate = source.date
    and target.Reading = source.reading
    and target.ActingUserId = source.actingUserId
    and target.ActualUserId = source.actualUserId
    and target.ReadingType = source.readingType
    and target.IsSubmitted = source.isSubmitted
    and target.ReportingYear = source.reportingYear
)
when not matched then
    insert (
        MeterInstallationID,
        ReadingDate,
        Reading,
        ActingUserId,
        ActualUserId,
        GallonsPerMinute,
        ReadingType,
        IsSubmitted,
        ReportingYear
    ) values (
        source.installationId,
        source.date,
        source.reading,
        source.actingUserId,
        source.actualUserId,
        source.gpm,
        source.readingType,
        source.isSubmitted,
        source.reportingYear
    )
when matched then
    update set
        MeterInstallationID = source.installationId;
",
                    new Param("@installationId", reading.MeterInstallationId),
                    new Param("@date", reading.Date),
                    new Param("@reading", reading.Reading.HasValue ? (object)reading.Reading.Value : DBNull.Value),
                    new Param("@actingUserId", reading.ActingUserId),
                    new Param("@actualUserId", reading.ActualUserId),
                    new Param("@gpm", TryToDouble(reading.Rate) ?? (object)DBNull.Value),
                    new Param("@readingType", reading.ReadingType),
                    new Param("@isSubmitted", reading.IsSubmitted),
                    new Param("@reportingYear", reading.ReportingYear));
                    }
                }
                StringBuilder builder = new StringBuilder(" (");
                if (deletes.Length > 0)
                {
                    foreach (string str in deletes)
                    {
                        builder.Append(str + ", ");
                    }
                    builder.Remove(builder.Length - 2, 2);
                    builder.Append(")");
                    ExecuteNonQuery(@"
delete from MeterInstallationReadings where MeterInstallationReadingID in " + builder.ToString()
                                                                            );
                }
            }
            catch (Exception exception)
            {
                errorMessage = exception.Message;
                return false;
            }
            return true;
        }

		/// <summary>
		/// Gets volumes as calculated or user-revised for meters that have had
		/// usage reported. Return value is a Dictionary:
		/// { Key: MeterInstallationId, Value: { Item1: CalcVol, Item2: UserRevisedVol } }
		/// </summary>
		/// <param name="meterInstallationId"></param>
		/// <returns></returns>
		public Dictionary<int, Tuple<int, int>> GetReportedVolumeGallons(IEnumerable<int> meterInstallationIds, int caId, int year) {
			if (meterInstallationIds.Count() == 0) {
				return new Dictionary<int, Tuple<int, int>>();
			}
			var paramNames = string.Join(",", meterInstallationIds.Select((x, i) => "@p" + i.ToString()));
			var paramValues = meterInstallationIds.Select((x, i) => new Param("@p" + i.ToString(), x));
			return ExecuteDataTable(@"
select
	MeterInstallationId,
	CalculatedVolumeGallons,
	UserRevisedVolumeGallons
from ReportedMeterVolumes
where
	MeterInstallationID in (" + paramNames + @")
	and ContiguousAcresId = @caId
	and OperatingYear = @year;", paramValues.Concat(new[] { new Param("@year", year), new Param("@caId", caId) }).ToArray())
							   .AsEnumerable().ToDictionary(
									row => row["MeterInstallationId"].ToInteger(),
									row => new Tuple<int, int>(
										row["CalculatedVolumeGallons"].ToInteger(),
										row["UserRevisedVolumeGallons"].ToInteger()
									)
								);
		}

		public IEnumerable<Well> GetAssociatedWells(int meterId) {
			// First ensure that meter exists
			if ((ExecuteScalar("select MeterInstallationId from vMeterInstallations where MeterInstallationId = @meterId;",
									new Param("@meterId", meterId)).ToInteger() != meterId) || meterId == 0) {
				throw new MeterNotFoundException(meterId);
			}

			var dt = ExecuteDataTable(@"
select
	miw.WellID,
	w.PermitNumber,
	wl.Latitude,
	wl.Longitude,
	w.CountyID,
	c.County
from MeterInstallationWells miw
inner join WellLocations wl
	on wl.WellID = miw.WellID
inner join Wells w
	on w.WellID = miw.WellID
left join Counties c
	on c.CountyID = w.CountyID
inner join vMeterInstallations vmi	-- Join to this view to filter out deleted meters (which will not be present in the view)
	on vmi.MeterInstallationId = @meterId
where
	miw.MeterInstallationID = @meterId;
",
							new Param("@meterId", meterId));

			return (from row in dt.AsEnumerable()
					select new Well() {
						WellId = row["WellID"].ToInteger(),
						PermitNumber = row["PermitNumber"].TryToInteger(),
						Latitude = row["Latitude"].ToDouble(),
						Longitude = row["Longitude"].ToDouble(),
						CountyId = row["CountyID"].ToInteger(),
						County = row["County"].GetString()
					});
		}




		/// <summary>
		/// Associates the specified well permits with the given meterObjectId.
		/// 
		/// This method removes all previous associations and only saves those wells
		/// specified in the arguments.
		/// 
		/// Exceptions:
		///		MultipleWellsFoundException
		///		WellNotFoundException
		/// </summary>
		/// <param name="actingUserId"></param>
		/// <param name="meterObjectId"></param>
		/// <param name="wellPermitNumbers"></param>
		public void SaveWellAssociation(int actualUserId, int actingUserId, int meterObjectId, params int[] wellIds) {
			using (var trans = BeginTransaction()) {
				try {
					ExecuteNonQuery(@"
delete from MeterInstallationWells
where
	MeterInstallationID = @meterId;", new Param("@meterId", meterObjectId));

					foreach (var wellId in wellIds) {
						var wells = ExecuteDataTable(@"
select WellID
from Wells
where
	WellID = @wellId
order by WellId asc;",
								new Param("@wellId", wellId)
							);

						if (wells.Rows.Count == 0) {
							throw new WellNotFoundException(wellId);
						}
						// Note that if there are duplicates (which there shouldn't be),
						// this routine just takes the first well found.

						ExecuteNonQuery(@"
insert into MeterInstallationWells (
	MeterInstallationID,
	ConfigurationDate,
	WellID,
	CreatorActualUserId,
	CreatorActingUserId
) values (
	@meterId,
	getdate(),
	@wellId,
	@actualUserId,
	@actingUserId
);",
							new Param("@meterId", meterObjectId),
							new Param("@wellId", wells.Rows[0]["WellID"].ToInteger()),
							new Param("@actualUserId", actualUserId),
							new Param("@actingUserId", actingUserId)
						);

					}

					trans.Commit();
	
				} catch (Exception) {
					trans.Rollback();
					throw;
				}
			}
		}
	}
}
