﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using libMatt.Converters;
using System.Web.Script.Serialization;
using HPEntities.Entities;
using HPEntities.Entities.JsonClasses;
using System.Data;
using HPEntities.Helpers;
using System.Configuration;
using Newtonsoft.Json;
namespace HPEntities.Dalcs {
	public class ReportingDalc : AuthDalcBase {

        private static string dbTableSuffix = !string.IsNullOrEmpty(ConfigurationManager.AppSettings["gis_table_suffix"])
                                        ? System.Text.RegularExpressions.Regex.Replace(ConfigurationManager.AppSettings["gis_table_suffix"], "[^A-Za-z_0-9]", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
                                        : "";

		/// <summary>
		/// Retrieves a reporting summary object from the database for the specified user.
		/// If no reporting summary exists, this method returns an empty string.
		/// </summary>
		/// <param name="user"></param>
		/// <returns></returns>
		public string GetReportingSummary(int userId) {
			//TODO: Revise for performance
			string json = ExecuteScalar(@"
select rs.ReportingSummaryJson
from ReportingSummaries rs
left join ReportingSummaries rs2
	on rs2.ActingUserId = rs.ActingUserId
	and rs2.CreateDatetime > rs.CreateDatetime
where
	rs2.ReportingSummaryId is null
	and rs.ActingUserId = @userId;", new Param("@userId", userId)).GetString();

			return json;
			
		}



		/// <summary>
		/// Saves the specified reporting summary to the database for the given user.
		/// </summary>
		/// <param name="user"></param>
		/// <param name="reportingSummaryJson"></param>
		public void SaveReportingSummary(User user, string reportingSummaryJson, bool isUserInitiated) {
			if (user == null) {
				throw new ArgumentNullException("user");
			}
			// If this save is user-initiated, that means it's a reporting submittal and
			// needs to undergo some validation checks and some data needs to be stored out
			// in other tables.
			// TODO: Implement those checks, identify that data.

			ExecuteNonQuery(@"
insert into ReportingSummaries (
	ActingUserId,
	ActualUserId,
	CreateDatetime,
	ReportingSummaryJson,
	IsUserInitiated
) values (
	@actingUserId,
	@actualUserId,
	getdate(),
	@json,
	@isUserInit
);",
					new Param("@actingUserId", user.ActingAsUserId ?? user.Id),
					new Param("@actualUserId", user.Id),
					new Param("@json", reportingSummaryJson),
					new Param("@isUserInit", isUserInitiated)
				);



		}

		/// <summary>
		/// Saves a user's error response concerning a well to the database.
		/// </summary>
		/// <param name="user"></param>
		/// <param name="wellId"></param>
		/// <param name="errorResponse"></param>
		public void SaveErrorResponse(User user, int? wellId, int? meterInstallationId, string errorResponse) {
			if (user == null) {
				throw new ArgumentNullException("user");
			}
			// TODO: Ensure the well or meter is actually associated with this user

			if (string.IsNullOrEmpty(errorResponse)) {
				throw new ArgumentException("You must specify an error response", "errorResponse");
			}


			ExecuteNonQuery(@"
merge ReportingErrorResponses as target
using (
	select 
		@actualUserId as actualUserId,
		@actingUserId as actingUserId,
		@wellId as wellId,
		@meterInstallationId as meterInstallationId,
		@errorResponse as errorResponse
) as source on (
	target.actualUserId = source.actualUserId
	and target.actingUserId = source.actingUserId
	and target.wellId = source.wellId
	and target.meterInstallationId = source.meterInstallationId
)
when not matched then 
	insert (
		ActualUserId,
		ActingUserId,
		WellId,
		MeterInstallationId,
		ErrorResponse,
		DateRecorded
	) values (
		source.actualUserId,
		source.actingUserId,
		source.wellId,
		source.meterInstallationId,
		source.errorResponse,
		getdate()
	)
when matched then
	update set
		ErrorResponse = source.errorResponse,
		DateRecorded = getdate();
",
					new Param("@actualUserId", user.Id),
					new Param("@actingUserId", user.ActingAsUserId ?? user.Id),
					new Param("@wellId", wellId ?? -1),
					new Param("@meterInstallationId", meterInstallationId ?? -1),
					new Param("@errorResponse", errorResponse)
				);

		}

		/// <summary>
		/// Returns all historical banked water records for the given CA ID,
		/// ordered by operating year ascending.
		/// </summary>
		/// <param name="caId"></param>
		/// <returns></returns>
		public IEnumerable<JsonBankedWaterRecord> GetHistoricalBankedWater(int caId) {
			return ExecuteDataTable(@"
select
	ContiguousAcresId,
	OperatingYear,
	Acres,
	BankedWaterInches
from BankedWater
where
	ContiguousAcresId = @id
order by OperatingYear asc;", new Param("@id", caId)).AsEnumerable().Select(row => new JsonBankedWaterRecord(
						caId,
						row["Acres"].ToInteger(),
                        row["BankedWaterInches"].ToDouble(), //mjia: round banked water to the 10ths. 
						row["OperatingYear"].ToInteger()
					));

		}

		/// <summary>
		/// No validation is performed here; this is strictly data access.
		/// </summary>
		/// <param name="user"></param>
		/// <param name="ca"></param>
		/// <returns></returns>
		public bool SaveAnnualUsageReport(User user, JsonContiguousAcres ca, out string errors) {
			errors = "";
			using (var trans = BeginTransaction()) {
				try {
					ExecuteNonQuery(@"
merge BankedWater as target
using (
	select
		@caId as caId,
		@actualUserId as actualUserId,
		@actingUserId as actingUserId,
		@acres as acres,
		@inches as inches,
		@year as operatingYear
) as source on (
	target.ContiguousAcresId = source.caId
	and target.OperatingYear = source.operatingYear
)
when matched then
	update set
		ActualUserId = source.actualUserId,
		ActingUserId = source.actingUserId,
		Acres = source.acres,
		BankedWaterInches = source.inches
when not matched then
	insert (
		ContiguousAcresId,
		ActualUserId,
		ActingUserId,
		Acres,
		BankedWaterInches,
		OperatingYear
	) values (
		source.caId,
		source.actualUserId,
		source.actingUserId,
		source.acres,
		source.inches,
		source.operatingYear
	);",
							new Param("@caId", ca.number),
							new Param("@actualUserId", user.Id),
							new Param("@actingUserId", user.ActingAsUserId ?? user.Id),
							new Param("@acres", ca.annualUsageSummary.contiguousArea),
							new Param("@inches", ca.annualUsageSummary.desiredBankInches),
							new Param("@year", ca.year)
						);

					if (Config.IsCafoDeployed) {
						foreach (var cafo in ca.cafos) {
							ExecuteNonQuery(@"
merge ReportedCafoUsage as target
using (
	select
		@cafoUsageId as cafoUsageId,
        @caId as caId,
		@operatingYear as operatingYear,
		@cafoOperationId as cafoOperationId,
		@avgLivestock as avgLivestock,
		@calcVolGals as calcVolGals,
		@userRevisedVol as userRevisedVol,
        @userRevisedVolUnitId as userRevisedVolUnitId
) as source on (
	target.CafoUsageId = source.cafoUsageId
)
when matched then
	update set
		CafoOperationId = source.cafoOperationId,
		OperatingYear = source.operatingYear,
        ContiguousAcresId = source.caId,
		AvgLivestockPerDay = source.avgLivestock,
		CalculatedVolumeGallons = source.calcVolGals,
		UserRevisedVolume = source.userRevisedVol,
        UserRevisedVolumeUnitId = source.userRevisedVolUnitId
when not matched then
	insert (
		OperatingYear,
        ContiguousAcresId,
		CafoOperationId,
		AvgLivestockPerDay,
		CalculatedVolumeGallons,
		UserRevisedVolume,
        UserRevisedVolumeUnitId
	) values (
		source.operatingYear,
        source.caId,
		source.cafoOperationId,
		source.avgLivestock,
		source.calcVolGals,
		source.userRevisedVol,
        source.userRevisedVolUnitId
	);", 
								new Param("@cafoUsageId", cafo.id > -1 ? cafo.id : (object)DBNull.Value),
								new Param("@operatingYear", ca.year),
                                new Param("@caId", ca.number),
								new Param("@cafoOperationId", cafo.cafoId),
								new Param("@avgLivestock", cafo.avgLivestock),
								new Param("@calcVolGals", cafo.calculatedVolumeGallons),
								new Param("@userRevisedVol", (cafo.acceptCalculation ? (object)DBNull.Value : cafo.userRevisedVolume)),
                                new Param("@userRevisedVolUnitId", (cafo.acceptCalculation ? (object)DBNull.Value : cafo.userRevisedVolumeUnitId))
							);
						}
					}
	
					// Save reported meter volumes
					foreach (var mr in ca.meterReadings) {
						ExecuteNonQuery(@"
merge ReportedMeterVolumes as target
using (
	select
		@caId as caId,
		@miid as miid,
		@year as year,
		@actualUserId as actualUserId,
		@actingUserId as actingUserId,
		@calcVolGals as calcVolGals,
		@userRevisedVol as userRevisedVol,
        @userRevisedVolUnitId as userRevisedVolUnitId
) as source on (
	target.ContiguousAcresId = source.caId
	and target.MeterInstallationId = source.miid
	and target.OperatingYear = source.year
)
when matched then
	update set
		ActualUserId = source.actualUserId,
		ActingUserId = source.actingUserId,
		CalculatedVolumeGallons = source.calcVolGals,
		UserRevisedVolume = source.userRevisedVol,
        UserRevisedVolumeUnitId = source.userRevisedVolUnitId,
		SubmitDatetime = GETDATE()
when not matched then
	insert (
		MeterInstallationId,
		ContiguousAcresId,
		OperatingYear,
		ActualUserId,
		ActingUserId,
		CalculatedVolumeGallons,
		UserRevisedVolume,
        UserRevisedVolumeUnitId,
		SubmitDatetime
	) values (
		source.miid,
		source.caId,
		source.year,
		source.actualUserId,
		source.actingUserId,
		source.calcVolGals,
		source.userRevisedVol,
        source.userRevisedVolUnitId,
		GETDATE()
	);",
								new Param("@caId", ca.number),
								new Param("@miid", mr.Value.meterInstallationId),
								new Param("@year", ca.year),
								new Param("@actualUserId", user.Id),
								new Param("@actingUserId", user.ActingAsUserId ?? user.Id),
								new Param("@calcVolGals", mr.Value.calculatedVolumeGallons),
								new Param("@userRevisedVol", mr.Value.userRevisedVolume.GetValueOrDefault() == 0 ? (object)DBNull.Value : mr.Value.userRevisedVolume),
                                new Param("@userRevisedVolUnitId", mr.Value.userRevisedVolume.GetValueOrDefault() == 0 ? (object)DBNull.Value : mr.Value.userRevisedVolumeUnitId)
						);

                        // mjia: change submittal status from "0" or "NULL" to "1"
                        if (mr.Value.readings.Length != 0)
                        {
                            StringBuilder builder = new StringBuilder("update MeterInstallationReadings set IsSubmitted = 1 where MeterInstallationReadingID in (");
                            foreach (JsonMeterReading reading in mr.Value.readings)
                            {
                                builder.Append(reading.meterInstalltionReadingID + ",");
                            }
                            builder.Replace(",", ")", builder.Length - 1, 1);
                            ExecuteNonQuery(builder.ToString());
                        }
					}
					
					trans.Commit();
				} catch (Exception ex) {
					trans.Rollback();
					new LogDalc().SaveError(user, ex);
					errors = "Server error: " + ex.Message;
					return false;
				}
			}
			return true;
		}



		/// <summary>
		/// Returns a bool indicating whether the contiguous acres represented by CAID
		/// has already been submitted.
		/// </summary>
		/// <param name="caId"></param>
		/// <returns></returns>
		public bool IsSubmitted(int caId, int year) {
			return ExecuteScalar(@"
select 1
from BankedWater bw
where 
	bw.OperatingYear = @year and bw.ContiguousAcresId = @id

union all

select 1
from ReportedMeterVolumes rmv
where
	rmv.OperatingYear = @year and rmv.ContiguousAcresId = @id;", new Param("@year", year),
																	 new Param("@id", caId)).ToBoolean();
		}

		/// <summary>
		/// Returns usage reports by 
		/// </summary>
		/// <param name="userId"></param>
		/// <returns></returns>
		public IEnumerable<JsonUsageReports> GetUsageReportsByUserNameOrEmail(string searchTerm) {
			return (from row in ExecuteDataTable(@"
select
	c.ClientID,
	c.DisplayName,
	c.EmailAddress,
	ca.caID,
	ca.description,
	bw.OperatingYear
from Clients c
inner join HPWD_GIS.dbo.hp_contiguous_acres" + dbTableSuffix + @" ca
	on ca.actingID = c.ClientID
left join BankedWater bw
	on bw.ContiguousAcresId = ca.caId
	and bw.ActingUserId = ca.actingID
where
	(
    SearchName like @term
	or EmailAddress like @term
    ) and
    ca.deletion = 'False'
order by LastNameOrCompany asc;", new Param("@term", Config.Instance.FormatStringSearchTerm(searchTerm.Replace(" ", "")))).AsEnumerable()
					group row by new { 
						userId = row["ClientID"].ToInteger(), 
						email = row["EmailAddress"].GetString(),
						name = row["DisplayName"].GetString()
					} into clientGrp
					select new JsonUsageReports() {
						userId = clientGrp.Key.userId,
						email = clientGrp.Key.email,
						name = clientGrp.Key.name,
						years = (from client in clientGrp
								 where client["caID"] != DBNull.Value
								 group client by client["OperatingYear"].ToInteger() into yearGrp
								 select new {
									 year = yearGrp.Key,
									 CAs = yearGrp.Select(ca => new JsonContiguousAcres() {
																	number = ca["caID"].ToInteger(),
																	description = ca["description"].GetString(),
																	isSubmitted = yearGrp.Key > 0
										})
								 }).ToDictionary(
									x => x.year,
									x => x.CAs
								)
					});
			

		}


		/// <summary>
		/// Unsubmits a CA by deleting the values out of the BankedWater and RepotedMeterVolumes tables.
		/// </summary>
		/// <param name="year"></param>
		/// <param name="caId"></param>
		public void UnsubmitCA(int year, int caId) {
            // Get user ID we're dealing with, in order to properly
            // delete the user's JSON summary state below
            var userId = ExecuteScalar(@"
select ActingUserId 
from BankedWater 
where 
    OperatingYear = @year
    and ContiguousAcresId = @id;", new Param("@year", year),
                                 new Param("@id", caId)).ToInteger();

            if (userId > 0) {
                // Attempt to clean up the user's JSON
                var rsJson = GetReportingSummary(userId);
                ReportingSummary rs;
                try {
                    rs = JsonConvert.DeserializeObject<ReportingSummary>(rsJson);
                    JsonReportingYear jsonYear;
                    if (rs != null && rs.years.TryGetValue(year, out jsonYear)) {
                        // Find and delete the offending CA.
                        var index = jsonYear.contiguousAcres.FindIndex(ca => ca.number == caId);
                        if (index > -1) {
                            // Delete any ReportingErrorResponses for this CA.
                            foreach (var kv in jsonYear.contiguousAcres[index].meterInstallationErrors) {
                                // Key: meter installation ID.
                                ExecuteNonQuery(@"
delete from ReportingErrorResponses
where
    MeterInstallationId = @miid
    and ActingUserId = @userId;", new Param("@miid", kv.Key), new Param("@userId", userId));
                            }

                            foreach (var well in jsonYear.contiguousAcres[index].wells) {
                                ExecuteNonQuery(@"
delete from ReportingErrorResponses
where
    WellID = @wellId
    and ActingUserId = @userId;", new Param("@wellId", well.id), new Param("@userId", userId));
                            }

                            // mjia: change submittal status from "1" to "0"
                            var meterReadings = jsonYear.contiguousAcres[index].meterReadings;
                            foreach (var mr in meterReadings)
                            {
                                if (mr.Value.readings.Length != 0)
                                {
                                    StringBuilder builder = new StringBuilder("update MeterInstallationReadings set IsSubmitted = 0 where MeterInstallationReadingID in (");
                                    foreach (JsonMeterReading reading in mr.Value.readings)
                                    {
                                        builder.Append(reading.meterInstalltionReadingID + ",");
                                    }
                                    builder.Replace(",", ")", builder.Length - 1, 1);
                                    ExecuteNonQuery(builder.ToString());
                                }
                            }

                            // Remove the CA from serialized JSON and save the revised JSON to the db.
                            jsonYear.contiguousAcres.RemoveAt(index);
                            SaveReportingSummary(new UserDalc().GetUser(userId),
                                                JsonConvert.SerializeObject(rs),
                                                false);
                        }
                    }
                } catch (Exception ex) {
                    // We tried, we failed. Give up.
                }

            }

            ExecuteNonQuery(@"
delete from BankedWater
where
	OperatingYear = @year
	and ContiguousAcresId = @id;

delete from ReportedMeterVolumes
where
	OperatingYear = @year
	and ContiguousAcresId = @id;

delete from ReportedCafoUsage
where
    OperatingYear = @year
    and ContiguousAcresId = @id;", new Param("@year", year),
                     new Param("@id", caId));

		}

		public bool IsWellErrorResponseRecorded(int wellId, int userId) {
			return ExecuteScalar(@"
select 1
from ReportingErrorResponses
where
	ActingUserId = @userId
	and WellId = @wellId;", new Param("@userId", userId),
						  new Param("@wellId", wellId)).ToBoolean();
		}

		public string GetMeterInstallationErrorResponse(int meterInstallationId, int userId) {
			return ExecuteScalar(@"
select ErrorResponse
from ReportingErrorResponses
where
	ActingUserId = @userId
	and MeterInstallationId = @miid;", new Param("@userId", userId),
									 new Param("@miid", meterInstallationId)).GetString();
		}

		public int GetAllowableProductionRate(int year) {
			return ExecuteScalar(@"
select
	Inches
from ReportingAllowableProductionRates
where
	OperatingYear = @year;", new Param("@year", year)).ToInteger();
		}

		public void SetReportingAccessOverride(int userId, int adminUserId, bool canReport) {
			if (canReport) {
				ExecuteNonQuery(@"
merge ReportingAccessOverrides as target
using (
	select 
		@userId as userId,
		@adminUserId as adminUserId
) as source on (
	target.UserId = source.userId
)
when not matched then
	insert (UserId, CreatedByUserId, CreatedAt)
	values (source.userId, source.adminUserId, getdate());", new Param("@userId", userId),
														  new Param("@adminUserId", adminUserId)
														);
			} else {
				ExecuteNonQuery(@"delete from ReportingAccessOverrides where UserId = @userId;",
								new Param("@userid", userId));
			}
		}

		public bool CanUserOverrideReportingDates(int userId) {
			return ExecuteScalar(@"
select 1
from ReportingAccessOverrides
where
	UserId = @userId;", new Param("@userId", userId)).ToBoolean();
		}


		public Dictionary<int, JsonCafoLookup> GetCafoLookups() {
			return (from row in ExecuteDataTable(@"
select
	OperationId,
	OperationName,
	GalPerHeadPerDay,
	Note
from CafoUsageLookup;").AsEnumerable()
					select new JsonCafoLookup() {
						operationId = row["OperationId"].ToInteger(),
						name = row["OperationName"].GetString(),
						galPerHeadPerDay = row["GalPerHeadPerDay"].ToDouble(),
						notes = row["Note"].GetString()
					}).ToDictionary(x => x.operationId, x => x);
		}

		/// <summary>
		/// Determines whether the given reading is valid for calculating volume.
		/// Only readings that fall between Dec 15 - Jan 15 are valid for purposes
		/// of calculating volume.
		/// </summary>
		/// <param name="reading"></param>
		/// <param name="operatingYear"></param>
		/// <returns></returns>
		public static bool IsMeterReadingValidBeginReading(MeterReading reading, int operatingYear) {
			//return (reading.DateTime >= new DateTime(operatingYear - 1, 12, 15) && reading.DateTime < new DateTime(operatingYear, 1, 16));
            return (reading.ReadingType.GetValueOrDefault() == 1 && reading.ReportingYear.GetValueOrDefault() == operatingYear);
		}

		public static bool IsMeterReadingValidEndReading(MeterReading reading, int operatingYear) {
			//return (reading.DateTime >= new DateTime(operatingYear, 12, 15) && reading.DateTime < new DateTime(operatingYear + 1, 1, 16));
            return (reading.ReadingType.GetValueOrDefault() == 3 && reading.ReportingYear.GetValueOrDefault() == operatingYear);
		}

        public static bool IsMeterReadingAnnualTotal(MeterReading reading, int operatingYear) {
            return (reading.ReadingType.GetValueOrDefault() == 4 && reading.ReportingYear.GetValueOrDefault() == operatingYear);
        }
	}
}
