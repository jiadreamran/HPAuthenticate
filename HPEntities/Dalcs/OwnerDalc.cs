using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using libMatt.Converters;
using System.Data.SqlClient;
using HPEntities.Entities;
using System.Text.RegularExpressions;
using HPEntities.Helpers;

namespace HPEntities.Dalcs {
	public class OwnerDalc : AuthDalcBase {

		public OwnerDalc() : base() { }
		public OwnerDalc(IDbTransaction trans) : base(trans) { }

		#region Helpers

		/// <summary>
		/// The database fields being selected from the vwPropertyOwners view.
		/// If these change, you'll need to search this code for references and
		/// update them as well, such as in GetOwners.
		/// </summary>
		private static string[] _ownerDbFields = new[] {
			"o.OwnerName",
			"o.LegalDescription",
			"o.MailingAddress",
			"o.City",
			"o.State",
			"o.PostalCode",
			"o.PhoneNumber"
		};


		/// <summary>
		/// Returns a comma-delimited string of the database fields, suitable for selecting owner data.
		/// </summary>
		private static string DatabaseFields { get { return string.Join("," + (char)10 + (char)9, _ownerDbFields); } }

		/// <summary>
		/// Returns the Owner object from this row, without populating any property descriptions.
		/// </summary>
		/// <param name="row"></param>
		/// <returns></returns>
		private static Owner GetOwner(DataRow row, params string[] propertyDescriptions) {
			return new Owner(row["OwnerName"].GetString(), propertyDescriptions) {
				MailingAddress = row["MailingAddress"].GetString(),
				City = row["City"].GetString(),
				State = row["State"].GetString(),
				PostalCode = row["PostalCode"].GetString(),
				PhoneNumber = row["PhoneNumber"].GetString()
			};
		}

		private static IEnumerable<Owner> GetOwners(IEnumerable<DataRow> rows) {
			return (from row in rows
					group row by new {
						name = row["OwnerName"].GetString(),
						addr = row["MailingAddress"].GetString(),
						city = row["City"].GetString(),
						state = row["State"].GetString(),
						zip = row["PostalCode"].GetString(),
						phone = row["PhoneNumber"].GetString()
					} into owner
					select new Owner(owner.Key.name) {
						MailingAddress = owner.Key.addr,
						City = owner.Key.city,
						State = owner.Key.state,
						PostalCode = owner.Key.zip,
						PhoneNumber = owner.Key.phone
					});

		}


		#endregion


		#region Retrievals


		/// <summary>
		/// Returns a list of owners where the owner name contains the search term
		/// (case-insensitive), up to a maximum of [limit] owners. (To return all users,
		/// specify 0 for limit.)
		/// </summary>
		/// <param name="searchTerm">(string) The partial owner name to search for.</param>
		/// <param name="limit">(int) The maximum number of owners to return. Specify 0 for no limit.</param>
		/// <returns>(List&lt;Owner&gt;) A list of owners whose names contain the search string, or an empty list if no such owners exist.</returns>
		public IEnumerable<Owner> GetOwnersByName(string searchTerm, int limit) {
			var sql = @"
select distinct " + (limit > 0 ? "top " + limit.ToString() : "") + @"
	OwnerName,
	MailingAddress,
	City,
	State,
	PostalCode,
	PhoneNumber
from vwPropertyOwners o
where
	SearchName like @searchTerm
order by OwnerName asc;";
			return ExecuteDataTable(sql, new Param("@searchTerm", Config.Instance.FormatStringSearchTerm(Regex.Replace(searchTerm, "[^A-Za-z0-9]", "")))).AsEnumerable()
					.Select(row => new Owner(row["OwnerName"].GetString()) {
						MailingAddress = row["MailingAddress"].GetString(),
						City = row["City"].GetString(),
						State = row["State"].GetString(),
						PostalCode = row["PostalCode"].GetString(),
						PhoneNumber = row["PhoneNumber"].GetString()
					});
		}


		/// <summary>
		/// Returns the owner corresponding to the given name, or null if no such owner is found.
		/// </summary>
		/// <param name="name"></param>
		/// <returns></returns>
		public Owner GetOwner(string name) {
			var sql = @"
select
	" + DatabaseFields + @"
from vwPropertyOwners o
where
	OwnerName = @ownerName;";
			var dt = ExecuteDataTable(sql, new Param("@ownerName", name));
			return (from DataRow row in dt.Rows
					group row by new {
						name = row["OwnerName"].GetString(),
						addr = row["MailingAddress"].GetString(),
						city = row["City"].GetString(),
						state = row["State"].GetString(),
						zip = row["PostalCode"].GetString(),
						phone = row["PhoneNumber"].GetString()
					} into owner
					select new Owner(
						owner.Key.name,
						owner.Select(r => r["LegalDescription"].GetString())
					) {
						MailingAddress = owner.Key.addr,
						City = owner.Key.city,
						State = owner.Key.state,
						PostalCode = owner.Key.zip,
						PhoneNumber = owner.Key.phone
					}).DefaultIfEmpty(null).First();

		}


		public bool OwnerPropertyExists(string ownerName, string propertyDescription) {
			return ExecuteScalar(@"
select 1
from vwPropertyOwners o
where
	OwnerName = @ownerName
	and LegalDescription = @desc;", new Param("@ownerName", ownerName),
									 new Param("@desc", propertyDescription)).ToBoolean();

		}



		
		#endregion

	}
}
