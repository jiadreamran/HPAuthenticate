using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HPEntities.Entities;
using System.Data;
using libMatt.Converters;
using System.Configuration;	

namespace HPEntities.Dalcs {
	public class GisDalc: GisDalcBase {

		private static string dbTableSuffix = !string.IsNullOrEmpty(ConfigurationManager.AppSettings["gis_table_suffix"])
												? System.Text.RegularExpressions.Regex.Replace(ConfigurationManager.AppSettings["gis_table_suffix"], "[^A-Za-z_0-9]", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
												: "";

		#region Helpers

		/// <summary>
		/// Converts a DataRow into a CA object. The DataRow must
		/// contain the following columns: OBJECTID, caID, description,
		/// area_m2, approved, actingId.
		/// </summary>
		/// <param name="row"></param>
		/// <returns></returns>
		protected ContiguousAcres GetCAFromDataRow(DataRow row) {
			return new ContiguousAcres(
					row["OBJECTID"].ToInteger(),
					row["caID"].ToInteger(),
					row["description"].GetString(),
					// Magic number is conversion m^2-to-acres from Google
					row["area_m2"] == DBNull.Value ? row["area_acres"].ToDouble() : (row["area_m2"].ToDouble() * 0.000247105381),
					row["approved"].ToBoolean(),
					row["actingId"].ToInteger()
				);
		}

		#endregion


		private GisMeter GetMeter(DataTable table) {
			return (from row in table.AsEnumerable()
					select new GisMeter() {
						MeterInstallationId = row["meter_inst_ID"].ToInteger(),
						Manufacturer = row["manu"].GetString(),
						Model = row["model"].GetString(),
						Serial = row["serial"].GetString(),
						Size = row["size"].GetString(),
						Guid = row["GlobalID"].GetString(),
						Gf_Manu = row["gf_manu"].GetString(),
						Gf_Model = row["gf_model"].GetString(),
						inst_date = row["inst_date"].ToDateTime(),
						read_date = row["read_date"].ToDateTime(),
						Producer = row["producer"].GetString(),
						Owner = row["owner"].GetString(),
						Shape = row["SHAPE"].ToInteger(),
						ActualUserId = row["actualID"].ToInteger(),
						ActingUserId = row["actingID"].ToInteger(),
						InitialReading = (row["ini_read"] == DBNull.Value ? new double?() : row["ini_read"].ToDouble()),
						Application = row["application"].GetString()
					}).DefaultIfEmpty(null).First();
		}


		public GisMeter GetMeter(int meterInstId) {
			return GetMeter(ExecuteDataTable(@"
select 
	meter_inst_ID,
	manu,
	model,
	serial,
	size,
	GlobalID,
	gf_manu,
	gf_model,
	inst_date,
	read_date,
	producer,
	owner,
	SHAPE,
	actualID,
	actingID,
	ini_read,
	application
from HP_METERS" + dbTableSuffix + @"
where
	meter_inst_id = @id
	and isnull(deletion, '') <> 'True';", new Param("@id", meterInstId)));
		}

		public GisMeter GetMeterByObjectId(int objectId) {
			return GetMeter(ExecuteDataTable(@"
select 
	meter_inst_ID,
	manu,
	model,
	serial,
	size,
	GlobalID,
	gf_manu,
	gf_model,
	inst_date,
	read_date,
	producer,
	owner,
	SHAPE,
	actualID,
	actingID,
	ini_read,
	application
from HP_METERS" + dbTableSuffix + @"
where
	objectid = @id
	and isnull(deletion, '') <> 'True';", new Param("@id", objectId)));


		}


		public int GetContiguousAcresIdFromOBJECTID(int OBJECTID) {
			return ExecuteScalar(@"
select
	caID
from HP_CONTIGUOUS_ACRES" + dbTableSuffix + @"
where 
	OBJECTID = @objectId
	and isnull(Deletion, '') <> 'True';", new Param("@objectId", OBJECTID)).ToInteger();
		}

		/// <summary>
		/// Attempts to look up the caid in the database and returns
		/// true if a corresponding record exists.
		/// </summary>
		/// <param name="caID"></param>
		/// <returns></returns>
		public bool ContiguousAcresExists(int caID) {
			return ExecuteScalar(@"
select 1
from HP_CONTIGUOUS_ACRES" + dbTableSuffix + @"
where 
	caID = @caId
	and isnull(Deletion, '') <> 'True';", new Param("@caId", caID)).ToBoolean();
		}

		/// <summary>
		/// Returns the contiguous acres associated with the user. This method does NOT
		/// populate the contiguous acres' wells property.
		/// </summary>
		/// <param name="user"></param>
		/// <param name="includeNonOwned">If true, query returns CAs associated with the account in addition to CAs the user owns.</param>
		/// <returns></returns>
		public IEnumerable<ContiguousAcres> GetContiguousAcres(User user, bool includeNonOwned) {
			if (user == null) {
				throw new ArgumentNullException("user");
			}
			return GetContiguousAcres(user.ActingAsUserId ?? user.Id, includeNonOwned);
		}

		/// <summary>
		/// Returns the contiguous acres associated with the user. This method does NOT
		/// populate the contiguous acres' wells property.
		/// </summary>
		/// <param name="user"></param>
		/// <returns></returns>
		public List<ContiguousAcres> GetContiguousAcres(int userId, bool includeNonOwned) {

			Param[] nonOwnedCAParams = new Param[] {};
			string[] nonOwnedCAParamNames = null;
			if (includeNonOwned) {
				var caIds = new PropertyDalc().GetAssociatedCAIds(userId);
				if (caIds.Count() == 0) {
					// No associations exist; don't even try to retrieve them.
					includeNonOwned = false;
				} else {
					nonOwnedCAParams = ParameterizeInClause("caId", out nonOwnedCAParamNames, caIds.ToArray());
				}
			}

			return ExecuteDataTable(@"
select
	ca.OBJECTID,
	ca.caID,
	ca.area as area_m2,
	ca.admin_area as area_acres,
	ca.description,
	ca.approved,
	ca.actingId
from HP_CONTIGUOUS_ACRES" + dbTableSuffix + @" ca
where
	(ca.actingId = @id" + (includeNonOwned
						? " or ca.caId in (" + string.Join(", ", nonOwnedCAParamNames) + "))"
						: ")") + @"
	and isnull(ca.Deletion, '') <> 'True';", 
						new Param[] { new Param("@id", userId) }.Concat(nonOwnedCAParams).ToArray()
				).AsEnumerable().Select(row => GetCAFromDataRow(row)).ToList();


		}

		/// <summary>
		/// Locates and returns a CA by its CAID. If no such CA is found, returns null.
		/// Note that this function will not return CAs marked as deleted.
		/// </summary>
		/// <param name="caId">The CA ID (NOT OBJECTID) for which to search.</param>
		/// <returns>A ContiguousAcres object, or null if no non-deleted CA is found.</returns>
		public ContiguousAcres GetContiguousAcres(int caId) {
			return ExecuteDataTable(@"
select
	ca.OBJECTID,
	ca.caID,
	ca.area as area_m2,
	ca.admin_area as area_acres,
	ca.description,
	ca.approved,
	ca.actingId
from HP_CONTIGUOUS_ACRES" + dbTableSuffix + @" ca
where
	ca.caID = @id;", new Param("@id", caId)).AsEnumerable().Select(row => GetCAFromDataRow(row)).DefaultIfEmpty(null).First();
		}

		/// <summary>
		/// Returns a boolean indicating whether the given user is associated with the given CA caID.
		/// </summary>
		/// <param name="user"></param>
		/// <param name="caID"></param>
		/// <returns></returns>
		public bool OwnsContiguousAcres(User user, int caId) {
			if (user == null) {
				return false;
			}

			return ExecuteScalar(@"
select 1
from HP_CONTIGUOUS_ACRES" + dbTableSuffix + @" ca
where
	ca.caID = @caId
	and ca.actingId = @userId
	and isnull(ca.Deletion, '') <> 'True';", new Param("@caId", caId),
							   new Param("@userId", user.ActingAsUserId ?? user.Id)
				).ToBoolean();
		}


	}
}
