using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using libMatt.Data;
using libMatt.Converters;
using System.Xml.Linq;

namespace DataDiff.Dalcs {
	internal class SqlDalc: Dalc {

		public SqlDalc(string connStr) : base(new SqlDataProvider(connStr)) { }

		/// <summary>
		/// Retrieves the column structure for a given table name/schema.
		/// If the returned DataTable has no columns, this indicates that
		/// the table does not exist.
		/// 
		/// Due to the nature of user-supplied table names, this function
		/// should always be called before each query to protect against
		/// SQL injection by ensuring that the table actually exists in
		/// the database.
		/// </summary>
		/// <param name="tableName"></param>
		/// <returns></returns>
		public DataTable GetSchema(string schema, string tableName) {
			var ret = new DataTable();
			foreach (var row in ExecuteDataTable(@"
select
	column_name,
	data_type
from INFORMATION_SCHEMA.COLUMNS
where 
	table_schema = @schema
	and table_name = @tableName;", new Param("@schema", schema),
								   new Param("@tableName", tableName)
								 ).AsEnumerable()) {

				ret.Columns.Add(row["column_name"].GetString());
			}
			return ret;
		}

		public bool TableExists(string tableName) {
			return this.GetSchema("dbo", tableName).Columns.Count > 0;
		}

		/// <summary>
		/// Builds a query to find mismatched rows based on the given criteria.
		/// </summary>
		/// <param name="tableName"></param>
		/// <param name="keyColumns"></param>
		/// <param name="comparisonColumns"></param>
		/// <returns></returns>
		public DataTable GetMismatchedAppraisalRows(string srcTableName, string destTableName) {
			var dt = ExecuteDataTable(string.Format(@"
select
	src.AppraisalRollId as srcId,
	src.County as srcCounty,
	src.PropertyNumber as srcPropertyNumber,
	dest.AppraisalRollId as destId,
	dest.County as destCounty,
	dest.PropertyNumber as destPropertyNumber,
	case
		when cp.ClientPropertyId is null then 0
		else 1
	end as IsAssociated
from (select * from {1} dest inner join (select min(AppraisalRollId) as id from {1} group by County, PropertyNumber) x on x.id = dest.AppraisalRollId) as dest
full outer join (select * from {0} src inner join (select min(AppraisalRollId) as id from {0} group by County, PropertyNumber) x on x.id = src.AppraisalRollId) as src
	on src.County = dest.County and src.PropertyNumber = dest.PropertyNumber
left join ClientProperties cp
	on cp.County = dest.County and cp.ParcelId = dest.PropertyNumber
where
	src.county is null or dest.county is null
	or dest.OwnerName1 <> src.OwnerName1 or dest.OwnerName2 <> src.OwnerName2 or dest.OwnerName3 <> src.OwnerName3 or dest.MailingAddressLine1 <> src.MailingAddressLine1 or dest.MailingAddressLine2 <> src.MailingAddressLine2 or dest.MailingAddressLine3 <> src.MailingAddressLine3 or dest.MailingCity <> src.MailingCity
	or dest.MailingState <> src.MailingState or dest.MailingZipCode <> src.MailingZipCode or dest.County <> src.County or dest.FIPSCode <> src.FIPSCode or dest.PhoneNumber <> src.PhoneNumber or dest.EmailAddress <> src.EmailAddress or dest.PropertyNumber <> src.PropertyNumber or dest.LegalDescription <> src.LegalDescription or dest.LegalAcres <> src.LegalAcres or dest.MapNumber <> src.MapNumber or dest.TrackOrLot <> src.TrackOrLot
	or dest.Block <> src.Block or dest.Subdivision <> src.Subdivision or dest.SubdivisionCode <> src.SubdivisionCode or dest.PropertyType <> src.PropertyType or dest.GeoNumber <> src.GeoNumber or dest.SectionNumber <> src.SectionNumber or dest.RevisionDate <> src.RevisionDate",
						srcTableName, destTableName));
			return dt;
		}



		/// <summary>
		/// Finds any client property records that are associated with the given table/parcel/counties
		/// </summary>
		/// <param name="destTableName"></param>
		/// <param name="destKey"></param>
		/// <param name="destKeyCol"></param>
		/// <param name="destParcelColName"></param>
		/// <param name="destCountyColName"></param>
		/// <returns></returns>
		public IEnumerable<int> GetAssociatedClientPropertyIds(string destTableName, object destKey, string destKeyCol) {
			var schema = GetSchema("dbo", destTableName);
			// Figure out which kind of destination table this is - AR or OWNERSHIP_TOTAL
			// First, try OWNERSHIP_TOTAL
			string destParcelColName = "PropID", destCountyColName = "County_FIPS";
			if (!schema.Columns.Contains(destParcelColName) || !schema.Columns.Contains(destCountyColName)) {
				// That wasn't it. Try appraisalrolls.
				destParcelColName = "PropertyNumber";
				destCountyColName = "County";
			}
			if (schema.Columns.Contains(destParcelColName) && schema.Columns.Contains(destCountyColName)) {
				return ExecuteDataTable(string.Format(@"
select 
	cp.ClientPropertyId 
from ClientProperties cp 
inner join {0} destTable
	on destTable.{1} = cp.ParcelID
	and destTable.{2} = cp.County
where
	destTable.{3} = @destKey;", destTableName, destParcelColName, destCountyColName, destKeyCol),
										new Param("@destKey", destKey)).AsEnumerable().Select(row => row["ClientPropertyId"].ToInteger());
			} 

			return new int[] { };
		}


		internal XElement GetTableRow(string tableName, string keyColName, object keyValue) {
			var schema = GetSchema("dbo", tableName);
			var columnNames = schema.Columns.Cast<DataColumn>();
			foreach (var colName in new string[] { keyColName }.Concat(columnNames.Select(x => x.ColumnName))) {
				if (!schema.Columns.Contains(colName)) {
					throw new ArgumentException("Invalid column name for table '" + tableName + "': '" + colName + "'");
				}
			}

			var dt = ExecuteDataTable("select * from " + tableName + " where " + keyColName + " = @keyVal;",
							new Param("@keyVal", keyValue)
					);

			if (dt.Rows.Count < 1) {
				return new XElement("tr", new XAttribute("class", "warning"), 
					new XElement("td", new XAttribute("colspan", dt.Columns.Count),
						"Unable to find records for keyValue '" + keyValue.GetString() + "'"
					)
				);
			}
			// Format the results as an HTML table and return as an XElement
			return new XElement("tr", dt.Columns.Cast<DataColumn>().Select(col => new XElement("td", dt.Rows[0][col].GetString())).ToArray());
		}



		internal IEnumerable<string> GetClientPropertyIds(string parcelId, string county) {
			return ExecuteDataTable(@"
select
	ClientPropertyId
from ClientProperties
where
	ParcelID = @parcelId
	and County = @county;", new Param("@parcelId", parcelId), new Param("@county", county))
						  .AsEnumerable()
						  .Select(row => row["ClientPropertyId"].GetString());
		}
	}
}
